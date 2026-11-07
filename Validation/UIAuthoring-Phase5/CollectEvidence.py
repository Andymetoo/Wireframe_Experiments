from pathlib import Path
import difflib
import hashlib
import json
import re
import shutil
import struct
import uuid

ROOT = Path(__file__).resolve().parents[2]
EXP = ROOT/'Assets/Experiments/IceResurfacer'
RUN = ROOT/'Validation/UIAuthoring-Phase5'
SOURCE = RUN/'AcceptanceProject/ValidationResults'
OUT = RUN/'Evidence'
OUT.mkdir(exist_ok=True)
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
def read_log(path):
    raw = path.read_bytes()
    return raw.decode('utf-16' if raw.startswith((b'\xff\xfe', b'\xfe\xff')) else 'utf-8-sig', errors='replace')
logs = ['Unity-Acceptance-05.log', 'Unity-Restart-01.log', 'Unity-Delivered-final-02.log',
        'Unity-Projection-render-02.log', 'Unity-Menus-regression-01.log',
        'Unity-Input-regression-01.log', 'Unity-Routes-regression-01.log', 'Unity-Navigation-regression-01.log']
for name in logs:
    assert (RUN/(name+'.exit')).read_text().strip() == '0', f'Final Unity run did not exit cleanly: {name}'
    log = (RUN/name).read_text(encoding='utf-8-sig', errors='replace')
    assert not re.search(r'error CS\d+|^FAIL |^InvalidOperationException:|^NullReferenceException:|^MissingReferenceException:|^========== OUTPUTTING STACK TRACE', log, re.M), f'Unexpected error in {name}'
    shutil.copy2(RUN/name, OUT/name)
    shutil.copy2(RUN/(name+'.exit'), OUT/(name+'.exit'))
counts = {}
for name in ['Phase5-results.txt', 'Phase4-results.txt', 'Phase3-results.txt', 'UIInput-results.txt', 'MachineryPhysical-results.txt']:
    content = (SOURCE/name).read_text(encoding='utf-8-sig')
    assert not re.search(r'^FAIL ', content, re.M), f'Failed final assertion recorder: {name}'
    shutil.copy2(SOURCE/name, OUT/name)
    counts[name] = len(re.findall(r'^PASS ', content, re.M))
service = RUN/'AcceptanceProject/Assets/Experiments/IceResurfacer/Validation~/Service-results.txt'
service_text = service.read_text(encoding='utf-8-sig')
assert not re.search(r'^FAIL ', service_text, re.M)
shutil.copy2(service, OUT/'Service-results.txt')
counts['Service-results.txt'] = len(re.findall(r'^PASS ', service_text, re.M))
phase5 = (SOURCE/'Phase5-results.txt').read_text(encoding='utf-8-sig')
start_marker = 'PASS Persistent Phase 5 root and explicit scene bindings exist before Play.'
finish_marker = 'PASS Phase 5 Unity acceptance complete; native cursor, full-frame profiling and manual visual sign-off remain separate.'
final_run = phase5[phase5.rindex(start_marker):]
assert 'PASS Exact delivered scene retains all serialized bytes after Phase 5 Play tests.' in final_run
assert final_run.rstrip().endswith(finish_marker)
(OUT/'Phase5-delivered-results.txt').write_text(final_run, encoding='utf-8')
authoring = phase5[:phase5.index(finish_marker)+len(finish_marker)]+'\n'
assert 'PASS Procedural pause/help state remains functional.' in authoring
(OUT/'Phase5-authoring-results.txt').write_text(authoring, encoding='utf-8')
projection_end = phase5.rindex('PASS Rendered integrated ProjectedGPS at (1366, 768) / default')
projection_start = phase5.rfind('PASS Authored GPS graphic receives exactly', 0, projection_end)
projection_finish = phase5.index(finish_marker, projection_end)+len(finish_marker)
projection = phase5[projection_start:projection_finish]+'\n'
assert 'PASS Final fresh-process reopen retains all deliberate Phase 5 authoring changes.' in projection
assert 'PASS Leaving Play retains no transient GPS query colliders.' in projection
(OUT/'Phase5-projection-final-results.txt').write_text(projection, encoding='utf-8')
captures = sorted(SOURCE.glob('Phase5-*.png'))
assert len(captures) == 51
for path in captures:
    data = path.read_bytes()
    assert data[:8] == b'\x89PNG\r\n\x1a\n'
    expected_size = tuple(map(int, re.search(r'-(\d+)x(\d+)\.png$', path.name).groups()))
    assert struct.unpack('>II', data[16:24]) == expected_size, f'Capture dimensions differ: {path}'
