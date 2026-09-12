import json
import unittest
from pathlib import Path
from unittest.mock import patch
import privacy
import router


class PrivacyTests(unittest.TestCase):
    def payload(self, text):
        return {'messages': [{'role': 'user', 'content': text}]}

    def test_project_source_paths_art_and_stat_data_are_allowed(self):
        privacy.check(self.payload('Assets/Scripts/Slots/SlotView.cs\npublic void Attack() { health -= damage; }\nBoss HP 24; C:/Users/vulpi/FlipCards'))
        privacy.check(self.payload([{'type':'image_url','image_url':{'url':'data:image/jpeg;base64,project-image'}}]))

    def test_secrets_are_blocked_without_echo(self):
        samples = ['sk-local-'+'a'*64, 'password="real-password-123"', '-----BEGIN PRIVATE KEY-----', 'Bearer '+'z'*35, 'https://user:secret@host/path', '<private>persona e dati riservati</private>']
        for secret in samples:
            with self.subTest(secret=secret[:8]):
                with self.assertRaises(privacy.PrivatePayload) as error:
                    privacy.check(self.payload(secret))
                self.assertNotIn(secret, str(error.exception))

    def test_json_arguments_and_placeholder(self):
        with self.assertRaises(privacy.PrivatePayload):
            privacy.check(self.payload(json.dumps({'command':'password="real-password-123"'})))
        privacy.check(self.payload('api_key="YOUR_API_KEY"'))

    def test_outbound_guard_precedes_network(self):
        with patch.object(router, 'HTTPSConnection', side_effect=AssertionError('Network must not open')):
            with self.assertRaises(privacy.PrivatePayload):
                router.upstream(router.FLASH, self.payload('sk-proj-'+'a'*32))

    def test_actual_project_instructions_do_not_block_online(self):
        root=Path(__file__).resolve().parents[2]
        for name in ['.kilo/agent/auto.md','.kilo/local-llm/ART-WORKFLOW.md','AGENTS.md']:
            privacy.check(self.payload((root/name).read_text(encoding='utf-8')))

if __name__ == '__main__':
    unittest.main()
