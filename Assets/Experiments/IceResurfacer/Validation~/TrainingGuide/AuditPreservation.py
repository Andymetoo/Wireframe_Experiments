"""Read-only audit; --restore-test-artifacts restores only this pass's test side effects.
The workspace was clean at the pass baseline. Git is used for reads only.
"""
from pathlib import Path
import hashlib
import json
import subprocess
import sys

experiment = Path(__file__).resolve().parents[2]
repo = experiment.parents[2]
output = experiment / 'Validation~/TrainingGuide'
baseline = experiment / 'Validation~/TrainingGuideBefore'
def digest(data): return hashlib.sha256(data).hexdigest()
def git(*args):
    process = subprocess.run(['git', '-c', 'safe.directory=' + repo.as_posix(), *args], cwd=repo, capture_output=True)
    if process.returncode: raise RuntimeError(process.stderr.decode(errors='replace'))
    return process.stdout

raw = json.loads((baseline / 'Raw-recordings-sha256.json').read_text(encoding='utf-8-sig'))
if '--restore-test-artifacts' in sys.argv:
    intentional = {'Program.cs', 'IceResurfacer.Validation.csproj', 'RunIsolatedUnity.ps1', 'RunPerformancePlayer.ps1'}
    changed = git('diff', '--name-only', '-z', '--', 'Assets/Experiments/IceResurfacer/Validation~').decode().split('\0')
    for relative in filter(None, changed):
        file = repo / relative
        local = file.relative_to(experiment / 'Validation~').as_posix()
        if local in intentional: continue
        # Every restored file existed at the initially clean baseline. Archive
        # current generated output first, with the same relative directory tree.
        archive = output / 'RegressionArtifacts' / local
        archive.parent.mkdir(parents=True, exist_ok=True)
        archive.write_bytes(file.read_bytes())
        original = git('show', 'HEAD:' + relative)
        expected = raw.get(file.relative_to(experiment).as_posix())
        if expected:
            lf = original.replace(b'\r\n', b'\n')
            candidates = [original, lf, lf.replace(b'\n', b'\r\n')]
            candidates += [b'\xef\xbb\xbf' + b.removeprefix(b'\xef\xbb\xbf') for b in candidates]
            matches = [b for b in candidates if digest(b) == expected]
            if not matches: raise RuntimeError('Cannot recover original exact raw bytes: ' + relative)
            original = matches[0]
        file.write_bytes(original)

allowed = {'Core/ResurfacingRoute.cs', 'Presentation/IceRouteGuide.cs', 'Presentation/IceDashboard.cs',
           'Presentation/RouteRecordingPanel.cs', 'Presentation/RouteRecordingStorage.cs',
           'Presentation/IceLayoutPlayerProbe.cs', 'Presentation/IcePerformanceProbe.cs',
           'Editor/IceRouteRecordingsWindow.cs', 'Editor/IceResurfacerEditor.cs'}
source = json.loads((baseline / 'Baseline-sha256.json').read_text(encoding='utf-8-sig'))
modified = [p for p, sha in source.items() if digest((experiment / p).read_bytes()) != sha]
unexpected = [p for p in modified if p not in allowed]
raw_changed = [p for p, sha in raw.items() if digest((experiment / p).read_bytes()) != sha]
outside = [p for p in git('diff', '--name-only', '-z').decode().split('\0') if p and not p.startswith('Assets/Experiments/IceResurfacer/')]
guids = {}
duplicates = []
for folder in ['Core', 'Presentation', 'Editor', 'Resources', 'Documentation', 'Scenes', 'Shaders']:
    for file in (experiment / folder).rglob('*.meta'):
        for line in file.read_text(encoding='utf-8-sig').splitlines():
            if line.startswith('guid: '):
                guid = line[6:]
                if guid in guids: duplicates.append([str(file), str(guids[guid])])
                guids[guid] = file
report = {'baseline_source_files': len(source), 'unchanged_source_files': len(source)-len(modified),
          'authorized_changed_source_files': modified, 'unexpected_source_changes': unexpected,
          'preserved_raw_recordings': len(raw)-len(raw_changed), 'raw_recording_changes': raw_changed,
          'tracked_changes_outside_experiment': outside, 'duplicate_meta_guids': duplicates,
          'physics_audio_config_scene_and_ice_sources_unchanged': not unexpected}
(output / 'Preservation-audit.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
print(json.dumps(report, indent=2))
if unexpected or raw_changed or outside or duplicates: sys.exit(1)
