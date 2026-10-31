from pathlib import Path
import json,html

folder=Path(__file__).parent;validation=folder.parent
gui=(folder/'Editor-GUI-results.txt').read_text(encoding='utf-8-sig')
actual=(folder/'Unity-real-route-results.txt').read_text(encoding='utf-8-sig')
regression=(folder/'Unity-regression-results.txt').read_text(encoding='utf-8-sig')
native=(folder/'Native-results.txt').read_text(encoding='utf-8-sig')
core_bytes=(validation/'RouteRepair-core-final.log').read_bytes()
core=core_bytes.decode('utf-16' if core_bytes.startswith(b'\xff\xfe') else 'utf-8-sig')
assert 'PASS: 40 deterministic simulation scenarios.' in core
assert 'FAIL ' not in gui and 'FAIL ' not in actual
assert 'Final runtime error/exception count: 0' in regression
assert 'PASS: all player HUD checks.' in native and 'FAIL ' not in native
assert 'synthetic raw trajectory outside the physical floor remains blocked' in actual
assert 'fresh Unity process reloads the actual corrected' in (folder/'Fresh-guide-persistence.txt').read_text(encoding='utf-8-sig')
gui_log=(validation/'RouteRepair-editor-gui-verified.log').read_text(encoding='utf-8-sig',errors='replace')
for error in ['ArgumentOutOfRangeException:','IndexOutOfRangeException:','NullReferenceException:','Getting control ','GUI Error:']:
    assert error not in gui_log,error
