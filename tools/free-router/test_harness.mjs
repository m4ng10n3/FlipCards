import assert from 'node:assert/strict'
import { fresh, sync, before, checkpoint, successful } from '../../.kilo/local-llm/harness.mjs'
const user = id => ({info:{role:'user',id,agent:'auto'},parts:[{type:'text',text:'Cambia template slot e colori dinamici'}]})
const call = (id,name,input,output,status='completed') => ({info:{role:'assistant'},parts:[{type:'tool',callID:id,tool:name,state:{status,input,output}}]})
let s=sync(fresh(),[user('u1')])
assert.throws(()=>before(s,'edit',{}),/Prima di eseguire/)
assert.throws(()=>checkpoint(s,{phase:'verify',note:'skip planning'}),/contratto/)
checkpoint(s,{phase:'plan',criteria:['Sequenza e lampade aggiornate'],note:'Read owner, edit, compile, sample, capture'})
before(s,'edit',{})
assert.throws(()=>checkpoint(s,{phase:'plan',criteria:['Solo compila']}),/già fissato/)
assert.throws(()=>before(s,'unity-mcp_Unity_RunCommand',{Code:'EditorApplication.isPlaying = false; Layout.Rebuild();'}),/distinte/)
assert.equal(successful({status:'completed',output:'{"success":true,"data":{"isCompilationSuccessful":false}}\n[HARNESS evidence=a]'}),false)
assert.equal(successful({status:'completed',output:'{"success":false}'}),false)
assert.equal(successful({status:'completed',exit:1,output:'test failed'}),false)
const history=[user('u1'),call('old','read',{},'old'),call('edit','edit',{filePath:'test.cs'},'ok'),call('run','unity-mcp_Unity_RunCommand',{Code:'Log verified'},'{"success":true}'),call('console','unity-mcp_Unity_GetConsoleLogs',{},'{"success":true}'),call('pic','unity-mcp_Unity_Camera_Capture',{cameraInstanceID:123},'{"success":true}')]
sync(s,history)
assert.throws(()=>checkpoint(s,{phase:'complete',checks:[{criterion:0,evidence:['old'],observation:'old capture'}]}),/precedente/)
assert.throws(()=>checkpoint(s,{phase:'complete',checks:[{criterion:0,evidence:['invented'],observation:'pretend'}]}),/assente/)
checkpoint(s,{phase:'complete',checks:[{criterion:0,evidence:['run','console','pic'],observation:'Sample passed, visual inspected'}]})
assert.equal(s.phase,'complete')
for (const width of [1,2,3,4]) {
 const h=[user('loop')]
 for(let i=0;i<width*3;i++)h.push(call('c'+i,'read',{filePath:'path'+(i%width)},'same '+(i%width)+'\n[HARNESS evidence=c'+i+']'))
 const loop=sync(fresh(),h);assert.match(loop.stop,/Ciclo/)
 assert.throws(()=>before(loop,'read',{}),/STOP/)
 assert.equal(sync(loop,[user('new')]).stop,'')
}
const errors=sync(fresh(),[user('err'),...Array.from({length:4},(_,i)=>call('e'+i,'read',{filePath:String(i)},'not found','error'))])
assert.match(errors.stop,/Quattro/)
assert.equal(sync(s,history).records.length,5) // reread transcript is idempotent
console.log('Harness regression suite passed: contract, false MCP success, stale evidence, cycles, recovery, idempotence')
