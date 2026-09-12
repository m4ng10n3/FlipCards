"""Parse complete SSE events; preserve all model/tool/reasoning deltas verbatim."""
import json
import time


class UpstreamError(Exception):
    pass


def events(response, deadline):
    buffer = b""
    while True:
        if time.monotonic() > deadline:
            raise UpstreamError("tempo massimo della risposta superato")
        block = response.read1(8192)
        if not block:
            if buffer.strip():
                raise UpstreamError("evento SSE troncato")
            return
        buffer += block
        if len(buffer) > 8 * 1024 * 1024:
            raise UpstreamError("evento SSE troppo grande")
        # CRLF may be split across network blocks: normalize only complete frames.
        while True:
            offsets = [(buffer.find(sep), sep) for sep in (b"\n\n", b"\r\n\r\n") if sep in buffer]
            if not offsets:
                break
            pos, separator = min(offsets, key=lambda item: item[0])
            frame, buffer = buffer[:pos], buffer[pos + len(separator):]
            lines = frame.replace(b"\r\n", b"\n").split(b"\n")
            data = b"\n".join(line[5:].lstrip(b" ") for line in lines if line.startswith(b"data:"))
            if not data:
                continue
            if data == b"[DONE]":
                yield frame + separator, None
            else:
                try:
                    obj = json.loads(data)
                except (ValueError, UnicodeError) as exc:
                    raise UpstreamError("JSON SSE non valido") from exc
                if not isinstance(obj, dict):
                    raise UpstreamError("evento SSE non oggetto")
                if obj.get("error"):
                    # Do not treat quoted 'error' inside content or arguments as errors.
                    raise UpstreamError("errore del provider nello stream")
                yield frame + separator, obj


def forward(response, start, write, deadline):
    pending = []
    committed = False
    finished = False
    tokens = None
    for frame, obj in events(response, deadline):
        if obj is None:
            if not committed or not finished:
                raise UpstreamError("stream terminato senza risposta completa")
            write(frame)
            return tokens
        if obj.get("usage"):
            tokens = obj["usage"].get("total_tokens", tokens)
        meaningful = False
        for choice in obj.get("choices", []):
            delta = choice.get("delta") or {}
            meaningful |= bool(delta.get("content") or delta.get("tool_calls") or delta.get("reasoning_content") or delta.get("reasoning") or delta.get("reasoning_details"))
            finished |= choice.get("finish_reason") is not None
        if not committed:
            pending.append(frame)
            if sum(map(len, pending)) > 256 * 1024:
                raise UpstreamError("preambolo SSE senza contenuto")
            if meaningful:
                start()
                committed = True
                for item in pending:
                    write(item)
                pending.clear()
        else:
            write(frame)
    if not committed or not finished:
        raise UpstreamError("connessione chiusa prima di finish_reason")
    # Some compatible servers finish with finish_reason + EOF instead of [DONE].
    write(b"data: [DONE]\n\n")
    return tokens
