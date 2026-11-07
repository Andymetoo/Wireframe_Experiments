# Aurora R-54 — Phases 1 to 5 master UI acceptance

The prior **218 detailed checks are retained verbatim**, reorganized below. Their
manual statuses and notes are unchanged. New Phase 5 manual checks start NOT TESTED.
Automated evidence is separate; injected input/rendering does not sign native
hardware, cursor, final artwork or full-frame profiling.

Project: `G:\UnityProjects\Wireframe_Experiments`, Unity 6000.4.1f1. Paths are
relative to `Assets/Experiments/IceResurfacer`. Use a scene copy for deliberate edits,
new disposable recordings for storage actions, and preserve prefab connections.
Tester/date: __________ Device/DPI/resolution: __________ Revision: __________

**Updated focus policy:** clicking another Editor panel or alt-tabbing must not
pause Aurora or open its menu. Background simulation continues; held input and
mouse look release. This supersedes D06's preserved historical auto-pause/Resume
expectation below; its command-release and fresh-click checks still apply.

## MASTER: TEST THIS NOW

Twenty quick smoke checks; detailed canonical checks follow. Record actual results.

| Ref | Exact action | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |
| QUICK-01 | Open authored scene and enter/exit Play. | No missing bindings, duplicate UI or new exceptions. | NOT TESTED | ____ |
| QUICK-02 | Preview Phase 4 screens and Phase 5 service/history/warnings; Restore. | Only requested samples appear; prior presentation restored. | NOT TESTED | ____ |
| QUICK-03 | Start scored, practice and assisted training. | Original mode/scoring distinctions. | NOT TESTED | ____ |
| QUICK-04 | Use HUD meters and Machine Load/F3. | Live values and diagnostics work. | NOT TESTED | ____ |
| QUICK-05 | Toggle compact/expanded with Tab; hold/release both hydraulics. | Correct command directions and neutral release. | NOT TESTED | ____ |
| QUICK-06 | Adjust flood, wash and blade using UI then keyboard. | Same authoritative controls and synchronized readings. | NOT TESTED | ____ |
| QUICK-07 | Move UI over a cockpit grip, then away, in a scene copy. | Only current visible graphics capture. | NOT TESTED | ____ |
| QUICK-08 | Pick a known map cell and toggle F2. | Correct cell and existing textures. | NOT TESTED | ____ |
| QUICK-09 | Toggle GPS and select a disposable compatible guide. | Correct route/leg; strict compatibility retained. | NOT TESTED | ____ |
| QUICK-10 | Open Route Manager and record/save a disposable take. | Rows/actions work; originals untouched. | NOT TESTED | ____ |
| QUICK-11 | Open Pause during a held command or drag. | Transient commands release; no click-through. | NOT TESTED | ____ |
| QUICK-12 | Set quarter-mph speed; F1 from Pause, close, Resume. | Setting persists; help returns to Pause. | NOT TESTED | ____ |
| QUICK-13 | Finish practice and inspect Results. | Unscored report and new-session/help actions correct. | NOT TESTED | ____ |
| QUICK-14 | F dismount; mouse/WASD; E take hose. | Grounded movement and actual-target prompt. | NOT TESTED | ____ |
| QUICK-15 | Connect flood then wash and operate supply. | Separate actual fills and updated service readouts. | NOT TESTED | ____ |
| QUICK-16 | Observe overflow, close supply, disconnect and return hose; F board. | Warning/effects and normal recovery intact. | NOT TESTED | ____ |
| QUICK-17 | Move service panel over holder then away; test clicks and E. | Real bounds block; no stale y>700 hit band. | NOT TESTED | ____ |
| QUICK-18 | Trigger warning; routine messages off; open/scroll/close history. | Critical survives; timing, order and 32-entry limit retained. | NOT TESTED | ____ |
| QUICK-19 | Numpad 1 hide/restore; alt-tab during gestures and RMB/on-foot look. | No invisible blocker/stuck command; native cursor recorded. | NOT TESTED | ____ |
| QUICK-20 | Save visual edits, restart Unity and test original procedural scene separately. | Edits persist; fallback remains playable. | NOT TESTED | ____ |

## Automated evidence and prior records

Phase 5 results and limitations: [implementation report](UI-Authoring-Phase5.md).
Practical editing locations: [authoring handbook](Aurora-UI-Authoring-Handbook.md).
The unchanged [Phase 1–4 record](UI-Phases-1-to-4-Master-Acceptance.md) retains
its historical evidence and section context; the exact prior evidence excerpts
are retained at the end of this document. Automated PASS is not manual PASS.

## 1. Project startup and asset integrity

| ID | Exact action / Unity location | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |
| A01 | Open primary scene; wait for import/compile; clear Console, enter and exit Play once. | No new compile errors or runtime exceptions. Record pre-existing warnings separately. | NOT TESTED | ____ |
| A02 | Before Play, expand `Aurora UI` and the hierarchy above. | Eight HUD widgets, both console layouts, confirmation and one UI Event System already exist as saved objects. No runtime-created replacement console. | NOT TESTED | ____ |
| A03 | Select both presenters and each console control; inspect Game/Canvas/raycaster/groups/widget lists. | Scene presenters reference the existing game and Canvas; controls reference their console owner. No missing list entries or Missing Script components. Reusable prefab assets may intentionally leave scene Game/Canvas empty. | NOT TESTED | ____ |
| A04 | Inspect UI Images/TMP labels in Scene view; enter Play and expand console. | No missing sprites, pink materials, empty glyph boxes, UNAVAILABLE readings or binding errors. Optional unassigned art fields are valid if the local fallback is intentional. | NOT TESTED | ____ |
| A05 | Frame the saved machine and arena before Play; compare with your reference or backup. | Existing machine, rink, camera anchors, brush rig, guide roller, fill ports and authored materials/geometry remain intact. Do not regenerate to troubleshoot UI. | NOT TESTED | ____ |
| A06 | Enter Play, remain at startup, open/close Operator's guide, then start practice. | Startup/help owns the screen; live HUD/console appears only after starting. Preview badges disappear in Play. | NOT TESTED | ____ |
| A07 | In Play Hierarchy search `t:EventSystem`; inspect its input module. | One active EventSystem with InputSystemUIInputModule; no StandaloneInputModule competitor. Driving keys do not navigate selected UI buttons. | NOT TESTED | ____ |
| A08 | Outside Play, inspect scene dirty indicator before/after selecting controls and opening Prefab Mode. | Passive inspection does not mutate the scene/prefab or write simulation state. Explicit Preview/Apply Theme operations may correctly dirty their target. | NOT TESTED | ____ |
| P4-P01 | Inspect active Canvas/EventSystem/input modules and repeated modal transitions. | One existing Canvas and EventSystem; no duplicate screens, modules or accumulated listeners. | NOT TESTED | ____ |
| P4-P06 | Compare backup hashes and protected route files. | All earlier UI, machine, arena and calibration assets remain unchanged; routes remain byte-identical. | NOT TESTED | ____ |
| P5-01-01 | Open the authored scene; inspect Aurora UI/Messages/Aurora Operator Interface. | One saved Phase 5 assembly, no missing references; machine, arena, anchors and earlier UI match the verified baseline. | NOT TESTED | ____ |
| P5-01-02 | Inspect UI Event System and Console after Play. | Exactly one EventSystem/InputSystemUIInputModule and existing Canvas; no unexpected new exceptions. | NOT TESTED | ____ |

## 2. Driving HUD

