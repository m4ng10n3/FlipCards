import { createHash } from 'node:crypto'

export const REVISION = '4'
export const digest = value => createHash('sha256').update(JSON.stringify(value)).digest('hex')
export function fresh() {
  return { revision: REVISION, turn: '', phase: 'inspect', criteria: [], checks: [], records: [], stop: '', compactions: 0 }
}
export function isMutation(name, args = {}) {
  // Shell and arbitrary editor code can mutate too; require a contract for both.
  return ['edit', 'write', 'apply_patch', 'bash'].includes(name) || /RunCommand$/.test(name) || (name === 'unity_art_bundle' && args.action !== 'inspect')
}
export function successful(record) {
  if (record.status !== 'completed') return false
  if (record.exit != null && record.exit !== 0) return false
  const text = (record.output || '').split('\n[HARNESS evidence=')[0]
  // MCP transport success is not execution success. Inspect its structured body.
  try {
    const obj = JSON.parse(text)
    if (obj.success === false || obj.isError === true || obj.data?.isCompilationSuccessful === false || obj.data?.isExecutionSuccessful === false || obj.data?.errorCount > 0) return false
  } catch { /* Native reads/edits return plain text. */ }
  return !/"(?:success|isCompilationSuccessful|isExecutionSuccessful)"\s*:\s*false|"isError"\s*:\s*true/.test(text)
}
export function sync(state, messages) {
  const users = messages.filter(m => m.info?.role === 'user' && m.parts?.some(p => p.type === 'text' && !p.synthetic && !p.ignored))
  const last = users.at(-1)
  if (last && state.turn !== last.info.id) {
    Object.assign(state, fresh(), { turn: last.info.id,
      objective: users.map(m => m.parts.filter(p => p.type === 'text' && !p.synthetic && !p.ignored).map(p => p.text).join('\n')).join('\nAGGIORNAMENTO UTENTE:\n') })
  }
  const start = last ? messages.indexOf(last) : 0
  const currentText = (last?.parts || []).filter(p => p.type === 'text' && !p.synthetic).map(p => p.text).join('\n')
  state.artWorkflow = currentText.length < 400 && /\b(monta\w*|installa\w*)\b/i.test(currentText) && /asset/i.test(currentText) && /slot|cassa/i.test(currentText)
  for (const m of messages.slice(start)) for (const p of m.parts || []) {
    if (p.type !== 'tool' || !['completed', 'error'].includes(p.state?.status)) continue
    const id = p.callID || p.id
    if (state.records.some(r => r.id === id)) continue
    const s = p.state
    state.records.push({ id, name: p.tool, fingerprint: digest([p.tool, s.input]),
      status: s.status, output: String(s.error || s.output || '').split('\n[HARNESS evidence=')[0].slice(0, 12000),
      // Retain flags, not complete generated code or images, in the compact ledger.
      mutation: isMutation(p.tool, s.input), camera: /Camera_Capture$/.test(p.tool) && Number(s.input?.cameraInstanceID) !== 0 && s.input?.cameraInstanceID != null,
      code: /RunCommand$/.test(p.tool) ? String(s.input?.Code || '') : '',
      exit: s.metadata?.exit ?? s.metadata?.exitCode,
      bundleAction: p.tool === 'unity_art_bundle' ? s.input?.action : undefined,
    })
  }
  const work = state.records.filter(r => r.name !== 'harness_checkpoint')
  if (state.artWorkflow && work.length >= 8) state.stop = 'Otto operazioni di montaggio senza conclusione: fermarsi con le prove e il motivo concreto.'
  if (work.length >= 48) state.stop = '48 operazioni nel turno: salva il punto di ripresa e riduci il sotto-task.'
  if (work.length >= 4 && work.slice(-4).every(r => !successful(r))) state.stop = 'Quattro strumenti falliti consecutivamente. Correggi la causa prima di riprendere.'
  // Detect cycles of length 1..4 using both call arguments and actual outcome.
  for (let width = 1; width <= 4; width++) {
    const tail = work.slice(-width * 3)
    if (tail.length !== width * 3) continue
    const key = r => digest([r.fingerprint, r.status, r.output])
    if (tail.every((r, i) => key(r) === key(tail[i % width]))) state.stop = 'Ciclo ripetuto tre volte con gli stessi risultati: nessun progresso verificato.'
  }
  return state
}
export function before(state, name, args) {
  if (state.stop && name !== 'harness_checkpoint') throw Error('HARNESS STOP: ' + state.stop)
  if (state.artWorkflow && !['unity_art_bundle', 'harness_checkpoint'].includes(name)) throw Error('Montaggio bundle: usa unity_art_bundle inspect/install/verify/capture. Non servono shell o codice generato.')
  if (isMutation(name, args) && !state.criteria.length) throw Error('Prima di eseguire: harness_checkpoint phase=plan con criteri osservabili e piano mirato. Una lettura MCP richiede un piano breve, non un permesso aggiuntivo.')
  const lastChange = state.records.findLastIndex(r => r.mutation && successful(r))
  const matches = state.records.slice(lastChange + 1).filter(r => r.fingerprint === digest([name, args]))
  if (matches.length >= 3) throw Error('Operazione identica già eseguita tre volte. Usa il risultato esistente o cambia strategia; non ripetere la chiamata.')
  if (/RunCommand$/.test(name) && /isPlaying\s*=\s*false/.test(args.Code || '') && /\.Rebuild\s*\(/.test(args.Code || ''))
    throw Error('Unity: uscita da Play e Rebuild devono essere due chiamate distinte.')
}
export function checkpoint(state, args) {
  if (args.phase === 'plan') {
    if (state.phase !== 'inspect') throw Error('Il contratto è già fissato per questo turno: verifica i criteri originali, senza riscriverli per dichiarare successo.')
    if (!args.criteria?.length) throw Error('Specificare almeno un criterio osservabile.')
    state.criteria = args.criteria
    state.phase = 'implement'
  } else if (args.phase === 'complete') {
    if (!state.criteria.length) throw Error('Manca il contratto iniziale.')
    if (state.stop) throw Error('Sessione bloccata: ' + state.stop)
    const mutation = state.records.findLastIndex(r => ['edit', 'write', 'apply_patch'].includes(r.name) || r.bundleAction === 'install')
    for (let i = 0; i < state.criteria.length; i++) {
      const check = args.checks?.find(c => c.criterion === i)
      if (!check?.observation?.trim() || !check.evidence?.length) throw Error('Manca osservazione e prova per il criterio ' + i)
      for (const id of check.evidence) {
        const index = state.records.findIndex(r => r.id === id)
        if (index <= mutation || state.records[index]?.name === 'harness_checkpoint' || !successful(state.records[index] || {})) throw Error('Prova assente, fallita o precedente all’ultima modifica: ' + id)
      }
    }
    const unityEdit = mutation >= 0 && state.records.some(r => /Unity_RunCommand$/.test(r.name))
    const recent = state.records.slice(mutation + 1).filter(successful)
    if (state.artWorkflow) {
      const verified = recent.findLast(r => r.bundleAction === 'verify')
      const captured = recent.findLast(r => r.bundleAction === 'capture')
      if (!verified || !captured) throw Error('Montaggio non concluso: servono verify riuscito e capture reale dopo install.')
      const v = JSON.parse(verified.output), c = JSON.parse(captured.output)
      if (!v.bundleDigest || v.bundleDigest !== c.bundleDigest || v.tests?.pixelSamples !== 63 || v.tests?.valueSamples !== 63 || !c.cameraInstanceID || !c.image)
        throw Error('Prove bundle incoerenti, incomplete o appartenenti ad asset diversi.')
    }
    if (unityEdit && !recent.some(r => /GetConsoleLogs$/.test(r.name))) throw Error('Manca controllo Console dopo le modifiche Unity.')
    if (unityEdit && /simbol|color|template|layout|lamp|visual/i.test(state.objective || '') && !recent.some(r => r.camera)) throw Error('Manca cattura Main Camera dopo le modifiche visive.')
    state.checks = args.checks
    state.phase = 'complete'
  } else if (args.phase === 'blocked') {
    state.phase = 'blocked'
    state.stop = args.note || 'Blocco dichiarato dall’agente'
  } else {
    if (!state.criteria.length) throw Error('Manca il contratto iniziale: prima phase=plan.')
    state.phase = 'verify'
  }
  state.note = args.note
  return summary(state)
}
export function summary(state) {
  return { revision: REVISION, phase: state.phase, stop: state.stop, criteria: state.criteria,
    calls: state.records.length, recent: state.records.slice(-6).map(r => ({ id: r.id, tool: r.name, ok: successful(r) })), checks: state.checks }
}

// The prepared art workflow owns its contract. A small model need not recreate it.
export function finishArtWorkflow(state) {
  if (!state.artWorkflow || state.stop || state.phase === 'complete') return false
  const install = state.records.findLastIndex(r => r.bundleAction === 'install')
  const recent = state.records.slice(install + 1).filter(successful)
  const verify = recent.findLast(r => r.bundleAction === 'verify')
  const capture = recent.findLast(r => r.bundleAction === 'capture')
  if (install < 0 || !verify || !capture) return false
  checkpoint(state, {phase:'complete', note:'Chiusura automatica del contratto del bundle con prove native reali.', checks:[
    {criterion:0,evidence:[verify.id],observation:'Bundle installato e verificato contro il digest corrente.'},
    {criterion:1,evidence:[verify.id],observation:'63 valori, 63 pixel acceso/spento e risonanza verificati nel motore.'},
    {criterion:2,evidence:[capture.id],observation:'Main Camera acquisita per revisione visiva del supervisore; coordinatore locale solo testo.'},
  ]})
  return true
}
