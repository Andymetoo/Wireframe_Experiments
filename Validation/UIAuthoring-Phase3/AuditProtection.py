from pathlib import Path
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[2]
EXP = ROOT / 'Assets/Experiments/IceResurfacer'
BACKUP = EXP / 'Backups~/UIAuthoring-Phase3-20261106'
EVIDENCE = ROOT / 'Validation/UIAuthoring-Phase3/Evidence'
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
baseline = json.loads((BACKUP / 'baseline-sha256.json').read_text(encoding='utf-8-sig'))
changed = [path for path, digest in baseline.items() if not (EXP / path).is_file() or sha(EXP / path) != digest]
allowed = {'Presentation/IceDashboard.cs', 'Presentation/IceResurfacerGame.cs', 'Presentation/IceRouteGuide.cs', 'Scenes/IceResurfacer_AuthoredMachine.unity'}
assert set(changed).issubset(allowed), f'Unexpected asset/source changes: {set(changed) - allowed}'
routes = json.loads((BACKUP / 'route-sha256.json').read_text())
assert all((EXP / path).is_file() and sha(EXP / path) == digest for path, digest in routes.items()), 'Protected route file changed.'
actual_routes = {p.relative_to(EXP).as_posix() for p in (EXP / 'RouteRecordings~').rglob('*') if p.is_file()}
assert actual_routes == set(routes), 'Working route storage gained/lost files.'
files = [p for p in EXP.rglob('*') if p.is_file() and not any(x.endswith('~') for x in p.relative_to(EXP).parts)]
new = sorted(p.relative_to(EXP).as_posix() for p in files if p.relative_to(EXP).as_posix() not in baseline)
original_checks = (EXP / 'Documentation/UI-Phases-1-and-2-Master-Acceptance.md').read_text(encoding='utf-8-sig')
successor = (EXP / 'Documentation/UI-Phases-1-to-3-Master-Acceptance.md').read_text(encoding='utf-8-sig')
checks = re.findall(r'^\| ([A-I]\d\d) \| (.*?) \| (.*?) \| ____ \| ____ \|$', original_checks, re.M)
assert len(checks) == 94
for key, action, expected in checks:
    assert f'| {key} | {action} | {expected} | NOT TESTED | ____ |' in successor, f'Changed historical check {key}'
guid_paths = {}
for p in files:
    if p.suffix != '.meta': continue
    match = re.search(r'^guid: (\w+)', p.read_text(errors='replace'), re.M)
    if not match: continue  # Pre-existing malformed SnowRecoveryAndContact metadata is not rewritten.
    guid = match.group(1)
    assert guid not in guid_paths, f'Duplicate asset GUID {guid}'
    guid_paths[guid] = p.relative_to(EXP).as_posix()
for p in (EXP / 'UI/Prefabs/Navigation').glob('*.prefab'):
    text = p.read_text()
    for guid in re.findall(r'm_Script: \{fileID: 11500000, guid: (\w+)', text):
        # Package/uGUI/TMP scripts are external dependencies checked by actual Unity import.
        if guid in guid_paths: assert (EXP / guid_paths[guid].removesuffix('.meta')).is_file()
report = dict(status='PASS', baselineFileCount=len(baseline), changedExistingFiles=changed, newExperimentFiles=new,
              protectedRouteFiles=len(routes), routeStorageExact=True, preservedHistoricalChecks=94,
              phase3ManualChecks=len(re.findall(r'^\| P3-(?:S|G|R|X|P)\d\d \|', successor, re.M)),
              originalChecklistSha256=sha(EXP/'Documentation/UI-Phases-1-and-2-Master-Acceptance.md'),
              currentHashes={path: sha(EXP/path) for path in changed})
EVIDENCE.mkdir(parents=True, exist_ok=True)
(EVIDENCE/'Protection-audit.json').write_text(json.dumps(report, indent=2)+'\n')
print(json.dumps({k:v for k,v in report.items() if k not in ('newExperimentFiles','currentHashes')}, indent=2))
