# Aurora R-54 — Phases 1 and 2 master acceptance

This is the manual acceptance record for the persistent Driving HUD and Machinery
Console. All manual result fields are deliberately blank. Automated evidence is
listed separately below; it does not certify native mouse interaction or your
Editor authoring session.

Project: `G:\UnityProjects\Wireframe_Experiments`, Unity **6000.4.1f1**.
Paths below are relative to `Assets/Experiments/IceResurfacer` unless stated otherwise.
Primary scene: `Scenes/IceResurfacer_AuthoredMachine.unity`.
Procedural fallback: `Scenes/IceResurfacer.unity`.

Tester: __________  Date: __________  Revision/local changes: __________
GPU/display/DPI: __________  Unity version: __________
Game view resolution/scale: __________  Input devices: __________

## How to run this checklist

1. Open the primary scene. Save unrelated work before testing. For deliberate UI
   edits, use **File > Save As** to make a scene copy in a personal test folder.
   Preserve the prefab instance links. Do not apply test overrides to the shared
   machine, arena, or UI prefabs.
2. Start with **1600 x 900** in the Game view resolution menu. Click inside Game
   view before keyboard tests. Start **practice session** for ordinary controls;
   use **Assisted training** only for its explicit interlock checks. A stationary
   machine in the service bay is sufficient for most input checks. Actual cutting
   and water application require driving onto the ice.
3. The console starts compact. **Tab** or **Open console [Tab]** expands it.
   To inspect both layouts outside Play, select `Aurora Machinery Console`, then
   **Tools > Aurora > UI > Preview Selected Console > Compact / Expanded**.
   These are deliberate, undoable presentation edits. They do not start simulation.
4. Record **PASS**, **FAIL**, or **BLOCKED** plus notes for every row. Include a
   screenshot/log and the exact control, mode and resolution for failures. A
   warning does not automatically mean failure; compare it with the expected
   result. Retest changed areas after any fix.
5. Do destructive finish/reset tests last, or start a fresh disposable run afterward.
   Avoid modifying saved GPS routes. Use a new disposable route name if testing
   recording. Restore any speed preference you change in Operator settings.
6. Exit Play before saving visual changes. Do not copy runtime hydraulic commands
   or simulated values into saved components. Do not run machine/arena recovery,
   regeneration, calibration-transfer, or rebake tools for UI acceptance.

Useful hierarchy:

```text
Aurora UI (Canvas / CanvasScaler / GraphicRaycaster)
  Driving HUD (IceAuthoredHud)
    Identity, Speed, Machine Load, Flood Water, Wash Water,
    Snow Tank, Conditioner, Blade
  Machinery Controls
    Aurora Machinery Console (IceMachineryConsole)
      Compact Console
      Expanded Console
      Design Preview - hidden in Play
      Session Confirmation (last sibling)
  UI Event System (EventSystem / InputSystemUIInputModule)
```

Controls bind by serialized references and `IceConsoleControl.Kind`, not object
names. If you rename/reorganize them, locate the control by that Inspector field.
Live labels normally refresh at 10 Hz; allow about 0.1 seconds for an external
keyboard/physical change to appear. Hydraulic release must affect the command
immediately, independently of the label refresh.

Keyboard reference: `1` lower conditioner, **Right Shift+1** raise; `7` raise tank,
**Right Shift+7** lower; `2` horizontal auger, `3` vertical auger, `4` brush, `5`
pump, `6` breaker; `[` / `]` close/open flood, **Right Shift+[ / ]** wash;
`,` / `.` shallower/deeper blade; `J` jam reset; `F` dismount/board; `WASD`
drive/walk, `Ctrl` brake; hold `E` controls, `Q` conditioner, `C` chase,
**Left Shift** rear; `X` forward, `R` standing, `Space` temporary rise;
`RMB` look; `Esc` pause; `F1` help; `F2` surface overlay; `F3` diagnostics;
`F4` performance; **Numpad 1** hide/show interface; **Numpad 7 / F5** GPS,
**Numpad 8 / F6** routes, **Numpad 9 / F7** record/save.

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

