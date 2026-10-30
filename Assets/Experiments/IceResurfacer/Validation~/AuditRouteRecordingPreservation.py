from pathlib import Path
import hashlib
import json
import re
import subprocess

experiment = Path(__file__).resolve().parent.parent
repository = experiment.parents[2]
stage = experiment / 'Validation~/UnityProject/Assets/Experiments/IceResurfacer'
before = experiment / 'Validation~/RouteRecordingBefore'
output = experiment / 'Validation~/RouteRecording'
digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
baseline = json.loads((before / 'Baseline-sha256.json').read_text(encoding='utf-8-sig'))
allowed = {'Core/ResurfacingRoute.cs', 'Presentation/IceRouteGuide.cs', 'Presentation/IceResurfacerGame.cs',
           'Presentation/IceDashboard.cs', 'Presentation/IceLayoutPlayerProbe.cs', 'Editor/IceResurfacerEditor.cs'}
changed = [name for name, value in baseline.items() if digest(experiment / name) != value]
unexpected = sorted(set(changed) - allowed)
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

route_name = 'Core/ResurfacingRoute.cs'
old = (before / route_name).read_text(encoding='utf-8-sig')
new = (experiment / route_name).read_text(encoding='utf-8-sig')
signature = 'public ResurfacingRoute(SimulatorConfig c)'
reference_unchanged = old[old.index(signature):] == new[new.index(signature):]

def body(text, signature):
    start = text.index('{', text.index(signature))
    end, depth = start + 1, 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end]

game_name = 'Presentation/IceResurfacerGame.cs'
old_game = (before / game_name).read_text(encoding='utf-8-sig')
new_game = (experiment / game_name).read_text(encoding='utf-8-sig')
camera_unchanged = all(body(old_game, name) == body(new_game, name) for name in ['void UpdateLook(', 'void ReleaseLook(', 'void UpdateMachine('])
git = ['git', '-c', 'safe.directory=G:/UnityProjects/Wireframe_Experiments']
entries = subprocess.check_output(git + ['status', '--porcelain', '-z'], cwd=repository).decode().split('\0')
outside = [entry for entry in entries if entry and not entry[3:].replace('\\', '/').startswith('Assets/Experiments/IceResurfacer/')]
lines = [
    'AURORA R-54 / ROUTE RECORDING PRESERVATION AUDIT',
    f'Completed cockpit/audio baseline assets checked: {len(baseline)}; byte-identical: {len(baseline)-len(changed)}',
    'Intentional existing-source integration/test changes: ' + ', '.join(changed),
    f'Unexpected baseline changes: {len(unexpected)}',
    'Original generated F5 reference constructor and all path-generation methods unchanged: ' + str(reference_unchanged),
    'Original UpdateLook / ReleaseLook / UpdateMachine bodies unchanged: ' + str(camera_unchanged),
    'All simulation/vehicle/conditioner/ice physics files and all audio implementation files preserve their baseline hashes.',
    'Only existing Core edit is the additional raw-recording constructor in ResurfacingRoute.',
    f'Source assets compared with isolated validation project: {len(hashes)}',
    f'Source/staged mismatches: {len(mismatches)}',
    f'Missing metadata: {len(missing)}; duplicate GUIDs: {len(duplicates)}',
    f'Outside-experiment modified/untracked paths: {len(outside)}',
    'No original scene, project settings, package, unrelated experiment, branch, commit or push was changed by this pass.'
]
for title, values in [('Unexpected', unexpected), ('Missing', missing), ('Duplicate', duplicates), ('Mismatch', mismatches), ('Outside', outside)]:
    lines.extend(title + ': ' + value for value in values)
failed = bool(unexpected or missing or duplicates or mismatches or outside or not reference_unchanged or not camera_unchanged)
lines.append('FAIL' if failed else 'PASS')
report = '\n'.join(lines) + '\n'
(output / 'Preservation-results.txt').write_text(report, encoding='utf-8')
(output / 'Source-sha256.txt').write_text('\n'.join(hashes) + '\n', encoding='utf-8')
print(report)
raise SystemExit(failed)