for variant, expected in [('default', 27), ('edited', 24)]:
    assert len(list(SOURCE.glob('Phase5-'+variant+'-*.png'))) == expected
for path in captures + sorted(SOURCE.glob('Phase3-Delivered-*.png')) + sorted(SOURCE.glob('Phase4-*.png')):
    shutil.copy2(path, OUT/path.name)
core = read_log(RUN/'Core-regression-latest.log')
compile_log = read_log(RUN/'Compile-final.log')
assert 'PASS: 43 deterministic simulation scenarios.' in core
assert 'PASS static C# compilation only.' in compile_log and not re.search(r'error CS\d+', compile_log)
shutil.copy2(RUN/'Core-regression-latest.log', OUT/'Core-regression.txt')
shutil.copy2(RUN/'Compile-final.log', OUT/'Compile-final.log')
for name in ['UI-Authoring-Phase5.md', 'Aurora-UI-Authoring-Handbook.md', 'UI-Phases-1-to-5-Master-Acceptance.md']:
    meta = EXP/('Documentation/'+name+'.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
backup = EXP/'Backups~/UIAuthoring-Phase5-20261106'
baseline = json.loads((backup/'baseline-sha256.json').read_text(encoding='utf-8-sig'))
changes = []
for name in sorted(baseline):
    if name.endswith('.cs') and (EXP/name).read_bytes() != (backup/name).read_bytes():
        changes.extend(difflib.unified_diff((backup/name).read_text(encoding='utf-8-sig').splitlines(True),
                      (EXP/name).read_text(encoding='utf-8-sig').splitlines(True), fromfile='phase5-baseline/'+name, tofile=name))
(OUT/'Existing-source-changes.patch').write_text(''.join(changes), encoding='utf-8')
rejected = {}
for path in RUN.glob('*.log.exit'):
    value = path.read_text().strip()
    if value != '0': rejected[path.name.removesuffix('.exit')] = value
report = dict(status='PASS', successfulUnityRuns=logs,
    finalDeliveredPassRecords=len(re.findall(r'^PASS ', final_run, re.M)),
    finalAuthoringPassRecords=len(re.findall(r'^PASS ', authoring, re.M)),
    finalProjectionPassRecords=len(re.findall(r'^PASS ', projection, re.M)),
    cumulativeRecorderPassRecords=counts,
    recorderCountCaveat='Cumulative recorder includes repeated suites and pre-exit assertions from a rejected development capture. Final run slices above are separated; only clean-exit logs are accepted. Counts are not unique manual checks.',
    phase5Captures=[dict(file=p.name, sha256=sha(p)) for p in captures], coreScenarios=43,
    captureDimensionsVerified=True,
    finalMeasurementLines=re.findall(r'^PASS MEASUREMENT.*$', final_run, re.M),
    projectionMeasurementLines=re.findall(r'^PASS MEASUREMENT.*$', projection, re.M),
    rejectedDevelopmentRuns=rejected, rejectedLogsRetained='Validation/UIAuthoring-Phase5; excluded from accepted logs',
    nativeHardwareCursorAndMixedIMGUI='NOT TESTED; manual acceptance remains',
    wholeFrameProfiling='NOT TESTED; managed counter failed retained 4096-byte calibration',
    manualVisualAcceptance='NOT TESTED; automated images were rendered and representative combinations inspected',
    editedFixtureLocation='Validation/UIAuthoring-Phase5/AcceptanceProject/ValidationResults/Authored-persistence.unity; custom font/sprite assets remain in the isolated project')
(OUT/'Validation-summary.json').write_text(json.dumps(report, indent=2)+'\n')
print(json.dumps({k:v for k,v in report.items() if k not in ('phase5Captures',)}, indent=2))