## A. Startup and asset integrity

| ID | Exact location/action | Expected result and warning signs | PASS/FAIL/BLOCKED | Notes |
| --- | --- | --- | --- | --- |
| A01 | Open primary scene; wait for import/compile; clear Console, enter and exit Play once. | No new compile errors or runtime exceptions. Record pre-existing warnings separately. | ____ | ____ |
| A02 | Before Play, expand `Aurora UI` and the hierarchy above. | Eight HUD widgets, both console layouts, confirmation and one UI Event System already exist as saved objects. No runtime-created replacement console. | ____ | ____ |
| A03 | Select both presenters and each console control; inspect Game/Canvas/raycaster/groups/widget lists. | Scene presenters reference the existing game and Canvas; controls reference their console owner. No missing list entries or Missing Script components. Reusable prefab assets may intentionally leave scene Game/Canvas empty. | ____ | ____ |
| A04 | Inspect UI Images/TMP labels in Scene view; enter Play and expand console. | No missing sprites, pink materials, empty glyph boxes, UNAVAILABLE readings or binding errors. Optional unassigned art fields are valid if the local fallback is intentional. | ____ | ____ |
| A05 | Frame the saved machine and arena before Play; compare with your reference or backup. | Existing machine, rink, camera anchors, brush rig, guide roller, fill ports and authored materials/geometry remain intact. Do not regenerate to troubleshoot UI. | ____ | ____ |
| A06 | Enter Play, remain at startup, open/close Operator's guide, then start practice. | Startup/help owns the screen; live HUD/console appears only after starting. Preview badges disappear in Play. | ____ | ____ |
| A07 | In Play Hierarchy search `t:EventSystem`; inspect its input module. | One active EventSystem with InputSystemUIInputModule; no StandaloneInputModule competitor. Driving keys do not navigate selected UI buttons. | ____ | ____ |
| A08 | Outside Play, inspect scene dirty indicator before/after selecting controls and opening Prefab Mode. | Passive inspection does not mutate the scene/prefab or write simulation state. Explicit Preview/Apply Theme operations may correctly dirty their target. | ____ | ____ |

## B. Phase 1 — Driving HUD

| ID | Exact location/action | Expected result and warning signs | PASS/FAIL/BLOCKED | Notes |
| --- | --- | --- | --- | --- |
| B01 | Start practice; inspect the top HUD. | Identity/time, speed, load, flood, wash, snow, conditioner and blade are all present once. No old header text is visible behind them. | ____ | ____ |
| B02 | Drive forward/backward briefly and stop; watch Identity and Speed. | Time advances during running; speed/direction follows the real vehicle, and returns near zero when stopped. | ____ | ____ |
| B03 | Toggle augers with `2/3`; use blade/water later on ice. | Machine Load responds to actual work. Click its widget, then `F3`; existing diagnostics opens/closes through both paths. | ____ | ____ |
| B04 | Use flood and wash separately during a pass; compare their HUD bars/liters. | Each reservoir's true remaining amount changes independently. Console valve percentages are settings, not tank fill percentages. | ____ | ____ |
| B05 | Cut snow and return to dump; inspect Snow Tank. | Snow volume and percentage follow actual collection/unloading, not the preview sample. | ____ | ____ |
| B06 | Partially raise/lower conditioner; alter blade set depth. | Conditioner position/status and blade SET/CONTACT match machinery. CONTACT may be zero with a nonzero SET when off ice. | ____ | ____ |
| B07 | In test scene Edit Mode, move Flood Water to free space and resize it; save, Play, then reopen. | Authored position/size/anchors persist; live text and fill remain functional. Old location has no residual header hit region (also test D/E). | ____ | ____ |
| B08 | Disable one widget style's shared font/color/sprite channel; edit its TMP font/size and Image; save and Play. | Local override survives. Other opted-in widgets still use shared theme. No automatic transform/font-size reset from a disabled channel. | ____ | ____ |
| B09 | Press Numpad 1 twice, then test Esc and F1 open/close. | HUD hides/restores; menus own input. No live header behind modal screens. Numpad 1 is keypad 1, not the conditioner shortcut. | ____ | ____ |
| B10 | Stop safely, `F` off machine, then board. | HUD remains visible but read-only on foot; service input retains control. Seated diagnostics interaction returns after boarding. | ____ | ____ |

