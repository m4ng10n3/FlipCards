import { buildHandoff } from '../local-llm/handoff.mjs'

export default {
  id: 'flipcards-local-coordination',
  server: async ({client}) => ({
    'tool.execute.before': async (input, output) => {
      const response = await client.session.messages({path: {id: input.sessionID}})
      if (response.error) throw new Error('Non posso recuperare la richiesta originale per la delega.')
      const messages = response.data || []
      const lastUser = messages.filter(m => m.info?.role === 'user').at(-1)
      if (lastUser?.info?.agent !== 'coordinatore') return
      // Permission deny on the parent propagates to children in Kilo. Enforce the
      // coordinator role here, without removing the specialist's native permissions.
      if (!['task', 'todowrite', 'todoread', 'question'].includes(input.tool)) {
        throw new Error('Il coordinatore deve delegare questa operazione a rapido o specialista.')
      }
      if (input.tool !== 'task') return
      // Kilo's tool runner retains the args object: mutate it in place.
      Object.assign(output.args, buildHandoff(messages, output.args))
    },
    'experimental.session.compacting': async (_input, output) => {
      output.context.push('Conserva obiettivo e vincoli originali dell utente, task_id delle deleghe aperte, file modificati e verifiche con esito reale. Distingui fatti osservati, ipotesi e lavoro non ancora fatto. Non dichiarare finito il task perche hai prodotto un riassunto.')
    },
  }),
}
