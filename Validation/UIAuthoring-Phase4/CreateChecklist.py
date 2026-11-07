from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
DOC = ROOT / 'Assets/Experiments/IceResurfacer/Documentation'
previous = (DOC / 'UI-Phases-1-to-3-Master-Acceptance.md').read_text(encoding='utf-8-sig')
prefix = '''# Aurora R-54 — Phases 1 to 4 master acceptance

This checklist preserves the previous **148 checks verbatim**, including their
historical evidence and pending manual statuses. Phase 4 operator checks below
also start **NOT TESTED**. Automated injected-input tests do not sign off native
mouse, keyboard, cursor, focus, accessibility, readability or full-frame profiling.
Use PASS / FAIL / NOT TESTED and add dated notes, resolution and evidence.

## Priority smoke test for the next Unity session

1. Open `Scenes/IceResurfacer_AuthoredMachine.unity`. Select
   `Aurora UI / Menus / Aurora Session Screens`. Use its Inspector preview buttons
   to inspect Welcome, Pause, Help and Results, then Restore Saved Presentation.
   Confirm the badges say sample data. No session should start in Edit Mode.
2. In a scene copy, move a welcome button; change its sprite and TMP appearance.
   Save, reopen, and enter Play. Verify its location and correct session action.
3. Start practice; expand machinery with Tab and hold a hydraulic lever. Open
   Pause with Escape while held. Confirm the command releases, mouse unlocks,
   and machine controls cannot respond through the menu.
4. Set 6.25 mph. Open help with F1; close it and verify Pause remains. Resume.
   Restart Play and then Unity; verify 6.25 mph persists. Restore your own setting.
5. Verify scored / practice / assisted training initialize distinct existing modes.
   Finish a disposable practice run and inspect the unscored report. Test both
   new-session buttons and help's return to Results.
6. Check all four screens at 1366×768, 1600×900 and 1920×1080. Scroll the entire
   help document. Check native Tab, Shift+Tab, arrows, Enter and gamepad if used.
7. Resume and check live HUD, diagnostics, machinery, map picking, GPS and Route
   Manager. Use disposable route files only. The incompatible Andy guide must
   remain rejected. Run the procedural scene's legacy menus separately.

Smoke status: NOT TESTED  Tester/date: __________  Notes: __________

The following retained record is historical. Its headings and instructions belong
to Phases 1–3; the Phase 4 preview workflow above supersedes only menu authoring.

---

'''