| ID | Exact action / Unity location | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |
| B01 | Start practice; inspect the top HUD. | Identity/time, speed, load, flood, wash, snow, conditioner and blade are all present once. No old header text is visible behind them. | NOT TESTED | ____ |
| B02 | Drive forward/backward briefly and stop; watch Identity and Speed. | Time advances during running; speed/direction follows the real vehicle, and returns near zero when stopped. | NOT TESTED | ____ |
| B03 | Toggle augers with `2/3`; use blade/water later on ice. | Machine Load responds to actual work. Click its widget, then `F3`; existing diagnostics opens/closes through both paths. | NOT TESTED | ____ |
| B04 | Use flood and wash separately during a pass; compare their HUD bars/liters. | Each reservoir's true remaining amount changes independently. Console valve percentages are settings, not tank fill percentages. | NOT TESTED | ____ |
| B05 | Cut snow and return to dump; inspect Snow Tank. | Snow volume and percentage follow actual collection/unloading, not the preview sample. | NOT TESTED | ____ |
| B06 | Partially raise/lower conditioner; alter blade set depth. | Conditioner position/status and blade SET/CONTACT match machinery. CONTACT may be zero with a nonzero SET when off ice. | NOT TESTED | ____ |
| B07 | In test scene Edit Mode, move Flood Water to free space and resize it; save, Play, then reopen. | Authored position/size/anchors persist; live text and fill remain functional. Old location has no residual header hit region (also test D/E). | NOT TESTED | ____ |
| B08 | Disable one widget style's shared font/color/sprite channel; edit its TMP font/size and Image; save and Play. | Local override survives. Other opted-in widgets still use shared theme. No automatic transform/font-size reset from a disabled channel. | NOT TESTED | ____ |
| B09 | Press Numpad 1 twice, then test Esc and F1 open/close. | HUD hides/restores; menus own input. No live header behind modal screens. Numpad 1 is keypad 1, not the conditioner shortcut. | NOT TESTED | ____ |
| B10 | Stop safely, `F` off machine, then board. | HUD remains visible but read-only on foot; service input retains control. Seated diagnostics interaction returns after boarding. | NOT TESTED | ____ |
| P4-X01 | Resume and inspect all eight HUD widgets, prior overrides and diagnostics. | Live values, existing customizations and diagnostic interaction remain functional. | NOT TESTED | ____ |
| P5-02-01 | Start practice, dismount and board again; inspect all eight HUD values. | Readings remain live with existing font/layout overrides; no legacy duplicate header. | NOT TESTED | ____ |
| P5-02-02 | Click Machine Load before and after opening message history. | Existing F3 diagnostic action remains functional and separate from ordinary UI. | NOT TESTED | ____ |

## 3. Machinery console

| ID | Exact action / Unity location | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |
| C01 | Start practice; inspect compact panel, click Open, click Close, repeat with Tab. | Compact summary and expanded controls alternate; only the current group receives input. Machinery settings persist. No new objects accumulate. | NOT TESTED | ____ |
| C02 | In Edit Mode, preview expanded; move conditioner lever to another clear location; save and Play. | Actual hit area follows it. Top/away commands RAISE; bottom/toward commands LOWER. Original position no longer operates the moved lever. | NOT TESTED | ____ |
| C03 | Hold the conditioner lever top/bottom, then release midway through travel. | Real conditioner moves in the requested direction. Command becomes neutral and mechanism holds attained position; grip springs to neutral. | NOT TESTED | ____ |
| C04 | Hold conditioner, move pointer outside its interaction area while still held, then reenter. | Command releases on exit. Reentering does not restart until a fresh press. Do not confuse physical settling with a continuing command. | NOT TESTED | ____ |
| C05 | Test tank lever top/bottom/release/exit, initially parked safely. | Same direction and neutral-hold semantics; actual tank moves when simulation permits. Training may reject an unsafe dump position with an existing message. | NOT TESTED | ____ |
| C06 | For each hydraulic lever hold it, then separately open Esc, F1, routes (F6), Numpad 1 hide, Tab, and camera quick look; resume each time. | Every transition releases the UI gesture. Returning does not silently resume it. Maintained settings stay as set unless the existing session reset changes them. | NOT TESTED | ____ |
| C07 | While holding conditioner UI, press/hold `1` or Right Shift+1; release UI while retaining key, then release key. Repeat tank with `7`. | Keyboard command takes ownership and survives UI cancellation. Releasing the key neutralizes it. UI must not reassert stale demand. | NOT TESTED | ____ |
| C08 | Click V/H augers individually, then use `3/2`, then physical switches. | Maintained ON/OFF state and actual auger motion agree in all three paths. Releasing mouse does not switch an auger off. | NOT TESTED | ____ |
| C09 | Click Brush/Pump; repeat with `4/5` and physical switches. | Each command toggles once and remains set. UI reflects authoritative state; brush moves, pump follows real prime/water rules. | NOT TESTED | ____ |
| C10 | Drag Flood through roughly a quarter turn CCW to open/CW to close; then test Wash. | Each spans 0–100%, clamps at bounds and remains independent. Mouse release holds valve setting; motion wholly inside the center dead zone does not change the setting. | NOT TESTED | ____ |
| C11 | Scroll over each valve, use bracket shortcuts, then operate physical handles. | Correct valve updates and UI/physical angle agrees; Right Shift targets wash. Scroll over unrelated empty space does not change it. Record native notch/trackpad behavior. | NOT TESTED | ____ |
| C12 | Drag Blade clockwise deeper/counterclockwise shallower through several revolutions, lifting/repressing as needed inside the wheel. | Full calibrated range spans 3.5 turns by default; depth clamps between zero and configured maximum. No one-turn wrap/reset. | NOT TESTED | ____ |
| C13 | Scroll over Blade; use comma/period; operate physical wheel. | All adjust the same continuous set depth. UI wheel/depth readout, physical wheel and HUD agree. CONTACT/engagement follows actual cutting geometry. Numeric turns can be shown in an optional Detail label. | NOT TESTED | ____ |
| C14 | Click Snow Breaker, release, click again; compare `6` and physical plunger. | One stroke per accepted activation; existing stroke cooldown/load effects remain. It is not a maintained ON/OFF switch. | NOT TESTED | ____ |
| C15 | Click Refill help and inspect notice. | Existing on-foot/hose instructions appear. It does not instantly fill tanks or change service calibration. | NOT TESTED | ____ |
| C16 | Start a valve/lever drag, move over Finish/New Session, then release. | Neither terminal action fires: press ownership stays with the original control. Repeat beginning in empty space. | NOT TESTED | ____ |
| C17 | Deliberately click New Session, click Cancel, then repeat and Confirm. | Authored confirmation blocks other pointer controls. Cancel preserves run; Confirm calls existing reset and returns to startup. No confirmation remains afterward. | NOT TESTED | ____ |
| C18 | While New Session confirmation is open, click HUD Load/world/legacy controls, then open Esc/F1 or lose focus. | No background pointer activation; transition cancels confirmation. Normal UI is usable afterward. Confirmation itself does not pause the simulation. | NOT TESTED | ____ |
| C19 | In a disposable free practice run, deliberately click Finish run. | Existing free-mode finish opens results immediately. HUD/console hides; startup/results actions still work. Default Finish has no extra confirmation. | NOT TESTED | ____ |
| C20 | Start Assisted training and click Finish before entering rink; later finish after completing its required safe return. | Early finish is rejected with training reason. Valid finish shows report. No bypass of simulator safeguards. | NOT TESTED | ____ |
| C21 | Outside Play, independently resize Flood valve, move a maintained toggle, edit a sprite and TMP style; save, Play and operate all changed controls. | Graphics/hit regions/labels remain authored, functioning and synchronized. Moving expanded controls does not rewrite compact layout. | NOT TESTED | ____ |
| C22 | Make a variant of a control prefab, replace one existing control in the test scene; assign Owner and exactly one Controls entry. | One activation produces one command. Standard Button/Toggle event lists have no manually duplicated gameplay callback. Remove/disable the replaced target. | NOT TESTED | ____ |
| C23 | Test optional `Aurora Bound Slider` in the test scene, bound to Flood/Wash/Blade, replacing its corresponding control. | Normalized slider maps to the existing range, tracks external changes, and stops drag on hide/focus/exit. No second active overlapping target. | NOT TESTED | ____ |
| C24 | In Edit Mode preview both layouts; inspect representative states, then Play and stop. | Partial conditioner, lowered tank, one auger on, partial water, blade depth and brush/pump ON are text/indicator samples only. Mechanism art retains its authored rest pose. Live startup replaces samples; stop restores saved preview. | NOT TESTED | ____ |
| P4-X02 | Toggle compact/expanded console; use conditioner and tank hydraulics. | Earlier layouts and spring-centered commands retain behavior. | NOT TESTED | ____ |
| P4-X03 | Use flood/wash valves and blade through UI, keyboard and diegetic controls. | Values synchronize through existing simulation controls with no stale drag. | NOT TESTED | ____ |
| P5-03-01 | Hold conditioner/tank hydraulics and open Pause, help, Route Manager or dismount. | Momentary ownership releases with no stuck demand; settings remain authoritative. | NOT TESTED | ____ |
| P5-03-02 | Use compact/expanded controls while an ordinary notice or critical warning is visible. | Only the actual authored graphics block pointer acquisition; no duplicate console. | NOT TESTED | ____ |
| P5-03-03 | Adjust flood, wash and blade using UI, keyboard and real cockpit controls. | Directions and live values synchronize without altering calibration. | NOT TESTED | ____ |

## 4. Surface Survey and minimap

