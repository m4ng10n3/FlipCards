import copy
import json
import tempfile
import threading
import time
import unittest
from http.client import HTTPConnection
from pathlib import Path
from unittest.mock import patch

import policy
import router
import streaming
import budget


def event(delta=None, finish=None, **extra):
    return ('data: '+json.dumps({'choices':[{'index':0,'delta':delta or {},'finish_reason':finish}],**extra})+'\n\n').encode()


class Response:
    def __init__(self, chunks, status=200, headers=None):
        self.chunks=list(chunks)
        self.status=status
        self.headers=headers or {}
    def read1(self, size):
        item=self.chunks.pop(0) if self.chunks else b''
        if isinstance(item, Exception): raise item
        return item
    def getheader(self, name): return self.headers.get(name)
    def read(self): return b''.join(self.chunks)


class StreamTests(unittest.TestCase):
    def run_stream(self, data):
        written=[]; started=[]
        token=streaming.forward(Response(data),lambda:started.append(True),written.append,time.monotonic()+5)
        return b''.join(written),started,token
    def test_chunk_boundaries_and_unicode(self):
        data=event({'content':'perché'})+event(finish='stop',usage={'total_tokens':37})+b'data: [DONE]\n\n'
        out,start,token=self.run_stream([bytes([b]) for b in data])
        self.assertEqual(out,data);self.assertEqual(start,[True]);self.assertEqual(token,37)
    def test_crlf_split(self):
        data=(event({'content':'ok'})+event(finish='stop')+b'data: [DONE]\n\n').replace(b'\n',b'\r\n')
        self.assertEqual(self.run_stream([bytes([b]) for b in data])[0],data)
    def test_tool_arguments_containing_error_not_provider_error(self):
        data=event({'tool_calls':[{'index':0,'function':{'arguments':'{"error":"expected"}'}}]})+event(finish='tool_calls')
        self.assertTrue(self.run_stream([data])[0].endswith(b'[DONE]\n\n'))
    def test_real_reasoning_preserved(self):
        data=event({'reasoning_content':'thinking'})+event({'content':'ok'})+event(finish='stop')
        self.assertIn(b'thinking',self.run_stream([data])[0])
    def test_role_only_before_error_not_committed(self):
        started=[]
        with self.assertRaises(streaming.UpstreamError):
            streaming.forward(Response([event({'role':'assistant'}),b'data: {"error":{"message":"no"}}\n\n']),lambda:started.append(1),lambda b:None,time.monotonic()+2)
        self.assertEqual(started,[])
    def test_truncated_stream_raises(self):
        with self.assertRaises(streaming.UpstreamError): self.run_stream([event({'content':'partial'})])
    def test_done_without_finish_raises(self):
        with self.assertRaises(streaming.UpstreamError): self.run_stream([event({'content':'partial'}),b'data: [DONE]\n\n'])
    def test_empty_done_raises(self):
        with self.assertRaises(streaming.UpstreamError): self.run_stream([b'data: [DONE]\n\n'])


class PolicyTests(unittest.TestCase):
    def setUp(self):
        self.p={'messages':[{'role':'user','content':'test'}],'tools':[{}],'max_tokens':100}
        self.a='a:free';self.b='b:free'
        self.meta={'free':True,'context':10000,'output':1000,'image':True,'tools':True}
        self.cat={self.a:dict(self.meta),self.b:dict(self.meta)}
    def pick(self,**kwargs): return policy.candidates('fast',self.p,self.cat,{'fast':[self.a,self.b]},kwargs.pop('cooldown',{}),**kwargs)
    def test_session_survives_compaction(self):
        self.assertEqual(policy.session_key(self.p,'s1'),policy.session_key({'messages':[]},'s1'))
        self.assertNotEqual(policy.session_key(self.p,'s1'),policy.session_key(self.p,'s2'))
    def test_first_user_not_truncated(self):
        a={'messages':[{'role':'user','content':'a'*500+'x'}]};b=copy.deepcopy(a);b['messages'][0]['content']='a'*500+'y'
        self.assertNotEqual(policy.session_key(a),policy.session_key(b))
    def test_context_hard_gate(self):
        for m in self.cat.values(): m['context']=10
        self.assertEqual(self.pick(),[])
    def test_tools_hard_gate(self):
        self.cat[self.a]['tools']=False
        self.assertEqual(self.pick(),[self.b])
    def test_vision_never_falls_back_to_text(self):
        self.p['messages'][0]['content']=[{'type':'image_url','image_url':{'url':'data:image/png;base64,'+'a'*100000}}]
        for m in self.cat.values(): m['image']=False
        self.assertEqual(self.pick(),[])
    def test_base64_not_counted_as_text_tokens(self):
        self.p['messages'][0]['content']=[{'type':'image_url','image_url':{'url':'a'*1000000}}]
        self.assertLess(policy.input_tokens(self.p),10000)
    def test_cooldown_never_readded(self):
        self.assertEqual(self.pick(cooldown={self.a:time.time()+500,self.b:time.time()+500}),[])
    def test_sticky_beats_load(self):
        self.assertEqual(self.pick(sticky=self.b)[0],self.b)
    def test_paid_suffix_insufficient(self):
        self.assertFalse(policy.catalog_entries([{'id':'fake:free','pricing':{'prompt':'0.1','completion':'0'}}]))
    def test_unknown_capabilities_rejected(self):
        self.assertFalse(policy.eligible('a:free',None,self.p,100))
    def test_explicit_edit_cannot_be_downgraded_by_classifier(self):
        self.assertTrue(policy.needs_code({'messages':[{'role':'user','content':'Rimuovi un listener duplicato nel metodo OnEnable'}]}))
        self.assertFalse(policy.needs_code({'messages':[{'role':'user','content':'Elenca i listener nel metodo OnEnable'}]}))
        self.assertFalse(policy.needs_code({'messages':[{'role':'user','content':'Leggi il codice senza modificare alcun file.'}]}))
        self.assertTrue(policy.needs_code({'messages':[{'role':'user','content':'Usa Unity_RunCommand per leggere la scena attiva'}]}))
    def test_coordinator_context_filters_tools_not_child_permissions(self):
        payload={'messages':[{'role':'user','content':'Keep this objective'}], 'tools':[{'function':{'name':n}} for n in ['task','read','unity-mcp_Unity_RunCommand','todowrite']]}
        out=policy.role_payload(payload,'coordinatore')
        self.assertEqual([t['function']['name'] for t in out['tools']],['task','todowrite'])
        self.assertEqual(out['messages'],payload['messages'])
        self.assertEqual(len(payload['tools']),4)
        self.assertIs(policy.role_payload(payload,'specialista'),payload)


class HTTPTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory()
        self.stack=[]
        def mock(target,value):
            p=patch(target,value);p.start();self.stack.append(p)
        mock('router.STATE_FILE',Path(self.temp.name)/'state.json')
        mock('budget.ROOT',Path(self.temp.name))
        mock('router.local_key',lambda:'test')
        mock('router.refresh_catalog',lambda: {m:{'free':True,'context':262144,'output':8192,'image':True,'tools':True} for m in ['a:free','b:free']})
        mock('router.ranked_groups',lambda:{g:['a:free','b:free'] for g in router.GROUPS})
        mock('policy.local_classify',lambda *a:None)
        mock('router.benchmarks',lambda:{})
        router._sticky.clear();router._attempts.clear();router._cooldown.clear();router._requests.clear()
        self.calls=[]
        self.responses=[]
        def upstream(model,payload,*args):
            self.calls.append((model,copy.deepcopy(payload)))
            return type('Conn',(),{'close':lambda s:None})(),self.responses.pop(0)
        mock('router.upstream',upstream)
        self.server=router.ThreadingHTTPServer(('127.0.0.1',0),router.Handler)
        self.thread=threading.Thread(target=self.server.serve_forever,daemon=True);self.thread.start()
    def tearDown(self):
        self.server.shutdown();self.server.server_close()
        for p in reversed(self.stack):p.stop()
        self.temp.cleanup()
    def request(self,model='auto'):
        conn=HTTPConnection('127.0.0.1',self.server.server_port,timeout=3)
        body={'model':model,'stream':True,'messages':[{'role':'user','content':'Unity test'}],'max_tokens':100}
        conn.request('POST','/v1/chat/completions',json.dumps(body),{'Content-Type':'application/json','Authorization':'Bearer test','X-FlipCards-Session':'test-session'})
        resp=conn.getresponse();data=resp.read();conn.close();return resp.status,data
    def test_retry_before_any_output(self):
        self.responses=[Response([event({'role':'assistant'}),b'data: {"error":{"message":"busy"}}\n\n']),Response([event({'content':'ok'})+event(finish='stop')])]
        status,data=self.request()
        self.assertEqual(status,200);self.assertEqual(len(self.calls),2);self.assertNotIn(b'busy',data)
        self.assertEqual(self.calls[0][1]['messages'],self.calls[1][1]['messages'])
    def test_upstream_reset_before_content_retries(self):
        self.responses=[Response([ConnectionResetError('upstream')]),Response([event({'content':'ok'})+event(finish='stop')])]
        self.assertEqual(self.request()[0],200);self.assertEqual(len(self.calls),2)
    def test_no_retry_after_partial_tool(self):
        self.responses=[Response([event({'tool_calls':[{'index':0,'function':{'arguments':'{'}}]})])]
        status,data=self.request()
        self.assertEqual(len(self.calls),1)
        self.assertIn(b'"error"',data);self.assertNotIn(b'[DONE]',data)
    def test_429_retry_after(self):
        self.responses=[Response([],429,{'Retry-After':'300'}),Response([event({'content':'ok'})+event(finish='stop')])]
        self.assertEqual(self.request()[0],200)
        self.assertGreater(router._cooldown['a:free'],time.time()+290)
    def test_unknown_model_rejected(self):
        self.assertEqual(self.request('typo')[0],400);self.assertEqual(self.calls,[])
    def test_local_failure_never_uses_cloud_or_cloud_budget(self):
        self.responses=[Response([OSError('worker offline')])]
        self.assertEqual(self.request('rapido')[0],503)
        self.assertEqual([c[0] for c in self.calls],['qwen3.5-2b'])
        self.assertEqual(budget.used(),0)
    def test_hourly_budget_enforced(self):
        for _ in range(router.SETTINGS['hourly_request_budget']): budget.reserve(router.SETTINGS['hourly_request_budget'],'test')
        self.assertEqual(self.request()[0],429);self.assertEqual(self.calls,[])


if __name__=='__main__': unittest.main()
