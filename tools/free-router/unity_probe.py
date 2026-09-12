"""Direct local MCP verification. Arguments come from a reviewed JSON file."""
import argparse
import base64
import json
from pathlib import Path
import queue
import subprocess
import threading

def call(name, arguments):
    project = Path(__file__).resolve().parents[2]
    relay = Path.home()/'.unity/relay/relay_win.exe'
    proc = subprocess.Popen([str(relay), '--mcp', '--project-path', str(project)], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True, encoding='utf-8')
    lines = queue.Queue()
    threading.Thread(target=lambda: [lines.put(line) for line in proc.stdout], daemon=True).start()
    def rpc(i, method, params):
        proc.stdin.write(json.dumps({'jsonrpc':'2.0','id':i,'method':method,'params':params})+'\n');proc.stdin.flush()
        while True:
            message=json.loads(lines.get(timeout=60))
            if message.get('id')==i:return message
    try:
        rpc(1,'initialize',{'protocolVersion':'2024-11-05','capabilities':{},'clientInfo':{'name':'flipcards-validation','version':'4'}})
        proc.stdin.write('{"jsonrpc":"2.0","method":"notifications/initialized"}\n');proc.stdin.flush()
        return rpc(2,'tools/call',{'name':name,'arguments':arguments})
    finally:
        proc.terminate()
        proc.wait(timeout=5)

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('input');p.add_argument('--output',required=True);args=p.parse_args()
    request=json.loads(Path(args.input).read_text(encoding='utf-8-sig'))
    response=call(request['name'],request['arguments'])
    dest=Path(args.output)
    for i,block in enumerate(response.get('result',{}).get('content',[])):
        if block.get('type')=='image':
            path=dest.with_suffix(f'.{i}.png');path.write_bytes(base64.b64decode(block.pop('data')));block['saved']=str(path)
    dest.write_text(json.dumps(response,indent=2),encoding='utf-8')
    print(json.dumps(response,ensure_ascii=False)[:6000])