## C. Phase 2 — Machinery Console

| ID | Exact location/action | Expected result and warning signs | PASS/FAIL/BLOCKED | Notes |
| --- | --- | --- | --- | --- |
| C01 | Start practice; inspect compact panel, click Open, click Close, repeat with Tab. | Compact summary and expanded controls alternate; only the current group receives input. Machinery settings persist. No new objects accumulate. | ____ | ____ |
| C02 | In Edit Mode, preview expanded; move conditioner lever to another clear location; save and Play. | Actual hit area follows it. Top/away commands RAISE; bottom/toward commands LOWER. Original position no longer operates the moved lever. | ____ | ____ |
| C03 | Hold the conditioner lever top/bottom, then release midway through travel. | Real conditioner moves in the requested direction. Command becomes neutral and mechanism holds attained position; grip springs to neutral. | ____ | ____ |
| C04 | Hold conditioner, move pointer outside its interaction area while still held, then reenter. | Command releases on exit. Reentering does not restart until a fresh press. Do not confuse physical settling with a continuing command. | ____ | ____ |
| C05 | Test tank lever top/bottom/release/exit, initially parked safely. | Same direction and neutral-hold semantics; actual tank moves when simulation permits. Training may reject an unsafe dump position with an existing message. | ____ | ____ |
| C06 | For each hydraulic lever hold it, then separately open Esc, F1, routes (F6), Numpad 1 hide, Tab, and camera quick look; resume each time. | Every transition releases the UI gesture. Returning does not silently resume it. Maintained settings stay as set unless the existing session reset changes them. | ____ | ____ |
| C07 | While holding conditioner UI, press/hold `1` or Right Shift+1; release UI while retaining key, then release key. Repeat tank with `7`. | Keyboard command takes ownership and survives UI cancellation. Releasing the key neutralizes it. UI must not reassert stale demand. | ____ | ____ |
| C08 | Click V/H augers individually, then use `3/2`, then physical switches. | Maintained ON/OFF state and actual auger motion agree in all three paths. Releasing mouse does not switch an auger off. | ____ | ____ |
| C09 | Click Brush/Pump; repeat with `4/5` and physical switches. | Each command toggles once and remains set. UI reflects authoritative state; brush moves, pump follows real prime/water rules. | ____ | ____ |
| C10 | Drag Flood through roughly a quarter turn CCW to open/CW to close; then test Wash. | Each spans 0–100%, clamps at bounds and remains independent. Mouse release holds valve setting; motion wholly inside the center dead zone does not change the setting. | ____ | ____ |
| C11 | Scroll over each valve, use bracket shortcuts, then operate physical handles. | Correct valve updates and UI/physical angle agrees; Right Shift targets wash. Scroll over unrelated empty space does not change it. Record native notch/trackpad behavior. | ____ | ____ |
| C12 | Drag Blade clockwise deeper/counterclockwise shallower through several revolutions, lifting/repressing as needed inside the wheel. | Full calibrated range spans 3.5 turns by default; depth clamps between zero and configured maximum. No one-turn wrap/reset. | ____ | ____ |
| C13 | Scroll over Blade; use comma/period; operate physical wheel. | All adjust the same continuous set depth. UI wheel/depth readout, physical wheel and HUD agree. CONTACT/engagement follows actual cutting geometry. Numeric turns can be shown in an optional Detail label. | ____ | ____ |
| C14 | Click Snow Breaker, release, click again; compare `6` and physical plunger. | One stroke per accepted activation; existing stroke cooldown/load effects remain. It is not a maintained ON/OFF switch. | ____ | ____ |
| C15 | Click Refill help and inspect notice. | Existing on-foot/hose instructions appear. It does not instantly fill tanks or change service calibration. | ____ | ____ |
| C16 | Start a valve/lever drag, move over Finish/New Session, then release. | Neither terminal action fires: press ownership stays with the original control. Repeat beginning in empty space. | ____ | ____ |
| C17 | Deliberately click New Session, click Cancel, then repeat and Confirm. | Authored confirmation blocks other pointer controls. Cancel preserves run; Confirm calls existing reset and returns to startup. No confirmation remains afterward. | ____ | ____ |
| C18 | While New Session confirmation is open, click HUD Load/world/legacy controls, then open Esc/F1 or lose focus. | No background pointer activation; transition cancels confirmation. Normal UI is usable afterward. Confirmation itself does not pause the simulation. | ____ | ____ |
| C19 | In a disposable free practice run, deliberately click Finish run. | Existing free-mode finish opens results immediately. HUD/console hides; startup/results actions still work. Default Finish has no extra confirmation. | ____ | ____ |
| C20 | Start Assisted training and click Finish before entering rink; later finish after completing its required safe return. | Early finish is rejected with training reason. Valid finish shows report. No bypass of simulator safeguards. | ____ | ____ |
| C21 | Outside Play, independently resize Flood valve, move a maintained toggle, edit a sprite and TMP style; save, Play and operate all changed controls. | Graphics/hit regions/labels remain authored, functioning and synchronized. Moving expanded controls does not rewrite compact layout. | ____ | ____ |
| C22 | Make a variant of a control prefab, replace one existing control in the test scene; assign Owner and exactly one Controls entry. | One activation produces one command. Standard Button/Toggle event lists have no manually duplicated gameplay callback. Remove/disable the replaced target. | ____ | ____ |
| C23 | Test optional `Aurora Bound Slider` in the test scene, bound to Flood/Wash/Blade, replacing its corresponding control. | Normalized slider maps to the existing range, tracks external changes, and stops drag on hide/focus/exit. No second active overlapping target. | ____ | ____ |
| C24 | In Edit Mode preview both layouts; inspect representative states, then Play and stop. | Partial conditioner, lowered tank, one auger on, partial water, blade depth and brush/pump ON are text/indicator samples only. Mechanism art retains its authored rest pose. Live startup replaces samples; stop restores saved preview. | ____ | ____ |

