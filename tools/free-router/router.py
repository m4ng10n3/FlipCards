#!/usr/bin/env python3
"""Instradatore locale per i modelli gratuiti del gateway Kilo.

Si presenta a Kilo come un provider OpenAI-compatibile su http://127.0.0.1:8099/v1,
quindi le sue voci compaiono nel menu "Select model" di VS Code.

La logica segue quella documentata per Kilo Auto (classificare il task, distribuire il
traffico sui modelli migliori, ricadere su una baseline perche' la qualita' non crolli),
con tre aggiunte che a Kilo Auto Free mancano:

- le immagini: Auto Free dichiara `attachment: false` e non le manda affatto, qui invece
  una richiesta con immagini va su un modello che le legge (serve alle catture di Unity);
- un cooldown sui modelli che rispondono 429, che nel gratuito capita spesso (misurato:
  tre modelli su otto contemporaneamente);
- la latenza: Nemotron Ultra risponde bene ma con i prompt grossi di un agente impiega
  minuti per passo, quindi lo si usa solo su richieste corte di analisi.

Cruscotto su http://127.0.0.1:8099/ con regole, decisioni e consumo dell'ora.
Nessuna credenziale: i modelli :free del gateway rispondono senza autenticazione, con un
tetto di 200 richieste all'ora per indirizzo IP. Verso Kilo invece si pretende la stessa
API key del server locale, per evitare che altre pagine aperte nel browser lo usino.
"""

import json
import os
import re
import ssl
import sys
import threading
import time
from collections import deque
from http.client import HTTPSConnection
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

PORT = int(os.environ.get("ROUTER_PORT", "8099"))
UPSTREAM_HOST = "api.kilo.ai"
UPSTREAM_PATH = "/api/openrouter/v1/chat/completions"
HOURLY_LIMIT = 200
KEY_FILE = os.path.join(os.path.expanduser("~"), ".llama-local", "api-key.txt")
# Aggiunge in testa alla risposta una riga di ragionamento con la decisione presa,
# cosi' l'instradamento si vede nella chat di Kilo e non solo nel cruscotto.
SHOW_ROUTING = True

VISION = "inclusionai/ling-3.0-flash-vl:free"
OMNI = "nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free"
ULTRA = "nvidia/nemotron-3-ultra-550b-a55b:free"
SUPER = "nvidia/nemotron-3-super-120b-a12b:free"
CODE = "poolside/laguna-s-2.1:free"
CODE_XS = "poolside/laguna-xs-2.1:free"
FAST = "nex-agi/nex-n2.5-pro:free"
FLASH = "stepfun/step-3.7-flash:free"
LIGHTNING = "nvidia/nemotron-3.5-lightning:free"

# Ordine statico di preferenza dentro ogni gruppo. Latenze misurate il 12/09/2026 con un
# prompt da ~3,9k token: nex-pro 3,2 s, step-flash 4,0 s, nano-omni 6,0 s, lightning
# 14,1 s (si porta il ragionamento nel testo), ling-vl 55 s su una cattura vera.
GROUPS = {
    "vision": [VISION, OMNI],
    "fast": [FAST, FLASH, OMNI, LIGHTNING, CODE],
    "code": [CODE, CODE_XS, FAST, FLASH],
    "reasoning": [ULTRA, SUPER, FAST],
}
GROUP_NOTES = {
    "vision": "richieste con immagini: modelli che le leggono",
    "fast": "passi di lavoro con strumenti: si guarda alla latenza",
    "code": "lavoro sul codice: modelli specializzati",
    "reasoning": "analisi difficili su prompt corti: Ultra e Super",
}
# Se tutto il gruppo e' a 429 si finisce qui, come la baseline di Kilo Auto.
BASELINE = FAST
COOLDOWN_SECONDS = 120
# Un fornitore gratuito puo' rispondere 200 e poi smettere di inviare dati: misurato il
# 12/09/2026 con step-3.7-flash, che ha tenuto appesa una richiesta per sei minuti.
STALL_SECONDS = 75
# Oltre questa dimensione del corpo della richiesta i modelli giganti costano minuti.
REASONING_MAX_BODY = 24000
REASONING_WORDS = ("perch", "analiz", "progett", "refactor", "debug", "architett",
                   "invariant", "spiega", "confront", "valuta", "ottimizz", "strategi",
                   "come mai", "diagnos")

