"""Package final validation results and a local full-resolution screenshot gallery."""
from pathlib import Path
from html import escape
import json, shutil

folder = Path(__file__).parent
validation = folder.parent
root = validation.parent
resolutions = ["1920x1080", "1366x768", "960x600"]
counts = {}
for res in resolutions:
    source = validation / f"HUD-player-results-{res}.txt"
    target = folder / f"Player-{res}"
    if not (target / "Results.txt").exists():
        shutil.copyfile(source, target / "Results.txt")
    report = (target / "Results.txt").read_text(encoding="utf-8-sig")
    assert "PASS: all player HUD checks." in report and "FAIL " not in report, res
    assert "no measured text clipping" in (target / f"HUD-layout-issues-{res}.txt").read_text()
    counts[res] = sum(line.startswith("PASS ") for line in report.splitlines())

core = (validation / "HUDVideoSpeed-core-2.log").read_text(encoding="utf-16" if (validation / "HUDVideoSpeed-core-2.log").read_bytes().startswith(b"\xff\xfe") else "utf-8-sig")
assert "PASS: 40 deterministic simulation scenarios." in core
(folder / "Core-results.txt").write_text(core, encoding="utf-8")
assert "Final runtime error/exception count: 0" in (folder / "Unity-regression-results.txt").read_text()
assert "fresh Unity Play reloads 5.50 MPH" in (folder / "Preference-fresh-play.txt").read_text()
audit = json.loads((folder / "Preservation-audit.json").read_text())
assert audit["route_library_files_unchanged"] == 17 and not audit["unexpected_changes"]

states = [
    ("Normal compact HUD", "HUD-{res}-compact.png"),
    ("Expanded console and blade contact", "HUD-{res}-operator.png"),
    ("ESC speed slider", "Refinement-{res}-speed.png"),
    ("Rightward seated view after a sharp turn", "Refinement-{res}-right-turn.png"),
    ("Collapsed survey", "Refinement-{res}-map-collapsed.png"),
    ("Retracted blade", "Refinement-{res}-blade-retracted.png"),
    ("Shallow blade contact", "Refinement-{res}-blade-shallow.png"),
    ("Deep blade contact", "Refinement-{res}-blade-deep.png"),
    ("Conditioner lifted: no blade contact", "Refinement-{res}-blade-lifted.png"),
    ("Clean video: HUD, GPS, F2/F3 diagnostics hidden", "Refinement-{res}-video.png"),
    ("Explicit ESC in video mode", "Refinement-{res}-video-menu.png"),
    ("Restored HUD and GPS", "Refinement-{res}-restored.png"),
    ("Left board / conditioner quicklook", "HUD-{res}-look-Q.png"),
]
html = ["<!doctype html><meta charset='utf-8'><title>Aurora HUD / Video / Speed validation</title>",
        "<style>body{background:#101a20;color:#dcece9;font:16px system-ui;margin:32px auto;max-width:1440px}a{color:#8ce6c5}figure{margin:28px 0;background:#1c2a32;padding:12px}img{width:100%;height:auto}figcaption{margin-bottom:12px}nav{position:sticky;top:0;background:#101a20;padding:14px}</style>",
        "<h1>Aurora R-54: HUD, video and operator speed</h1><p>Actual Unity player framebuffer captures. Test poses and machinery states are labeled deterministic fixtures. Click any image for its original resolution.</p>",
        "<nav>" + " | ".join(f"<a href='#{r}'>{r}</a>" for r in resolutions) + " | <a href='Report.txt'>Validation report</a></nav>"]
for res in resolutions:
    html.append(f"<h2 id='{res}'>{res}</h2>")
    for label, pattern in states:
        name = pattern.format(res=res)
        path = folder / f"Player-{res}" / name
        assert path.is_file(), path
        link = f"Player-{res}/{name}"
        html.append(f"<figure><figcaption>{escape(label)}</figcaption><a href='{link}'><img loading='lazy' src='{link}' alt='{escape(label)} at {res}'></a></figure>")
(folder / "Screenshots.html").write_text("\n".join(html), encoding="utf-8")