## D. Mouse/camera interaction

| ID | Exact location/action | Expected result and warning signs | PASS/FAIL/BLOCKED | Notes |
| --- | --- | --- | --- | --- |
| D01 | In a test layout place a UI hydraulic/valve target over a visible physical cockpit grip; keep E/Q/C/Left Shift released and click/drag UI. Aim with RMB then release it if needed. | Only UI acquires the gesture. A different physical control behind it must not change. Use held E separately for inspection: it hides the console and restores the prior view on release. | ____ | ____ |
| D02 | Move that UI away; click the exposed physical control. | Physical picking works again. Transparent/decorative text/Images with Raycast Target off do not block it. | ____ | ____ |
| D03 | Begin a physical lever/valve drag, cross an authored UI panel, then release. | Physical owner continues its existing drag/release behavior. Crossing UI does not toggle a UI action or acquire another physical control. | ____ | ____ |
| D04 | Begin UI drag, leave panel, move over physical cockpit and release. | UI hydraulic releases on exit; world does not acquire mid-drag. A fresh press is needed for world control. | ____ | ____ |
| D05 | Hold RMB, move mouse, release; repeat while hovering UI and after a UI hydraulic press. | Camera look works; UI input disables; hydraulic releases; cursor locks/restores correctly in visible Game view. Hidden-batch cursor behavior is not evidence for this row. | ____ | ____ |
| D06 | Hold a hydraulic UI command then Alt-Tab away; release mouse outside Unity; return. | Command is neutral, no stuck capture; existing focus-loss pause applies. Resume, then fresh clicks work. Repeat on-foot focus handling. | ____ | ____ |
| D07 | Hold/release E, Q, C, Left Shift; use X, R and Space. | Existing views/stance and return transitions remain correct. Console hides for held quick looks and releases gesture. HUD/other modules follow existing policies. | ____ | ____ |
| D08 | Raise/lower conditioner; operate its mounted physical controls at each height. | Picking follows moved physical geometry. UI stays independently usable; no old fixed world-space hit positions. | ____ | ____ |
| D09 | Stop safely, dismount F, walk and look, operate hose with E, board F. | Console hidden on foot, HUD read-only, no UI cursor trap. Service prompts and centered interactions work. | ____ | ____ |
| D10 | Disable the authored console presenter during a disposable Play test, then re-enable. | Legacy console returns while unavailable, authored targets hide; re-enable gives one authored console. Repeat HUD separately. Missing-binding logs should explain fallback rather than leave silent dead controls. | ____ | ____ |

