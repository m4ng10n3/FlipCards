// Kilo carica questo file da .kilo/plugin/. Quando la richiesta va al modello locale
// (provider llamacpp) sostituisce AGENTS.md, ~12.700 token, con la guida compatta in
// .kilo/local-llm/AGENTS.local.md. Con i modelli cloud il prompt resta intatto.
//
// Si appoggia all'hook "experimental.chat.system.transform" di Kilo 7.6: se un
// aggiornamento lo cambia, il prompt locale torna a contenere AGENTS.md intero.
import { readFileSync } from "node:fs"
import { fileURLToPath } from "node:url"

const LOCAL_PROVIDERS = new Set(["llamacpp"])
const DIGEST_PATH = fileURLToPath(new URL("../local-llm/AGENTS.local.md", import.meta.url))
// Kilo intesta ogni file di istruzioni con "Instructions from: <percorso>\n<contenuto>".
const AGENTS_HEADER = /Instructions from: ([^\n]*[\\/]AGENTS\.md)\n/g

function readText(path) {
  try {
    return readFileSync(path, "utf8")
  } catch {
    return undefined
  }
}

function replaceAgentsBlocks(text, digest) {
  let result = text
  for (const header of text.matchAll(AGENTS_HEADER)) {
    const content = readText(header[1])
    if (content === undefined) continue
    // Kilo concatena il file com'e' su disco (AGENTS.md ha i CRLF); il secondo
    // candidato copre un'eventuale normalizzazione dei fine riga.
    const block = [header[0] + content, header[0] + content.replace(/\r\n/g, "\n")].find((candidate) =>
      result.includes(candidate),
    )
    if (block === undefined) {
      console.warn(`[local-llm-context] AGENTS.md non riconosciuto nel prompt (${header[1]}): lasciato intatto`)
      continue
    }
    result = result.replace(block, () => `Instructions from: ${DIGEST_PATH}\n${digest}`)
  }
  return result
}

export default {
  id: "flipcards-local-llm-context",
  server: async () => ({
    "experimental.chat.system.transform": async (input, output) => {
      const providerID = input.model?.providerID ?? input.model?.provider?.id
      if (!LOCAL_PROVIDERS.has(providerID)) return
      const digest = readText(DIGEST_PATH)
      if (digest === undefined) return
      for (let i = 0; i < output.system.length; i++) {
        output.system[i] = replaceAgentsBlocks(output.system[i], digest)
      }
    },
  }),
}
