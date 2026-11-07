from pathlib import Path
import difflib
import hashlib
import json
import re
import shutil
import uuid

ROOT = Path(__file__).resolve().parents[2]
EXP = ROOT / 'Assets/Experiments/IceResurfacer'
RUN = ROOT / 'Validation/UIAuthoring-Phase4'
SOURCE = RUN / 'AcceptanceProject/ValidationResults'
OUT = RUN / 'Evidence'
OUT.mkdir(exist_ok=True)
logs = ['Unity-Acceptance-04.log', 'Unity-Restart-01.log', 'Unity-Delivered-final.log',
        'Unity-Input-regression-01.log', 'Unity-Routes-regression-01.log', 'Unity-Navigation-regression-01.log']
for name in logs:
    assert (RUN / (name + '.exit')).read_text().strip() == '0', f'Final Unity run did not pass: {name}'
    shutil.copy2(RUN / name, OUT / name)
pass_count = {}
for name in ['Phase4-results.txt', 'Phase3-results.txt', 'UIInput-results.txt', 'MachineryPhysical-results.txt']:
    text = (SOURCE / name).read_text(encoding='utf-8-sig')
    assert not re.search(r'^FAIL ', text, re.M), f'Failed final check in {name}'
    shutil.copy2(SOURCE / name, OUT / name)
    pass_count[name] = len(re.findall(r'^PASS ', text, re.M))
captures = sorted(SOURCE.glob('Phase4-*.png'))
assert len(captures) == 24
for path in captures + sorted(SOURCE.glob('Phase3-Delivered-*.png')):
    shutil.copy2(path, OUT / path.name)
shutil.copy2(RUN / 'Core-regression-latest.log', OUT / 'Core-regression.txt')
shutil.copy2(RUN / 'Compile-final.log', OUT / 'Compile-final.log')
for name in ['UI-Authoring-Phase4.md', 'UI-Phases-1-to-4-Master-Acceptance.md']:
    meta = EXP / ('Documentation/' + name + '.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
backup = EXP / 'Backups~/UIAuthoring-Phase4-20261106'
baseline = json.loads((backup / 'baseline-sha256.json').read_text(encoding='utf-8-sig'))
changes = []
for name in baseline:
    if name.endswith('.cs') and (EXP / name).read_bytes() != (backup / name).read_bytes():
        changes += list(difflib.unified_diff((backup / name).read_text(encoding='utf-8-sig').splitlines(True),
                        (EXP / name).read_text(encoding='utf-8-sig').splitlines(True), fromfile='phase4-baseline/' + name, tofile=name))
(OUT / 'Existing-source-changes.patch').write_text(''.join(changes), encoding='utf-8')
phase4 = (SOURCE / 'Phase4-results.txt').read_text()
report = dict(successfulUnityRuns=logs, successfulPassRecords=pass_count,
              menuCaptures=[p.name for p in captures], coreScenarios=43,
              measurementLines=re.findall(r'^PASS MEASUREMENT.*$', phase4, re.M),
              nativeInputAndMixedComposition='NOT TESTED; manual operator checklist',
              wholeFrameProfiling='NOT TESTED; Unity allocation counter failed retained 4096-byte calibration',
              completeRunEvidence='Existing deterministic core complete scored run; representative Complete formatter fixture; native end-to-end guided run remains manual')
(OUT / 'Validation-summary.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
