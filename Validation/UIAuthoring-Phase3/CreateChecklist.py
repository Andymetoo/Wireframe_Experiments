"""Preserve all 94 historical checks and append explicit, unsigned Phase 3 acceptance."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
DOC = ROOT / 'Assets/Experiments/IceResurfacer/Documentation'
old = (DOC / 'UI-Phases-1-and-2-Master-Acceptance.md').read_text(encoding='utf-8-sig')
assert len(re.findall(r'^\| [A-I]\d\d \|', old, re.M)) == 94
body = old[old.index('Project:'):]
body = body.replace('| PASS/FAIL/BLOCKED |', '| PASS / FAIL / NOT TESTED |')
body = re.sub(r'(?m)^(\| [A-I]\d\d \|.*)\| ____ \| ____ \|$', r'\1| NOT TESTED | ____ |', body)
intro = '''# Aurora R-54 — Phases 1 to 3 master acceptance

This successor preserves all **94 Phase 1/2 checks**, their meaning, historical
automated evidence and outstanding manual sign-off. The original
[Phases 1/2 acceptance record](UI-Phases-1-and-2-Master-Acceptance.md) is unchanged.
Blank historical manual fields become **NOT TESTED** here; prior automated
results do not become manual PASS results. Record PASS / FAIL / NOT TESTED, using
Notes to explain blocked checks. The historical BLOCKED wording below means an
unperformed check, not a passing result.

Phase 3 automated evidence is separately identified after the historical sections.
All operator checks remain unsigned until you perform them in your own Editor.

'''
groups = [
('S. Phase 3 — Surface Survey', [
('S01','Edit Mode: Aurora UI > Surface Survey > Aurora Surface Survey.','Header, coverage/finish, legend, map and patch preview are real saved objects with no running simulator.'),
('S02','Move Map frame and independently resize Live map - resize independently; save/reopen/Play.','Map position/size/anchors persist without moving header or restoring historical IMGUI coordinates.'),
('S03','Make map wide, tall and re-anchored; repeat at 1366 x 768, 1600 x 900, 1920 x 1080.','Correct physical proportions with Preserve Map Aspect; letterbox bars never select an ice cell.'),
('S04','Hover/click known physical locations before/after map movement, scaling and re-anchoring.','Same physical cell is inspected using actual rendered region, event camera and current field dimensions; +Z stays upward.'),
('S05','Hover, click to pin, move away, close patch; inspect several cells.','Actual classification/cut/water/wash/snow/groove values and selected 16-cell patch match the clicked authoritative cell.'),
('S06','Cycle F2 three times while treating the rink.','Overview, shaving mask and material debug modes retain real colors and updates, with appropriate authored legend.'),
('S07','Drive an actual cut/wash/flood/snow pass.','Live texture/coverage/finish update from simulator; no copied ice-state model or per-cell UI objects.'),
('S08','Change panel/border sprite, TMP font/size and legend layout with style opt-outs.','Appearance stays locally authored through save/reopen/Play; meaningful treatment swatch colors remain identifiable.'),
('S09','Enable GPS and inspect machine, conditioner, gate/service outline, turn and cut/water overlays.','Existing route window/footprints and machine orientation project correctly in the moved/resized map.'),
('S10','Collapse, expand and repeat while patch is pinned.','Body/patch visuals and raycasts hide together; expansion uses authored layout and keeps collapse state coherent.'),
('S11','Move survey away from former top-left region and over a physical cockpit control.','Only real authored graphics intentionally block picking; obsolete survey/detail rectangles do not block empty space.'),
('S12','Save visual edits, exit/reopen Unity and enter/exit Play.','Map, fonts, backgrounds and design previews persist; actual data replaces previews only in Play.'),
]),
('G. Phase 3 — GPS status', [
('G01','Edit Mode: Aurora UI > Navigation > Aurora GPS Status.','Selected route, leg, instruction, deviation, progress, warning and buttons are individually editable.'),
('G02','Move/reparent GPS labels inside visibility root, change font/art, save/reopen.','Changes persist without runtime layout restoration or losing explicit bindings.'),
('G03','Activate a compatible guide and drive forward/reverse through turns.','Name, leg/direction, instruction, pace, offset and progress reflect existing navigation.'),
('G04','Use authored show/hide button, Numpad 7 and F5.','All invoke existing GPS visibility; hidden status/Show control remains usable.'),
('G05','Inspect world-space guide from normal/chase views and behind machine/arena geometry.','Approx. 24.384 m / 80 ft lookahead, yellow heading/centerline, depth testing and visibility rules remain unchanged.'),
('G06','Select an incompatible or invalid guide; scroll calibration/status messages.','Strict rejection and complete readable warnings; no route rewrite, altered calibration or duplicated IMGUI status.'),
('G07','Observe GPS during pause/help/on-foot/quicklook/RMB/hide-all transitions.','Existing world visibility and documented widget policy remain coherent, with no unintended pointer capture.'),
('G08','Inspect GPS text and warnings at all three required resolutions.','Labels readable; long warnings scroll within their viewport and do not overlap buttons.'),
]),
('R. Phase 3 — Route Manager and recording', [
('R01','Edit Mode: Aurora UI > Menus > Aurora Route Manager.','Window, tabs, inputs, actions and three labeled sample rows are editable; samples have no storage identity.'),
('R02','Observe route storage hashes before opening/editing previews and before Play.','No design-preview file writes or simulation mutations; Andy_Try_1 and existing guides remain unchanged.'),
('R03','Resize/move manager; alter row prefab, background, font, selection graphic and Content spacing.','Runtime rows inherit saved prefab design; list/metadata remain usable and scrollable.'),
('R04','Open with Numpad 8/F6 or GPS Routes; Refresh files and all tabs.','Saved recordings, calibrated guides, invalid files and recovery drafts appear in the correct sections with stable selection.'),
('R05','Select duplicate-name routes; scroll, refresh/reorder and rename an alias.','Explicit path remains associated with the correct file; visual changes/row reuse never redirect actions.'),
('R06','Activate a compatible validated guide with Use selected as GPS.','Existing strict readiness/compatibility checks succeed and GPS selects the explicit saved file.'),
('R07','Attempt activation of incompatible guide, incomplete draft and malformed file.','Existing errors/interlocks remain; draft/malformed files cannot activate GPS or bypass validation.'),
('R08','Start new recording from manager; drive and stop/save via button and Numpad 9/F7.','Existing recorder captures actual route and publishes one complete new file; starting returns to driving.'),
('R09','Stop without saving; inspect raw samples and save recording/copy.','Recovery/incomplete safeguards retained; sample details correspond to actual selected raw data.'),
('R10','Name next recording, save new copy, rename library alias and refresh.','Correct identity/name/file association; alias rename does not change original trajectory bytes.'),
('R11','Double-click save and press/drag/hide/reopen/release over Save.','No duplicate save or stale canceled click; keyboard and physical commands cannot remain held.'),
('R12','Delete only a disposable selected copy; cancel once, then explicitly confirm.','Confirmation targets captured path/identity; only confirmed selected copy is removed; originals/drafts remain protected.'),
('R13','Load/preview saved raw recording or guide; change sample scrubber and preview layer.','Existing cached previews/data are shown; selecting another file does not leave an unrelated recording displayed.'),
('R14','Guides tab: change layer, leg, rejoin, GPS magenta/amber and opacity.','Existing diagnostics/render controls work; explicit rejoin changes guide cursor without changing physics/route data.'),
('R15','Invoke Edit route in Editor and existing revalidation workflow; cancel without saving.','Existing Editor invocation retained; incompatible source stays unchanged unless user explicitly saves a validated new copy.'),
('R16','Open routes folder on supported desktop platform.','Existing location opens; errors are readable without breaking manager.'),
('R17','Focus name/rename TMP fields, type/edit/select/copy/paste; use Tab/F-keys/numpad.','Text editing/focus works; gameplay shortcuts do not fire while editing. Closing clears field ownership.'),
('R18','Scroll both columns with hardware wheel/drag; close using button/F6; reopen.','Scrollable content works, cursor restores, no stale drag/button action or command survives close.'),
]),
('X. Phase 3 — integration, responsive layout and persistence', [
('X01','Run Phase 1/2 checks A01–I07 above with Phase 3 installed.','All 94 retain their meaning; record new failures separately from historical unsigned manual checks.'),
('X02','Test physical cockpit picking/drag/release and UI map/row/button interaction across panel edges.','One interaction owner; actual authored graphics block acquisition and existing physical drags retain release.'),
('X03','RMB look, focus loss, cursor lock/unlock and on-foot service/boarding.','Existing look/service behavior preserved and restored; UI does not capture world commands.'),
('X04','Pause/help, Tab console compact/expanded, Numpad 1 hide-all, recording and route modal transitions.','Correct visibility/ownership; no duplicate legacy survey/GPS/manager or hidden commands.'),
('X05','1366 x 768: survey/GPS + route list/detail + both console modes.','Correct map aspect, readable text, usable scroll areas and no overlapping controls.'),
('X06','1600 x 900: repeat layout and click-location tests.','Authored anchors, aspect and click regions remain correct.'),
('X07','1920 x 1080: repeat layout and click-location tests.','Authored anchors, aspect and click regions remain correct.'),
('X08','Save/reopen the scene and row prefab; fresh Editor process, then Play.','Appearance/layout bindings persist; preview content returns on exit; runtime rows never become recordings.'),
('X09','Original Scenes/IceResurfacer.unity: startup, drive, F2/F5/F6/F7 and physical controls.','Existing procedural UI/simulation works with no authored UI instantiated; scene bytes unchanged.'),
('X10','Compare protected hashes and authored assets before/after acceptance.','Machine/arena/camera/brush/roller/fill assets, tuning/Core/scoring, original routes/guides and Phase 1/2 visuals unchanged.'),
]),
('P. Phase 3 — performance (record comparable conditions)', [
('P01','Profiler: equivalent stationary and busy cut/wash/flood passes, legacy versus authored versus hidden UI.','Record median/upper frame/main-thread time; no material regression; binding microbenchmarks alone do not certify this.'),
('P02','GC.Alloc/CPU timeline during idle, live map, GPS and scrolling 150+ realistic saved recordings.','No new per-frame row construction or duplicate texture upload; quantify allocations and GC pauses.'),
('P03','UI Profiler: Canvas.BuildBatch, layout/text/graphic rebuilds while stationary and navigating tabs.','Unchanged content does not cause unnecessary rebuilds; changing data/scrolling may legitimately rebuild.'),
('P04','Frame Debugger: batches/draw calls/SetPass and duplicate legacy rendering.','No survey/GPS/route panel rendered behind replacement; record actual rendering cost.'),
('P05','Refresh/scroll 150+ files, including realistic long recordings and guides, repeatedly.','Correct file binding, acceptable refresh/scroll latency and stable pooled row counts; record file sizes/count.'),
('P06','Repeat open/close/refresh 100 times and reset sessions; inspect textures/listeners/GameObject counts.','No accumulating listeners/objects/textures; simulation retains its authoritative streaming-texture update policy.'),
]),
]
append = '''
## Phase 3 — verified automated evidence (separate from manual sign-off)

Run scope: marked isolated Unity 6000.4.1f1 project, separate preferences and
validation-only route storage. See [Phase 3 report](UI-Authoring-Phase3.md) and
project-root `Validation/UIAuthoring-Phase3/Evidence`. Test log lines explicitly
separate injected input/camera captures from native desktop acceptance. Original
Andy guide copies that differ in GuideRollerX are rejected without rewriting.

| Automated action/location | Expected / measured result | Status | Notes |
| --- | --- | --- | --- |
| Current Core regression suite | All 43 existing deterministic scenarios pass. | PASS | Core-regression.txt |
| Unity authoring/save/reopen/Play | Moved/resized map, moved GPS, narrowed manager, local TMP/background retained. | PASS | Phase3-results.txt |
| Survey picking / patch / F2 | Actual UI clicks map to correct cells after relocation; bars excluded, pin/collapse/modes/live textures work. | PASS | Phase3-results.txt |
| GPS and existing compatibility rules | Live status, visibility shortcuts/world window and incompatible-guide rejection retained. | PASS | Phase3-results.txt |
| Recording/library list | Start/stop/save, duplicate suppression, stable refresh/scroll selection and 150+ pooled rows. | PASS | Phase3-results.txt |
| Guide activation / copies / confirmation / input safety | Strict rejection, successful test-guide activation, alias/copy/delete actions, real wheel events and stale-press cancellation pass. | PASS | Phase3-results.txt; native hardware remains manual. |
| Fresh Editor process / exact delivered scene | Reopened saved edits and played exact integrated bytes. | PASS | Phase3-results.txt and successful final Unity logs. |
| Three-resolution camera captures | Twelve captures: edited and delivered survey/GPS and manager at required sizes. | PASS | Captures exclude remaining IMGUI composition. |
| Procedural launch + Phase 1/2 smoke | Procedural simulation/keyboard and both previous module registrations/commands retained. | PASS | Full hardware acceptance above remains unsigned. |
| Binding microbenchmarks | Record per-refresh binding timings, row/file counts and scope. | PASS | Excludes whole-frame rendering/allocation costs. |
| Whole-frame/native input acceptance | Operator Profiler, Frame Debugger, hardware and visible Editor checks below. | NOT TESTED | Do not infer PASS from camera/injected tests. |

The following checks are **manual acceptance**. Every row begins NOT TESTED.
Record notes, screenshots/log paths, input devices and resolution for each result.
'''
for heading, rows in groups:
    append += f'\n## {heading}\n\n| ID | Unity location/action | Expected result | PASS / FAIL / NOT TESTED | Notes |\n| --- | --- | --- | --- | --- |\n'
    for key, action, expected in rows:
        append += f'| P3-{key} | {action} | {expected} | NOT TESTED | ____ |\n'

target = DOC / 'UI-Phases-1-to-3-Master-Acceptance.md'
target.write_text(intro + body + append, encoding='utf-8')
assert len(re.findall(r'^\| [A-I]\d\d \|', target.read_text(), re.M)) == 94
print(f'Preserved 94 historical checks; added {sum(len(r) for _, r in groups)} Phase 3 manual checks.')