(folder/'Core-results.txt').write_text(core,encoding='utf-8')
counts={label:sum(line.startswith('PASS ') for line in text.splitlines()) for label,text in [('editor_GUI',gui),('actual_recording',actual),('native_player',native)]}
audit=json.loads((folder/'Preservation-audit.json').read_text());assert not audit['unexpected_changes'] and audit['all_existing_library_files_unchanged']==23
guide=(folder/'Saved-guide.txt').read_text().strip()
report=f'''AURORA R-54 / ANDY_TRY_1 ROUTE REPAIR / 30 OCTOBER 2026

Result
Your raw recording passes the production pose checks. The reported 16 curvature
and 63 boundary failures are in the saved derived geometry. The repaired guide
has 0 curvature failures, 0 boundary failures and 0 observed-radius warnings.
The real recording, not an invented historical route, is the acceptance case.

Before / after
Saved derived draft: 33,701 samples; minimum radius 2.662324 m; maximum original
nearest-source-sample deviation 0.366617 m; fitted length 1003.275814 m.
Raw: 5,130 observations; 1003.234406572 m over 519.9 seconds; 0 raw vehicle and
0 raw conditioner corner failures. Its minimum observed radius is 4.484842 m.
Fresh old automatic fitting: 0 curvature failures but the same 63 body failures.
Repaired recorded track: 46,616 dense validation samples; 1003.234406572 m;
zero displacement from the corresponding raw polyline and actual conditioner
interpolation. The unchanged ideal rear-axle radius is 4.485012 m; the 0.17 mm
observed numerical difference is inside the pre-existing 0.1% tolerance.

Root cause and exact regions
The two grouped regions are at derived distances 154.792065–155.233890 m
(curvature, control 61) and 158.151079–159.864178 m (vehicle boundary, control 62).
The latter is the front-left production body-envelope sample intersecting the
south boards. Its centerline can look inside while its fitted body heading puts
the corner through the board. The raw pose is clear. This is not a conditioner
collision or a legal service-floor point being rejected as outside the rink.
The selected endpoint X=14.506881, Z=-31.132488 is control 397 / source 5129,
on the service floor at Side=0.264735 / Outward=5.387131 m; its full pose is clear.

The old optimizer scored curvature without body clearance and applied its
12 cm cap relative to each invocation, allowing cumulative drift. The saved
control 62 is 0.366617 m from its source anchor. Shared Bezier handles/headings
couple adjacent curves, so changing/deleting a point can shift failure into its
neighbor. Restore recorded track restores the source spans together instead.
Analysis.txt and Failure-regions.json give source indices, distances, raw and
derived vehicle/conditioner positions and headings, exact contact coordinates,
body-local geometry, normals, deviation and production raw-pose results for
each distinct region. Before-saved-draft.json retains the detailed reproduction.

Use the corrected copy
{guide}
NUMPAD 8 > Refresh if needed > select Andy_Try_1 / recorded track (repaired) >
USE AS GPS. NUMPAD 7 toggles the advisory line. The original generated reference
remains the default for a new Play instance. A new operator route can be loaded
the same way without code modifications.

New raw imports preserve observed position, heading, direction and actual
conditioner motion, using bounded interpolation. Recorded-track GPS READY means
sampled clearance passed; it is not an ideal C2 spline certificate. Unsafe raw
geometry stays blocked. Derived Bezier edits still face the unchanged radius
and clearance checks. No physics, scoring, driving or machinery is operated by
guidance. Existing progression and the 40-foot display framework are preserved.

Editor workflow
Experiments > Ice Resurfacer > Training Route Editor. Open the original raw
recording or the old derived draft. Wheel zooms about the cursor; middle drag
pans. Fit Whole Route resets overview, Zoom to Selected Point frames a control.
Next/Previous Error and Zoom to Error navigate grouped failures; clicking an
error selects it, with raw comparison, reason, radius, deviation and contact.
Selected errors show raw/derived body envelopes and exact collision markers.
Overview markers are thinned; zoom in for detailed points/handles. Raw/guide/
conditioner visibility toggles make coincident paths inspectable. Every layer
and hitbox uses one viewport transform, with clipping and outside-edit protection.
Selecting alone preserves the track. Dragging a point/handle converts only its
adjacent spans to Bezier. Insert/remove/mark and undo/redo remain available.
Restore faithful recorded track is undoable; Save corrected COPY is separate.

Validation
40/40 deterministic simulation scenarios pass. Full Unity 6000.4.1f1 regression
suites pass with zero runtime errors/exceptions, including existing route,
audio, controls, HUD/video, speed setting and scoring checks.
New validation counts: {json.dumps(counts)}.
Actual saved draft reproduces the exact reported counts. All 46,616 reconstructed
vehicle and conditioner positions match their source-interval interpolation
within 0.001 mm. Guide save/reload and Route Manager loading pass. Undo restores
the legacy guide with the original 16 + 63 counts, and redo restores the repair.
Replaying all 5,130 recorded observations completes 1003.234406572 m across 132
contexts without jumping to a later loop. Maximum source-distance lead is
0.201903 m (one raw sampling interval during context transitions); max local
search is 336 samples. Exact replay time is in Unity-real-route-results.txt.
The actual corrected version-2 file also reloads in a fresh Unity process after
Play ended, with identical embedded raw data and validated full length
(Fresh-guide-persistence.txt). The 40-foot look-ahead is checked independently. An intentionally
translated synthetic raw track outside the floor is refused, preserving the
prior valid selection. Unsafe edited raw anchors are also refused.

Real EditorWindow Layout/Repaint tests clamp stale indices -1, 398 and
int.MaxValue before indexing. They exercise empty collections, insertion,
deletion, undo/redo, loading a shorter route, wheel zoom, middle pan, precise
point/handle dragging, outside-viewport protection and error clicks/navigation.
The hidden test host uses genuine Layout/Repaint and injects pointer events
inside OnGUI to bypass Unity's native focus handling for an undisplayed window.
An earlier direct-input harness attempt hit AuxWindowManager::OnGotFocus; it
was isolated to the test process. Final tests do not use that focus path.
Native player screenshots separately verify actual rendered GPS/HUD and input.

Persistence and preservation
All 17 existing raw/backup files and all 23 pre-existing library files retain
their exact starting SHA-256 values. The corrected guide is an additional file.
Raw recording format remains version 1. Derived guide version 2 explicitly
identifies recorded spans so old builds reject, rather than silently misread,
the representation. Current readers retain legacy guide-v1 support. Embedded
raw JSON and checksum remain intact. Raw input is never destructively smoothed.
Vehicle physics, speed calibration/preference code, snow/water/scoring, physical
controls, spatial audio, HUD/video, raw schema and progress tracker files remain
byte-identical. Scene, geometry, materials and shaders remain unchanged.
Only IceResurfacer changed. No branch, commit or push.

Known limits
Recorded interpolation is not C2 smoothing or full dynamic certification. It
cannot recreate unobserved detail between original samples; validation checks
no more than 2.5 cm / .23 degree spacing. Conditioner clearance is a 2D floor
envelope check, separate from the production vehicle collision probe. The real
route passes both. Editable Bezier sections may still need deliberate authoring;
the recovery option restores observed geometry rather than guessing new turns.
Imports/footprint analysis are synchronous and can pause briefly on long takes;
the existing 100,000-guide-sample budget remains. Runtime search/rendering remain
bounded. The automated full-route traversal replays observations through the
tracker; it is not a new manual full driving run or an autopilot demonstration.
Viewport images are exports of the actual shared Unity map renderer at tested
zoom/pan settings; native player images include the normal development watermark.
Hidden-window presentation warnings are retained in Native-results.txt and do
not establish focused-window latency or subjective audio quality.

See Screenshots.html, Analysis.txt, Editor-GUI-results.txt, Unity-real-route-results.txt,
Unity-regression-results.txt, Core-results.txt, Native-results.txt, ModifiedFiles.txt
and Preservation-audit.json.
'''
(folder/'Report.txt').write_text(report,encoding='utf-8')
views=[('Before: full saved draft','Before-overview.png'),('Before: impossible derived curve','Before-curvature-zoom.png'),('Before: derived body intersects the south boards','Before-boundary-zoom.png'),('After: original driven path restored','After-south-board-zoom.png'),('After: full corrected recording','After-overview.png'),('Selected legal service-floor endpoint','After-service-entrance-zoom.png'),('Service gate, room and rink transition','After-service-gate-and-room.png'),('Actual recorded conditioner path, other paths hidden','After-actual-conditioner-path.png'),('Same viewport after zoom and pan','After-zoom-and-pan.png')]
parts=['<!doctype html><meta charset="utf-8"><title>Andy_Try_1 route repair</title>',
 '<style>body{background:#0d1a22;color:#dcece9;font:16px system-ui;max-width:1300px;margin:32px auto}a{color:#8ce6c5}figure{margin:26px 0;padding:16px;background:#1c2b34}img{display:block;max-width:100%;height:auto}figcaption{margin-bottom:12px}nav{position:sticky;top:0;background:#0d1a22;padding:14px}.compare{position:relative;width:min(900px,100%)}.compare img{width:100%}.after{position:absolute;inset:0;clip-path:inset(0 50% 0 0)}</style>',
 '<h1>Andy_Try_1: recorded-route repair</h1><p>Actual Unity authoring viewport exports. Gray: raw; cyan: guide; gold: conditioner; magenta: derived curvature; red: boundary failure and selected body/contact. Green/orange dots: vehicle start/end.</p>',
 '<nav><a href="Report.txt">Results and limitations</a> · <a href="Analysis.txt">Grouped exact diagnostics</a> · <a href="Editor-GUI-results.txt">Editor input regression</a> · <a href="ModifiedFiles.txt">Modified files</a></nav>',
 '<h2>South-board repair comparison</h2><p>Drag the slider: repaired path on the left, saved derived draft on the right. The raw recording is unchanged.</p><div class="compare"><img src="Before-boundary-zoom.png"><img id="after" class="after" src="After-south-board-zoom.png"></div><input style="width:min(900px,100%)" type="range" value="50" oninput="document.getElementById(\'after\').style.clipPath=\'inset(0 \'+(100-this.value)+\'% 0 0)\'">']
for label,name in views:
    assert (folder/name).is_file(),name
    parts.append(f'<figure><figcaption>{html.escape(label)}</figcaption><a href="{name}"><img loading="lazy" src="{name}" alt="{html.escape(label)}"></a></figure>')
for p in sorted((folder/'Player-1366x768').glob('*operator-1003m.png')):
    link=p.relative_to(folder).as_posix();parts.append(f'<h2>Native seated GPS / actual Andy_Try_1</h2><p>Recorded-pose render fixture. The runtime display remains limited to the upcoming section.</p><a href="{link}"><img src="{link}"></a>')
(folder/'Screenshots.html').write_text('\n'.join(parts),encoding='utf-8')
print('Final evidence:',counts)
