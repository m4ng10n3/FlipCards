#!/usr/bin/env python3
"""FlipCards Auto: benchmark-ranked, session-stable OpenAI compatible router."""
import hmac
import json
import os
from pathlib import Path
import threading
import time
from collections import deque
from email.utils import parsedate_to_datetime
from http.client import HTTPConnection, HTTPSConnection
from http.client import HTTPException
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlsplit
import policy
import streaming
import budget

ROOT = Path(__file__).resolve().parent
PORT = int(os.environ.get('ROUTER_PORT', '8099'))
KEY_FILE = Path.home() / '.llama-local/api-key.txt'
SETTINGS = json.loads((ROOT / 'router.settings.json').read_text(encoding='utf-8'))
STATE_FILE = ROOT / 'runtime/state.json'
FAST = 'nex-agi/nex-n2.5-pro:free'
FLASH = 'stepfun/step-3.7-flash:free'
CODE = 'poolside/laguna-s-2.1:free'
VISION = 'inclusionai/ling-3.0-flash-vl:free'
OMNI = 'nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free'
ULTRA = 'nvidia/nemotron-3-ultra-550b-a55b:free'
SUPER = 'nvidia/nemotron-3-super-120b-a12b:free'
# Explicit agent-capable pool, never add catalog specialists automatically.
GROUPS = {'fast': [FAST, FLASH, OMNI], 'code': [CODE, FAST, FLASH, SUPER],
          'vision': [FAST, FLASH, VISION, OMNI], 'reasoning': [SUPER, ULTRA, FAST]}
ENTRIES = {
    'auto': {'group': None, 'label': 'Auto gratuito - benchmark + sessione', 'attachment': True, 'context': 131072},
    'vista': {'group': 'vision', 'label': 'Vista - benchmark immagini e strumenti', 'attachment': True, 'context': 131072},
    'cervello': {'group': 'reasoning', 'label': 'Analisi - benchmark logica', 'attachment': True, 'context': 131072},
    'codice': {'group': 'code', 'label': 'Codice - benchmark implementazione', 'attachment': True, 'context': 131072},
    'locale': {'group': 'local', 'label': 'Solo locale - Qwen 9B', 'attachment': False, 'context': 32768},
    'rapido': {'group': 'local', 'label': 'Locale rapido - Qwen 2B', 'attachment': False, 'context': 16384},
    'coordinatore': {'group': 'local', 'label': 'Coordinatore locale - Qwen 2B', 'attachment': False, 'context': 32768},
}
RETRY_STATUSES = {408, 409, 425, 429, 500, 502, 503, 504, 529}
_lock = threading.RLock()
_catalog_lock = threading.Lock()
_session_locks = [threading.Lock() for _ in range(64)]
_requests = deque(maxlen=2000)
_attempts = deque()
_cooldown = {}
_sticky = {}
_errors = deque(maxlen=20)
_catalog = {'when': 0, 'free': {}, 'error': None}
_active = {}


class ClientDisconnected(Exception):
    pass


def log(message):
    print('[router] ' + message, flush=True)


def local_key():
    return KEY_FILE.read_text(encoding='utf-8').strip()


def persist():
    with _lock:
        STATE_FILE.parent.mkdir(exist_ok=True)
        tmp = STATE_FILE.with_suffix('.tmp')
        tmp.write_text(json.dumps({'attempts': list(_attempts), 'cooldown': _cooldown,
            'sticky': _sticky, 'requests': list(_requests)}), encoding='utf-8')
        tmp.replace(STATE_FILE)


def load_state():
    try:
        data = json.loads(STATE_FILE.read_text(encoding='utf-8'))
        now = time.time()
        _attempts.extend(t for t in data.get('attempts', []) if t > now - 3600)
        _cooldown.update({m: t for m, t in data.get('cooldown', {}).items() if t > now})
        _sticky.update({k: v for k, v in data.get('sticky', {}).items() if v['when'] > now - SETTINGS['session_ttl_seconds']})
        _requests.extend(data.get('requests', [])[-2000:])
    except (OSError, ValueError, KeyError, TypeError):
        pass


def reserve_attempt():
    with _lock:
        now = time.time()
        while _attempts and _attempts[0] <= now - 3600:
            _attempts.popleft()
        if not budget.reserve(SETTINGS['hourly_request_budget'], 'router'):
            return False
        _attempts.append(now)
        persist()
        return True