# Voce esposta a Kilo -> gruppo forzato (None = decide la classificazione).
ENTRIES = {
    "auto": {"group": None, "label": "Free Auto (classifica e distribuisce)", "attachment": True,
             "context": 262144},
    "vista": {"group": "vision", "label": "Free Vista (immagini)", "attachment": True,
              "context": 262144},
    "cervello": {"group": "reasoning", "label": "Free Cervello (Ultra 550B, 1M)", "attachment": False,
                 "context": 1000000},
    "codice": {"group": "code", "label": "Free Codice (Laguna)", "attachment": False,
               "context": 262144},
}

RETRY_STATUSES = {408, 409, 425, 429, 500, 502, 503, 504, 529}

_lock = threading.Lock()
_requests = deque(maxlen=2000)  # (ts, modello, immagini, stato, ms, token, gruppo, motivo)
_counts = {}
_cooldown = {}
_errors = deque(maxlen=40)

# Catalogo del gateway: dice quali modelli gratuiti esistono adesso e quali accettano
# immagini (`architecture.input_modalities`). Serve a non dover riscrivere le liste qui
# sopra quando il gruppo gratuito cambia.
# Il catalogo sta sotto la radice del gateway; si prova anche la variante con /v1.
CATALOG_PATHS = ("/api/openrouter/models", "/api/openrouter/v1/models")
CATALOG_TTL = 1800
CATALOG_RETRY = 300
_catalog = {"when": 0, "free": {}, "error": None, "fonte": None}


def refresh_catalog(force=False):
    """Legge dal gateway quali modelli gratuiti esistono e quali accettano immagini.

    Se non ci riesce non blocca nulla: si continua con le liste statiche, e si riprova
    dopo CATALOG_RETRY secondi invece che a ogni richiesta.
    """
    now = time.time()
    if not force and now - _catalog["when"] < CATALOG_TTL:
        return _catalog
    problems = []
    for path in CATALOG_PATHS:
        try:
            conn = HTTPSConnection(UPSTREAM_HOST, timeout=30, context=ssl.create_default_context())
            conn.request("GET", path, headers={"User-Agent": "flipcards-free-router/2.0"})
            resp = conn.getresponse()
            body = resp.read().decode("utf-8", "replace")
            conn.close()
            if resp.status != 200:
                problems.append("%s HTTP %d" % (path, resp.status))
                continue
            data = json.loads(body)
        except Exception as exc:
            problems.append("%s %s" % (path, str(exc)[:60]))
            continue
        free = {}
        for entry in data.get("data", []):
            mid = entry.get("id", "")
            if not mid.endswith(":free"):
                continue
            arch = entry.get("architecture") or {}
            mods = arch.get("input_modalities") or []
            free[mid] = {
                "image": "image" in mods,
                "context": entry.get("context_length") or 0,
                "name": entry.get("name") or mid,
            }
        if free:
            _catalog.update({"when": now, "free": free, "error": None, "fonte": path})
            log("catalogo: %d modelli gratuiti, %d con la vista (da %s)"
                % (len(free), sum(1 for v in free.values() if v["image"]), path))
            return _catalog
        problems.append("%s nessun :free" % path)
    # Riprova fra CATALOG_RETRY secondi, non a ogni richiesta.
    _catalog["when"] = now - CATALOG_TTL + CATALOG_RETRY
    message = "; ".join(problems)[:160]
    if message != _catalog.get("error"):
        log("catalogo non letto: %s (riprovo fra %d s)" % (message, CATALOG_RETRY))
    _catalog["error"] = message
    return _catalog


