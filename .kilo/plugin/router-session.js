// Native Kilo hook: session identity survives compaction; no changes to reasoning.
export default {
  id: "flipcards-router-session",
  server: async () => ({
    "chat.headers": async (input, output) => {
      const provider = input.model?.providerID ?? input.model?.provider?.id
      if (provider !== "router") return
      output.headers["X-FlipCards-Session"] = input.sessionID
      output.headers["X-FlipCards-Agent"] = input.agent
    },
  }),
}
