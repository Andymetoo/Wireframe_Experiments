"""Compare this pass with its initial working tree, including prior uncommitted work."""
import hashlib, json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
folder = Path(__file__).parent
baseline = json.loads((folder / "Baseline-sha256.json").read_text(encoding="utf-8-sig"))
allowed = {
    "Core/Simulation.cs", "Core/SimulatorConfig.cs", "Core/SurfaceProcessing.cs",
    "Presentation/IceResurfacerGame.cs", "Presentation/IceDashboard.cs",
    "Presentation/IcePhysicalControls.cs", "Presentation/IceWaterService.cs",
    "Presentation/IceCollisionOverlay.cs", "Presentation/IceRouteGuide.cs",
    "Presentation/RouteSilhouetteContinuation.cs", "Presentation/IceLayoutPlayerProbe.cs",
    "Editor/IceResurfacerEditor.cs", "README.txt",
}
changed, missing = [], []
for name, digest in baseline.items():
    path = root / name
    if not path.exists():
        missing.append(name)
    elif hashlib.sha256(path.read_bytes()).hexdigest() != digest:
        changed.append(name)
unexpected = sorted(set(changed) - allowed)
routes = [name for name in baseline if name.startswith("RouteRecordings~/")]
report = {
    "changed_existing_files_this_pass": changed,
    "unexpected_changes": unexpected,
    "missing_files": missing,
    "route_library_files_unchanged": len(routes) if not any(n in changed + missing for n in routes) else False,
    "scene_geometry_materials_audio_route_formats_unchanged": not any(
        n in changed + missing for n in baseline
        if n.startswith(("Scenes/", "Resources/", "Shaders/"))
        or n.endswith(("IceMachineAudio.cs", "IceWorldBuilder.cs", "RouteRecording.cs", "TrainingRoute.cs"))
    ),
}
(folder / "Preservation-audit.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(json.dumps(report, indent=2))
assert not missing and not unexpected
assert report["route_library_files_unchanged"] == len(routes)
