"""Compare pre-existing experiment files with the verified Phase 2 backup manifest."""
import hashlib
import json
import os
from pathlib import Path

root = Path(__file__).resolve().parents[2]
experiment = root / 'Assets/Experiments/IceResurfacer'
baseline = json.loads((experiment / 'Backups~/UIAuthoring-Phase2-20261106/baseline-sha256.json').read_text(encoding='utf-8-sig'))
allowed = {
    'Presentation/IceDashboard.cs', 'Presentation/IceResurfacerGame.cs',
    'UI/Runtime/IceUIStyle.cs', 'UI/Runtime/IceAuthoredHud.cs',
    'UI/Editor/IceUIStyleEditor.cs', 'Scenes/IceResurfacer_AuthoredMachine.unity',
}
changed, unchanged, missing = [], [], []
for relative, expected in baseline.items():
    path = experiment / relative
    if not path.exists():
        missing.append(relative)
        continue
    actual = hashlib.sha256(path.read_bytes()).hexdigest()
    (unchanged if actual == expected else changed).append(relative)
unexpected = sorted(set(changed) - allowed)
assert not missing, f'Baseline files missing: {missing}'
assert not unexpected, f'Unexpected pre-existing file changes: {unexpected}'
new_files = []
for folder, directories, files in os.walk(experiment):
    directories[:] = [name for name in directories if not name.endswith('~')]
    for name in files:
        relative = (Path(folder) / name).relative_to(experiment).as_posix()
        if relative not in baseline:
            new_files.append(relative)
new_files.sort()
report = {
    'baselineFiles': len(baseline), 'unchangedFiles': len(unchanged),
    'allowedChangedFiles': sorted(changed), 'unexpectedChanges': unexpected,
    'missingFiles': missing, 'newExperimentFiles': new_files,
    'protectedExamples': {
        relative: 'UNCHANGED' for relative in unchanged
        if relative.startswith(('Core/', 'Machine/', 'Arena/', 'UI/Themes/', 'UI/Typography/'))
        or relative in ('UI/Prefabs/Aurora UI.prefab', 'Scenes/IceResurfacer.unity')
    },
}
destination = root / 'Validation/UIAuthoring-Phase2/Evidence/Protection-audit.json'
destination.parent.mkdir(parents=True, exist_ok=True)
destination.write_text(json.dumps(report, indent=2) + '\n')
print(f'PASS {len(baseline)} baseline files checked; {len(unchanged)} unchanged; {len(changed)} intended changes; {len(new_files)} new files. No unexpected changes or missing files.')
