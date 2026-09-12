"""Operational regression probes from the failed slot task; no scene mutation.

This evaluates evidence interpretation and ownership, not complete coding ability.
Keep separate from arithmetic/vision scores; failures cannot be hidden by averaging.
"""
import argparse
import json
import time
from pathlib import Path
import benchmark
import router

OUT = router.ROOT / 'benchmarks/workflow.json'
SUITE = 'slot-workflow-v1'

def cases():
    files = """Synthetic fixture, unrelated to any private project:
class HiddenPanel: enabled = False; attackLamp = None
class CabinetGauge:
    def update(self, enemy):
        attack = enemy.damage + enemy.bonus if enemy.alive and enemy.attacking else 0
        self.yellow_bulbs.set_count(attack)
class EnemyProgram:
    PatternLength = len(pattern)
    PatternStep = (next_index - 1) % len(pattern) if pattern else 0
    def PatternSideAt(self, i): return pattern[i % len(pattern)] if pattern else 'attack'
"""
    return [
        ('ownership', 'Task: replace enemy-slot printed attack notches with colored state symbols. Yellow cabinet bulbs alone must show dynamic attack. The hidden AtkChip is disabled. Inspect this synthetic source:\n'+files+
         '\nReturn result={"attackOwner":class name,"sequenceLength":API property,"currentStep":API property,"fixedLength":integer}. fixedLength means length of the displayed sequence when PatternLength is zero.',
         lambda r:r=={'attackOwner':'CabinetGauge','sequenceLength':'PatternLength','currentStep':'PatternStep','fixedLength':1}),
        ('failed-verification', 'An edit completed. Unity_RunCommand returned {"success":true,"data":{"isCompilationSuccessful":false,"compilationLogs":"CS0103 unknown symbol"}}. No screenshot was taken. Return result={"complete":boolean,"next":"repair_compile" or "capture" or "finish","verified":boolean}. Consider actual execution, not HTTP status.',
         lambda r:r=={'complete':False,'next':'repair_compile','verified':False}),
        ('cycle-and-acceptance', 'Acceptance: full program length (0,1,5), current-step marker preserves state color, yellow attack bulbs change with bonuses and turn state. Trace: edit -> compile OK -> same screenshot repeated 3 times; no dynamic samples. Return result={"complete":boolean,"repeatCapture":boolean,"missing":["pattern_edges","dynamic_attack"]}. Missing checks must be included; repeated screenshots are identical.',
         lambda r:r=={'complete':False,'repeatCapture':False,'missing':['pattern_edges','dynamic_attack']})]

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--models',nargs='+',default=[router.FLASH,router.SUPER,router.FAST]);args=parser.parse_args()
    try:data=json.loads(OUT.read_text())
    except (OSError,ValueError):data={'suite':SUITE,'models':{}}
    for model in args.models:
        rows=[]
        for case in cases():
            row=benchmark.run_case(model,'workflow',case);rows.append(row)
        data['models'][model]={'when':time.time(),'passed':all(r['passed'] for r in rows),'cases':rows}
        data['when']=time.time();data['scope']=__doc__
        OUT.write_text(json.dumps(data,indent=2),encoding='utf-8')
if __name__=='__main__': main()
