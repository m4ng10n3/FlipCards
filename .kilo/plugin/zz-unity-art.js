import { tool } from '@kilocode/plugin/tool'
import { execFile } from 'node:child_process'
import { promisify } from 'node:util'
import { fileURLToPath } from 'node:url'
import { readFileSync, writeFileSync } from 'node:fs'

const execute = promisify(execFile)
const script = fileURLToPath(new URL('../../tools/free-router/art_bundle.py', import.meta.url))
const digestPath = fileURLToPath(new URL('../local-llm/ART-WORKFLOW.md', import.meta.url))
const matches = text => text.length < 400 && /\b(monta\w*|installa\w*)\b/i.test(text) && /asset/i.test(text) && /slot|cassa/i.test(text)
const localSessions = new Set()

export default {
  id: 'flipcards-unity-art-bundle',
  server: async ({ client }) => ({
    'chat.params': async input => {
      const id=input.model?.id ?? input.model?.modelID
      const response=await client.session.messages({path:{id:input.sessionID}})
      const user=response.data?.findLast(m=>m.info?.role==='user' && m.parts?.some(p=>p.type==='text'&&!p.synthetic))
      const text=(user?.parts||[]).filter(p=>p.type==='text'&&!p.synthetic).map(p=>p.text).join('\n')
      if(input.model?.providerID==='router' && ['rapido','locale','coordinatore'].includes(id))localSessions.add(input.sessionID)
      else localSessions.delete(input.sessionID)
    },
    tool: {
      unity_art_bundle: tool({
        description: 'Monta il bundle grafico pronto della cassa/slot e verifica Unity con procedure locali misurate. inspect descrive asset e criteri; install ricostruisce; verify prova valori, 63 pixel e risonanza; capture restituisce immagine Main Camera e lascia Edit Mode. Non genera codice e non richiede bash. Usa quando richiesto il montaggio dei nuovi asset della slot.',
        args: { action: tool.schema.enum(['inspect', 'install', 'verify', 'capture']) },
        async execute(args, context) {
          const { stdout } = await execute('python', ['-B', '-X', 'utf8', script, args.action], {
            cwd: context.directory, windowsHide: true, timeout: 120000, maxBuffer: 1024 * 1024, signal: context.abort,
          }).catch(error => { throw Error(error.stdout || error.message) })
          const result = JSON.parse(stdout.trim())
          if (!result.success) throw Error(result.error || 'Art workflow failed')
          const local = localSessions.has(context.sessionID)
          if(local && result.contract)result.contract[2]='Main Camera catturata per revisione del supervisore; limiti dichiarati'
          if(local && result.image)result.next='Cattura salvata per il supervisore. Sei un modello solo testo: non dichiarare di aver visto la cattura. Completa installazione e prove automatiche citando questa limitazione.'
          if(!local && result.image)result.next='Il router passa questa cattura di gioco al modello online gratuito con vista. Valuta la composizione visibile: integrazione nella cassa, sette luci per banco, ingombri e bordi dei rulli. Segnala difetti concreti senza certificare animazioni da una foto. Nessun altro montaggio o test: rispondi con la valutazione.'
          return { title: 'Cassa integrata: ' + args.action, output: JSON.stringify(result),
            attachments: result.image && !local ? [{type:'file',mime:'image/jpeg',url:'data:image/jpeg;base64,'+readFileSync(result.image).toString('base64'),filename:'cabinet-main-camera.jpg'}] : [] }
        },
      }),
    },
    'experimental.chat.system.transform': async (input, output) => {
      if (!input.sessionID) return
      const response = await client.session.messages({ path: { id: input.sessionID } })
      const last = response.data?.findLast(m => m.info?.role === 'user')
      const text = (last?.parts || []).filter(p => p.type === 'text' && !p.synthetic).map(p => p.text).join('\n')
      if (!matches(text)) return
      const nativeSystem=output.system
      // Text coordination stays local; Auto hands the captured project image
      // to a benchmark-qualified free vision model. Explicit local stays local.
      const digest = readFileSync(digestPath, 'utf8')
      const originalChars=output.system.reduce((n,s)=>n+s.length,0)
      const projectGuide=readFileSync(fileURLToPath(new URL('../../AGENTS.md',import.meta.url)),'utf8').replace(/\r\n/g,'\n')
      // Kilo versions wrap project instructions differently. Match the known
      // project document itself; retain all other system/user instructions.
      output.system=output.system.map(s=>s.replace(/\r\n/g,'\n').replace(projectGuide,digest))
      // The project body has already been replaced above, regardless of header format.
      const workerPath=fileURLToPath(new URL('../local-llm/AGENTS.worker.md',import.meta.url))
      output.system=output.system.map(s=>s.replace('Instructions from: '+workerPath+'\n'+readFileSync(workerPath,'utf8'),'Instructions from: '+digestPath+'\n'+digest))
      output.system.push(digest)
      // Kilo retains the original array reference: changing output.system alone
      // does not change the request. Mutate that array in place.
      nativeSystem.splice(0,nativeSystem.length,...output.system)
      output.system=nativeSystem
      writeFileSync(fileURLToPath(new URL('../../tools/free-router/runtime/art-context-metrics.json',import.meta.url)),JSON.stringify({originalChars,systemChars:output.system.reduce((n,s)=>n+s.length,0),blocks:output.system.map(s=>({chars:s.length,header:s.slice(0,120)}))}))
    },
  }),
}