## E. Layout and responsiveness

Run each resolution twice: compact and expanded. Set exact Game view resolutions,
not only Free Aspect. Check all visible content and real hit regions. Record each
combination separately.

| ID | Exact location/action | Expected result and warning signs | PASS/FAIL/BLOCKED | Notes |
| --- | --- | --- | --- | --- |
| E01 | 1366 x 768, compact; inspect HUD/console/survey/bottom strip; operate Open. | Legible text, no clipping/overlap, correct clickable bounds; only compact targets active. | ____ | ____ |
| E02 | 1366 x 768, expanded; operate each control including terminal Cancel. | All controls/readouts fit and remain reachable; smallest target still usable. Check long conditioner/blade text. | ____ | ____ |
| E03 | 1600 x 900, compact; repeat E01. | Same relationships at reference size, including edited widgets. | ____ | ____ |
| E04 | 1600 x 900, expanded; repeat E02. | No duplicate legacy console behind it, mismatched hit areas or stretched text. | ____ | ____ |
| E05 | 1920 x 1080, compact; repeat E01. | Clean scaling and correct pointer acquisition. | ____ | ____ |
| E06 | 1920 x 1080, expanded; repeat E02. | Clean scaling and correct drag/scroll magnitude independent of pixel scale. | ____ | ____ |
| E07 | In test scene set a control's anchor/pivot intentionally; resize Game view, Play, switch modes. | Authored anchor behavior governs position. Runtime does not reset RectTransform layout. Explicit indicator child animation may move/rotate only its assigned art. | ____ | ____ |
| E08 | Move both console panels away from former lower-right positions in the test scene; click the cleared areas and the new targets. | No obsolete fixed console hit rectangle. Existing unrelated survey/status/message rectangles can still legitimately own their regions. | ____ | ____ |
| E09 | Open diagnostics, GPS, routes, help, pause, startup and results at each size. | Correct visible module policy; no duplicated console/HUD on modal screens and no labels obscuring important actions. This verifies mixed IMGUI/uGUI composition. | ____ | ____ |
| E10 | Press Tab repeatedly with distinct compact/expanded positions; compare child counts/transforms before/after. | Independent layouts retain edits; inactive targets never click through; no runtime creation/destruction per mode switch. | ____ | ____ |

## F. Gameplay regression

Use free practice for an uninterrupted machinery run and Assisted training for
interlock checks. Real priming, snow, equipment position and surface state matter;
a command being ON does not guarantee productive work. Preserve normal calibration.

