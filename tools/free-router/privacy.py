"""Outbound secret guard. Project code/art/context are allowed by default.

No prompt logging, silent redaction, or filesystem/path-based project blocking.
Detection is deliberately bounded; callers must mark unstructured private data.
"""
import json
import re


class PrivatePayload(ValueError):
    pass


PATTERNS = {
    'chiave privata': r'-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----',
    'token API': r'\b(?:sk-(?:proj-|local-)?[A-Za-z0-9_-]{20,}|gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|xox[baprs]-[A-Za-z0-9-]{20,}|AKIA[A-Z0-9]{16})\b',
    'credenziale HTTP': r'\b(?:Bearer|Basic)\s+[A-Za-z0-9_+/=-]{16,}',
    'JWT': r'\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}',
    'password o segreto assegnato': r'''(?i)\b(?:password|passwd|api[_-]?key|access[_-]?token|client[_-]?secret)\b["']?\s*[:=]\s*["']([^"'\r\n]{8,})["']''',
    'credenziali in URL': r'https?://[^\s/:@]+:[^\s/@]{4,}@',
    'contenuto dichiarato riservato': r'(?i)<(?:private|confidential|riservato)\b|\[PRIVATE\]|\[RISERVATO\]',
}
PLACEHOLDER = re.compile(r'(?i)^(?:your[_ -].*|example.*|placeholder.*|changeme|redacted|\*+|<[^>]+>|\$\{[^}]+\})$')


def text_parts(value):
    if isinstance(value, str):
        # Image bytes are project assets; inspect surrounding text, not base64.
        if not value.startswith('data:image/'):
            yield value
    elif isinstance(value, dict):
        for key, item in value.items():
            if isinstance(item, str) and re.fullmatch(r'(?i)password|passwd|api[_-]?key|access[_-]?token|client[_-]?secret', key):
                yield key + '=' + repr(item)
        for item in value.values():
            yield from text_parts(item)
    elif isinstance(value, list):
        for item in value:
            yield from text_parts(item)


def check(payload, known_secret_files=()):
    texts = list(text_parts({k: payload[k] for k in ('messages', 'tools', 'response_format') if k in payload}))
    for path in known_secret_files:
        try:
            secret = path.read_text(encoding='utf-8').strip()
        except OSError:
            continue
        if len(secret) >= 8 and any(secret in text for text in texts):
            raise PrivatePayload('Invio online fermato: credenziale locale nel contesto. Rimuovi il segreto o usa un profilo solo locale; i normali file del progetto restano condivisibili.')
    found = set()
    for text in texts:
        # Tool-call arguments are themselves JSON strings.
        candidates = [text]
        try:
            decoded = json.loads(text)
            if isinstance(decoded, (dict, list)):
                candidates.extend(text_parts(decoded))
        except (ValueError, TypeError):
            pass
        for candidate in candidates:
            for label, pattern in PATTERNS.items():
                for match in re.finditer(pattern, candidate):
                    if match.lastindex and PLACEHOLDER.fullmatch(match.group(1)):
                        continue
                    found.add(label)
    if found:
        raise PrivatePayload('Invio online fermato: ' + ', '.join(sorted(found)) + '. Rimuovi i dati riservati o usa un profilo solo locale. Codice e asset del progetto sono condivisibili.')