def refresh_catalog(force=False):
    if not force and time.time() - _catalog['when'] < 1800:
        return _catalog['free']
    if not _catalog_lock.acquire(blocking=False):
        return _catalog['free']
    try:
        conn = HTTPSConnection('api.kilo.ai', timeout=12)
        try:
            conn.request('GET', '/api/openrouter/models')
            resp = conn.getresponse()
            if resp.status != 200:
                raise ValueError('catalogo HTTP %s' % resp.status)
            rows = json.loads(resp.read())['data']
            entries = policy.catalog_entries(rows)
            if not entries:
                raise ValueError('catalogo gratuito vuoto')
            _catalog.update(when=time.time(), free=entries, error=None)
            (ROOT / 'runtime/catalog.json').write_text(json.dumps({'when': time.time(), 'rows': rows}), encoding='utf-8')
        finally:
            conn.close()
    except (OSError, ValueError, KeyError, HTTPException) as exc:
        _catalog.update(when=time.time() - 1500, error=str(exc))
        if not _catalog['free']:
            try:
                cached = json.loads((ROOT / 'runtime/catalog.json').read_text(encoding='utf-8'))
                if time.time() - cached['when'] < 86400:
                    _catalog['free'] = policy.catalog_entries(cached['rows'])
            except (OSError, ValueError, KeyError):
                pass
    finally:
        _catalog_lock.release()
    return _catalog['free']


def benchmarks():
    try:
        data = json.loads((ROOT / 'benchmarks/results.json').read_text(encoding='utf-8'))
        return data if time.time() - data['when'] < 7 * 86400 else {}
    except (OSError, ValueError, KeyError):
        return {}


def local_capabilities():
    try:
        data = json.loads((ROOT/'benchmarks/local-capabilities.json').read_text(encoding='utf-8'))
        return data if time.time()-data['when'] < 7*86400 else {}
    except (OSError, ValueError, KeyError):
        return {}


def ranked_groups():
    bench = benchmarks().get('models', {})
    groups = {}
    for group, pool in GROUPS.items():
        measured = [m for m in pool if bench.get(m, {}).get('fast', {}).get('score', 0) >= .8
                    and time.time() - bench[m]['fast'].get('when', 0) < 7*86400
                    and bench.get(m, {}).get(group, {}).get('passed', 0) >= 2
                    and time.time() - bench[m][group].get('when', 0) < 7*86400
                    and bench[m][group].get('score', 0) >= .8]
        groups[group] = sorted(measured, key=lambda m: (-bench[m][group]['score'], bench[m][group]['median_ms']))
    return groups


def upstream(model, payload, local=False, worker=False):
    body = dict(payload)
    config = SETTINGS['worker'] if worker else SETTINGS['local']
    body['model'] = config['model'] if local else model
    endpoint = config['url'] if local else 'https://api.kilo.ai/api/openrouter/v1'
    url = urlsplit(endpoint)
    klass = HTTPSConnection if url.scheme == 'https' else HTTPConnection
    conn = klass(url.hostname, url.port, timeout=SETTINGS['connect_timeout_seconds'])
    headers = {'Content-Type': 'application/json', 'Accept': 'text/event-stream' if payload.get('stream') else 'application/json'}
    if local:
        headers['Authorization'] = 'Bearer ' + local_key()
    else:
        body['provider'] = {'require_parameters': True}
    if body.get('stream'):
        body['stream_options'] = {**body.get('stream_options', {}), 'include_usage': True}
    try:
        conn.request('POST', url.path.rstrip('/') + '/chat/completions', json.dumps(body).encode(), headers)
        resp = conn.getresponse()
        if conn.sock:
            conn.sock.settimeout(SETTINGS['stall_timeout_seconds'])
        return conn, resp
    except Exception:
        conn.close()
        raise


def cooldown_seconds(header):
    try:
        return max(1, float(header))
    except (TypeError, ValueError):
        try:
            return max(1, parsedate_to_datetime(header).timestamp() - time.time())
        except (TypeError, ValueError, OverflowError):
            return 120


