"""Read-only source audit and report generation for the focused calibration fix."""
from pathlib import Path
import hashlib
import html
import json

out = Path(__file__).resolve().parent
root = out.parent.parent
baseline = json.loads((out / "Baseline-sha256.json").read_text())
allowed = {
    "Core/TrainingRoute.cs", "Core/RouteFitDiagnostics.cs",
    "Editor/TrainingRouteEditorWindow.cs", "Presentation/IceDashboard.cs",
    "Presentation/IceLayoutPlayerProbe.cs", "Presentation/IceRouteGuide.cs",
    "Presentation/RouteManagerPanel.cs", "Presentation/TrainingGuideStorage.cs",
    "Documentation/HUDVideoSpeed.txt", "Documentation/TrainingGuide.txt",
}

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

changed = [p for p, sha in baseline.items() if not (root / p).is_file() or digest(root / p) != sha]
assert set(changed) == allowed, changed
protected = [p for p in baseline if p.startswith(("Scenes/", "Machine/", "Resources/", "Shaders/", "RouteRecordings~/"))]
assert not set(changed).intersection(protected)
raw_files = [p for p in baseline if p.endswith((".route.json", ".route.json.bak"))]
guide_files = [p for p in baseline if p.startswith("RouteRecordings~/")]

original_path = root / "RouteRecordings~/Guides/Andy_Try_1___recorded_track__20261031_052846_ec332e9e.guide.json"
corrected_path = Path((out / "Corrected-guide-path.txt").read_text().strip())
original = json.loads(original_path.read_text(encoding="utf-8-sig"))["guide"]
corrected = json.loads(corrected_path.read_text(encoding="utf-8-sig"))["guide"]
assert corrected["id"] != original["id"]
assert corrected["rawRecordingJson"] == original["rawRecordingJson"]
assert corrected["sourceSha256"] == original["sourceSha256"]
assert corrected["controls"] == original["controls"]
assert corrected["configuration"]["ConditionerYawLimitRadians"] == 0
assert corrected["rawRecordingJson"] == (root / "RouteRecordings~/Andy_Try_1_20261031_041839_75958600.route.json").read_text(encoding="utf-8-sig")

def read(path):
    b = path.read_bytes()
    return b.decode("utf-16" if b[:2] in (b"\xff\xfe", b"\xfe\xff") else "utf-8-sig")

checks = {}
for name in ["IceResurfacer-results.txt", "IceResurfacer_AuthoredMachine-results.txt", "Editor-GUI-results.txt", "Native-original-results.txt", "Native-authored-results.txt"]:
    text = read(out / name)
    assert not any(line.startswith("FAIL") for line in text.splitlines()), name
    checks[name] = sum(line.startswith("PASS ") for line in text.splitlines())
assert "PASS: 40 deterministic simulation scenarios." in read(out / "Core-results.txt")
assert "Final runtime error/exception count: 0" in read(out / "Unity-regression-results.txt")
for name in ["IceResurfacer-results.txt", "IceResurfacer_AuthoredMachine-results.txt"]:
    assert "REPLAY samples=5130 length=1003.234406572 contexts=132" in read(out / name)

audit = {
    "baselineFiles": len(baseline), "unchangedBaselineFiles": len(baseline) - len(changed),
    "modifiedBaselineFiles": sorted(changed), "unchangedProtectedFiles": protected,
    "unchangedExistingLibraryFiles": guide_files, "unchangedRawAndBackupFiles": raw_files,
    "correctedGuide": str(corrected_path), "correctedGuideSha256": digest(corrected_path),
    "exactEmbeddedRawPreserved": True, "exact398ControlPointsPreserved": True, "checks": checks,
}
(out / "Preservation-audit.json").write_text(json.dumps(audit, indent=2) + "\n")
new = [
    "Documentation/RouteCalibrationCompatibility.txt", "Documentation/RouteCalibrationCompatibility.txt.meta",
    "Editor/IceRouteCalibrationValidation.cs", "Editor/IceRouteCalibrationValidation.cs.meta",
    "Presentation/IceLayoutPlayerProbe.Calibration.cs", "Presentation/IceLayoutPlayerProbe.Calibration.cs.meta",
    str(corrected_path.relative_to(root)).replace("\\", "/"),
]
(out / "Modified-files.txt").write_text(
    "MODIFIED EXISTING FILES\n" + "\n".join(sorted(changed)) +
    "\nValidation~/RunIsolatedUnity.ps1\n\nNEW FILES\n" + "\n".join(new) +
    "\n\nEvidence: Validation~/CalibrationCompatibility/ and native screenshots in\n"
    "Validation~/CalibrationCompatibility-{Original,Authored}/Player-1366x768/.\n"
    "Disposable native fixture libraries: Validation~/CC-Native-O/ (and A if present).\n")

