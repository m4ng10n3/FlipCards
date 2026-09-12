const vscode = require('vscode')

function activate(context) {
  let panel
  const root = vscode.workspace.workspaceFolders?.find(f => /FlipCards$/i.test(f.uri.fsPath))
  if (!root) return
  const button = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 10)
  button.text = '$(circuit-board) Auto locale'
  button.tooltip = 'Router, modelli locali e benchmark di Kilo'
  button.command = 'flipcardsAuto.open'
  button.show()
  context.subscriptions.push(button, vscode.commands.registerCommand('flipcardsAuto.open', () => {
    if (panel) { panel.reveal(); return }
    panel = vscode.window.createWebviewPanel('flipcardsAuto', 'FlipCards Auto', vscode.ViewColumn.Beside, {
      enableScripts: true, retainContextWhenHidden: true,
    })
    const nonce = require('crypto').randomBytes(16).toString('hex')
    panel.webview.html = `<!doctype html><html lang="it"><head><meta charset="utf-8">
      <meta http-equiv="Content-Security-Policy" content="default-src 'none'; frame-src http://127.0.0.1:8099; style-src 'unsafe-inline'; script-src 'nonce-${nonce}';">
      <style>body{margin:0;color:var(--vscode-foreground);background:var(--vscode-editor-background);font-family:var(--vscode-font-family)}nav{padding:12px;display:flex;gap:8px;flex-wrap:wrap}button{background:var(--vscode-button-background);color:var(--vscode-button-foreground);border:0;padding:8px;cursor:pointer}iframe{border:0;width:100%;height:calc(100vh - 72px)}</style></head>
      <body><nav><button data-action="start">Avvia Auto + locale</button><button data-action="stop">Ferma Auto</button><button data-action="chat">Apri Kilo</button><button data-action="reload">Ricarica Kilo</button><button data-action="benchmark">Benchmark online</button><button data-action="local">Benchmark locale</button><button data-action="check">Diagnostica</button></nav>
      <iframe id="dashboard" src="http://127.0.0.1:8099/" title="Stato Auto"></iframe>
      <script nonce="${nonce}">const api=acquireVsCodeApi();document.querySelectorAll('button').forEach(b=>b.onclick=()=>api.postMessage({action:b.dataset.action}));window.addEventListener('message',e=>{if(e.data==='refresh')document.getElementById('dashboard').src='http://127.0.0.1:8099/';});</script></body></html>`
    panel.onDidDispose(() => { panel = undefined })
    panel.webview.onDidReceiveMessage(async message => {
      try {
        if (message.action === 'chat') await vscode.commands.executeCommand('kilo-code.new.openInTab')
        else if (message.action === 'reload') await vscode.commands.executeCommand('kilo-code.new.reload')
        else {
          const names = {start:'Auto: avvia tutto', stop:'Auto: ferma tutto', benchmark:'Auto: benchmark online', local:'Auto: benchmark locale', check:'Auto: diagnostica'}
          const name = names[message.action]
          if (!name) return
          const task = (await vscode.tasks.fetchTasks()).find(t => t.name === name && t.scope?.uri?.fsPath === root.uri.fsPath)
          if (!task) throw new Error('Task non trovato: ' + name)
          await vscode.tasks.executeTask(task)
        }
      } catch (error) { vscode.window.showErrorMessage(String(error.message || error)) }
    }, undefined, context.subscriptions)
  }))
  context.subscriptions.push(vscode.tasks.onDidEndTask(() => panel?.webview.postMessage('refresh')))
}
module.exports = {activate}
