"""Read the current saved calibration; no Unity assets are written."""
from pathlib import Path
import importlib.util
import json
import sys
sys.dont_write_bytecode = True
here = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('asset_audit', here.parent/'OperatorTuning/AssetAudit.py')
a = importlib.util.module_from_spec(spec)
spec.loader.exec_module(a)
scene = (here.parent.parent/'Scenes/IceResurfacer_AuthoredMachine.unity').read_text()
data = {'calibration': a.numeric_block(scene, 'calibration', 2), 'tuning': a.numeric_block(scene, 'simulation', 2)}
(here/'scene-config.json').write_text(json.dumps(data, indent=2))
print('Extracted current authored calibration and Operator Tuning:', len(data['calibration']), 'calibration fields')
