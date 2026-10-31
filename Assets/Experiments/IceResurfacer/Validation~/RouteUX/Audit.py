from pathlib import Path
import hashlib,json,subprocess,sys
r=Path(__file__).resolve().parents[2]; repo=r.parents[2]; out=r/'Validation~/RouteUX'
baseline=json.loads((out/'Baseline-sha256.json').read_text())
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def git(*args): return subprocess.run(['git','-c','safe.directory='+repo.as_posix(),'-c','core.safecrlf=false',*args],cwd=repo,check=True,capture_output=True).stdout
if '--restore-artifacts' in sys.argv:
    keep={'Program.cs','IceResurfacer.Validation.csproj','TrainingGuideValidation.cs','RunIsolatedUnity.ps1','RunLayoutPlayer.ps1','RunPerformancePlayer.ps1'}
    for rel in filter(None,git('diff','--name-only','-z','--','Assets/Experiments/IceResurfacer/Validation~').decode().split('\0')):
        p=repo/rel;local=p.relative_to(r/'Validation~').as_posix()
        if local in keep: continue
        archive=out/'Regenerated-artifacts'/local;archive.parent.mkdir(parents=True,exist_ok=True);archive.write_bytes(p.read_bytes())
        p.write_bytes(git('show','HEAD:'+rel))
changed=[p for p,h in baseline.items() if not (r/p).is_file() or sha(r/p)!=h]
raw=[p for p in baseline if p.startswith('RouteRecordings~/')]
raw_changed=[p for p in changed if p in raw]
protected=[p for p in baseline if (p.startswith('Core/') and p!='Core/RouteProgress.cs') or p.startswith(('Scenes/','Shaders/')) or p in ['Presentation/IceResurfacerGame.cs','Presentation/IceMachineAudio.cs','Presentation/IceWorldBuilder.cs']]
bad=[p for p in changed if p in protected]
outside=[p for p in git('diff','--name-only','-z').decode().split('\0') if p and not p.startswith('Assets/Experiments/IceResurfacer/')]
result={'original_route_library_files':len(raw),'changed_original_route_library_files':raw_changed,'protected_simulation_geometry_audio_scene_changes':bad,'tracked_changes_outside_experiment':outside,'intentional_existing_file_changes':changed}
(out/'Preservation-audit.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
source={}
for folder in ['Core','Presentation','Editor','Resources','Documentation','Scenes','Shaders']:
    for p in (r/folder).rglob('*'):
        if p.is_file():source[p.relative_to(r).as_posix()]=sha(p)
(out/'Accepted-source-sha256.json').write_text(json.dumps(source,indent=2)+'\n')
if raw_changed or bad or outside:sys.exit(1)
