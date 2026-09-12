"""Exercise real Kilo hooks/tools with a deterministic LOCAL fake model.
No online calls. Only runtime/harness-native.txt is edited. This is an integration
test of the harness, not a benchmark of a language model's intelligence.
"""
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

ROOT=Path(__file__).resolve().parents[2]
TARGET=ROOT/'tools/free-router/runtime/harness-native.txt'
TARGET.write_text('verification_number=731\n')
seen=[]
class Handler(BaseHTTPRequestHandler):
    def log_message(self,*args):pass
    def do_POST(self):
        payload=json.loads(self.rfile.read(int(self.headers['Content-Length'])))
        seen.append({'headers': self.headers.get('X-FlipCards-Harness'), 'messages':payload['messages']})
        n=len(seen)
        actions=[
            ('edit',{'filePath':str(TARGET),'oldString':'731','newString':'734'}),
            ('harness_checkpoint',{'phase':'plan','criteria':['fixture contains 734'],'note':'Read fixture, edit, read result'}),
            ('read',{'filePath':str(TARGET)}),
            ('edit',{'filePath':str(TARGET),'oldString':'731','newString':'734'}),
            ('read',{'filePath':str(TARGET)}),
        ]
        if '--with-local-extract' in sys.argv:
            actions.append(('local_extract',{'excerpt':'verification_number=734; enabled=false','question':'Return only verification_number and enabled, preserving values.'}))
        if n<=len(actions):name,args=actions[n-1]
        elif n==len(actions)+1:
            evidence=re.findall(r'\[HARNESS evidence=([^\]]+)\]',json.dumps(payload['messages']))
            name,args='harness_checkpoint',{'phase':'complete','checks':[{'criterion':0,'evidence':['fixture_call_5' if evidence else 'missing'],'observation':'Read result contains 734'}],'note':'Done'}
        else:name,args=None,None
        delta={'tool_calls':[{'index':0,'id':'fixture_call_'+str(n),'type':'function','function':{'name':name,'arguments':json.dumps(args)}}]} if name else {'content':'Local harness integration completed.'}
        body='data: '+json.dumps({'id':'fixture','object':'chat.completion.chunk','model':'auto','choices':[{'index':0,'delta':delta,'finish_reason':None}]})+'\n\n'
        body+='data: '+json.dumps({'id':'fixture','choices':[{'index':0,'delta':{},'finish_reason':'tool_calls' if name else 'stop'}]})+'\n\ndata: [DONE]\n\n'
        self.send_response(200);self.send_header('Content-Type','text/event-stream');self.send_header('Content-Length',str(len(body.encode())));self.end_headers();self.wfile.write(body.encode())

server=ThreadingHTTPServer(('127.0.0.1',0),Handler)
threading.Thread(target=server.serve_forever,daemon=True).start()
config={'mcp':{'unity-mcp':{'enabled':False}},'small_model':'router/auto', 'provider':{'router':{'options':{'baseURL':f'http://127.0.0.1:{server.server_port}/v1','apiKey':'test-only'}}},'agent':{'auto':{'steps':10,'permission':{'edit':'allow','read':'allow','harness_checkpoint':'allow'}}}}
env=os.environ.copy();env['KILO_CONFIG_CONTENT']=json.dumps(config)
exe=next((Path.home()/'.vscode/extensions').glob('kilocode.kilo-code-7.6.2-*/bin/kilo.exe'))
try:
    result=subprocess.run([str(exe),'run','--agent','auto','--model','router/auto','Local synthetic fixture verification.'],cwd=ROOT,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf-8',timeout=90)
    (ROOT/'tools/free-router/runtime/native-harness.log').write_text(result.stdout,encoding='utf-8')
    assert result.returncode==0,result.stdout[-3000:]
    assert TARGET.read_text().strip()=='verification_number=734'
    assert len(seen)>=7 and all(x['headers']=='4' for x in seen)
    text=json.dumps(seen[-1]['messages'])
    assert 'Prima di eseguire' in text,'unguarded first edit'
    assert '\\"phase\\":\\"complete\\"' in text or '"phase": "complete"' in text or '"phase":"complete"' in text,'completion checkpoint missing'
    if '--with-local-extract' in sys.argv:
        outputs=[str(m.get('content','')) for m in seen[-1]['messages'] if m.get('role')=='tool']
        assert any('Estrazione locale' in output and '734' in output and 'false' in output for output in outputs),'real 2B extractor did not preserve the fields'
        print('PASS real 2B local_extract inside native Kilo: number and boolean preserved.')
    print('PASS real Kilo: harness header, blocked unplanned edit, native read/edit, evidence-backed completion. Requests:',len(seen))
finally:
    server.shutdown()