| ID | Exact action / Unity location | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |
| P3-S01 | Edit Mode: Aurora UI > Surface Survey > Aurora Surface Survey. | Header, coverage/finish, legend, map and patch preview are real saved objects with no running simulator. | NOT TESTED | ____ |
| P3-S02 | Move Map frame and independently resize Live map - resize independently; save/reopen/Play. | Map position/size/anchors persist without moving header or restoring historical IMGUI coordinates. | NOT TESTED | ____ |
| P3-S03 | Make map wide, tall and re-anchored; repeat at 1366 x 768, 1600 x 900, 1920 x 1080. | Correct physical proportions with Preserve Map Aspect; letterbox bars never select an ice cell. | NOT TESTED | ____ |
| P3-S04 | Hover/click known physical locations before/after map movement, scaling and re-anchoring. | Same physical cell is inspected using actual rendered region, event camera and current field dimensions; +Z stays upward. | NOT TESTED | ____ |
| P3-S05 | Hover, click to pin, move away, close patch; inspect several cells. | Actual classification/cut/water/wash/snow/groove values and selected 16-cell patch match the clicked authoritative cell. | NOT TESTED | ____ |
| P3-S06 | Cycle F2 three times while treating the rink. | Overview, shaving mask and material debug modes retain real colors and updates, with appropriate authored legend. | NOT TESTED | ____ |
| P3-S07 | Drive an actual cut/wash/flood/snow pass. | Live texture/coverage/finish update from simulator; no copied ice-state model or per-cell UI objects. | NOT TESTED | ____ |
| P3-S08 | Change panel/border sprite, TMP font/size and legend layout with style opt-outs. | Appearance stays locally authored through save/reopen/Play; meaningful treatment swatch colors remain identifiable. | NOT TESTED | ____ |
| P3-S09 | Enable GPS and inspect machine, conditioner, gate/service outline, turn and cut/water overlays. | Existing route window/footprints and machine orientation project correctly in the moved/resized map. | NOT TESTED | ____ |
| P3-S10 | Collapse, expand and repeat while patch is pinned. | Body/patch visuals and raycasts hide together; expansion uses authored layout and keeps collapse state coherent. | NOT TESTED | ____ |
| P3-S11 | Move survey away from former top-left region and over a physical cockpit control. | Only real authored graphics intentionally block picking; obsolete survey/detail rectangles do not block empty space. | NOT TESTED | ____ |
| P3-S12 | Save visual edits, exit/reopen Unity and enter/exit Play. | Map, fonts, backgrounds and design previews persist; actual data replaces previews only in Play. | NOT TESTED | ____ |
| P4-X04 | Hover known map cells and toggle treatment/overview with F2. | Surface Survey still identifies the correct cell and textures after closing menus. | NOT TESTED | ____ |
| P5-04-01 | Pick known map cells after service, history and modal transitions. | Actual selected cell and treatment texture remain correct. | NOT TESTED | ____ |
| P5-04-02 | Move the map in a scene copy, then test both its old and new positions. | Only the current graphic bounds capture; no obsolete legacy map hitbox. | NOT TESTED | ____ |

## 5. GPS and Route Manager

| ID | Exact action / Unity location | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |
| P3-G01 | Edit Mode: Aurora UI > Navigation > Aurora GPS Status. | Selected route, leg, instruction, deviation, progress, warning and buttons are individually editable. | NOT TESTED | ____ |
| P3-G02 | Move/reparent GPS labels inside visibility root, change font/art, save/reopen. | Changes persist without runtime layout restoration or losing explicit bindings. | NOT TESTED | ____ |
| P3-G03 | Activate a compatible guide and drive forward/reverse through turns. | Name, leg/direction, instruction, pace, offset and progress reflect existing navigation. | NOT TESTED | ____ |
| P3-G04 | Use authored show/hide button, Numpad 7 and F5. | All invoke existing GPS visibility; hidden status/Show control remains usable. | NOT TESTED | ____ |
| P3-G05 | Inspect world-space guide from normal/chase views and behind machine/arena geometry. | Approx. 24.384 m / 80 ft lookahead, yellow heading/centerline, depth testing and visibility rules remain unchanged. | NOT TESTED | ____ |
| P3-G06 | Select an incompatible or invalid guide; scroll calibration/status messages. | Strict rejection and complete readable warnings; no route rewrite, altered calibration or duplicated IMGUI status. | NOT TESTED | ____ |
| P3-G07 | Observe GPS during pause/help/on-foot/quicklook/RMB/hide-all transitions. | Existing world visibility and documented widget policy remain coherent, with no unintended pointer capture. | NOT TESTED | ____ |
| P3-G08 | Inspect GPS text and warnings at all three required resolutions. | Labels readable; long warnings scroll within their viewport and do not overlap buttons. | NOT TESTED | ____ |
| P3-R01 | Edit Mode: Aurora UI > Menus > Aurora Route Manager. | Window, tabs, inputs, actions and three labeled sample rows are editable; samples have no storage identity. | NOT TESTED | ____ |
| P3-R02 | Observe route storage hashes before opening/editing previews and before Play. | No design-preview file writes or simulation mutations; Andy_Try_1 and existing guides remain unchanged. | NOT TESTED | ____ |
| P3-R03 | Resize/move manager; alter row prefab, background, font, selection graphic and Content spacing. | Runtime rows inherit saved prefab design; list/metadata remain usable and scrollable. | NOT TESTED | ____ |
| P3-R04 | Open with Numpad 8/F6 or GPS Routes; Refresh files and all tabs. | Saved recordings, calibrated guides, invalid files and recovery drafts appear in the correct sections with stable selection. | NOT TESTED | ____ |
| P3-R05 | Select duplicate-name routes; scroll, refresh/reorder and rename an alias. | Explicit path remains associated with the correct file; visual changes/row reuse never redirect actions. | NOT TESTED | ____ |
| P3-R06 | Activate a compatible validated guide with Use selected as GPS. | Existing strict readiness/compatibility checks succeed and GPS selects the explicit saved file. | NOT TESTED | ____ |
| P3-R07 | Attempt activation of incompatible guide, incomplete draft and malformed file. | Existing errors/interlocks remain; draft/malformed files cannot activate GPS or bypass validation. | NOT TESTED | ____ |
| P3-R08 | Start new recording from manager; drive and stop/save via button and Numpad 9/F7. | Existing recorder captures actual route and publishes one complete new file; starting returns to driving. | NOT TESTED | ____ |
| P3-R09 | Stop without saving; inspect raw samples and save recording/copy. | Recovery/incomplete safeguards retained; sample details correspond to actual selected raw data. | NOT TESTED | ____ |
| P3-R10 | Name next recording, save new copy, rename library alias and refresh. | Correct identity/name/file association; alias rename does not change original trajectory bytes. | NOT TESTED | ____ |
| P3-R11 | Double-click save and press/drag/hide/reopen/release over Save. | No duplicate save or stale canceled click; keyboard and physical commands cannot remain held. | NOT TESTED | ____ |
| P3-R12 | Delete only a disposable selected copy; cancel once, then explicitly confirm. | Confirmation targets captured path/identity; only confirmed selected copy is removed; originals/drafts remain protected. | NOT TESTED | ____ |
| P3-R13 | Load/preview saved raw recording or guide; change sample scrubber and preview layer. | Existing cached previews/data are shown; selecting another file does not leave an unrelated recording displayed. | NOT TESTED | ____ |
| P3-R14 | Guides tab: change layer, leg, rejoin, GPS magenta/amber and opacity. | Existing diagnostics/render controls work; explicit rejoin changes guide cursor without changing physics/route data. | NOT TESTED | ____ |
| P3-R15 | Invoke Edit route in Editor and existing revalidation workflow; cancel without saving. | Existing Editor invocation retained; incompatible source stays unchanged unless user explicitly saves a validated new copy. | NOT TESTED | ____ |
| P3-R16 | Open routes folder on supported desktop platform. | Existing location opens; errors are readable without breaking manager. | NOT TESTED | ____ |
| P3-R17 | Focus name/rename TMP fields, type/edit/select/copy/paste; use Tab/F-keys/numpad. | Text editing/focus works; gameplay shortcuts do not fire while editing. Closing clears field ownership. | NOT TESTED | ____ |
| P3-R18 | Scroll both columns with hardware wheel/drag; close using button/F6; reopen. | Scrollable content works, cursor restores, no stale drag/button action or command survives close. | NOT TESTED | ____ |
| P4-X05 | Toggle GPS and inspect route, leg and guidance labels. | Existing status and guide visibility remain correct. | NOT TESTED | ____ |
| P4-X06 | Open Route Manager; scroll dynamic rows and select a disposable guide. | Rows retain explicit paths, scrolling, selection and existing actions. | NOT TESTED | ____ |
| P4-X07 | Record/save a disposable route and select a compatible guide. | Recording and guide activation remain functional; original recordings are untouched. | NOT TESTED | ____ |
| P4-X08 | Select the previously incompatible Andy guide. | Strict compatibility still rejects GuideRollerX mismatch without rewriting the guide. | NOT TESTED | ____ |
| P5-05-01 | In a scene copy select Messages/Aurora Operator Interface/Aurora Projected GPS; edit widths, colors and viewport; save/reopen and enable a guide behind the machine. | Editable screen strokes retain existing occlusion/geometry; full-view viewport aligns physically, deliberate viewport moves persist, and the graphic never captures input. | NOT TESTED | ____ |
| P5-05-02 | Activate a compatible disposable guide, then select the unchanged incompatible Andy guide. | Compatible guide works; Andy GuideRollerX mismatch remains strictly rejected without rewriting it. | NOT TESTED | ____ |
| P5-05-03 | Open/close Route Manager with a routine/critical notice active. | Manager owns modal input and Phase 5 panels hide; no additional legacy backdrop duplicates. | NOT TESTED | ____ |