report = f"""AURORA R-54 / FOCUSED GPS CALIBRATION COMPATIBILITY FIX
User-facing date: 2026-11-01, America/Los_Angeles. Saved-guide filenames use UTC.

EXACT ORIGINAL MISMATCH (reported before implementation)
Field: ConditionerYawLimitRadians
Earlier repaired guide JSON: 0.03490658476948738 radians (2 degrees)
IceResurfacer.unity active config: 0 (field absent; current default applies)
IceResurfacer_AuthoredMachine.unity active config: 0 (explicit serialized value)
The other 19 fields in RouteFit.Compatible match within the existing 0.0001
tolerance. Before-calibration-comparison.txt lists every field for BOTH scenes;
the two Play Mode result files independently log the actual active float values.
This is independent of the authored prefab.

ROOT CAUSE AND REPAIR
The earlier repair preserved a derived calibration snapshot copied from the
historical raw recording. Production motion now locks the conditioner square to
the chassis and retains its old yaw field only for legacy serialization. The old
guide still declares 2 degrees, causing the strict compatibility check to refuse it.

No compatibility field or tolerance was removed or relaxed. Current calibration
is adopted only on a NEW derived copy after full clearance/source/curve checking.
The fitter also checks the actual current production conditioner attachment at
every recorded vehicle pose, in addition to the historical observed conditioner.
Unsafe enlarged geometry and a deliberately false historical conditioner fixture
are both rejected. No physics or authored geometry was changed to make a guide fit.

NEW CORRECTED GUIDE
Name: {corrected['name']}
Path: {corrected_path}
SHA-256: {digest(corrected_path)}
Guide version 2; embedded original raw recording remains version 1.
All 398 controls, source indices, handles and annotations are exactly preserved.
Embedded original raw bytes/string and source checksum are exactly preserved.
The earlier repaired guide and every pre-existing library file remain unchanged.
Both scenes tested this same new guide. The old guide intentionally still reports
its stale declaration; select the NEW copy rather than the earlier repaired file.

OPERATOR CONTROLS
Play > Free Simulation / Practice > Numpad 8 > Refresh > select the new guide
whose full name ends '/ current calibration' > Use as GPS. Numpad 7 toggles it.
The built-in original reference remains available and is not permanently replaced.

For future calibration changes, open the intended scene, then Experiments >
Ice Resurfacer > Training Route Editor; open a guide and choose 'Revalidate for
active scene (save NEW copy)'. Failed checks prevent publishing a ready copy.
Route Manager now lists actual mismatched field names and guide/session values;
its status area scrolls for multiple differences. Old raw .route.json import can
prepare a current-calibration derived copy after full safety checks, preserving
the historical raw config and all observations. Existing saved guides require
explicit revalidation; selection never silently substitutes their calibration.

ESC SPEED CONTROL
Removed RESET TO ORIGINAL SPEED / 5.93 MPH and its UI hitbox. Kept the existing
3-8 MPH slider, 0.25 MPH steps, PlayerPrefs key/save behavior and exact 2.65 m/s
internal default. No velocity, acceleration, load, braking or traction changes.
Run/session diagnostics still retain the original and selected values.

VALIDATION
40 deterministic core scenarios: PASS.
Existing full Unity suite: PASS, final runtime errors/exceptions 0.
Focused actual Play Mode: {checks['IceResurfacer-results.txt']} checks in original scene,
{checks['IceResurfacer_AuthoredMachine-results.txt']} in authored scene, both PASS.
Existing actual Editor GUI suite: {checks['Editor-GUI-results.txt']} PASS.
Native 1366x768 player checks: {checks['Native-original-results.txt']} PASS original,
{checks['Native-authored-results.txt']} PASS authored. Real UI clicks reproduce the
old guide's specific mismatch, accept the new guide, verify speed 3/8/5.50 MPH,
verify the former reset area does nothing and preserve the built-in reference.
Screenshots include mismatch/accepted manager, seeded GPS view and ESC menu.

Each scene's Editor AND native replay traversed all 5,130 real recorded poses,
46,616 guide samples and 132 sequential contexts to the 1003.234406572 m endpoint.
Maximum source-distance lead 0.201903 m (one original observation interval),
maximum progress advance 0.411230 m; no later-loop jumps. Local search <=336 samples.
Current calibrated fit: boundary errors 0, curvature errors 0, source errors 0,
observed-radius warnings 0, raw trajectory displacement 0. All existing
compatibility fields tested individually; null/non-finite config rejected.
Replay leaves live vehicle, conditioner, controls, scoring and simulation state
unchanged. The live route display remains bounded to the existing 40 feet.
Fresh authored-scene process reloads the exact guide saved by the original run.

PRESERVATION
{len(protected)} protected scene/machine/resource/shader/library files unchanged by SHA-256,
including {len(guide_files)} existing library files and {len(raw_files)} raw/backup files.
In total, {len(baseline)-len(changed)} baseline files outside the focused changes remain
byte-identical, including physics, SimulatorConfig, speed preferences, progress,
raw format, ice/snow/water/scoring, cockpit, spatial audio and authored assets.
Preservation-audit.json and Baseline-sha256.json hold the complete evidence.
Only IceResurfacer changed. No authored-machine rebake, branch, commit or push.

LIMITATIONS
This is deterministic observation replay, not a new manual physical drive.
Recorded conditioner poses remain historical observations; current attached
conditioner safety is additionally checked. No ideal C2 spline certification is
asserted. The native test windows run invisibly and report 5 original / 2 authored
D3D presentation warnings; these tests do not establish focused input latency or
subjective audio quality. Screenshots are actual native framebuffer captures.
No remaining supplied-guide calibration mismatch was found in either scene.
Future calibration changes intentionally require another validated derived copy.

See Modified-files.txt, Screenshots.html and Documentation/RouteCalibrationCompatibility.txt.
"""
(out / "Report.txt").write_text(report)

