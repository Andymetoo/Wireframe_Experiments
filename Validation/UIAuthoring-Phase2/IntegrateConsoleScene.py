"""Transplant only Unity-authored additive console records; preserve existing scene bytes.

Run --check first. Before --apply, independently verify the real Wireframe Editor
is closed using GetOpenUnityProjects.ps1. The script also refuses a live Unity lock.
The disposable installed scene must be captured BEFORE authoring-test overrides.
"""
import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EXP = ROOT / 'Assets/Experiments/IceResurfacer'
SCENE = EXP / 'Scenes/IceResurfacer_AuthoredMachine.unity'
BACKUP = EXP / 'Backups~/UIAuthoring-Phase2-20261106'
INSTALLED = ROOT / 'Validation/UIAuthoring-Phase2/AcceptanceProject/ValidationResults/InstalledBeforeAuthoringEdits.unity'
EVIDENCE = ROOT / 'Validation/UIAuthoring-Phase2/Evidence'
UI_GUID = '8017bbc47d6454f29ab6caac8805d16e'
UI_INSTANCE = '1905400000'

def digest(data):
    return hashlib.sha256(data).hexdigest()

def records(data):
    matches = list(re.finditer(rb'^--- !u!(\d+) &(\d+)(?: stripped)?\r?\n', data, re.M))
    result = {}
    for index, match in enumerate(matches):
        end = matches[index + 1].start() if index + 1 < len(matches) else len(data)
        key = match.group(2).decode()
        assert key not in result, f'duplicate fileID {key}'
        result[key] = data[match.start():end]
    return result

def build():
    baseline = json.loads((BACKUP / 'baseline-sha256.json').read_text(encoding='utf-8-sig'))
    original = SCENE.read_bytes()
    expected = baseline['Scenes/IceResurfacer_AuthoredMachine.unity']
    assert digest(original) == expected, 'Main scene changed since verified backup; do not overwrite.'
    installed = INSTALLED.read_bytes()
    old = records(original)
    new = records(installed)
    assert set(old).issubset(new), 'Installed artifact lost existing scene records.'
    console_guid = re.search(r'^guid: (\w+)', (EXP / 'UI/Prefabs/Machinery/Aurora Machinery Console.prefab.meta').read_text(), re.M).group(1)
    # Unity may drop a no-longer-referenced stripped root and reuse its fileID for
    # a raycaster. Keep the original root byte-for-byte and remap that new record.
    def identity(block):
        kind = re.match(rb'--- !u!(\d+)', block).group(1)
        source = re.search(rb'm_CorrespondingSourceObject: ([^\r\n]+)', block)
        return kind, source.group(1) if source else None
    reused = {}
    reserved = set(old) | set(new)
    candidate = 1905400100
    for key in old.keys() & new.keys():
        if identity(old[key]) == identity(new[key]):
            continue
        assert (b' stripped' in old[key].splitlines()[0] and
                b' stripped' in new[key].splitlines()[0] and
                UI_GUID.encode() in old[key] and UI_GUID.encode() in new[key]), f'unexpected reused existing ID {key}'
        while str(candidate) in reserved:
            candidate += 1
        reused[key] = str(candidate)
        reserved.add(str(candidate))
    def remap(block):
        for previous, replacement in reused.items():
            block = re.sub(rb'(?m)^(--- !u!\d+ &)' + previous.encode() + rb'(?=\s)', lambda m: m.group(1) + replacement.encode(), block)
            block = block.replace(('{fileID: ' + previous + '}').encode(), ('{fileID: ' + replacement + '}').encode())
        return block
    extra = {reused.get(key, key): remap(value) for key, value in new.items() if key not in old or key in reused}
    assert extra, 'No additive console records.'
    instances = 0
    for key, block in extra.items():
        if block.startswith(b'--- !u!1001 '):
            assert f'm_SourcePrefab: {{fileID: 100100000, guid: {console_guid}, type: 3}}'.encode() in block, f'unexpected new prefab {key}'
            instances += 1
        else:
            assert b' stripped\n' in block or b' stripped\r\n' in block, f'unexpected non-prefab object {key}'
            guid = re.search(rb'm_CorrespondingSourceObject: \{fileID: \d+, guid: (\w+)', block).group(1).decode()
            assert guid in (UI_GUID, console_guid), f'unexpected source for record {key}'
    assert instances == 1, 'Exactly one new console prefab instance is required.'
    match = re.search(rb'    m_AddedGameObjects:\r?\n.*?(?=    m_AddedComponents:)', new[UI_INSTANCE], re.S)
    assert match and b'addedObject:' in match.group(), 'Missing added-child override on the existing UI instance.'
    newline = b'\r\n' if b'\r\n' in old[UI_INSTANCE] else b'\n'
    added = remap(match.group()).replace(b'\r\n', b'\n').replace(b'\n', newline)
    replacement = old[UI_INSTANCE].replace(b'    m_AddedGameObjects: []' + newline, added)
    assert replacement != old[UI_INSTANCE]
    output = original.replace(old[UI_INSTANCE], replacement, 1)
    scene_roots = old['9223372036854775807']
    output = output.replace(scene_roots, b''.join(extra.values()) + scene_roots, 1)
    result = records(output)
    unchanged = [key for key in old if key != UI_INSTANCE]
    assert all(result[key] == old[key] for key in unchanged), 'An existing non-UI scene record changed.'
    assert result[UI_INSTANCE].replace(added, b'    m_AddedGameObjects: []' + newline, 1) == old[UI_INSTANCE]
    report = {
        'originalSceneSha256': expected,
        'unityInstalledArtifactSha256': digest(installed),
        'integratedSceneSha256': digest(output),
        'unchangedExistingRecords': len(unchanged),
        'existingUIInstanceChange': 'Only m_AddedGameObjects adds the persistent console child.',
        'newRecordIds': list(extra),
        'remappedReusedStrippedIds': reused,
        'consolePrefabGuid': console_guid,
    }
    return original, output, report

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--apply', action='store_true')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    before, output, report = build()
    if args.apply:
        assert not (ROOT / 'Temp/UnityLockfile').exists(), 'Real project has a Unity lock; use in-Editor installation instead.'
        assert SCENE.read_bytes() == before, 'Concurrent modification; refusing scene write.'
        SCENE.write_bytes(output)
        EVIDENCE.mkdir(parents=True, exist_ok=True)
        (EVIDENCE / 'Scene-integration.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(dict(report, applied=args.apply), indent=2))
