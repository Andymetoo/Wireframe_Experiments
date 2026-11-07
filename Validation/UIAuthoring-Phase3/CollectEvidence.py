from pathlib import Path
import json
import re
import shutil

ROOT = Path(__file__).resolve().parents[2]
RUN = ROOT/'Validation/UIAuthoring-Phase3'
SOURCE = RUN/'AcceptanceProject/ValidationResults'
OUT = RUN/'Evidence'
OUT.mkdir(exist_ok=True)
logs = ['Unity-acceptance-annotations.log', 'Unity-commands-annotations.log', 'Unity-persistence-annotations.log', 'Unity-integrated-annotations.log']
for name in logs:
    assert (RUN/(name+'.exit')).read_text().strip() == '0', f'Final Unity run did not pass: {name}'
    shutil.copy2(RUN/name, OUT/name)
raw = (SOURCE/'Phase3-results.txt').read_text(encoding='utf-8-sig')
(OUT/'Development-full-results.txt').write_text(raw, encoding='utf-8')
# A discarded infrastructure attempt reopened the default scene for the edited-scene check.
# Retain the full history above and list successful final re-verification separately.
clean = re.sub(r'(?m)^FAIL .*?(?=^PASS |\Z)', '', raw, flags=re.S)
assert 'Fresh Editor process retained all Phase 3 authoring changes.' in clean
assert 'Exact delivered scene retained every serialized byte through Play.' in clean
assert 'RMB free look releases survey interaction ownership.' in clean
prefix = 'Successful final isolated runs; prior development/infrastructure attempts retained in Development-full-results.txt and root Unity logs.\n'
(OUT/'Phase3-results.txt').write_text(prefix+clean, encoding='utf-8')
captures = sorted(SOURCE.glob('Phase3-*.png'))
assert len(captures) == 12
for path in captures:
    shutil.copy2(path, OUT/path.name)
shutil.copy2(RUN/'Core-regression-latest.log', OUT/'Core-regression.txt')
shutil.copy2(RUN/'Compile-final.txt', OUT/'Compile-final.txt')
report = dict(successfulUnityRuns=logs, captures=[p.name for p in captures],
              successfulPassRecords=len(re.findall(r'^PASS ',clean,re.M)),
              measurementLines=re.findall(r'^PASS MEASUREMENT.*$',clean,re.M),
              nativeInputAndMixedComposition='NOT TESTED; operator checklist',
              wholeFrameProfiling='NOT TESTED; allocation counter failed retained 4096-byte calibration')
(OUT/'Validation-summary.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