class Handler(BaseHTTPRequestHandler):
    protocol_version = 'HTTP/1.1'
    server_version = 'FlipCardsAuto/3.0'

    def log_message(self, *args):
        pass

    def _json(self, status, data):
        body = json.dumps(data).encode()
        self.send_response(status)
        self.send_header('Content-Type', 'application/json')
        self.send_header('Content-Length', str(len(body)))
        self.send_header('Connection', 'close')
        self.end_headers()
        self.wfile.write(body)
        self.close_connection = True

    def error(self, status, text):
        self._json(status, {'error': {'message': text, 'type': 'router_error', 'code': status}})

    def do_GET(self):
        path = self.path.split('?')[0]
        if path in ('/', '/index.html'):
            body = (ROOT / 'dashboard.html').read_bytes()
            self.send_response(200)
            self.send_header('Content-Type', 'text/html; charset=utf-8')
            self.send_header('Content-Length', str(len(body)))
            self.send_header('Connection', 'close')
            self.end_headers()
            self.wfile.write(body)
            self.close_connection = True
        elif path in ('/health', '/v1/health'):
            self._json(200, {'status': 'ok', 'version': 3, 'pid': os.getpid(), 'active': len(_active)})
        elif path in ('/models', '/v1/models'):
            self._json(200, {'object': 'list', 'data': [{'id': k, 'object': 'model', 'owned_by': 'flipcards-auto'} for k in ENTRIES]})
        elif path == '/api/state':
            with _lock:
                self._json(200, {'port': PORT, 'hourly_limit': SETTINGS['hourly_request_budget'],
                    'used_last_hour': budget.used(),
                    'cooling': {m: round(t - time.time()) for m, t in _cooldown.items() if t > time.time()},
                    'baseline': 'solo modelli qualificati dai benchmark', 'entries': ENTRIES,
                    'groups': {g: {'models': ms, 'note': 'ordinati per successo, poi latenza'} for g, ms in ranked_groups().items()},
                    'catalogo': {'modelli_gratuiti': len(_catalog['free']), 'con_vista': sum(bool(m['image']) for m in _catalog['free'].values()), 'errore': _catalog['error']},
                    'last': [{'quando': time.strftime('%H:%M:%S', time.localtime(r[0])), 'modello': r[1], 'immagini': r[2], 'stato': r[3], 'ms': r[4], 'token': r[5], 'gruppo': r[6], 'motivo': r[7]} for r in reversed(list(_requests)[-15:])],
                    'errori': list(_errors), 'active': list(_active.values()), 'benchmarks': benchmarks(), 'classifier': SETTINGS['classifier'], 'local_capabilities': local_capabilities()})
        else:
            self.error(404, 'Percorso non trovato')

    def do_POST(self):
        try:
            self.handle_post()
        except (BrokenPipeError, ConnectionResetError, ClientDisconnected):
            self.close_connection = True

    def handle_post(self):
        if self.path not in ('/v1/chat/completions', '/chat/completions'):
            return self.error(404, 'Percorso non trovato')
        if not hmac.compare_digest(self.headers.get('Authorization', ''), 'Bearer ' + local_key()):
            return self.error(401, 'API key locale non valida')
        try:
            length = int(self.headers.get('Content-Length', '0'))
            if not 0 < length <= 32 * 1024 * 1024:
                return self.error(413, 'Dimensione richiesta non valida (massimo 32 MB)')
            payload = json.loads(self.rfile.read(length))
            if not isinstance(payload, dict) or not isinstance(payload.get('messages'), list) or not payload['messages']:
                raise ValueError('messages deve essere una lista non vuota')
            requested = str(payload.get('model', 'auto')).removeprefix('router/')
            if requested not in ENTRIES:
                raise ValueError('modello router sconosciuto')
            for field in ('max_tokens', 'max_completion_tokens'):
                if field in payload and (not isinstance(payload[field], int) or payload[field] <= 0):
                    raise ValueError(field + ' deve essere positivo')
        except (ValueError, TypeError) as exc:
            return self.error(400, str(exc))
        payload = policy.role_payload(payload, self.headers.get('X-FlipCards-Agent'))
        key = policy.session_key(payload, self.headers.get('X-FlipCards-Session'))
        mutex = _session_locks[int(key[:8], 16) % len(_session_locks)]
        if not mutex.acquire(timeout=1):
            return self.error(409, 'Una richiesta della sessione e gia attiva; riprova al completamento')
        try:
            self.route(payload, requested, key)
        finally:
            with _lock:
                _active.pop(key, None)
            mutex.release()

    def route(self, payload, requested, key):
        local = requested in ('locale', 'rapido', 'coordinatore')
        worker = requested in ('rapido', 'coordinatore')
        local_config = SETTINGS['worker'] if worker else SETTINGS['local']
        stream = bool(payload.get('stream'))
        payload = dict(payload)
        if not payload.get('max_tokens') and not payload.get('max_completion_tokens'):
            payload['max_tokens'] = local_config['output'] if local else 8192
        images = bool(policy.image_count(payload))
        if local:
            if images or policy.input_tokens(payload) + int(payload.get('max_completion_tokens') or payload.get('max_tokens')) > local_config['context']:
                return self.error(400, 'Il modello locale non supporta questa immagine o contesto; nessun invio online')
            group, why, chain = 'local', 'solo locale esplicito', [local_config['model']]
        else:
            catalog = refresh_catalog()
            pinned = _sticky.get(key, {})
            if time.time() - pinned.get('when', 0) > SETTINGS['session_ttl_seconds']:
                pinned = {}
            forced = ENTRIES[requested]['group']
            group, why = policy.classify(payload)
            if forced:
                group, why = forced, 'profilo manuale ' + requested
            elif pinned:
                group, why = pinned['group'], 'continuita della sessione'
            elif not images and local_capabilities().get('routing_passed'):
                advised = policy.local_classify(payload, SETTINGS, local_key())
                if advised:
                    group, why = advised
            if images:
                group = 'vision'
            elif policy.needs_code(payload) and not forced:
                group, why = 'code', why + '; operazione che richiede codice'
            groups = ranked_groups()
            if images and policy.needs_code(payload):
                # A visual repair still needs a model qualified to edit code.
                groups['vision'] = [m for m in groups['vision'] if m in groups['code']]
            sticky_model = pinned.get('model') if pinned.get('entry') == requested else None
            if sticky_model and sticky_model not in groups[group]:
                sticky_model = None
            chain = policy.candidates(group, payload, catalog, groups, _cooldown, sticky_model, bool(forced), list(_requests))
            if not chain:
                return self.error(503, 'Nessun modello qualificato disponibile per contesto, strumenti, immagini e cooldown. Consulta il cruscotto o rilancia i benchmark.')
        log('%s %s -> %s (%s)' % (key[:8], group, chain[0], why))
        headers_sent = False
        deadline = time.monotonic() + SETTINGS['request_timeout_seconds']
        last_error = 'nessun candidato'
        for model in chain[:SETTINGS['max_attempts']]:
            if time.monotonic() >= deadline:
                break
            if not local and not reserve_attempt():
                return self.error(429, 'Budget orario locale esaurito; non misura la quota residua del gateway')
            started = time.time()
            status, tokens, conn = 502, None, None
            with _lock:
                _active[key] = {'session': key[:8], 'model': model, 'group': group, 'since': started}
            try:
                conn, resp = upstream(model, payload, local, worker)
                if resp.status != 200:
                    status = resp.status
                    last_error = '%s: HTTP %d' % (model, status)
                    if status in RETRY_STATUSES or status in (404, 413, 422):
                        with _lock:
                            _cooldown[model] = time.time() + cooldown_seconds(resp.getheader('Retry-After'))
                        continue
                    return self.error(status, last_error)

                def start():
                    nonlocal headers_sent
                    self.send_response(200)
                    self.send_header('Content-Type', 'text/event-stream' if stream else 'application/json')
                    self.send_header('Cache-Control', 'no-cache')
                    self.send_header('Connection', 'close')
                    self.send_header('X-FlipCards-Model', model)
                    self.send_header('X-FlipCards-Route', group)
                    self.end_headers()
                    headers_sent = True

                def write(data):
                    try:
                        self.wfile.write(data)
                        self.wfile.flush()
                    except (BrokenPipeError, ConnectionResetError) as exc:
                        raise ClientDisconnected() from exc

                if stream:
                    tokens = streaming.forward(resp, start, write, deadline)
                else:
                    data = resp.read()
                    obj = json.loads(data)
                    if obj.get('error') or not obj.get('choices'):
                        raise streaming.UpstreamError('risposta JSON senza choices o con errore')
                    tokens = (obj.get('usage') or {}).get('total_tokens')
                    start()
                    write(data)
                status = 200
                if not local:
                    with _lock:
                        _sticky[key] = {'model': model, 'group': group, 'entry': requested, 'when': time.time()}
                return
            except ClientDisconnected:
                status = 499
                return
            except (OSError, ValueError, HTTPException, streaming.UpstreamError) as exc:
                last_error = '%s: %s' % (model, type(exc).__name__ + ': ' + str(exc)[:120])
                with _lock:
                    _cooldown[model] = time.time() + 120
                    _errors.append(last_error)
                if headers_sent:
                    # No successful DONE or replay once any model output was delivered.
                    if stream:
                        write(('data: ' + json.dumps({'error': {'message': last_error, 'code': 502}}) + '\n\n').encode())
                    return
            finally:
                if conn:
                    conn.close()
                with _lock:
                    _requests.append((time.time(), model, images, status, int((time.time() - started) * 1000), tokens, group, why))
                    persist()
                self.close_connection = True
        if not headers_sent:
            self.error(503, 'Provider gratuiti temporaneamente indisponibili: ' + last_error)


def main():
    if not KEY_FILE.exists() or not local_key():
        raise SystemExit('Manca la chiave locale: avvia Start-FreeRouter.ps1')
    STATE_FILE.parent.mkdir(exist_ok=True)
    load_state()
    refresh_catalog()
    server = ThreadingHTTPServer(('127.0.0.1', PORT), Handler)
    server.daemon_threads = True
    log('Auto v3 pronto: http://127.0.0.1:%d/' % PORT)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        persist()
        server.server_close()


if __name__ == '__main__':
    main()
