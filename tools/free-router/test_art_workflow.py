import unittest
import policy
import io
import json
from unittest.mock import patch
import router

class ArtWorkflow(unittest.TestCase):
    def test_catalog_is_small_only_for_explicit_mount(self):
        payload={'messages':[{'role':'user','content':'Monta i nuovi asset integrati della slot'}],
                 'tools':[{'function':{'name':name}} for name in ['read','bash','unity_art_bundle','harness_checkpoint']]}
        self.assertTrue(policy.art_bundle_task(payload))
        self.assertEqual([t['function']['name'] for t in policy.role_payload(payload,'auto')['tools']],['unity_art_bundle'])
        self.assertIs(policy.role_payload(payload,'specialista'),payload)
    def test_new_scope_restores_tools(self):
        payload={'messages':[{'role':'user','content':'Monta asset slot'}, {'role':'user','content':'Correggi il calcolo dei bonus'}]}
        self.assertFalse(policy.art_bundle_task(payload))
    def test_general_asset_edit_is_not_mount(self):
        self.assertFalse(policy.art_bundle_task({'messages':[{'role':'user','content':'Ridisegna gli asset e correggi la logica della slot'}]}))
    def test_auto_text_mount_never_consults_or_calls_cloud(self):
        handler=object.__new__(router.Handler)
        handler.wfile=io.BytesIO()
        handler.send_response=lambda *a:None
        handler.send_header=lambda *a:None
        handler.end_headers=lambda:None
        class Response:
            status=200
            def read(self): return json.dumps({'choices':[{'message':{'content':'ok'}}]}).encode()
        class Connection:
            def close(self):pass
        payload={'messages':[{'role':'user','content':'Monta i nuovi asset integrati della slot'}], 'max_tokens':8192}
        with patch.object(router,'refresh_catalog',side_effect=AssertionError('Cloud catalog forbidden')), patch.object(router,'persist'), patch.object(router,'upstream',return_value=(Connection(),Response())) as upstream:
            handler.route(payload,'auto','offline-test')
            args=upstream.call_args.args
            self.assertEqual(args[0],router.SETTINGS['worker']['model'])
            self.assertTrue(args[2]);self.assertTrue(args[3])
            self.assertLessEqual(args[1]['max_tokens'],router.SETTINGS['worker']['output'])
    def test_auto_capture_handoff_uses_qualified_free_vision(self):
        handler=object.__new__(router.Handler)
        handler.wfile=io.BytesIO()
        handler.send_response=lambda *a:None
        handler.send_header=lambda *a:None
        handler.end_headers=lambda:None
        class Response:
            status=200
            def read(self):return json.dumps({'choices':[{'message':{'content':'review'}}]}).encode()
        class Connection:
            def close(self):pass
        payload={'messages':[{'role':'user','content':'Monta i nuovi asset integrati della slot'},
            {'role':'tool','content':[{'type':'image_url','image_url':{'url':'data:image/jpeg;base64,test'}}]}]}
        with patch.object(router,'refresh_catalog',return_value={}), patch.object(router,'ranked_groups',return_value={'vision':[router.FLASH]}), patch.object(router.policy,'candidates',return_value=[router.FLASH]) as candidates, patch.object(router,'reserve_attempt',return_value=True), patch.object(router,'persist'), patch.object(router,'upstream',return_value=(Connection(),Response())) as upstream:
            handler.route(payload,'auto','image-test')
            self.assertEqual(candidates.call_args.args[0],'vision')
            self.assertFalse(upstream.call_args.args[2])
            self.assertEqual(upstream.call_args.args[0],router.FLASH)

if __name__=='__main__': unittest.main()