## 6. Welcome and session selection

| ID | Exact action / Unity location | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |
| P4-W01 | Open the authored scene outside Play. | Four persistent screen roots exist under the existing Menus group; welcome is editable. | NOT TESTED | ____ |
| P4-W02 | Start scored from Welcome. | New scored FreeSimulation starts; no practice or training flag. | NOT TESTED | ____ |
| P4-W03 | Start practice from Welcome. | Unscored FreeSimulation starts; practice never earns a score. | NOT TESTED | ____ |
| P4-W04 | Start Assisted training. | Existing GuidedTraining starts as practice with its original equipment and finish interlocks. | NOT TESTED | ____ |
| P4-W05 | Open and close help from Welcome with its button and F1. | Returns to Welcome without starting or replacing a session. | NOT TESTED | ____ |
| P4-W06 | Rearrange the four welcome buttons into a different layout in a scene copy. | Their explicit actions remain correct regardless of sibling order or position. | NOT TESTED | ____ |
| P4-W07 | Use the optional logo, subtitle, introduction and secondary labels independently. | Text and graphics can be hidden, moved and replaced without requiring a logo. | NOT TESTED | ____ |
| P4-W08 | Double-click a start button and press Submit rapidly. | One action per frame; hidden-screen callbacks cannot replace the newly started session. | NOT TESTED | ____ |
| P5-06-01 | Use each Welcome start button with Phase 5 installed. | Scored FreeSimulation, unscored practice and guided training preserve their existing distinctions. | NOT TESTED | ____ |
| P5-06-02 | Return to Welcome from Pause New session after reading history. | Existing lifecycle resets session history on the next active session; no duplicated UI objects. | NOT TESTED | ____ |

## 7. Pause/settings

| ID | Exact action / Unity location | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |
| P4-S01 | Open Pause with Escape while driving; resume by button and Escape. | Existing Paused state and cursor behavior are preserved; gameplay resumes correctly. | NOT TESTED | ____ |
| P4-S02 | Choose New session from Pause. | Returns to the original welcome/session selection workflow. | NOT TESTED | ____ |
| P4-S03 | Choose Refill practice. | Starts existing unscored service practice with partly empty tanks. | NOT TESTED | ____ |
| P4-S04 | Drag forward speed to both endpoints and quarter steps. | 3–8 mph inclusive, snapped to 0.25 mph; displayed and authoritative values agree. | NOT TESTED | ____ |
| P4-S05 | Set 6.25 mph, start another session, restart Play and restart Unity. | Selection persists through the existing PlayerPrefs key; original calibration is unchanged. | NOT TESTED | ____ |
| P4-S06 | Inspect all pause controls. | No RESET TO ORIGINAL SPEED button; all existing explanatory labels remain available. | NOT TESTED | ____ |
| P4-S07 | Move seated-eye slider through -8 to +12 cm in the authored scene. | Fallback value changes; authored Driver Eye Anchor is not moved or offset a second time. | NOT TESTED | ____ |
| P4-S08 | Use the eye adjustment in an existing fallback camera setup without an eye anchor. | Existing fallback offset applies once, with no camera drift. | NOT TESTED | ____ |
| P4-S09 | Replace slider track, thumb, fill and value formatting in a scene copy. | Appearance changes persist; range, precision, saved value and bindings remain correct. | NOT TESTED | ____ |
| P4-S10 | Hold hydraulics or drag a valve when opening Pause. | Transient command and drag release immediately; no held command resumes on close. | NOT TESTED | ____ |
| P5-07-01 | Pause while on foot near a hose target; resume. | Service panel and interaction capture hide, cursor releases through existing service logic, then service resumes. | NOT TESTED | ____ |
| P5-07-02 | Set a quarter-mph speed and reopen settings after service/history. | Existing range, precision, PlayerPrefs persistence and eye-anchor behavior remain intact. | NOT TESTED | ____ |

## 8. Help

| ID | Exact action / Unity location | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |
| P4-H01 | Read all six sections against actual controls. | Prepare, return, keyboard, camera, machine load and on-foot service descriptions remain accurate. | NOT TESTED | ____ |
| P4-H02 | Open F1 during gameplay and close it. | Returns to running gameplay with the previous session intact. | NOT TESTED | ____ |
| P4-H03 | Open F1 or the guide button from Pause; close help. | Returns to Pause without accidentally resuming. | NOT TESTED | ____ |
| P4-H04 | Open help from Results; close with F1 and the button. | Returns to the same completed report. | NOT TESTED | ____ |
| P4-H05 | Scroll from top to bottom using wheel and scrollbar at 1366×768. | All instructions remain readable and reachable; no microscopic text or clipped final section. | NOT TESTED | ____ |
| P4-H06 | Edit section text, font, spacing and headings in a scene copy. | Content height follows text; editable layout groups allow independent section rearrangement. | NOT TESTED | ____ |
| P4-H07 | Enable the optional diagram and assign custom artwork. | Diagram participates in scrolling and does not capture clicks unnecessarily. | NOT TESTED | ____ |
| P5-08-01 | Open F1 from driving, service, Pause and Results. | Correct return state; no service/status/message panels capture through help. | NOT TESTED | ____ |
| P5-08-02 | Edit help prose/fonts and use Fit Edited Help Text (Undoable). | Body heights fit edited content; Undo and saved layout work. | NOT TESTED | ____ |

## 9. Results

| ID | Exact action / Unity location | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |
| P4-R01 | Finish a scored partial run. | Actual RunReport score, partial status and 95% completion explanation display. | NOT TESTED | ____ |
| P4-R02 | Complete a scored run through the existing full resurfacing and return workflow. | Actual Complete report and all scoring remain consistent with the simulator. | NOT TESTED | ____ |
| P4-R03 | Finish a practice run. | Practice report is explicitly unscored; score remains zero and scoring point columns are blank. | NOT TESTED | ____ |
| P4-R04 | Inspect elapsed time, coverage, shaving, water, finish, missed and excess values. | Every value agrees with the actual completed report; no design samples remain in Play. | NOT TESTED | ____ |
| P4-R05 | Inspect water efficiency, dumps/safe return and misuse penalties. | Existing operating points and penalties agree with RunReport. | NOT TESTED | ____ |
| P4-R06 | Compare selected and original forward-speed values. | Separate values come from the completed report, preserving original calibration. | NOT TESTED | ____ |
| P4-R07 | Reorder rows and resize/style the score in a scene copy. | Explicit metric kinds preserve data identity independent of layout. | NOT TESTED | ____ |
| P4-R08 | Use Results New scored, New practice and help. | Each uses the existing lifecycle; help returns to Results. | NOT TESTED | ____ |
| P4-R09 | Leave an unchanged completed report open. | No repeated prefab creation or label rebuild; changes update only when report/state changes. | NOT TESTED | ____ |
| P5-09-01 | Finish scored and practice runs after Phase 5 notices occurred. | Actual RunReport remains authoritative; practice unscored; original completion/scoring rules intact. | NOT TESTED | ____ |
| P5-09-02 | Use Results new scored/practice actions and help. | Existing session actions work and Phase 5 ordinary panels remain hidden until active gameplay. | NOT TESTED | ____ |

## 10. Water-service UI

| ID | Exact action / Unity location | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |

