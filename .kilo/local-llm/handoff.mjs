// Deterministic preservation of user constraints: the small model cannot omit them.
export function previousTaskID(messages, target) {
  for (const message of [...messages].reverse()) {
    for (const part of [...(message.parts || [])].reverse()) {
      const state = part.state
      if (part.tool !== 'task' || state?.status !== 'completed' || state.input?.subagent_type !== target) continue
      const id = String(state.output || '').match(/<task id="(ses_[A-Za-z0-9]+)"/)
      if (id) return id[1]
    }
  }
}

export function buildHandoff(messages, args) {
  const requests = messages.filter(m => m.info?.role === 'user')
    .map(m => (m.parts || []).filter(p => p.type === 'text' && !p.synthetic && !p.ignored).map(p => p.text).join('\n'))
    .filter(Boolean)
  if (!requests.length) throw new Error('Manca la richiesta originale: non delegare senza obiettivo.')
  const original = requests.join('\n\n--- Aggiornamento utente ---\n')
  if (original.length > 10000) throw new Error('Vincoli utente troppo lunghi per il coordinatore 2B. Prosegui con agente auto; nessun vincolo e stato tagliato.')
  const header = '\n\nRICHIESTE ORIGINALI DA CONSERVARE (precedono il riassunto del coordinatore):\n'
  const prompt = String(args.prompt || '').split(header)[0]
  const taskID = args.task_id || previousTaskID(messages, args.subagent_type)
  return {...args, ...(taskID ? {task_id: taskID} : {}), prompt: prompt + header + original}
}
