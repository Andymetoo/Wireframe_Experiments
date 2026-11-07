"""Install only Unity-generated additive Phase 3 scene records, with baseline and lock guards."""
import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EXP = ROOT / 'Assets/Experiments/IceResurfacer'
SCENE = EXP / 'Scenes/IceResurfacer_AuthoredMachine.unity'
BACKUP = EXP / 'Backups~/UIAuthoring-Phase3-20261106'
INSTALLED = ROOT / 'Validation/UIAuthoring-Phase3/AcceptanceProject/ValidationResults/Installed-Phase3.unity'
EVIDENCE = ROOT / 'Validation/UIAuthoring-Phase3/Evidence'
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
    assert digest(original) == expected, 'Scene changed since verified backup; refusing overwrite.'
    installed = INSTALLED.read_bytes()
    old, new = records(original), records(installed)
    assert set(old).issubset(new), 'Unity artifact lost an existing scene record.'
    prefabs = ['Aurora Surface Survey', 'Aurora GPS Status', 'Aurora Route Manager']
    guids = {re.search(r'^guid: (\w+)', (EXP / ('UI/Prefabs/Navigation/' + name + '.prefab.meta')).read_text(), re.M).group(1) for name in prefabs}
    def identity(block):
        kind = re.match(rb'--- !u!(\d+)', block).group(1)
        source = re.search(rb'm_CorrespondingSourceObject: ([^\r\n]+)', block)
        return kind, source.group(1) if source else None
    reused, reserved, candidate = {}, set(old) | set(new), 1905400200
    for key in old.keys() & new.keys():
        if identity(old[key]) == identity(new[key]):
            continue
        assert b' stripped' in old[key].splitlines()[0] and b' stripped' in new[key].splitlines()[0], f'Unexpected reused ID {key}'
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
    instances = 0
    for key, block in extra.items():
        if block.startswith(b'--- !u!1001 '):
            assert any(f'm_SourcePrefab: {{fileID: 100100000, guid: {guid}, type: 3}}'.encode() in block for guid in guids), f'Unexpected new prefab {key}'
            instances += 1
        else:
            assert b' stripped\n' in block or b' stripped\r\n' in block, f'Unexpected new non-prefab {key}'
            guid = re.search(rb'm_CorrespondingSourceObject: \{fileID: \d+, guid: (\w+)', block).group(1).decode()
            assert guid in guids | {UI_GUID}, f'Unexpected stripped source {key}'
    assert instances == 3, f'Expected exactly three Phase 3 prefab instances; found {instances}'
    pattern = rb'    m_AddedGameObjects:\r?\n.*?(?=    m_AddedComponents:)'
    added = re.search(pattern, new[UI_INSTANCE], re.S)
    previous = re.search(pattern, old[UI_INSTANCE], re.S)
    assert added and previous
    newline = b'\r\n' if b'\r\n' in old[UI_INSTANCE] else b'\n'
    added = remap(added.group()).replace(b'\r\n', b'\n').replace(b'\n', newline)
    replacement = old[UI_INSTANCE].replace(previous.group(), added, 1)
    assert replacement != old[UI_INSTANCE]
    output = original.replace(old[UI_INSTANCE], replacement, 1)
    roots = old['9223372036854775807']
    output = output.replace(roots, b''.join(extra.values()) + roots, 1)
    result = records(output)
    unchanged = [key for key in old if key != UI_INSTANCE]
    assert all(result[key] == old[key] for key in unchanged), 'Existing non-UI scene records changed.'
    assert result[UI_INSTANCE].replace(added, previous.group(), 1) == old[UI_INSTANCE]
    # Every fileID in the preserved original additions must remain present in the replacement.
    for reference in re.findall(rb'addedObject: \{fileID: (\d+)\}', previous.group()):
        assert b'addedObject: {fileID: ' + reference + b'}' in added
    report = dict(originalSceneSha256=expected, unityArtifactSha256=digest(installed), integratedSceneSha256=digest(output),
                  unchangedExistingRecords=len(unchanged), newRecordIds=list(extra), remappedReusedIds=reused,
                  existingUIChange='Only m_AddedGameObjects; existing Phase 2 child retained.', newPrefabGuids=sorted(guids))
    return original, output, report

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--apply', action='store_true')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    before, output, report = build()
    if args.apply:
        assert not (ROOT / 'Temp/UnityLockfile').exists(), 'Working project has a Unity lock. Install using the Editor menu instead.'
        assert SCENE.read_bytes() == before, 'Concurrent scene modification; refusing write.'
        SCENE.write_bytes(output)
        EVIDENCE.mkdir(parents=True, exist_ok=True)
        (EVIDENCE / 'Scene-integration.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(dict(report, applied=args.apply), indent=2))
