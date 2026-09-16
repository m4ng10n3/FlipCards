import unittest
from streaming import error_summary


class DiagnosticTests(unittest.TestCase):
    def test_context_classification_without_echo(self):
        result = error_summary({'code': 400, 'message': 'Maximum context exceeded: private prompt contents'})
        self.assertIn('contesto troppo grande HTTP 400', result)
        self.assertNotIn('private', result)

    def test_untrusted_fields_never_echoed(self):
        self.assertEqual(error_summary({'code': 'secret', 'message': 'secret', 'metadata': 'secret'}),
                         'errore del provider nello stream')
        self.assertEqual(error_summary('secret'), 'errore del provider nello stream')

    def test_rate_limit_and_capacity(self):
        self.assertIn('limite richieste', error_summary({'message': 'Rate limit exceeded', 'code': 429}))
        self.assertIn('provider indisponibile', error_summary({'message': 'No endpoints available'}))


if __name__ == '__main__':
    unittest.main()