def median_ms(model):
    with _lock:
        vals = [r[4] for r in _requests if r[1] == model and r[3] == 200][-5:]
    return sorted(vals)[len(vals) // 2] if vals else None


def effective_groups():
    """Gruppi statici filtrati su cio' che il catalogo offre, piu' i modelli nuovi.

    L'ordine di qualita' scelto a mano si tiene per vision, reasoning e code; il gruppo
    fast viene riordinato sulla latenza misurata, che e' il criterio che conta nei passi
    di lavoro di un agente.
    """
    free = refresh_catalog()["free"]

    def available(models):
        return [m for m in models if not free or m in free]

    groups = {
        "vision": available(GROUPS["vision"]),
        "fast": available(GROUPS["fast"]),
        "code": available(GROUPS["code"]),
        "reasoning": available(GROUPS["reasoning"]),
    }
    known = set(GROUPS["vision"]) | set(GROUPS["fast"]) | set(GROUPS["code"]) | set(GROUPS["reasoning"])
    for mid, meta in free.items():
        if mid in known:
            continue
        if meta["image"]:
            groups["vision"].append(mid)
        else:
            groups["fast"].append(mid)
    if not groups["reasoning"]:
        groups["reasoning"] = groups["fast"][:2]
    static = {m: i for i, m in enumerate(groups["fast"])}
    groups["fast"].sort(key=lambda m: (median_ms(m) if median_ms(m) is not None else 10 ** 6 + static[m]))
    for name in groups:
        if not groups[name]:
            groups[name] = [BASELINE]
    return groups


def local_key():
    try:
        with open(KEY_FILE, "r", encoding="utf-8") as fh:
            return fh.read().strip()
    except OSError:
        return None


def log(msg):
    print("[router] %s" % msg, flush=True)


def record(model, images, status, ms, tokens, group, why):
    with _lock:
        _requests.append((time.time(), model, images, status, ms, tokens, group, why))
        _counts[model] = _counts.get(model, 0) + 1


def used_last_hour():
    cutoff = time.time() - 3600
    with _lock:
        return sum(1 for r in _requests if r[0] >= cutoff)


def has_images(payload):
    """True se nei messaggi c'e' almeno un'immagine, in qualunque forma.

    Kilo puo' mandarla come parte `image_url`, oppure come base64 dentro il testo del
    risultato di uno strumento MCP (le catture di Unity).
    """
    try:
        for msg in payload.get("messages", []):
            content = msg.get("content")
            if isinstance(content, list):
                for part in content:
                    if isinstance(part, dict):
                        kind = part.get("type", "")
                        if "image" in kind or part.get("image_url") or part.get("image"):
                            return True
                        text = part.get("text")
                        if isinstance(text, str) and "data:image/" in text[:4096]:
                            return True
            elif isinstance(content, str) and "data:image/" in content[:4096]:
                return True
    except Exception:
        pass
    return False


def last_user_text(payload):
    for msg in reversed(payload.get("messages", [])):
        if msg.get("role") != "user":
            continue
        content = msg.get("content")
        if isinstance(content, str):
            return content
        if isinstance(content, list):
            return " ".join(p.get("text", "") for p in content if isinstance(p, dict))
    return ""


def classify(payload, images, body_len):
    """Come Kilo Auto: si distingue il ragionamento dall'implementazione."""
    if images:
        return "vision", "immagini nella richiesta"
    lowered = last_user_text(payload).lower()
    heavy = any(w in lowered for w in REASONING_WORDS)
    if heavy and body_len < REASONING_MAX_BODY:
        return "reasoning", "richiesta di analisi con prompt corto"
    if heavy:
        return "fast", "analisi ma prompt grosso: i modelli lenti costerebbero minuti"
    return "fast", "passo di lavoro"


def pick(group):
    """Catena da provare: prima chi e' libero e ha lavorato meno, poi gli altri."""
    now = time.time()
    preferred = effective_groups().get(group) or GROUPS[group]
    ready = [m for m in preferred if _cooldown.get(m, 0) < now]
    if not ready:
        ready = sorted(preferred, key=lambda m: _cooldown.get(m, 0))
    cutoff = now - 600
    with _lock:
        recent = {}
        for row in _requests:
            if row[0] >= cutoff:
                recent[row[1]] = recent.get(row[1], 0) + 1
    ordered = sorted(ready, key=lambda m: (recent.get(m, 0), preferred.index(m)))
    chain = ordered + [m for m in preferred if m not in ordered]
    if BASELINE not in chain:
        chain.append(BASELINE)
    return chain


def describe(payload):
    """Struttura compatta della richiesta, per capire cosa manda Kilo."""
    parts = []
    for msg in payload.get("messages", []):
        role = str(msg.get("role"))[:4]
        content = msg.get("content")
        if isinstance(content, str):
            parts.append("%s:testo/%d" % (role, len(content)))
        elif isinstance(content, list):
            kinds = []
            for p in content:
                if isinstance(p, dict):
                    try:
                        size = len(json.dumps(p))
                    except Exception:
                        size = -1
                    kinds.append("%s/%d" % (p.get("type", "?"), size))
                else:
                    kinds.append("?")
            parts.append("%s:[%s]" % (role, ",".join(kinds)))
        else:
            parts.append("%s:%s" % (role, type(content).__name__))
    if payload.get("tools"):
        parts.append("tools/%d" % len(payload["tools"]))
    return " ".join(parts)


def extract_tokens(text):
    m = re.search(r'"total_tokens"\s*:\s*(\d+)', text or "")
    return int(m.group(1)) if m else None


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"
    server_version = "FlipCardsFreeRouter/2.0"

    def log_message(self, fmt, *args):
        pass

    def _json(self, code, obj):
        body = json.dumps(obj).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Connection", "close")
        self.end_headers()
        self.wfile.write(body)
        self.close_connection = True

    def _authorised(self):
        key = local_key()
        if not key:
            return True
        return self.headers.get("Authorization", "") == "Bearer " + key

    # --- GET --------------------------------------------------------------------
    def do_GET(self):
        path = self.path.split("?")[0]
        if path in ("/", "/index.html"):
            body = DASHBOARD_HTML.encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Connection", "close")
            self.end_headers()
            self.wfile.write(body)
            self.close_connection = True
            return
        if path == "/api/state":
            self._json(200, self.state())
            return
        if path in ("/v1/models", "/models"):
            self._json(200, {"object": "list", "data": [
                {"id": name, "object": "model", "owned_by": "kilo-free-router"}
                for name in ENTRIES
            ]})
            return
        if path in ("/health", "/v1/health"):
            self._json(200, {"status": "ok"})
            return
        self._json(404, {"error": {"message": "not found", "code": 404}})

    def state(self):
        now = time.time()
        cutoff = now - 3600
        with _lock:
            recent = [r for r in _requests if r[0] >= cutoff]
            last = list(_requests)[-12:]
            counts = dict(_counts)
            errors = list(_errors)[-8:]
            cooling = {m: int(t - now) for m, t in _cooldown.items() if t > now}
        return {
            "port": PORT,
            "hourly_limit": HOURLY_LIMIT,
            "used_last_hour": len(recent),
            "counts": counts,
            "cooling": cooling,
            "baseline": BASELINE,
            "groups": {g: {"models": ms, "note": GROUP_NOTES.get(g, "")}
                       for g, ms in effective_groups().items()},
            "catalogo": {
                "modelli_gratuiti": len(_catalog["free"]),
                "con_vista": sum(1 for v in _catalog["free"].values() if v["image"]),
                "aggiornato": time.strftime("%H:%M:%S", time.localtime(_catalog["when"])) if _catalog["when"] else "mai",
                "errore": _catalog["error"],
            },
            "entries": {name: {"label": e["label"], "group": e["group"] or "deciso dalla classificazione",
                               "attachment": e["attachment"], "context": e["context"]}
                        for name, e in ENTRIES.items()},
            "last": [
                {"quando": time.strftime("%H:%M:%S", time.localtime(r[0])), "modello": r[1],
                 "immagini": r[2], "stato": r[3], "ms": r[4], "token": r[5],
                 "gruppo": r[6], "motivo": r[7]}
                for r in reversed(last)
            ],
            "errori": errors,
        }

    # --- POST -------------------------------------------------------------------
    def do_POST(self):
        path = self.path.split("?")[0]
        # Il corpo va letto sempre, anche per rispondere un errore: se resta nel socket,
        # con le connessioni riusate la richiesta successiva viene interpretata male.
        length = int(self.headers.get("Content-Length", "0"))
        raw = self.rfile.read(length) if length else b"{}"

        if path not in ("/v1/chat/completions", "/chat/completions"):
            self._json(404, {"error": {"message": "not found", "code": 404}})
            return
        if not self._authorised():
            self._json(401, {"error": {"message": "API key non valida", "code": 401}})
            return
        try:
            payload = json.loads(raw.decode("utf-8"))
        except Exception as exc:
            self._json(400, {"error": {"message": "JSON non valido: %s" % exc, "code": 400}})
            return

        requested = str(payload.get("model", "auto")).split("/")[-1]
        entry = ENTRIES.get(requested, ENTRIES["auto"])
        images = has_images(payload)
        if entry["group"]:
            group, why = entry["group"], "voce %s scelta a mano" % requested
            if images and group != "vision":
                group, why = "vision", "immagini presenti: la voce %s non le legge" % requested
        else:
            group, why = classify(payload, images, len(raw))
        chain = pick(group)

        log("richiesta %s (%.1f KB) gruppo=%s (%s) | %s" % (
            requested, len(raw) / 1024.0, group, why, describe(payload)))
        used = used_last_hour()
        if used >= HOURLY_LIMIT:
            log("attenzione: tetto orario raggiunto (%d/%d)" % (used, HOURLY_LIMIT))

        stream = bool(payload.get("stream"))
        last_error = None
        headers_sent = False
        sent_content = False
        for model in chain:
            payload["model"] = model
            body = json.dumps(payload).encode("utf-8")
            started = time.time()
            try:
                conn = HTTPSConnection(UPSTREAM_HOST, timeout=900,
                                       context=ssl.create_default_context())
                conn.request("POST", UPSTREAM_PATH, body=body, headers={
                    "Content-Type": "application/json",
                    "Accept": "text/event-stream" if stream else "application/json",
                    "User-Agent": "flipcards-free-router/2.0",
                })
                resp = conn.getresponse()
            except Exception as exc:
                last_error = "%s: connessione fallita (%s)" % (model, exc)
                log(last_error)
                with _lock:
                    _errors.append(last_error)
                continue

            if resp.status >= 400:
                detail = resp.read().decode("utf-8", "replace")
                msg = "%s: HTTP %d %s" % (model, resp.status, detail[:180].replace("\n", " "))
                log(msg)
                with _lock:
                    _errors.append(msg)
                    if resp.status == 429:
                        _cooldown[model] = time.time() + COOLDOWN_SECONDS
                record(model, images, resp.status, int((time.time() - started) * 1000), None, group, why)
                conn.close()
                # Sui 400 di una richiesta con immagini vale la pena provare l'altro
                # modello con la vista: capita che uno rifiuti un formato accettato dall'altro.
                retryable = resp.status in RETRY_STATUSES or (images and resp.status == 400)
                if retryable and model != chain[-1]:
                    last_error = msg
                    continue
                if headers_sent:
                    # La risposta e' gia' partita verso Kilo: si chiude lo stream pulito.
                    try:
                        self.wfile.write(b"data: [DONE]\n\n")
                        self.wfile.flush()
                    except Exception:
                        pass
                    self.close_connection = True
                    return
                payload_err = detail.encode("utf-8")
                self.send_response(resp.status)
                self.send_header("Content-Type", resp.getheader("Content-Type") or "application/json")
                self.send_header("Content-Length", str(len(payload_err)))
                self.send_header("Connection", "close")
                self.end_headers()
                self.wfile.write(payload_err)
                self.close_connection = True
                return

            log("%s -> %s (%s)" % (group, model, resp.status))
            if not headers_sent:
                self.send_response(resp.status)
                ctype = resp.getheader("Content-Type") or ("text/event-stream" if stream else "application/json")
                self.send_header("Content-Type", ctype)
                self.send_header("Cache-Control", "no-cache")
                self.send_header("Connection", "close")
                self.end_headers()
                headers_sent = True

            if stream and SHOW_ROUTING:
                note = "[router] %s (%s) -> %s" % (group, why, model)
                chunk = {"id": "router", "object": "chat.completion.chunk",
                         "created": int(time.time()), "model": model,
                         "choices": [{"index": 0, "delta": {"reasoning_content": note},
                                      "finish_reason": None}]}
                try:
                    self.wfile.write(("data: " + json.dumps(chunk) + "\n\n").encode("utf-8"))
                    self.wfile.flush()
                except Exception:
                    pass

            tokens = None
            stalled = False
            try:
                if stream:
                    try:
                        conn.sock.settimeout(STALL_SECONDS)
                    except Exception:
                        pass
                    # Si legge dalla risposta HTTP (che togle il chunked transfer
                    # encoding) e non dal socket grezzo: leggendo da resp.fp i
                    # marcatori dei blocchi finivano dentro lo stream e Kilo riceveva
                    # JSON troncato.
                    tail = b""
                    while True:
                        try:
                            block = resp.read1(8192)
                        except Exception as exc:
                            stalled = True
                            log("%s: niente dati per %d s (%s)" % (model, STALL_SECONDS, type(exc).__name__))
                            break
                        if not block:
                            break
                        self.wfile.write(block)
                        self.wfile.flush()
                        combined = tail + block
                        if b'"content"' in combined or b'"tool_calls"' in combined:
                            sent_content = True
                        if b'"total_tokens"' in combined:
                            tokens = extract_tokens(combined.decode("utf-8", "replace"))
                        tail = combined[-80:]
                else:
                    data = resp.read()
                    tokens = extract_tokens(data.decode("utf-8", "replace"))
                    self.wfile.write(data)
                    self.wfile.flush()
                    sent_content = True
            except Exception as exc:
                log("interrotto mentre inoltravo: %s" % exc)
            finally:
                conn.close()

            if stalled:
                msg = "%s: stallo, nessun dato per %d s" % (model, STALL_SECONDS)
                with _lock:
                    _errors.append(msg)
                    _cooldown[model] = time.time() + COOLDOWN_SECONDS
                record(model, images, 599, int((time.time() - started) * 1000), tokens, group, why)
                if not sent_content and model != chain[-1]:
                    last_error = msg
                    continue
                try:
                    self.wfile.write(b"data: [DONE]\n\n")
                    self.wfile.flush()
                except Exception:
                    pass
                self.close_connection = True
                return

            record(model, images, resp.status, int((time.time() - started) * 1000), tokens, group, why)
            self.close_connection = True
            return

        self._json(503, {"error": {"message": "nessun modello gratuito disponibile: %s" % last_error,
                                   "code": 503}})


DASHBOARD_HTML = """<!doctype html>
<html lang="it"><head><meta charset="utf-8"><title>Router gratuito FlipCards</title>
<style>
 body{font:14px/1.5 system-ui,Segoe UI,sans-serif;margin:0;padding:20px;background:#14181d;color:#e6e6e6}
 h1{font-size:18px;margin:0 0 4px} h2{font-size:14px;margin:22px 0 8px;color:#9fd0ff}
 .sub{color:#8b949e;margin-bottom:16px;max-width:900px}
 table{border-collapse:collapse;width:100%;max-width:1000px}
 th,td{text-align:left;padding:6px 10px;border-bottom:1px solid #262d36;vertical-align:top}
 th{color:#8b949e;font-weight:600}
 code{background:#1e242b;padding:1px 5px;border-radius:4px}
 .bar{height:10px;background:#1e242b;border-radius:5px;overflow:hidden;max-width:420px}
 .bar>i{display:block;height:100%;background:#3fb950}
 .warn>i{background:#d29922} .full>i{background:#f85149}
 .big{font-size:26px;font-weight:600}
 .ok{color:#3fb950} .ko{color:#f85149} .muted{color:#8b949e}
</style></head><body>
<h1>Router gratuito FlipCards</h1>
<div class="sub">Instrada le richieste di Kilo sui modelli <code>:free</code> del gateway: classifica il
compito, distribuisce il traffico, evita per due minuti chi risponde 429 e manda le immagini a chi le legge.
Nessun costo, nessuna credenziale. Aggiornamento ogni 5 secondi.</div>
<div class="big"><span id="used">-</span> <span class="muted">/ <span id="limit">200</span> richieste nell'ultima ora</span></div>
<div class="bar" id="bar"><i id="fill" style="width:0%"></i></div>
<h2>Come si seleziona in Kilo (VS Code)</h2>
<table>
<tr><th>passo</th><th>dove</th></tr>
<tr><td>1. Ricarica la configurazione dopo ogni modifica</td>
    <td>Ctrl+Shift+P &rarr; <code>Kilo: Reload Config and Skills</code></td></tr>
<tr><td>2. Porta la chat in un tab</td>
    <td>Ctrl+Shift+P &rarr; <code>Kilo: Open in Tab</code></td></tr>
<tr><td>3. Scegli l'agente</td>
    <td>menu <b>Choose agent</b> accanto alla casella di testo: <code>cloud</code>, <code>pesante</code>,
        <code>locale</code>. Da tastiera il comando <i>Next agent mode</i>; Ctrl+Shift+A torna sulla casella.</td></tr>
<tr><td>4. (opzionale) Forza un modello</td>
    <td>menu <b>Select model</b> &rarr; cerca <code>Free</code> &rarr; gruppo <b>Free Router (locale)</b>.
        Senza toccarlo vale il modello dell'agente.</td></tr>
<tr><td>5. Vedi l'instradamento</td>
    <td>la prima riga di ragionamento di ogni risposta dice gruppo, motivo e modello scelto.</td></tr>
</table>
<h2>Come si avvia</h2>
<table>
<tr><th>cosa</th><th>come</th></tr>
<tr><td>Questo instradatore</td><td>Ctrl+Shift+P &rarr; <code>Tasks: Run Task</code> &rarr; <code>Router gratuito: avvia</code></td></tr>
<tr><td>Questa pagina in un tab</td><td>Ctrl+Shift+P &rarr; <code>Simple Browser: Show</code> &rarr; <code>http://127.0.0.1:8099/</code></td></tr>
<tr><td>Modello locale</td><td>task <code>LLM locale: avvia server</code>: serve solo all'agente <code>locale</code></td></tr>
</table>
<h2>Voci nel selettore e gruppi</h2>
<table id="entries"></table>
<h2>Gruppi di modelli</h2>
<table id="groups"></table>
<div class="sub" id="catalog"></div>
<h2>Ultime decisioni</h2>
<table id="last"></table>
<h2>Errori recenti</h2>
<table id="errors"></table>
<div class="sub">I modelli gratuiti dichiarano che i prompt possono essere registrati e usati per
addestramento: quello che mandi agli agenti <code>cloud</code> e <code>pesante</code> esce dalla macchina.
Per lavori riservati usa <code>locale</code>.</div>
<script>
function esc(s){return String(s).replace(/[&<>]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;'}[c]))}
async function tick(){
 let s; try{ s=await (await fetch('/api/state')).json() }catch(e){ return }
 document.getElementById('used').textContent=s.used_last_hour;
 document.getElementById('limit').textContent=s.hourly_limit;
 const pct=Math.min(100,100*s.used_last_hour/s.hourly_limit);
 document.getElementById('fill').style.width=pct+'%';
 document.getElementById('bar').className='bar'+(pct>90?' full':pct>60?' warn':'');
 let e='<tr><th>voce</th><th>gruppo</th><th>immagini</th><th>contesto</th></tr>';
 for(const [k,v] of Object.entries(s.entries)){
  e+=`<tr><td><code>${esc(k)}</code> ${esc(v.label)}</td><td>${esc(v.group)}</td>
      <td>${v.attachment?'<span class="ok">si</span>':'<span class="muted">no</span>'}</td>
      <td>${(v.context/1000).toFixed(0)}k</td></tr>`;
 }
 document.getElementById('entries').innerHTML=e;
 let g='<tr><th>gruppo</th><th>quando si usa</th><th>modelli, in ordine di preferenza</th></tr>';
 for(const [k,v] of Object.entries(s.groups)){
  g+=`<tr><td><code>${esc(k)}</code></td><td>${esc(v.note)}</td><td class="muted">${v.models.map(m=>{
    const cool=s.cooling[m]; return esc(m.replace(':free',''))+(cool?` <span class="ko">(429, ${cool}s)</span>`:'')
  }).join('<br>')}</td></tr>`;
 }
 g+=`<tr><td class="muted">riserva</td><td class="muted">se tutto il gruppo e' occupato</td><td class="muted">${esc(s.baseline.replace(':free',''))}</td></tr>`;
 document.getElementById('groups').innerHTML=g;
 const c=s.catalogo||{};
 document.getElementById('catalog').textContent=
  `Catalogo del gateway: ${c.modelli_gratuiti||0} modelli gratuiti, ${c.con_vista||0} con la vista, letto alle ${c.aggiornato||'-'}`
  +(c.errore?` (errore: ${c.errore})`:'');
 let l='<tr><th>ora</th><th>gruppo e motivo</th><th>modello che ha risposto</th><th>stato</th><th>durata</th><th>token</th></tr>';
 for(const r of s.last){
  l+=`<tr><td>${esc(r.quando)}</td><td>${esc(r.gruppo)}<br><span class="muted">${esc(r.motivo)}</span></td>
      <td>${esc(r.modello.replace(':free',''))}${r.immagini?' <span class="ok">+img</span>':''}</td>
      <td class="${r.stato==200?'ok':'ko'}">${r.stato}</td><td>${(r.ms/1000).toFixed(1)} s</td>
      <td>${r.token??''}</td></tr>`;
 }
 document.getElementById('last').innerHTML=l;
 document.getElementById('errors').innerHTML=s.errori.length
  ? '<tr><th>messaggio</th></tr>'+s.errori.map(x=>`<tr><td class="ko">${esc(x)}</td></tr>`).join('')
  : '<tr><td class="muted">nessuno</td></tr>';
}
tick(); setInterval(tick,5000);
</script></body></html>
"""


def main():
    if not local_key():
        log("ATTENZIONE: %s non trovato, si accettano richieste senza API key" % KEY_FILE)
    log("in ascolto su http://127.0.0.1:%d/v1 (cruscotto su http://127.0.0.1:%d/)" % (PORT, PORT))
    log("voci esposte: " + ", ".join(ENTRIES))
    server = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    server.daemon_threads = True
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        log("chiusura")
    finally:
        server.server_close()


if __name__ == "__main__":
    sys.exit(main())