| ID | Exact location/action | Expected result and warning signs | PASS/FAIL/BLOCKED | Notes |
| --- | --- | --- | --- | --- |
| F01 | Back onto rink with S, use C chase; lower/raise conditioner via UI and keyboard. | Actual lift/contact and attached controls follow existing mechanism. Holds at partial height after release. | ____ | ____ |
| F02 | Run both augers, set about 0.35 mm blade, drive a short pass; observe snow and diagnostics. | Real cutting, conveyor transport, load and tank collection occur. UI changes no snow capacity, scoring or physics. | ____ | ____ |
| F03 | Open flood partway during travel, then close. | Flood consumption/application changes with the real valve and machine conditions. Wash remains independent. | ____ | ____ |
| F04 | Open wash, prime for about 10 seconds, then start pickup; compare HUD/diagnostics/surface. | Existing priming/pickup/recovery and pump consequence behavior remains. No artificial refill or independent UI water model. | ____ | ____ |
| F05 | Change blade set depth with UI/keyboard/physical wheel while raised and down on ice. | Calibrated bounds, physical blade and true contact/cut depth remain consistent; engagement schematic agrees. | ____ | ____ |
| F06 | Deploy/stow brush via all inputs; inspect arm, head and guide roller along boards. | Existing rig articulation and mechanical endpoints remain intact. | ____ | ____ |
| F07 | Exercise snow breaker with conveyor snow present; use J if existing jam conditions permit reset. | Real stroke/cooldown/conveyor response and reset rules remain; no maintained breaker state. | ____ | ____ |
| F08 | Stow equipment, return to pit, stop; raise tank, briefly lower/raise to release residue, lower fully. | Existing safe dump geometry, residue and tank/hydraulic behavior remain. No UI-only snow clearing. | ____ | ____ |
| F09 | Esc > Refill practice; F off, walk to right wall hose, E take/connect/supply; close/uncouple/return; board. | Actual two-inlet/refill/service sequence works. Console hides on foot; HUD tracks reservoir changes. Existing hose reach/inhibit behavior remains. | ____ | ____ |
| F10 | Toggle GPS with F5, open/close routes with F6; inspect existing routes without altering them. | GPS advisory, route visibility/selection and storage remain functional. Route manager excludes console input. | ____ | ____ |
| F11 | If testing recorder, create a disposable recording using F7, drive, F7 save; inspect then remove only that test recording through its normal UI. | Existing recording workflow works; established recordings unchanged. Note name/path for cleanup. | ____ | ____ |
| F12 | F2 overlays, F3 diagnostics, F4 performance, survey cell inspect and message/history controls. | Nonmigrated panels still operate; entire dashboard has not been disabled. | ____ | ____ |
| F13 | Complete a disposable scored run and inspect results; start another run. | Existing surface/water/snow/report scoring uses actual simulation; no carried-over UI command or stale binding. | ____ | ____ |

## G. Persistence

Record the exact test scene and visual changes here before saving:
Scene: __________ Lever position/size/anchors: __________ Valve size: __________
Toggle position: __________ Sprite asset: __________ TMP font/size/color: __________
HUD edit: __________ Compact/expanded layout edits: __________

| ID | Exact location/action | Expected result and warning signs | PASS/FAIL/BLOCKED | Notes |
| --- | --- | --- | --- | --- |
| G01 | In Edit Mode make the recorded lever move, valve resize, toggle move, sprite/TMP edit and one HUD edit; save scene. | Edits visible immediately and represented as deliberate prefab overrides. Shared channels disabled where a local override is intended. | ____ | ____ |
| G02 | Enter Play; operate every changed control; use keyboard/physical counterparts. | Transforms/graphics stay authored, controls still drive actual machinery, external state updates readouts. No reset to hardcoded rectangles. | ____ | ____ |
| G03 | Stop Play; inspect saved scene without applying runtime values. | Saved preview restored, all visual edits retained, no live hydraulic demand persisted. | ____ | ____ |
| G04 | Save, close Unity completely, reopen project and exact test scene. | Lever/valve/toggle/HUD positions, anchors/pivots, sprite, TMP settings and style opt-outs remain. No missing nested prefab references. | ____ | ____ |
| G05 | Repeat G02 after fresh Editor start; switch compact/expanded twice. | Binding and hit areas still correct after a cold reopen, independent layout edits preserved. | ____ | ____ |
| G06 | In a test prefab variant change shared font/state artwork; Apply Themes to Loaded UI deliberately; save, close/reopen variant. | Opted-in channels update and persist; local overrides stay local. Original machine/arena assets untouched. | ____ | ____ |
| G07 | Compare original primary scene/prefabs with backups or version-control diff after testing. | Only intended test/customization edits exist. No machine/calibration/GPS changes caused by authoring preview. Restore test-only scene overrides if desired. | ____ | ____ |

## H. Original procedural scene

Save/close your authored test scene, then open `Scenes/IceResurfacer.unity`.

