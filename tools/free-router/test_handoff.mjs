import assert from 'node:assert/strict'
import { buildHandoff, previousTaskID } from '../../.kilo/local-llm/handoff.mjs'
const messages = [{info: {role:'user'}, parts:[{type:'text',text:'Leggi Assets/Scripts/Managers/GameManager.cs. Preserva 12 turni. Non modificare file.'}]}]
const args = {subagent_type:'rapido',prompt:'Leggi GameManager.cs'}
const result=buildHandoff(messages,args)
assert(result.prompt.includes('Assets/Scripts/Managers/GameManager.cs'))
assert(result.prompt.includes('Preserva 12 turni. Non modificare file.'))
assert.equal(args.prompt,'Leggi GameManager.cs')
assert.equal(buildHandoff(messages,result).prompt,result.prompt)
assert.throws(()=>buildHandoff([{info:{role:'user'},parts:[{type:'text',text:'x'.repeat(10001)}]}],args))
assert.throws(()=>buildHandoff([],args))
const history=[...messages,{info:{role:'assistant'},parts:[{tool:'task',state:{status:'completed',input:{subagent_type:'specialista'},output:'<task id="ses_123ABC" state="completed">result</task>'}}]}]
assert.equal(previousTaskID(history,'specialista'),'ses_123ABC')
assert.equal(previousTaskID(history,'rapido'),undefined)
assert.equal(buildHandoff(history,{subagent_type:'specialista',prompt:'Continue'}).task_id,'ses_123ABC')
console.log('9 handoff assertions passed')
