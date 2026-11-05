"""Read the CURRENT scene scalars and outline without loading/saving Unity assets."""
from pathlib import Path
import hashlib
import json
import re

here = Path(__file__).resolve().parent
root = here.parent.parent

def block(text, name):
    match = re.search(r"^  " + name + r":\n(.*?)(?=^ {0,2}\S|\Z)", text, re.M | re.S)
    values = {key: float(value) for key, value in re.findall(r"^    (\w+): ([-+\d.eE]+)\s*$", match[1], re.M)}
    outline = re.search(r"^    CollisionOutline:\n((?:    - [-+\d.eE]+\n)+)", match[1], re.M)
    if outline:
        values['CollisionOutline'] = [float(n) for n in re.findall(r"- ([-+\d.eE]+)", outline[1])]
    return values

for scene, output in [('IceResurfacer_AuthoredMachine.unity', 'scene-config.json'), ('IceResurfacer.unity', 'procedural-config.json')]:
    path = root / 'Scenes' / scene
    text = path.read_text(encoding='utf-8-sig')
    values = {'calibration': block(text, 'calibration'), 'scene_sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
    if '  simulation:\n' in text:
        values['tuning'] = block(text, 'simulation')
    target = here / output
    if target.exists():
        previous = json.loads(target.read_text())
        assert previous['calibration'] == values['calibration'], 'Current scene differs from earlier extracted values'
        assert previous.get('tuning') == values.get('tuning'), 'Current settings differ from earlier extraction'
    target.write_text(json.dumps(values, indent=2))
    print(scene, values['scene_sha256'])