groups = [
('W', 'Welcome and session selection', [
('Open the authored scene outside Play.', 'Four persistent screen roots exist under the existing Menus group; welcome is editable.'),
('Start scored from Welcome.', 'New scored FreeSimulation starts; no practice or training flag.'),
('Start practice from Welcome.', 'Unscored FreeSimulation starts; practice never earns a score.'),
('Start Assisted training.', 'Existing GuidedTraining starts as practice with its original equipment and finish interlocks.'),
('Open and close help from Welcome with its button and F1.', 'Returns to Welcome without starting or replacing a session.'),
('Rearrange the four welcome buttons into a different layout in a scene copy.', 'Their explicit actions remain correct regardless of sibling order or position.'),
('Use the optional logo, subtitle, introduction and secondary labels independently.', 'Text and graphics can be hidden, moved and replaced without requiring a logo.'),
('Double-click a start button and press Submit rapidly.', 'One action per frame; hidden-screen callbacks cannot replace the newly started session.'),
]),
('S', 'Pause and settings', [
('Open Pause with Escape while driving; resume by button and Escape.', 'Existing Paused state and cursor behavior are preserved; gameplay resumes correctly.'),
('Choose New session from Pause.', 'Returns to the original welcome/session selection workflow.'),
('Choose Refill practice.', 'Starts existing unscored service practice with partly empty tanks.'),
('Drag forward speed to both endpoints and quarter steps.', '3–8 mph inclusive, snapped to 0.25 mph; displayed and authoritative values agree.'),
('Set 6.25 mph, start another session, restart Play and restart Unity.', 'Selection persists through the existing PlayerPrefs key; original calibration is unchanged.'),
('Inspect all pause controls.', 'No RESET TO ORIGINAL SPEED button; all existing explanatory labels remain available.'),
('Move seated-eye slider through -8 to +12 cm in the authored scene.', 'Fallback value changes; authored Driver Eye Anchor is not moved or offset a second time.'),
('Use the eye adjustment in an existing fallback camera setup without an eye anchor.', 'Existing fallback offset applies once, with no camera drift.'),
('Replace slider track, thumb, fill and value formatting in a scene copy.', 'Appearance changes persist; range, precision, saved value and bindings remain correct.'),
('Hold hydraulics or drag a valve when opening Pause.', 'Transient command and drag release immediately; no held command resumes on close.'),
]),
('H', 'Operator guide', [
('Read all six sections against actual controls.', 'Prepare, return, keyboard, camera, machine load and on-foot service descriptions remain accurate.'),
('Open F1 during gameplay and close it.', 'Returns to running gameplay with the previous session intact.'),
('Open F1 or the guide button from Pause; close help.', 'Returns to Pause without accidentally resuming.'),
('Open help from Results; close with F1 and the button.', 'Returns to the same completed report.'),
('Scroll from top to bottom using wheel and scrollbar at 1366×768.', 'All instructions remain readable and reachable; no microscopic text or clipped final section.'),
('Edit section text, font, spacing and headings in a scene copy.', 'Content height follows text; editable layout groups allow independent section rearrangement.'),
('Enable the optional diagram and assign custom artwork.', 'Diagram participates in scrolling and does not capture clicks unnecessarily.'),
]),
('R', 'Results and scoring', [
('Finish a scored partial run.', 'Actual RunReport score, partial status and 95% completion explanation display.'),
('Complete a scored run through the existing full resurfacing and return workflow.', 'Actual Complete report and all scoring remain consistent with the simulator.'),
('Finish a practice run.', 'Practice report is explicitly unscored; score remains zero and scoring point columns are blank.'),
('Inspect elapsed time, coverage, shaving, water, finish, missed and excess values.', 'Every value agrees with the actual completed report; no design samples remain in Play.'),
('Inspect water efficiency, dumps/safe return and misuse penalties.', 'Existing operating points and penalties agree with RunReport.'),
('Compare selected and original forward-speed values.', 'Separate values come from the completed report, preserving original calibration.'),
('Reorder rows and resize/style the score in a scene copy.', 'Explicit metric kinds preserve data identity independent of layout.'),
('Use Results New scored, New practice and help.', 'Each uses the existing lifecycle; help returns to Results.'),
('Leave an unchanged completed report open.', 'No repeated prefab creation or label rebuild; changes update only when report/state changes.'),
]),
('I', 'Keyboard, modal and native input', [
('Navigate menus with native Tab and Shift+Tab.', 'Focus cycles through the authored navigation list; Tab does not toggle hidden machinery.'),
('Use arrows and Enter on buttons and settings.', 'Existing Input System navigation/Submit works once per action; settings preserve constraints.'),
('Use native gamepad navigation if applicable.', 'Menu selection and Submit work; record device and any unsupported behavior.'),
('Click machinery, diagnostics, map and GPS locations through each modal.', 'Modal owns the pointer; underlying interfaces receive no command.'),
('Close each screen and click those same locations.', 'No invisible full-screen raycast blocker remains.'),
('Press a menu button, hide/reopen its screen before release, then release.', 'Canceled old press cannot fire; a fresh press works.'),
('Alt-tab while holding a button, machinery lever or settings drag.', 'Focus loss cancels commands and drags; returning does not dispatch a stale action.'),
('Resume after RMB look, on-foot interaction and quicklook states.', 'Native cursor locking, unlocking and focus match existing game behavior.'),
('Open Route Manager, Pause and Help using existing shortcuts in different orders.', 'Existing modal priority is preserved; only the appropriate Phase 4 screen appears.'),
('Use Numpad 1 hide-all, then F1/Escape, and restore the instruments.', 'Existing hide-all and menu state behavior remain consistent without duplicate IMGUI screens.'),
]),
('A', 'Authoring, themes, preview and resolutions', [
('Preview Welcome, Pause, Help and Results outside Play.', 'Exactly the requested screen previews; results/settings show clearly labeled sample data.'),
('Preview screens then Restore Saved Presentation.', 'Every prior CanvasGroup visibility/input value returns; no session, preference or route changes.'),
('Preview, then save, enter Play, reload scripts, close scene or quit.', 'Preview restores safely before lifecycle operations; unrelated screens are not left disabled.'),
('Move, resize, anchor and rotate a startup button; save/reopen/restart Unity and Play.', 'All visual overrides persist without runtime position resets; correct action still fires.'),
('Replace a full-screen background with an imported PNG.', 'Sprite, scaling and tint persist; it fills the intended screen and blocks gameplay while modal.'),
('Replace button sprites and a nine-sliced panel; move captions separately.', 'Custom art and independent TMP positions survive without C# changes.'),
('Replace TMP fonts globally through the existing theme and locally with Apply Font disabled.', 'Shared changes reach four phases; intentional local overrides remain intact.'),
('Inspect all four screens at 1366×768.', 'Readable text, reachable actions, scrollable help, correct backdrop and no unintended clipping/overlap.'),
('Inspect all four screens at 1600×900.', 'Readable text, reachable actions, scrollable help, correct backdrop and no unintended clipping/overlap.'),
('Inspect all four screens at 1920×1080.', 'Readable text, reachable actions, scrollable help, correct backdrop and no unintended clipping/overlap.'),
('Create a menu button/panel prefab variant and replace decorative art.', 'Variants reuse the existing design system; only required graphics raycast.'),
]),
('X', 'Earlier phases and fallback integration', [
('Resume and inspect all eight HUD widgets, prior overrides and diagnostics.', 'Live values, existing customizations and diagnostic interaction remain functional.'),
('Toggle compact/expanded console; use conditioner and tank hydraulics.', 'Earlier layouts and spring-centered commands retain behavior.'),
('Use flood/wash valves and blade through UI, keyboard and diegetic controls.', 'Values synchronize through existing simulation controls with no stale drag.'),
('Hover known map cells and toggle treatment/overview with F2.', 'Surface Survey still identifies the correct cell and textures after closing menus.'),
('Toggle GPS and inspect route, leg and guidance labels.', 'Existing status and guide visibility remain correct.'),
('Open Route Manager; scroll dynamic rows and select a disposable guide.', 'Rows retain explicit paths, scrolling, selection and existing actions.'),
('Record/save a disposable route and select a compatible guide.', 'Recording and guide activation remain functional; original recordings are untouched.'),
('Select the previously incompatible Andy guide.', 'Strict compatibility still rejects GuideRollerX mismatch without rewriting the guide.'),
('Open Scenes/IceResurfacer.unity and test welcome, pause, help, results and machinery.', 'Procedural scene retains its functioning legacy UI with no authored menu dependency.'),
]),
('P', 'Performance and stability', [
('Inspect active Canvas/EventSystem/input modules and repeated modal transitions.', 'One existing Canvas and EventSystem; no duplicate screens, modules or accumulated listeners.'),
('Profile representative gameplay before/after Phase 4.', 'Record CPU/GPU frame time, GC allocation, draw calls and Canvas rebuilds on the same hardware/scene.'),
('Profile inactive menus and an unchanged Results screen.', 'Inactive labels do not update continuously; results update only on changed report or entry.'),
('Exit Play after edited-screen tests and compare saved assets.', 'No live sample/result data or simulation state writes back into authored assets.'),
('Run the deterministic simulation regression.', 'All 43 existing scenarios pass; no scoring, physics, tuning or threshold changes.'),
('Compare backup hashes and protected route files.', 'All earlier UI, machine, arena and calibration assets remain unchanged; routes remain byte-identical.'),
]),
]
body = '\n\n---\n\n## Phase 4 operator acceptance\n\nAll statuses below are manual sign-off, separate from the automated evidence in\n[UI-Authoring-Phase4.md](UI-Authoring-Phase4.md). Training selection remains on Welcome.\n'
count = 0
for code, title, rows in groups:
    body += f'\n### {title}\n\n| ID | Action | Expected result | Status | Notes / tester / evidence |\n| --- | --- | --- | --- | --- |\n'
    for index, (action, expected) in enumerate(rows, 1):
        body += f'| P4-{code}{index:02} | {action} | {expected} | NOT TESTED | ____ |\n'
        count += 1
body += f'\nPreserved checks: **148**. Added Phase 4 checks: **{count}**. Total: **{148 + count}**.\n'
result = prefix + previous + body
old_rows = re.findall(r'^\| (?:[A-I]\d\d|P3-[SGRXP]\d\d) \|.*$', previous, re.M)
assert len(old_rows) == 148 and all(row in result for row in old_rows)
(DOC / 'UI-Phases-1-to-4-Master-Acceptance.md').write_text(result, encoding='utf-8')
print(f'Preserved 148 exact rows; added {count} manual checks; total {148 + count}.')