| P5-10-01 | Use F to dismount/remount at the existing steps. | Grounded movement/camera and original boarding conditions remain intact. | NOT TESTED | ____ |
| P5-10-02 | Walk to the holder; aim at it, take and return the hose with E. | Prompt names the actual target and hose-state field updates without independent UI state. | NOT TESTED | ____ |
| P5-10-03 | Aim at both distant and reachable flood/wash sockets. | Existing approach/reach text and correct port/capacity information appear. | NOT TESTED | ____ |
| P5-10-04 | Connect flood, operate supply and watch filling. | Only flood quantity rises; supply percentage and actual L/s update. | NOT TESTED | ____ |
| P5-10-05 | Repeat connection and filling for wash. | Wash remains separate and finite; same nozzle workflow. | NOT TESTED | ____ |
| P5-10-06 | Fill a tank to capacity with supply open. | Overflow warning appears; capped quantity, original particles/audio/runoff remain. | NOT TESTED | ____ |
| P5-10-07 | Reach the hose limit, walk back to slack and test the existing breakaway/recovery sequence. | Tautness prompt and routing follow authoritative service behavior; no invisible trap or reset required. | NOT TESTED | ____ |
| P5-10-08 | Move the service panel over a world target, then away, in a scene copy. | Click is blocked only by actual visible graphics; moved-away panel and former y>700 band do not steal clicks. | NOT TESTED | ____ |
| P5-10-09 | Hide UI with Numpad 1 while servicing, then restore it. | Panels and hit regions hide; E, walking, hose and water behavior continue; restored fields are current. | NOT TESTED | ____ |

## 11. Notifications and warnings

| ID | Exact action / Unity location | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |

| P5-11-01 | Trigger an ordinary operator notice and repeat refresh without changing its text. | One existing history entry; no duplicated uncontrolled objects. | NOT TESTED | ____ |
| P5-11-02 | Wait through seven to eight seconds. | Original eight-second lifetime and configurable visual fade; expired notice no longer captures. | NOT TESTED | ____ |
| P5-11-03 | Cause an actual jam, tank-full condition, dry-pump wear or deep snow warning. | Existing severity/order and warning text remain authoritative; critical persists while condition exists. | NOT TESTED | ____ |
| P5-11-04 | Turn routine messages off while a critical condition exists. | Routine notices hide; important faults remain visible. | NOT TESTED | ____ |
| P5-11-05 | Open Messages from the bottom strip; inspect advice and close it. | Existing nonmodal history state and guided/free advice remain; simulation does not pause. | NOT TESTED | ____ |
| P5-11-06 | Generate over 32 notices in a disposable test. | Existing 32-entry retention, timestamp and newest-first display; pool stays bounded. | NOT TESTED | ____ |
| P5-11-07 | Scroll history and inspect long messages at minimum resolution. | Full messages remain available; row spacing/font/template remain editable. | NOT TESTED | ____ |
| P5-11-08 | Move/reskin warning, hint and routine templates in a scene copy. | Appearance persists; bindings, severity and timing do not depend on layout. | NOT TESTED | ____ |
| P5-11-09 | Inspect recording/GPS notices with Phase 3 active. | Existing GPS widget remains the source; bottom strip retains its existing recording/distance/navigation status without extra duplicate toast. | NOT TESTED | ____ |

## 12. Physical cockpit and mouse interaction

| ID | Exact action / Unity location | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |
| D01 | In a test layout place a UI hydraulic/valve target over a visible physical cockpit grip; keep E/Q/C/Left Shift released and click/drag UI. Aim with RMB then release it if needed. | Only UI acquires the gesture. A different physical control behind it must not change. Use held E separately for inspection: it hides the console and restores the prior view on release. | NOT TESTED | ____ |
| D02 | Move that UI away; click the exposed physical control. | Physical picking works again. Transparent/decorative text/Images with Raycast Target off do not block it. | NOT TESTED | ____ |
| D03 | Begin a physical lever/valve drag, cross an authored UI panel, then release. | Physical owner continues its existing drag/release behavior. Crossing UI does not toggle a UI action or acquire another physical control. | NOT TESTED | ____ |
| D04 | Begin UI drag, leave panel, move over physical cockpit and release. | UI hydraulic releases on exit; world does not acquire mid-drag. A fresh press is needed for world control. | NOT TESTED | ____ |
| D05 | Hold RMB, move mouse, release; repeat while hovering UI and after a UI hydraulic press. | Camera look works; UI input disables; hydraulic releases; cursor locks/restores correctly in visible Game view. Hidden-batch cursor behavior is not evidence for this row. | NOT TESTED | ____ |
| D06 | Hold a hydraulic UI command then Alt-Tab away; release mouse outside Unity; return. | Command is neutral, no stuck capture; existing focus-loss pause applies. Resume, then fresh clicks work. Repeat on-foot focus handling. | NOT TESTED | ____ |
| D07 | Hold/release E, Q, C, Left Shift; use X, R and Space. | Existing views/stance and return transitions remain correct. Console hides for held quick looks and releases gesture. HUD/other modules follow existing policies. | NOT TESTED | ____ |
| D08 | Raise/lower conditioner; operate its mounted physical controls at each height. | Picking follows moved physical geometry. UI stays independently usable; no old fixed world-space hit positions. | NOT TESTED | ____ |
| D09 | Stop safely, dismount F, walk and look, operate hose with E, board F. | Console hidden on foot, HUD read-only, no UI cursor trap. Service prompts and centered interactions work. | NOT TESTED | ____ |
| D10 | Disable the authored console presenter during a disposable Play test, then re-enable. | Legacy console returns while unavailable, authored targets hide; re-enable gives one authored console. Repeat HUD separately. Missing-binding logs should explain fallback rather than leave silent dead controls. | NOT TESTED | ____ |
| P5-12-01 | Hover physical controls with Phase 5 enabled. | Existing targeted Hint text appears in the editable context panel. | NOT TESTED | ____ |
| P5-12-02 | Press on service UI, release, move it away, then press the world target. | Released UI ownership clears; the fresh world click works. | NOT TESTED | ____ |
| P5-12-03 | Hold a bottom action, open/close a modal before release. | Canceled press does not dispatch a ghost action. | NOT TESTED | ____ |
| P5-12-04 | Alt-tab during hydraulic, valve, on-foot look and UI gestures. | No stuck command, stale drag or unintended action on return; record native cursor behavior. | NOT TESTED | ____ |
| P5-12-05 | Disable decorative Images and move interactive backgrounds. | Only enabled raycast graphics and actual current RectTransforms own input. | NOT TESTED | ____ |

## 13. Screen state/modal behavior

