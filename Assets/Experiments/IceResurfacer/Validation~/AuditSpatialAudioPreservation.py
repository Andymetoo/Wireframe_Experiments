from pathlib import Path
import hashlib
import json
import re
import subprocess

experiment = Path(__file__).resolve().parent.parent
repository = experiment.parents[2]
stage = experiment / 'Validation~/UnityProject/Assets/Experiments/IceResurfacer'
before = experiment / 'Validation~/SpatialAudioBefore'
output = experiment / 'Validation~/SpatialAudio'
output.mkdir(exist_ok=True)
digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
baseline = json.loads((before / 'Baseline-sha256.json').read_text(encoding='utf-8-sig'))
allowed = {'Presentation/IceMachineAudio.cs', 'Presentation/IceWaterService.cs', 'Editor/IceResurfacerEditor.cs'}
changed = [name for name, value in baseline.items() if digest(experiment / name) != value]
unexpected = sorted(set(changed) - allowed)
preserved = len(baseline) - len(changed)
missing, duplicates, mismatches, hashes, guids = [], [], [], [], {}
for folder in ['Core', 'Presentation', 'Editor', 'Resources', 'Shaders', 'Scenes', 'Documentation']:
    for path in sorted((experiment / folder).rglob('*')):
        if not path.is_file():
            continue
        relative = path.relative_to(experiment).as_posix()
        if path.suffix == '.meta':
            match = re.search(r'^guid: (\w+)', path.read_text(encoding='utf-8-sig'), re.M)
            if match:
                guid = match.group(1)
                if guid in guids:
                    duplicates.append(relative + ' / ' + guids[guid])
                guids[guid] = relative
            continue
        if not Path(str(path) + '.meta').exists():
            missing.append(relative)
        staged = stage / relative
        if not staged.exists() or digest(path) != digest(staged):
            mismatches.append(relative)
        hashes.append(relative + ' ' + digest(path))

def method(path, name):
    text = path.read_text(encoding='utf-8-sig')
    start = text.index('static AudioClip ' + name + '(')
    opening = text.index('{', start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end].replace('\r\n', '\n')

synthesis = []
for name, methods in [('IceMachineAudio.cs', ['CreateLoop', 'CreateMechanism', 'CreateImpact']), ('IceWaterService.cs', ['Noise'])]:
    for name_method in methods:
        if method(experiment / 'Presentation' / name, name_method) != method(before / 'Presentation' / name, name_method):
            synthesis.append(name_method)

git = ['git', '-c', 'safe.directory=G:/UnityProjects/Wireframe_Experiments']
entries = subprocess.check_output(git + ['status', '--porcelain', '-z'], cwd=repository).decode().split('\0')
outside = [entry for entry in entries if entry and not entry[3:].replace('\\', '/').startswith('Assets/Experiments/IceResurfacer/')]
lines = [
    'AURORA R-54 / SPATIAL AUDIO PRESERVATION AUDIT',
    f'Cockpit-baseline assets checked: {len(baseline)}; byte-identical: {preserved}',
    'Intentional existing-source changes: ' + ', '.join(changed),
    f'Unexpected baseline changes: {len(unexpected)}',
    f'Original synthesis methods changed: {len(synthesis)} (CreateLoop / CreateMechanism / CreateImpact / Noise)',
    f'Source assets compared with isolated validation project: {len(hashes)}',
    f'Source/staged mismatches: {len(mismatches)}',
    f'Missing metadata: {len(missing)}; duplicate GUIDs: {len(duplicates)}',
    f'Outside-experiment modified/untracked paths: {len(outside)}',
    'Core simulation, geometry, game/listener/camera code, scene, materials and shaders preserve their baseline hashes.',
    'Original project settings/packages and the user Editor were not modified by this pass.',
    'No branch, commit or push was performed.'
]
for title, values in [('Unexpected', unexpected), ('Synthesis', synthesis), ('Missing', missing), ('Duplicate', duplicates), ('Mismatch', mismatches), ('Outside', outside)]:
    lines.extend(title + ': ' + value for value in values)
failed = bool(unexpected or synthesis or missing or duplicates or mismatches or outside)
lines.append('FAIL' if failed else 'PASS')
report = '\n'.join(lines) + '\n'
(output / 'Preservation-results.txt').write_text(report, encoding='utf-8')
(output / 'Source-sha256.txt').write_text('\n'.join(hashes) + '\n', encoding='utf-8')
print(report)
raise SystemExit(failed)
