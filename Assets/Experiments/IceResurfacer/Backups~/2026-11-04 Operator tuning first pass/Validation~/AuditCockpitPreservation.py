from pathlib import Path
import hashlib
import re
import subprocess

experiment = Path(__file__).resolve().parent.parent
repository = experiment.parents[2]
stage = experiment / 'Validation~/UnityProject/Assets/Experiments/IceResurfacer'
before = experiment / 'Validation~/CockpitRefinementBefore'
git = ['git', '-c', 'safe.directory=G:/UnityProjects/Wireframe_Experiments']
entries = subprocess.check_output(git + ['status', '--porcelain', '-z'], cwd=repository).decode().split('\0')
outside = [entry for entry in entries if entry and not entry[3:].replace('\\', '/').startswith('Assets/Experiments/IceResurfacer/')]
missing, duplicates, mismatches, matched, guids = [], [], [], [], {}
digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
for folder in ['Core', 'Presentation', 'Editor', 'Resources', 'Shaders', 'Scenes']:
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
        else:
            matched.append(relative + ' ' + digest(path))

preserved = ['Core/SimulatorConfig.cs', 'Editor/IceAuthenticityValidation.cs', 'Editor/IceLayoutValidation.cs', 'Validation~/Program.cs']
changed_preserved = [name for name in preserved if digest(experiment / name) != digest(before / name)]
lines = [
    'AURORA R-54 / COCKPIT REFINEMENT PRESERVATION AUDIT',
    f'Source assets matching the isolated validated project: {len(matched)}',
    f'Missing metadata: {len(missing)}',
    f'Duplicate GUIDs: {len(duplicates)}',
    f'Outside-experiment modified/untracked paths: {len(outside)}',
    f'Source/staged mismatches: {len(mismatches)}',
    f'Changes to preserved handoff physics config / existing test harnesses: {len(changed_preserved)}',
    'Metadata GUIDs checked; Unity may add importer defaults only in the disposable copy.',
    'Validation/build uses Validation~/UnityProject. Original Editor is not switched or stopped.',
    'No branch, commit, push, original project-settings or shared-package edit was performed.'
]
for title, values in [('Missing', missing), ('Duplicate', duplicates), ('Outside', outside), ('Mismatch', mismatches), ('Preservation', changed_preserved)]:
    lines.extend(title + ': ' + value for value in values)
failed = bool(missing or duplicates or outside or mismatches or changed_preserved)
lines.append('FAIL' if failed else 'PASS')
report = '\n'.join(lines) + '\n'
(experiment / 'Validation~/Cockpit-refinement-preservation.txt').write_text(report, encoding='utf-8')
(experiment / 'Validation~/Cockpit-refinement-source-sha256.txt').write_text('\n'.join(matched) + '\n', encoding='utf-8')
print(report)
raise SystemExit(failed)