| ID | Exact location/action | Expected result and warning signs | PASS/FAIL/BLOCKED | Notes |
| --- | --- | --- | --- | --- |
| H01 | Inspect hierarchy before/after entering Play. | Existing procedural launch works with no added authored HUD/console or duplicate EventSystem. Do not install authored UI here for this check. | ____ | ____ |
| H02 | Start practice; inspect header; Tab expand/collapse. | Original IMGUI header and machinery console still render and receive input. | ____ | ____ |
| H03 | Operate hydraulic, auger, flood/wash, blade, brush/pump, breaker and service help; compare keyboard/physical control. | Legacy functionality and real simulation remain intact; Phase 2 registration is absent. | ____ | ____ |
| H04 | Test RMB, quick looks, hide/show, pause/help, on-foot service and finish. | Existing legacy policies and transitions remain usable. | ____ | ____ |
| H05 | Exit Play and compare procedural scene file/version-control diff. | No unintended scene modifications. Reopen authored scene to continue. | ____ | ____ |

## I. Performance

Use the same resolution, parked pose, camera, quality settings and input for each
comparison. Warm up for at least 10 seconds; sample 30 seconds per condition.
Disable Deep Profile initially. Prefer a development player when feasible; record
Editor-only overhead separately. Do not save presenter-disable experiments.

| ID | Exact location/action | Expected result and warning signs | PASS/FAIL/BLOCKED | Notes |
| --- | --- | --- | --- | --- |
| I01 | Window > Analysis > Profiler; CPU/Rendering/UI modules, compact then expanded then Numpad 1 hidden. | Record median/upper-range frame and main-thread ms for each. No persistent hitch or large unexplained regression. | ____ | ____ |
| I02 | Same pose: disable `IceMachineryConsole` component in Play (legacy console fallback), sample; re-enable, sample. Repeat HUD separately if needed. | Compare equivalent visible modes. Record absolute and relative differences; a hidden UI comparison alone does not measure migration cost. | ____ | ____ |
| I03 | CPU Profiler Timeline/Hierarchy: inspect GC.Alloc and allocation call stacks over 30 seconds. | Record bytes/frame and refresh spikes. Formatting may allocate at 10 Hz; do not require or claim zero. Investigate new sustained growth. | ____ | ____ |
| I04 | Window > Analysis > Frame Debugger plus Rendering profiler; compare fallback/authored modes. | Record UI batches/draw calls/SetPass and TMP atlases/materials. No duplicate legacy console render behind authored replacement. | ____ | ____ |
| I05 | UI Profiler/CPU timeline: inspect Canvas.BuildBatch and layout/graphic rebuilds while idle, operating, and switching Tab. | No per-frame layout reset, repeated instantiation or unexpectedly large rebuilds. Live value/art changes may legitimately rebuild affected graphics. | ____ | ____ |
| I06 | Hierarchy counts before/after 100 Tab switches and several resets; search EventSystem/Canvas and console objects. | Stable persistent UI object counts, one input module/owner, no accumulating listeners or duplicate actions. | ____ | ____ |
| I07 | Repeat busiest real pass with snow/water/brush and diagnostics visible at 1920 x 1080. | No meaningful input latency or frame-time regression relative to equivalent legacy fallback. Record machinery workload to make comparison reproducible. | ____ | ____ |

Performance record:

| Condition | Resolution | Median / upper frame ms | Main thread ms | GC bytes/frame | UI batches / draws / SetPass | Canvas rebuild notes |
| --- | --- | --- | --- | --- | --- | --- |
| Authored compact | ____ | ____ | ____ | ____ | ____ | ____ |
| Legacy compact fallback | ____ | ____ | ____ | ____ | ____ | ____ |
| Authored expanded | ____ | ____ | ____ | ____ | ____ | ____ |
| Legacy expanded fallback | ____ | ____ | ____ | ____ | ____ | ____ |
| Interface hidden | ____ | ____ | ____ | ____ | ____ | ____ |
| Working pass | ____ | ____ | ____ | ____ | ____ | ____ |

Open failures/blockers (IDs, evidence, reproduction): __________________________

Retest date/results: __________________________

Acceptance decision and remaining exceptions: __________________________
