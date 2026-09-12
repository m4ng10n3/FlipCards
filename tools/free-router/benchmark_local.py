"""Held-out local routing probes and a bounded task-delegation probe."""
import json
import time
from urllib.request import Request, urlopen
import policy
import router

TASKS = [
    ('Elenca i campi serializzati presenti nel file indicato', 'fast'),
    ('Leggi il numero massimo di carte dalla configurazione', 'fast'),
    ('Estrai solo i percorsi dei file da questo log', 'fast'),
    ('Posiziona il mazzo nella scena Unity e cattura la Main Camera', 'fast'),
    ('Verifica visivamente se i tre prefab sono allineati', 'fast'),
    ('Aggiungi un metodo che calcoli il danno oltre la corazza', 'code'),
    ('Scrivi test per un attacco che pareggia la vita dello slot', 'code'),
    ('Sostituisci il nome della variabile in queste tre righe C#', 'code'),
    ('Implementa la gestione di Retry-After nel proxy HTTP', 'code'),
    ('Rimuovi un listener duplicato nel metodo OnEnable', 'code'),
    ('Per quale causa il turno parte due volte durante il rullo?', 'reasoning'),
    ('Confronta due strategie di sincronizzazione delle coroutine', 'reasoning'),
    ('Progetta una struttura per separare regole e presentazione', 'reasoning'),
    ('Indaga una perdita di memoria che compare dopo dodici partite', 'reasoning'),
    ('Spiega perche il pronostico diverge dalla risoluzione', 'reasoning'),
]


def delegation_probe(task, target):
    messages=[{'role':'system','content':
        'You coordinate tasks. Delegate with task exactly once. Use rapido for read-only extraction and specialista for code modifications or Unity. '
        'Preserve the exact user objective and constraints in prompt. Never solve the task yourself.'},
        {'role':'user','content':task}]
    body={'model':'qwen3.5-2b','messages':messages,'temperature':0,'max_tokens':512,'stream':False,
        'tools':[{'type':'function','function':{'name':'task','description':'Delegate a bounded task.',
        'parameters':{'type':'object','properties':{'subagent_type':{'type':'string','enum':['rapido','specialista']},
        'description':{'type':'string'},'prompt':{'type':'string'}},'required':['subagent_type','description','prompt']}}}]}
    start=time.time()
    try:
        req=Request(router.SETTINGS['worker']['url']+'/chat/completions',json.dumps(body).encode(),
            {'Content-Type':'application/json','Authorization':'Bearer '+router.local_key()})
        with urlopen(req,timeout=15) as resp:
            data=json.load(resp)
        calls=data['choices'][0]['message'].get('tool_calls',[])
        args=json.loads(calls[0]['function']['arguments']) if len(calls)==1 else {}
        passed=args.get('subagent_type')==target and 'Assets/' in args.get('prompt','') and '12' in args.get('prompt','')
        return {'passed':passed,'ms':round((time.time()-start)*1000),'arguments':args}
    except Exception as exc:
        return {'passed':False,'error':str(exc)[:120]}


def main():
    rows=[]
    for task,expected in TASKS:
        start=time.time()
        answer=policy.local_classify({'messages':[{'role':'user','content':task}]},router.SETTINGS,router.local_key())
        row={'task':task,'expected':expected,'actual':answer[0] if answer else None,'ms':round((time.time()-start)*1000)}
        row['passed']=row['actual']==expected
        rows.append(row)
        print(json.dumps(row),flush=True)
    coordinator=[delegation_probe('Leggi Assets/Scripts/Managers/GameManager.cs e trova il limite dei 12 turni. Non modificare file.', 'rapido'),
        delegation_probe('Correggi il controllo dei 12 turni in Assets/Scripts/Managers/GameManager.cs e verifica in Unity.', 'specialista')]
    score=sum(r['passed'] for r in rows)/len(rows)
    result={'when':time.time(),'model':'qwen3.5-2b','routing_accuracy':score,'routing_passed':score>=.8,
        'cases':rows,'coordination':coordinator,'coordination_passed':all(r['passed'] for r in coordinator),
        'scope':'Held-out routing + two delegation probes. Coordinator remains experimental until an end-to-end Unity task passes.'}
    (router.ROOT/'benchmarks/local-capabilities.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ('cases','coordination')}),flush=True)


if __name__=='__main__': main()
