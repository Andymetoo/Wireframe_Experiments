from pathlib import Path
import hashlib
import json
import os
import re

ROOT = Path(__file__).resolve().parents[2]
EXP = ROOT / 'Assets/Experiments/IceResurfacer'
BACKUP = EXP / 'Backups~/UIAuthoring-Phase5-20261106'
OUT = ROOT / 'Validation/UIAuthoring-Phase5/Evidence'
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
baseline = json.loads((BACKUP / 'baseline-sha256.json').read_text(encoding='utf-8-sig'))
changed = sorted(path for path, digest in baseline.items() if not (EXP/path).is_file() or sha(EXP/path) != digest)
allowed = {
    'Scenes/IceResurfacer_AuthoredMachine.unity',
    'Presentation/IceDashboard.cs', 'Presentation/IceResurfacerGame.cs', 'Presentation/IceWaterService.cs',
    'Presentation/IceRouteGuide.cs', 'Presentation/RouteSilhouetteContinuation.cs', 'Presentation/GpsMachineAlignment.cs',
    'UI/Runtime/IceAuthoredHud.cs', 'UI/Runtime/IceMachineryConsole.cs', 'UI/Runtime/IceSurfaceSurvey.cs', 'UI/Runtime/IceGpsStatus.cs',
    'UI/Editor/IceMenuPreview.cs', 'UI/Editor/IceUIInputValidation.cs', 'UI/Editor/IceMachineryPhysicalValidation.cs',
    'Editor/IceServiceValidation.cs',
}
assert set(changed) == allowed, f'Unexpected changed/missing baseline files: {set(changed) ^ allowed}'
assert len(baseline) == 677
for path, digest in baseline.items():
    assert sha(BACKUP/path) == digest, f'Backup no longer verifies: {path}'
routes = json.loads((BACKUP / 'route-sha256.json').read_text(encoding='utf-8-sig'))
assert len(routes) == 31
actual_routes = {p.relative_to(EXP).as_posix() for p in (EXP/'RouteRecordings~').rglob('*') if p.is_file()}
assert actual_routes == set(routes), 'Saved route storage gained/lost files.'
assert all(sha(EXP/path) == digest for path, digest in routes.items()), 'Saved route bytes changed.'
files = []
for parent, dirs, names in os.walk(EXP):
    dirs[:] = [d for d in dirs if not d.endswith('~')]
    files.extend(Path(parent)/name for name in names)
new = sorted(p.relative_to(EXP).as_posix() for p in files if p.relative_to(EXP).as_posix() not in baseline)
prior = (EXP/'Documentation/UI-Phases-1-to-4-Master-Acceptance.md').read_text(encoding='utf-8-sig')
master = (EXP/'Documentation/UI-Phases-1-to-5-Master-Acceptance.md').read_text(encoding='utf-8-sig')
rows = re.findall(r'^\| (?:[A-I]\d\d|P[34]-[A-Z]\d\d) \|.*$', prior, re.M)
assert len(rows) == 218 and all(row in master for row in rows), 'Historical checklist changed.'
new_rows = re.findall(r'^\| P5-\d\d-\d\d \|.*$', master, re.M)
assert len(new_rows) == 77 and all(' | NOT TESTED | ' in row for row in new_rows)
assert len(re.findall(r'^\| QUICK-\d\d \|', master, re.M)) == 20
guid_paths = {}
for p in files:
    if p.suffix != '.meta': continue
    match = re.search(r'^guid: (\w+)', p.read_text(errors='replace'), re.M)
    if not match: continue  # Existing malformed metadata is preserved.
    guid = match.group(1)
    assert guid not in guid_paths, f'Duplicate GUID {guid}'
    guid_paths[guid] = p.relative_to(EXP).as_posix()
for p in (EXP/'UI/Prefabs/Operator').glob('*.prefab'):
    for guid in re.findall(r'm_Script: \{fileID: 11500000, guid: (\w+)', p.read_text()):
        if guid in guid_paths: assert (EXP/guid_paths[guid].removesuffix('.meta')).is_file()
stage = ROOT/'Validation/UIAuthoring-Phase5/AcceptanceProject/Assets/Experiments/IceResurfacer'
for name in changed + new:
    if name.endswith('.cs'):
        assert (EXP/(name+'.meta')).is_file(), f'Missing script metadata: {name}'
        assert sha(EXP/name) == sha(stage/name), f'Source differs from the final tested copy: {name}'
new_asset_manifest = json.loads((OUT/'New-asset-cleanup.json').read_text(encoding='utf-8-sig'))
assert len(new_asset_manifest) == 17
for entry in new_asset_manifest:
    assert sha(EXP/entry['path']) == entry['after'], f"Post-validation asset change: {entry['path']}"
scene_hash = sha(EXP/'Scenes/IceResurfacer_AuthoredMachine.unity')
assert scene_hash == 'e0ea510c5a50981d0f53ab8c26045a25efccc1b2d8daa2a62901e8330d675c74'
report = dict(status='PASS', baselineFileCount=len(baseline), backupHashesVerified=True,
              changedExistingFiles=changed, unchangedExistingFileCount=len(baseline)-len(changed),
              newExperimentFiles=new, protectedRouteFiles=len(routes), routeStorageExact=True,
              newFileHashes={path:sha(EXP/path) for path in new}, testedSourcesMatch=True,
              oldPrefabThemeAndWorldAssetsUnchanged=True, preservedHistoricalChecks=218,
              phase5ManualChecks=77, canonicalChecks=295, quickReferences=20,
              sceneSha256=scene_hash, currentHashes={path:sha(EXP/path) for path in changed})
OUT.mkdir(parents=True, exist_ok=True)
(OUT/'Protection-audit.json').write_text(json.dumps(report, indent=2)+'\n')
print(json.dumps({k:v for k,v in report.items() if k not in ('newExperimentFiles','currentHashes','newFileHashes')}, indent=2))