| ID | Exact action / Unity location | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |
| P3-X01 | Run Phase 1/2 checks A01–I07 above with Phase 3 installed. | All 94 retain their meaning; record new failures separately from historical unsigned manual checks. | NOT TESTED | ____ |
| P3-X02 | Test physical cockpit picking/drag/release and UI map/row/button interaction across panel edges. | One interaction owner; actual authored graphics block acquisition and existing physical drags retain release. | NOT TESTED | ____ |
| P3-X03 | RMB look, focus loss, cursor lock/unlock and on-foot service/boarding. | Existing look/service behavior preserved and restored; UI does not capture world commands. | NOT TESTED | ____ |
| P3-X04 | Pause/help, Tab console compact/expanded, Numpad 1 hide-all, recording and route modal transitions. | Correct visibility/ownership; no duplicate legacy survey/GPS/manager or hidden commands. | NOT TESTED | ____ |
| P3-X05 | 1366 x 768: survey/GPS + route list/detail + both console modes. | Correct map aspect, readable text, usable scroll areas and no overlapping controls. | NOT TESTED | ____ |
| P3-X06 | 1600 x 900: repeat layout and click-location tests. | Authored anchors, aspect and click regions remain correct. | NOT TESTED | ____ |
| P3-X07 | 1920 x 1080: repeat layout and click-location tests. | Authored anchors, aspect and click regions remain correct. | NOT TESTED | ____ |
| P3-X08 | Save/reopen the scene and row prefab; fresh Editor process, then Play. | Appearance/layout bindings persist; preview content returns on exit; runtime rows never become recordings. | NOT TESTED | ____ |
| P3-X09 | Original Scenes/IceResurfacer.unity: startup, drive, F2/F5/F6/F7 and physical controls. | Existing procedural UI/simulation works with no authored UI instantiated; scene bytes unchanged. | NOT TESTED | ____ |
| P3-X10 | Compare protected hashes and authored assets before/after acceptance. | Machine/arena/camera/brush/roller/fill assets, tuning/Core/scoring, original routes/guides and Phase 1/2 visuals unchanged. | NOT TESTED | ____ |
| P4-I01 | Navigate menus with native Tab and Shift+Tab. | Focus cycles through the authored navigation list; Tab does not toggle hidden machinery. | NOT TESTED | ____ |
| P4-I02 | Use arrows and Enter on buttons and settings. | Existing Input System navigation/Submit works once per action; settings preserve constraints. | NOT TESTED | ____ |
| P4-I03 | Use native gamepad navigation if applicable. | Menu selection and Submit work; record device and any unsupported behavior. | NOT TESTED | ____ |
| P4-I04 | Click machinery, diagnostics, map and GPS locations through each modal. | Modal owns the pointer; underlying interfaces receive no command. | NOT TESTED | ____ |
| P4-I05 | Close each screen and click those same locations. | No invisible full-screen raycast blocker remains. | NOT TESTED | ____ |
| P4-I06 | Press a menu button, hide/reopen its screen before release, then release. | Canceled old press cannot fire; a fresh press works. | NOT TESTED | ____ |
| P4-I07 | Alt-tab while holding a button, machinery lever or settings drag. | Focus loss cancels commands and drags; returning does not dispatch a stale action. | NOT TESTED | ____ |
| P4-I08 | Resume after RMB look, on-foot interaction and quicklook states. | Native cursor locking, unlocking and focus match existing game behavior. | NOT TESTED | ____ |
| P4-I09 | Open Route Manager, Pause and Help using existing shortcuts in different orders. | Existing modal priority is preserved; only the appropriate Phase 4 screen appears. | NOT TESTED | ____ |
| P4-I10 | Use Numpad 1 hide-all, then F1/Escape, and restore the instruments. | Existing hide-all and menu state behavior remain consistent without duplicate IMGUI screens. | NOT TESTED | ____ |
| P5-13-01 | Cycle Welcome, driving, on-foot, Pause, help and Results. | Only intended panels show; modal screens keep established priority. | NOT TESTED | ____ |
| P5-13-02 | Open Route Manager while a critical fault and history are visible. | Route modal wins; no Phase 5 click-through or stale history action. | NOT TESTED | ____ |
| P5-13-03 | Toggle Tab with notices/history visible. | Existing console state changes; message layout does not reset machinery values. | NOT TESTED | ____ |
| P5-13-04 | Toggle Numpad 1 with history, service, warnings and F4 active. | Intended player UI hides, history panel closes via existing policy; no invisible blockers. | NOT TESTED | ____ |
| P5-13-05 | Toggle F3/F2 diagnostics during gameplay and hide them again. | Developer overlays remain functional; inactive debug hit regions do not capture. | NOT TESTED | ____ |
| P5-13-06 | Use F4 with Phase 5 installed. | Single editable performance readout toggles; old badge suppressed. | NOT TESTED | ____ |
| P5-13-07 | Disable the Phase 5 presenter temporarily in a disposable copy. | Legacy equivalent remains available as fallback; no competing authoritative service/message state. | NOT TESTED | ____ |

## 14. Edit Mode preview and authoring

| ID | Exact action / Unity location | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |
| P4-A01 | Preview Welcome, Pause, Help and Results outside Play. | Exactly the requested screen previews; results/settings show clearly labeled sample data. | NOT TESTED | ____ |
| P4-A02 | Preview screens then Restore Saved Presentation. | Every prior CanvasGroup visibility/input value returns; no session, preference or route changes. | NOT TESTED | ____ |
| P4-A03 | Preview, then save, enter Play, reload scripts, close scene or quit. | Preview restores safely before lifecycle operations; unrelated screens are not left disabled. | NOT TESTED | ____ |
| P4-A05 | Replace a full-screen background with an imported PNG. | Sprite, scaling and tint persist; it fills the intended screen and blocks gameplay while modal. | NOT TESTED | ____ |
| P4-A06 | Replace button sprites and a nine-sliced panel; move captions separately. | Custom art and independent TMP positions survive without C# changes. | NOT TESTED | ____ |
| P4-A07 | Replace TMP fonts globally through the existing theme and locally with Apply Font disabled. | Shared changes reach four phases; intentional local overrides remain intact. | NOT TESTED | ____ |
| P4-A11 | Create a menu button/panel prefab variant and replace decorative art. | Variants reuse the existing design system; only required graphics raycast. | NOT TESTED | ____ |
| P5-14-01 | Use Operator Preview for driving, service, history and warnings outside Play. | Saved sample data and badge; no simulation, history, prefs or routes changed. | NOT TESTED | ____ |
| P5-14-02 | Alternate Operator Preview and Phase 4 Screen Preview. | Previous preview restores before the next takes ownership; one coherent preview. | NOT TESTED | ____ |
| P5-14-03 | Preview then Save, enter Play, reload scripts or close the scene. | Transient group values restore; saved authoring edits remain. | NOT TESTED | ____ |
| P5-14-04 | Change shared theme font/color/sprite/state channels, then explicitly apply to loaded UI. | Opted-in components update across phases; local channels remain untouched. | NOT TESTED | ____ |
| P5-14-05 | Disable a style channel and replace an individual font/sprite/tint. | Local override survives Play, theme application and reopen. | NOT TESTED | ____ |
| P5-14-06 | Create a variant of a notice, context hint, action or history row. | Appearance comes from editable prefab source; explicit content/button references stay assigned. | NOT TESTED | ____ |

## 15. Responsive layouts

| ID | Exact action / Unity location | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |
| E01 | 1366 x 768, compact; inspect HUD/console/survey/bottom strip; operate Open. | Legible text, no clipping/overlap, correct clickable bounds; only compact targets active. | NOT TESTED | ____ |
| E02 | 1366 x 768, expanded; operate each control including terminal Cancel. | All controls/readouts fit and remain reachable; smallest target still usable. Check long conditioner/blade text. | NOT TESTED | ____ |
| E03 | 1600 x 900, compact; repeat E01. | Same relationships at reference size, including edited widgets. | NOT TESTED | ____ |
| E04 | 1600 x 900, expanded; repeat E02. | No duplicate legacy console behind it, mismatched hit areas or stretched text. | NOT TESTED | ____ |
| E05 | 1920 x 1080, compact; repeat E01. | Clean scaling and correct pointer acquisition. | NOT TESTED | ____ |
| E06 | 1920 x 1080, expanded; repeat E02. | Clean scaling and correct drag/scroll magnitude independent of pixel scale. | NOT TESTED | ____ |
| E07 | In test scene set a control's anchor/pivot intentionally; resize Game view, Play, switch modes. | Authored anchor behavior governs position. Runtime does not reset RectTransform layout. Explicit indicator child animation may move/rotate only its assigned art. | NOT TESTED | ____ |
| E08 | Move both console panels away from former lower-right positions in the test scene; click the cleared areas and the new targets. | No obsolete fixed console hit rectangle. Existing unrelated survey/status/message rectangles can still legitimately own their regions. | NOT TESTED | ____ |
| E09 | Open diagnostics, GPS, routes, help, pause, startup and results at each size. | Correct visible module policy; no duplicated console/HUD on modal screens and no labels obscuring important actions. This verifies mixed IMGUI/uGUI composition. | NOT TESTED | ____ |
| E10 | Press Tab repeatedly with distinct compact/expanded positions; compare child counts/transforms before/after. | Independent layouts retain edits; inactive targets never click through; no runtime creation/destruction per mode switch. | NOT TESTED | ____ |
| P4-A08 | Inspect all four screens at 1366×768. | Readable text, reachable actions, scrollable help, correct backdrop and no unintended clipping/overlap. | NOT TESTED | ____ |
| P4-A09 | Inspect all four screens at 1600×900. | Readable text, reachable actions, scrollable help, correct backdrop and no unintended clipping/overlap. | NOT TESTED | ____ |
| P4-A10 | Inspect all four screens at 1920×1080. | Readable text, reachable actions, scrollable help, correct backdrop and no unintended clipping/overlap. | NOT TESTED | ____ |
| P5-15-01 | At 1366×768 inspect compact HUD/survey/GPS and all bottom actions. | Readable, reachable and no unintended clipping; actual hit regions follow graphics. | NOT TESTED | ____ |
| P5-15-02 | At all three resolutions inspect expanded console plus a real warning. | Warning visible with correct priority; any overlap requiring artistic decisions recorded rather than silently rearranged. | NOT TESTED | ____ |
| P5-15-03 | At all three resolutions inspect real on-foot filling/overflow, history and Route Manager. | Texts reachable, scrolls work and panels do not create invisible world-input regions. | NOT TESTED | ____ |
| P5-15-04 | At all three resolutions inspect Pause and Results with Phase 5 installed. | No ordinary overlays or duplicate legacy backdrops under the modals. | NOT TESTED | ____ |

## 16. Persistence across Unity restart

