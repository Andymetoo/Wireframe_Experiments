from pathlib import Path
import hashlib,json
folder=Path(__file__).parent;root=folder.parents[1]
baseline=json.loads((folder/'Baseline-sha256.json').read_text())
allowed={'Core/TrainingRoute.cs','Core/TrainingRouteEditing.cs','Editor/TrainingRouteEditorWindow.cs',
         'Editor/IceTrainingGuideValidation.cs','Presentation/TrainingGuideView.cs','Presentation/TrainingGuideStorage.cs',
         'Presentation/IceRouteGuide.cs','Presentation/RouteManagerPanel.cs','Presentation/IceLayoutPlayerProbe.cs','Documentation/TrainingGuide.txt','README.txt'}
changed=[];missing=[]
for name,checksum in baseline.items():
    p=root/name
    if not p.is_file():missing.append(name)
    elif hashlib.sha256(p.read_bytes()).hexdigest()!=checksum:changed.append(name)
raw=[name for name in baseline if name.startswith('RouteRecordings~/') and '.route.json' in name]
library=[name for name in baseline if name.startswith('RouteRecordings~/')]
protected=['Core/Simulation.cs','Core/MovementContact.cs','Core/SimulatorConfig.cs','Core/SurfaceProcessing.cs',
           'Core/RouteRecording.cs','Core/RouteProgress.cs','Core/WaterService.cs','Core/ServiceLayout.cs',
           'Presentation/IceDashboard.cs','Presentation/IceResurfacerGame.cs','Presentation/IceMachineAudio.cs',
           'Presentation/IcePhysicalControls.cs','Presentation/IceOperatorPreferences.cs']
report={'changed_existing_sources':changed,'unexpected_changes':sorted(set(changed)-allowed),'missing':missing,
        'raw_and_backup_files_unchanged':len(raw) if not set(raw)&set(changed+missing) else False,
        'all_existing_library_files_unchanged':len(library) if not set(library)&set(changed+missing) else False,
        'protected_simulation_controls_speed_hud_audio_and_route_format_unchanged':not set(protected)&set(changed+missing),
        'scenes_geometry_materials_shaders_unchanged':not any(n.startswith(('Scenes/','Resources/','Shaders/')) for n in changed+missing)}
(folder/'Preservation-audit.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2));assert not report['unexpected_changes'] and not missing
assert report['raw_and_backup_files_unchanged']==len(raw)
assert report['all_existing_library_files_unchanged']==len(library)
