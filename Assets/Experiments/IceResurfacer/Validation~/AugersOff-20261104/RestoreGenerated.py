"""Restore only generated build files proven clean in this turn's starting status."""
from pathlib import Path
import subprocess

here = Path(__file__).resolve().parent
root = here.parents[4]
git = ['git', '-c', 'safe.directory=' + root.as_posix()]
status = (here / 'before-git-status.txt').read_text(encoding='utf-8-sig')
paths = subprocess.check_output(git + ['diff', '--name-only', '--', 'Assets/Experiments/IceResurfacer/Validation~/OperatorTuning/bin', 'Assets/Experiments/IceResurfacer/Validation~/OperatorTuning/obj'], cwd=root, text=True).splitlines()
for path in paths:
    assert path not in status, 'Generated file was already modified; preserve it'
    assert '/OperatorTuning/bin/' in path or '/OperatorTuning/obj/' in path
    original = subprocess.check_output(git + ['show', 'HEAD:' + path], cwd=root)
    (root / path).write_bytes(original)
    print('Restored initially clean generated file:', path)