cards = []
for scene in ["Original", "Authored"]:
    for caption, stem in [("Exact mismatch", "mismatch"), ("Corrected guide accepted", "ready"), ("Corrected GPS (seeded render pose)", "GPS"), ("ESC slider, reset removed", "ESC")]:
        relative = f"../CalibrationCompatibility-{scene}/Player-1366x768/Calibration-{stem}-1366x768.png"
        assert (out / relative).is_file()
        cards.append(f'<figure><figcaption>{scene}: {html.escape(caption)}</figcaption><a href="{relative}"><img loading="lazy" src="{relative}" alt="{html.escape(caption)}"></a></figure>')
gallery = """<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width">
<title>Aurora GPS calibration evidence</title><style>body{font:16px system-ui;background:#111b24;color:#eef4f7;margin:24px}a{color:#83e9d1}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(500px,1fr));gap:20px}figure{margin:0;background:#21313d;border-radius:8px;overflow:hidden}figcaption{padding:12px}img{display:block;width:100%}p{max-width:1000px}</style>
<h1>Aurora R-54 — GPS calibration compatibility</h1><p>Actual native 1366×768 framebuffers from both unchanged scenes. The GPS camera pose is explicitly seeded after the complete sequential observation replay; it is not a newly driven route. Earlier guide and raw source remain intact.</p><p><a href="Report.txt">Validation report</a> · <a href="Before-calibration-comparison.txt">Exact before comparison (all 20 fields)</a> · <a href="Modified-files.txt">Modified files</a></p><main>""" + "".join(cards) + "</main></html>"
(out / "Screenshots.html").write_text(gallery, encoding="utf-8")
print(report)
