"""One persistent rolling request budget shared by router and benchmarks."""
import contextlib
import json
import os
from pathlib import Path
import time

ROOT = Path(__file__).resolve().parent / 'runtime'


@contextlib.contextmanager
def locked():
    ROOT.mkdir(exist_ok=True)
    with (ROOT/'quota.lock').open('a+b') as handle:
        if handle.tell() == 0:
            handle.write(b'0');handle.flush()
        handle.seek(0)
        if os.name == 'nt':
            import msvcrt
            msvcrt.locking(handle.fileno(), msvcrt.LK_LOCK, 1)
        else:
            import fcntl
            fcntl.flock(handle.fileno(), fcntl.LOCK_EX)
        try:
            yield
        finally:
            handle.seek(0)
            if os.name == 'nt': msvcrt.locking(handle.fileno(), msvcrt.LK_UNLCK, 1)
            else: fcntl.flock(handle.fileno(), fcntl.LOCK_UN)


def rows():
    try:
        data=json.loads((ROOT/'quota.json').read_text(encoding='utf-8'))
        return [r for r in data if r['when'] > time.time()-3600]
    except FileNotFoundError:
        return []


def used():
    with locked(): return len(rows())


def reserve(limit, source):
    with locked():
        data=rows()
        if len(data)>=limit: return False
        data.append({'when':time.time(),'source':source})
        tmp=ROOT/'quota.tmp'
        tmp.write_text(json.dumps(data),encoding='utf-8')
        tmp.replace(ROOT/'quota.json')
        return True