report = f"""AURORA R-54 / HUD, VIDEO AND OPERATOR SPEED / 30 OCTOBER 2026

Controls
ESC > MAX FORWARD SPEED: 3.00-8.00 MPH in .25 MPH steps. Immediate PlayerPrefs
save. RESET TO ORIGINAL SPEED restores exact 2.65 m/s, including between ticks.
NUMPAD 1 hides/restores ordinary gameplay UI, GPS, F2 surface diagnostic colors,
F3 contact overlays, FPS, tooltips and interaction highlights. Machinery, audio,
driving and camera control remain active. Explicit menus remain recoverable.
MAP - / MAP + collapses/restores the upper-left survey; TAB expands the lower-
right console. Instruments sit at the top right. Hydraulic mini-controls:
top/forward LOWER; bottom/toward seat RAISE; release holds actual height.
Blade side view is a schematic of extension and pressure-adjusted contact,
with exaggerated vertical travel. Numeric readouts remain; no pitch model added.

Before / after comparison (unloaded, raised/dry, straight, full pedal)
Before: serialized maximum 2.650000 m/s = 5.92788 MPH.
After / no preference or exact Reset: 2.650000 m/s = 5.92788 MPH.
After / minimum: measured 1.341120 m/s = 3.00000 MPH.
After / maximum: measured 3.576320 m/s = 8.00000 MPH.
These are test-fixture steady speeds, not a manufacturer's specification.
No permanent 15 percent reduction was applied. Exact-original steering/motion
matches the comparison fixture. Normal acceleration, reverse, geometry,
hydrostatic coefficients and traction calibration are unchanged. Lowering the
limit never assigns velocity: existing hydrostatic deceleration and traction
approach the new target. Normal acceleration approaches a higher target.
Snow-tank load, deep cutting/snow resistance, service braking and hydrostatic
pedal-release braking retained their effects in deterministic checks.

Validation
Core: 40/40 deterministic scenarios passed (Core-results.txt).
Unity 6000.4.1f1: actual Play Mode, all regression suites passed, zero runtime
errors/exceptions (Unity-regression-results.txt). New control checks include
3 / 5.5 / 8 MPH, unchanged GPS geometry, recording actual driving at 3 and 8 MPH,
PlayerPrefs session restoration/reset, Numpad 1, independent number-row controls,
ESC recovery, F2 suppression/restoration, and all four blade engagement states.
Fresh process: selected 5.50 MPH in Play, exited, launched a new Unity process,
entered Play again and recovered 5.50 MPH; reset restored unchanged 2.65 m/s.
Validation preferences use a separate test company/product from the real app.
Native player checks: {json.dumps(counts)}.
All three resolutions passed, with no measured text clipping. Native mouse
checks cover slider ends and quarter-MPH setting, exact reset, partial hydraulic
travel in both directions, release-to-hold, survey collapse/expand, and modal
route-panel input isolation. Virtual device checks exercise the actual Num1,
Num8, number-row 1, W and ESC handlers, including a focused route-name field.
The modal regression fix prevents expanded mini-controls consuming route UI clicks.
Existing route-editor, nearby-pass progression, native depth occlusion, map
inspection, audio structure, scored-run and vehicle simulation checks passed.

Preservation
All 17 pre-existing route-library files retain their exact SHA-256 values.
Saved scene calibration, world/conditioner geometry, audio implementation,
materials, shaders and route serialization formats retain their baseline hashes.
Prior uncommitted cockpit, route-manager and GPS work was preserved against the
start-of-pass working-tree snapshot, rather than restored from Git HEAD.
Only Assets/Experiments/IceResurfacer changed. No branch, commit or push.
See Preservation-audit.json and ModifiedFiles.txt for the detailed scope.

Persistence and limitations
PlayerPrefs key: AuroraR54.Operator.MaxForwardSpeedMph.v1. Reset deletes only it.
Results and RunReport include selected and original MPH. The field reports the
final selection, not a history of mid-run adjustments; compare separate runs.
The original value is available with Reset, rather than rounding it to a .25 tick.
Slider calibration is personal; manual full-cut driving feel remains for the
operator to judge. The schematic shows geometric effective contact, including
lift pressure, but stationary contact does not imply material is being removed.
The survey collapse state is a runtime UI choice, not a saved preference.
Screenshots establish layout and rendering at 1920x1080, 1366x768 and 960x600.
Small text at 960x600 still merits operator review on the intended monitor.
Hidden-window D3D presentation warnings are counted in each native Results.txt;
these captures do not establish focused-window latency or subjective audio quality.
The Development Build watermark belongs to the validation build. It is absent
from the normal Editor Game view and a non-development player build.
An initial capture found a schematic-line/readout overlap, corrected before the
final captures. Earlier native iterations exposed modal click-through and a
test helper that omitted the route component's Update; both were corrected.
"""
(folder / "Report.txt").write_text(report, encoding="utf-8")
print("Evidence ready:", counts)
