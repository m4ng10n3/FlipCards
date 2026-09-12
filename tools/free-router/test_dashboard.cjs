const fs = require('node:fs')
const vm = require('node:vm')
const assert = require('node:assert/strict')
const html = fs.readFileSync(__dirname + '/dashboard.html', 'utf8')
const code = html.match(/<script>([\s\S]*?)<\/script>/)[1]
async function main() {
  const state = await (await fetch('http://127.0.0.1:8099/api/state')).json()
  const elements = {}
  const context = {document:{getElementById:id=>elements[id] ||= {}},fetch:async()=>({ok:true,json:async()=>state}),setInterval:()=>{},Date,Math,console}
  vm.runInNewContext(code, context)
  await new Promise(resolve=>setImmediate(resolve))
  assert(elements.groups.innerHTML.includes('nex-agi'))
  assert(elements.bench.innerHTML.includes('reasoning'))
  assert(elements.quota.textContent.includes('/'))
  assert(!elements.error.innerHTML.includes('non raggiungibile'))
  console.log('Dashboard renders live state without JavaScript errors (DOM test, not visual QA).')
}
main().catch(e=>{console.error(e);process.exitCode=1})