| ID | Exact action / Unity location | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |
| G01 | In Edit Mode make the recorded lever move, valve resize, toggle move, sprite/TMP edit and one HUD edit; save scene. | Edits visible immediately and represented as deliberate prefab overrides. Shared channels disabled where a local override is intended. | NOT TESTED | ____ |
| G02 | Enter Play; operate every changed control; use keyboard/physical counterparts. | Transforms/graphics stay authored, controls still drive actual machinery, external state updates readouts. No reset to hardcoded rectangles. | NOT TESTED | ____ |
| G03 | Stop Play; inspect saved scene without applying runtime values. | Saved preview restored, all visual edits retained, no live hydraulic demand persisted. | NOT TESTED | ____ |
| G04 | Save, close Unity completely, reopen project and exact test scene. | Lever/valve/toggle/HUD positions, anchors/pivots, sprite, TMP settings and style opt-outs remain. No missing nested prefab references. | NOT TESTED | ____ |
| G05 | Repeat G02 after fresh Editor start; switch compact/expanded twice. | Binding and hit areas still correct after a cold reopen, independent layout edits preserved. | NOT TESTED | ____ |
| G06 | In a test prefab variant change shared font/state artwork; Apply Themes to Loaded UI deliberately; save, close/reopen variant. | Opted-in channels update and persist; local overrides stay local. Original machine/arena assets untouched. | NOT TESTED | ____ |
| G07 | Compare original primary scene/prefabs with backups or version-control diff after testing. | Only intended test/customization edits exist. No machine/calibration/GPS changes caused by authoring preview. Restore test-only scene overrides if desired. | NOT TESTED | ____ |
| P4-A04 | Move, resize, anchor and rotate a startup button; save/reopen/restart Unity and Play. | All visual overrides persist without runtime position resets; correct action still fires. | NOT TESTED | ____ |
| P4-P04 | Exit Play after edited-screen tests and compare saved assets. | No live sample/result data or simulation state writes back into authored assets. | NOT TESTED | ____ |
| P5-16-01 | Move service prompt/panel; resize warning; change TMP font asset; replace background; save/reopen. | All deliberate overrides and explicit bindings persist. | NOT TESTED | ____ |
| P5-16-02 | Restart Unity and enter Play; trigger actual service and fault events. | The same authored composition displays real content; no runtime positional/style reset. | NOT TESTED | ____ |
| P5-16-03 | Exit Play and reopen edited scene. | Sample data restored; no live UI values, hose state or simulation data written to assets. | NOT TESTED | ____ |

## 17. Procedural fallback scene

| ID | Exact action / Unity location | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |
| H01 | Inspect hierarchy before/after entering Play. | Existing procedural launch works with no added authored HUD/console or duplicate EventSystem. Do not install authored UI here for this check. | NOT TESTED | ____ |
| H02 | Start practice; inspect header; Tab expand/collapse. | Original IMGUI header and machinery console still render and receive input. | NOT TESTED | ____ |
| H03 | Operate hydraulic, auger, flood/wash, blade, brush/pump, breaker and service help; compare keyboard/physical control. | Legacy functionality and real simulation remain intact; Phase 2 registration is absent. | NOT TESTED | ____ |
| H04 | Test RMB, quick looks, hide/show, pause/help, on-foot service and finish. | Existing legacy policies and transitions remain usable. | NOT TESTED | ____ |
| H05 | Exit Play and compare procedural scene file/version-control diff. | No unintended scene modifications. Reopen authored scene to continue. | NOT TESTED | ____ |
| P4-X09 | Open Scenes/IceResurfacer.unity and test welcome, pause, help, results and machinery. | Procedural scene retains its functioning legacy UI with no authored menu dependency. | NOT TESTED | ____ |
| P5-17-01 | Open original Scenes/IceResurfacer.unity without installing authored UI. | Playable legacy UI and no serialized dependency on Phase 5 assets. | NOT TESTED | ____ |
| P5-17-02 | Run service, messages, pause/help and a finish in fallback. | Same authoritative simulation and history behavior; legacy panels available. | NOT TESTED | ____ |

## 18. Gameplay regression

| ID | Exact action / Unity location | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |
| F01 | Back onto rink with S, use C chase; lower/raise conditioner via UI and keyboard. | Actual lift/contact and attached controls follow existing mechanism. Holds at partial height after release. | NOT TESTED | ____ |
| F02 | Run both augers, set about 0.35 mm blade, drive a short pass; observe snow and diagnostics. | Real cutting, conveyor transport, load and tank collection occur. UI changes no snow capacity, scoring or physics. | NOT TESTED | ____ |
| F03 | Open flood partway during travel, then close. | Flood consumption/application changes with the real valve and machine conditions. Wash remains independent. | NOT TESTED | ____ |
| F04 | Open wash, prime for about 10 seconds, then start pickup; compare HUD/diagnostics/surface. | Existing priming/pickup/recovery and pump consequence behavior remains. No artificial refill or independent UI water model. | NOT TESTED | ____ |
| F05 | Change blade set depth with UI/keyboard/physical wheel while raised and down on ice. | Calibrated bounds, physical blade and true contact/cut depth remain consistent; engagement schematic agrees. | NOT TESTED | ____ |
| F06 | Deploy/stow brush via all inputs; inspect arm, head and guide roller along boards. | Existing rig articulation and mechanical endpoints remain intact. | NOT TESTED | ____ |
| F07 | Exercise snow breaker with conveyor snow present; use J if existing jam conditions permit reset. | Real stroke/cooldown/conveyor response and reset rules remain; no maintained breaker state. | NOT TESTED | ____ |
| F08 | Stow equipment, return to pit, stop; raise tank, briefly lower/raise to release residue, lower fully. | Existing safe dump geometry, residue and tank/hydraulic behavior remain. No UI-only snow clearing. | NOT TESTED | ____ |
| F09 | Esc > Refill practice; F off, walk to right wall hose, E take/connect/supply; close/uncouple/return; board. | Actual two-inlet/refill/service sequence works. Console hides on foot; HUD tracks reservoir changes. Existing hose reach/inhibit behavior remains. | NOT TESTED | ____ |
| F10 | Toggle GPS with F5, open/close routes with F6; inspect existing routes without altering them. | GPS advisory, route visibility/selection and storage remain functional. Route manager excludes console input. | NOT TESTED | ____ |
| F11 | If testing recorder, create a disposable recording using F7, drive, F7 save; inspect then remove only that test recording through its normal UI. | Existing recording workflow works; established recordings unchanged. Note name/path for cleanup. | NOT TESTED | ____ |
| F12 | F2 overlays, F3 diagnostics, F4 performance, survey cell inspect and message/history controls. | Nonmigrated panels still operate; entire dashboard has not been disabled. | NOT TESTED | ____ |
| F13 | Complete a disposable scored run and inspect results; start another run. | Existing surface/water/snow/report scoring uses actual simulation; no carried-over UI command or stale binding. | NOT TESTED | ____ |
| P4-P05 | Run the deterministic simulation regression. | All 43 existing scenarios pass; no scoring, physics, tuning or threshold changes. | NOT TESTED | ____ |
| P5-18-01 | Run the existing deterministic regression suite. | All 43 scenarios pass; physics, snow/water conservation, scoring and route compatibility unchanged. | NOT TESTED | ____ |
| P5-18-02 | Compare protected route hashes and asset baseline after all tests. | All 31 saved routes byte-identical; machine, arena, anchors, tuning and old UI assets unchanged. | NOT TESTED | ____ |
| P5-18-03 | Repeat normal driving, free/guided service and recording workflows. | No altered control direction, capacity, lifecycle, mechanical or GPS behavior. | NOT TESTED | ____ |

## 19. Performance and allocations

