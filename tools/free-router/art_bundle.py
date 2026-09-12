"""Local, bounded Unity art installation workflow. No model-generated editor code."""
import argparse
import base64
import hashlib
import json
from pathlib import Path
import time
from unity_probe import call

ROOT = Path(__file__).resolve().parents[2]
BUNDLE = ROOT / 'Assets/Graphics/1_NeonMonte_Attivo/SceneKit_v2/10_CabinetIntegrated'
RUNTIME = ROOT / 'tools/free-router/runtime/art-bundle'


def identity():
    manifest = json.loads((BUNDLE / 'bundle.json').read_text(encoding='utf-8'))
    paths = [BUNDLE / 'bundle.json', ROOT / manifest['cabinet'],
             ROOT / 'Assets/Shaders/CabinetIntegratedLamps.shader',
             ROOT / 'Assets/Scripts/UI/CabinetLampController.cs',
             ROOT / 'Assets/Editor/CabinetArtVerification.cs',
             ROOT / 'Assets/Scripts/UI/CabinetArtDefinition.cs',
             ROOT / 'Assets/Scripts/UI/ReelChrome.cs',
             ROOT / 'Assets/Editor/CabinetArtInstaller.cs',
             ROOT / 'Assets/Editor/MedallionSceneBuilder.cs',
             ROOT / 'Assets/Editor/MedallionSceneSkin.cs']
    digest = hashlib.sha256(b''.join(path.read_bytes() for path in paths)).hexdigest()
    return manifest, digest


def unpack(response):
    result = response.get('result', {})
    data = result.get('structuredContent')
    if not data:
        texts = [item['text'] for item in result.get('content', []) if item.get('type') == 'text']
        data = json.loads(texts[-1]) if texts else {}
    if result.get('isError') or data.get('success') is False or data.get('data', {}).get('isExecutionSuccessful') is False:
        raise RuntimeError(str(data.get('error') or data.get('message') or data)[:900])
    return data


def command(code, title):
    source = 'using UnityEngine; using UnityEditor; internal class CommandScript : IRunCommand { public void Execute(ExecutionResult result) { ' + code + ' } }'
    for attempt in range(3):
        response = call('Unity_RunCommand', {'Title': title, 'Code': source})
        try:
            return unpack(response)
        except RuntimeError as error:
            if attempt == 2 or not any(word in str(error) for word in ('Unity not detected', 'RunCommandMacroEvaluatorEntryPoint')):
                raise
            time.sleep(2)


def run(action):
    RUNTIME.mkdir(parents=True, exist_ok=True)
    manifest, digest = identity()
    receipt_path = RUNTIME / 'receipt.json'
    receipt = json.loads(receipt_path.read_text()) if receipt_path.exists() else {}
    result = {'success': True, 'action': action, 'bundleDigest': digest, 'bundle': manifest['id']}
    if action == 'inspect':
        result.update(lamps=63, banks=9, artwork=str(BUNDLE / 'cabinet.png'),
            contract=['Cassa integrata installata dal bundle, senza pannelli sovrapposti',
                      'Statistiche, pixel delle 63 luci, totali e risonanza verificati',
                      'Main Camera catturata e verifica visiva dichiarata con eventuali limiti'],
            next='Il harness registra il contratto automaticamente. Ora install, poi verify e capture. Nessuna lettura di codice o bash necessaria.')
    elif action == 'install':
        command('EditorApplication.isPlaying=false;', 'Exit Play before cabinet installation')
        data = command('CabinetArtInstaller.Install(); result.Log("Installed integrated cabinet v4");', 'Install measured cabinet art bundle')
        receipt = {'digest': digest, 'installed': True}
        result['installation'] = data.get('data', {}).get('executionLogs')
        result['next'] = 'verify'
    elif action in ('verify', 'capture'):
        if receipt.get('digest') != digest or not receipt.get('installed'):
            raise RuntimeError('Bundle changed or not installed: inspect and install first.')
        if action == 'verify':
            command('EditorApplication.isPlaying=true;', 'Enter Play for cabinet verification')
            try:
                command('result.Log(CabinetArtVerification.Run());', 'Verify actual cabinet statistics and rendered pixels')
                tests = json.loads((ROOT / 'Logs/cabinet-v4-verification.json').read_text())
                if not tests.get('success') or tests.get('pixelSamples') != 63 or tests.get('valueSamples') != 63 or tests.get('resonanceSamples') != 6 or tests.get('rectangleSamples') != 12 or tests.get('apertureSamples') != 3:
                    raise RuntimeError('Acceptance samples incomplete')
                console = unpack(call('Unity_GetConsoleLogs', {'logTypes': 'Error', 'maxEntries': 20}))
                if console.get('data', {}).get('errorCount', 0):
                    raise RuntimeError('Unity Console contains errors')
                result.update(tests=tests, console=console.get('data', {}), next='capture; inspect the returned image before completion')
                receipt['verified'] = True
            except Exception:
                command('EditorApplication.isPlaying=false;', 'Leave failed cabinet verification')
                raise
        else:
            if not receipt.get('verified'):
                raise RuntimeError('verify must pass before capture')
            try:
                command('System.IO.File.WriteAllText("Logs/cabinet-camera-id.txt",Camera.main.gameObject.GetInstanceID().ToString());', 'Read current Main Camera ID')
                camera = int((ROOT / 'Logs/cabinet-camera-id.txt').read_text())
                response = call('Unity_Camera_Capture', {'cameraInstanceID': camera})
                unpack(response)
                images = [b for b in response.get('result', {}).get('content', []) if b.get('type') == 'image']
                if not images:
                    raise RuntimeError('Camera did not return an image')
                path = RUNTIME / 'main-camera.png'
                path.write_bytes(base64.b64decode(images[0]['data']))
                # Format conversion of the camera capture keeps native image attachments small.
                from PIL import Image
                preview = path.with_suffix('.jpg')
                with Image.open(path) as image:
                    image.convert('RGB').save(preview, quality=95)
                result.update(cameraInstanceID=camera, image=str(preview), originalCapture=str(path), next='Valuta la cattura allegata e rispondi con difetti visibili, controlli automatici e limite sulle animazioni. Il harness registra le prove automaticamente.')
                receipt['captured'] = True
            finally:
                command('EditorApplication.isPlaying=false;', 'Finish cabinet verification in Edit mode')
    else:
        raise ValueError('Unknown action')
    receipt_path.write_text(json.dumps(receipt), encoding='utf-8')
    (RUNTIME / (action + '.json')).write_text(json.dumps(result, indent=2), encoding='utf-8')
    return result


if __name__ == '__main__':
    p = argparse.ArgumentParser(); p.add_argument('action', choices=['inspect', 'install', 'verify', 'capture']); args = p.parse_args()
    try:
        print(json.dumps(run(args.action), ensure_ascii=False))
    except Exception as error:
        print(json.dumps({'success': False, 'action': args.action, 'error': str(error)[:1000]}))
        raise SystemExit(1)