| ID | Exact action / Unity location | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |
| I01 | Window > Analysis > Profiler; CPU/Rendering/UI modules, compact then expanded then Numpad 1 hidden. | Record median/upper-range frame and main-thread ms for each. No persistent hitch or large unexplained regression. | NOT TESTED | ____ |
| I02 | Same pose: disable `IceMachineryConsole` component in Play (legacy console fallback), sample; re-enable, sample. Repeat HUD separately if needed. | Compare equivalent visible modes. Record absolute and relative differences; a hidden UI comparison alone does not measure migration cost. | NOT TESTED | ____ |
| I03 | CPU Profiler Timeline/Hierarchy: inspect GC.Alloc and allocation call stacks over 30 seconds. | Record bytes/frame and refresh spikes. Formatting may allocate at 10 Hz; do not require or claim zero. Investigate new sustained growth. | NOT TESTED | ____ |
| I04 | Window > Analysis > Frame Debugger plus Rendering profiler; compare fallback/authored modes. | Record UI batches/draw calls/SetPass and TMP atlases/materials. No duplicate legacy console render behind authored replacement. | NOT TESTED | ____ |
| I05 | UI Profiler/CPU timeline: inspect Canvas.BuildBatch and layout/graphic rebuilds while idle, operating, and switching Tab. | No per-frame layout reset, repeated instantiation or unexpectedly large rebuilds. Live value/art changes may legitimately rebuild affected graphics. | NOT TESTED | ____ |
| I06 | Hierarchy counts before/after 100 Tab switches and several resets; search EventSystem/Canvas and console objects. | Stable persistent UI object counts, one input module/owner, no accumulating listeners or duplicate actions. | NOT TESTED | ____ |
| I07 | Repeat busiest real pass with snow/water/brush and diagnostics visible at 1920 x 1080. | No meaningful input latency or frame-time regression relative to equivalent legacy fallback. Record machinery workload to make comparison reproducible. | NOT TESTED | ____ |
| P3-P01 | Profiler: equivalent stationary and busy cut/wash/flood passes, legacy versus authored versus hidden UI. | Record median/upper frame/main-thread time; no material regression; binding microbenchmarks alone do not certify this. | NOT TESTED | ____ |
| P3-P02 | GC.Alloc/CPU timeline during idle, live map, GPS and scrolling 150+ realistic saved recordings. | No new per-frame row construction or duplicate texture upload; quantify allocations and GC pauses. | NOT TESTED | ____ |
| P3-P03 | UI Profiler: Canvas.BuildBatch, layout/text/graphic rebuilds while stationary and navigating tabs. | Unchanged content does not cause unnecessary rebuilds; changing data/scrolling may legitimately rebuild. | NOT TESTED | ____ |
| P3-P04 | Frame Debugger: batches/draw calls/SetPass and duplicate legacy rendering. | No survey/GPS/route panel rendered behind replacement; record actual rendering cost. | NOT TESTED | ____ |
| P3-P05 | Refresh/scroll 150+ files, including realistic long recordings and guides, repeatedly. | Correct file binding, acceptable refresh/scroll latency and stable pooled row counts; record file sizes/count. | NOT TESTED | ____ |
| P3-P06 | Repeat open/close/refresh 100 times and reset sessions; inspect textures/listeners/GameObject counts. | No accumulating listeners/objects/textures; simulation retains its authoritative streaming-texture update policy. | NOT TESTED | ____ |
| P4-P02 | Profile representative gameplay before/after Phase 4. | Record CPU/GPU frame time, GC allocation, draw calls and Canvas rebuilds on the same hardware/scene. | NOT TESTED | ____ |
| P4-P03 | Profile inactive menus and an unchanged Results screen. | Inactive labels do not update continuously; results update only on changed report or entry. | NOT TESTED | ____ |
| P5-19-01 | Profile visible/hidden UI and compact/expanded console under equivalent workload. | Record CPU/GPU/GC/draw/Canvas results; microbenchmarks do not imply native-frame certification. | NOT TESTED | ____ |
| P5-19-02 | Watch TMP/Canvas rebuilds with inactive menus and unchanged history/results. | Inactive labels do not update needlessly; no recurring prefab instantiation. | NOT TESTED | ____ |
| P5-19-03 | Repeat sessions and open/close history/managers. | No unbounded rows, event listeners, textures, materials or UI objects. | NOT TESTED | ____ |
| P5-19-04 | Test long history and route lists. | Rows reused and bounded appropriately; scrolling does not change data identity. | NOT TESTED | ____ |
| P5-19-05 | Inspect GraphicRaycaster and decorative raycast targets. | No full-screen nonmodal invisible blocker; input cost recorded if material. | NOT TESTED | ____ |
| P5-19-06 | Calibrate any managed-allocation counter before reporting zero allocation. | Unavailable/invalid counter reported honestly; full-frame Profiler remains separate. | NOT TESTED | ____ |

## 20. Final visual acceptance

| ID | Exact action / Unity location | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |

| P5-20-01 | Review your finished artwork, fonts, states and spacing at all target sizes. | User approves the visual composition; no aesthetic sign-off inferred from automated renders. | NOT TESTED | ____ |
| P5-20-02 | Review every remaining code-driven presentation listed in the handbook. | Developer tools/world geometry distinctions and customization limits are understood. | NOT TESTED | ____ |
| P5-20-03 | Record final manual acceptance and known issues with tester/date/device. | PASS/FAIL/NOT TESTED reflect actual work; unresolved native/performance/visual checks remain explicit. | NOT TESTED | ____ |

Detailed canonical checks: **218 preserved + 77 Phase 5 = 295**.
The 20 quick references repeat high-priority workflows and are not counted twice.

## Retained historical automated evidence and limitations

## Verified automated evidence

Phase 1's prior isolated Unity evidence remains at project-root
`Validation/UIAuthoring-Phase1/Evidence`. It covers save/reopen/Play persistence,
live water readings, injected UI and cockpit input, hide/pause/on-foot transitions,
RMB runtime handling, procedural launch, and six camera captures. See
[the Phase 1 report](UI-Authoring-Infrastructure.md#executed-results).

Phase 2 executed these checks in a marked disposable Unity project, with separate
preferences and storage. These are automated results, not your manual sign-off:

| Automated check | Verified result / scope |
| --- | --- |
| Existing simulation suite | 43 deterministic scenarios passed, including hydraulics, snow/water, service, routes and scoring. |
| Serialized UI assets | 8,476 assertions passed across nine prefabs: GUIDs, internal references, 18 console controls, shared state styling, fonts, preview samples and resize anchors. |
| Unity authoring and Play | Moved lever, widened valve, changed TMP size/bold and independently moved brush toggle; saved overrides, reopened, entered Play, operated real bindings and restored saved preview on exit. |
| Fresh process and delivered scene | A fresh Unity process retained all edits. A separate no-save Play check of the exact delivered scene verified references/default controls and left its bytes unchanged. |
| Console input | Real UI module with injected mouse/keyboard events: hydraulic direction/hold/release/exit, independent valves, calibrated blade, maintained switches, breaker, help, terminal actions and confirmation. |
| Cancellation/ownership | UI gestures released for menu/hide/mode/camera/focus callbacks/reset/device-release paths; keyboard takeover survived UI cancellation; hidden/reopened stale Button press did not fire. |
| Physical/camera parity | Physical controls picked at three conditioner heights; moved UI blocked cockpit acquisition only at its actual location; existing operator-anchor/quick-look/switch/horn regression passed. Native hardware remains manual. |
| Phase 1 and procedural regression | Live HUD/diagnostics/on-foot policy passed. Original launch initialized without authored UI, kept legacy controls, and retained scene bytes. |
| Resolution rendering | Twelve captures: default/edited layouts, compact/expanded at each requested resolution. Camera capture excludes legacy IMGUI; inspect the normal combined Game view manually. |
| Asset protection | 560 baseline files checked. Only five existing source files and the additive authored scene changed; all existing machine/arena/core and Phase 1 visual assets were preserved. |

Evidence folder: project-root `Validation/UIAuthoring-Phase2/Evidence`.
Read `Machinery-results.txt`, `MachineryPhysical-results.txt`,
`OperatorIntegration-results.txt`, `Core-regression.txt`, `Static-assets.txt`,
`Protection-audit.json` and `Scene-integration.json`. The
[Phase 2 implementation report](UI-Authoring-Phase2.md#validation-results) provides
the run details and direct links. Every A–I row below still needs a visible-Editor
manual result, even when related automated coverage passed.

## Known test limits

- Hidden batch Unity can render and execute Play Mode but does not establish real
  desktop focus, native cursor lock, hardware wheel increments, or Editor handle
  dragging. Test those in the visible Editor/player.
- Camera captures temporarily render the authored Canvas through a camera; they
  do not prove the final **Screen Space Overlay + IMGUI** composition. Inspect
  menus, survey, messages, routes, quick looks and results in the normal Game view.
- Binding microbenchmarks are not whole-frame profiling. Use the Profiler and
  Frame Debugger in section I for allocations, Canvas rebuilds and draw calls.
- Free simulation deliberately permits finishing without assisted-training
  return checks. A free-mode finish is not an interlock defect. Training applies
  the existing entry/return/equipment/hose/snow checks.
- The existing malformed `Documentation/SnowRecoveryAndContact.txt.meta` warning
  predates this migration. Missing scripts, missing fonts, repeated runtime
  exceptions, or new prefab reference errors are separate failures.


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


Phase 4 verified evidence remains in [UI-Authoring-Phase4.md](UI-Authoring-Phase4.md),
separate from all unsigned manual rows above.
