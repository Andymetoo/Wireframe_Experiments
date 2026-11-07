# Aurora R-54 — UI authoring Phase 2

Phase 2 makes the compact and expanded machinery console persistent uGUI content
under the existing Aurora UI Canvas. The simulation still owns every machinery
setting. The saved hierarchy owns layout, artwork, typography and local overrides.
Phase 1's Driving HUD prefab, fonts and theme asset are retained.

Use [the master Phases 1 and 2 acceptance checklist](UI-Phases-1-and-2-Master-Acceptance.md)
for a later visible Editor test. Its manual results are intentionally blank.

## Open and customize

1. Open `Scenes/IceResurfacer_AuthoredMachine.unity` in Unity 6000.4.1f1.
2. Expand `Aurora UI > Machinery Controls > Aurora Machinery Console`. In Scene
   view, enable 2D and frame the Canvas or selected control. The simulator creates
   its world camera in Play, so Scene view is the useful authoring view before Play.
3. Select the console, then **Tools > Aurora > UI > Preview Selected Console >
   Expanded** or **Compact**. This Undo-backed command changes only the two
   CanvasGroups' preview visibility. Both layouts remain saved and independently
   editable. The normal runtime starts compact, following the existing Dashboard.
4. Move/resize a control with its RectTransform. Edit its child Images, TMP labels,
   anchors/pivots, and explicit binding fields. No runtime code repositions the
   panels or control roots. Only optional, explicitly assigned mechanism artwork
   children animate relative to their authored starting pose.
5. Disable a relevant `IceUIStyle` shared channel before making a local font,
   color, sprite, font-size or interaction-state override. Save the scene, enter
   Play, start practice and use Tab to expand the live console.

The console is already installed in the primary scene. The Editor installer is
available for a deliberate installation in another authored scene: **Tools >
Aurora > UI > Install Machinery Console in Current Scene**, or **Install Machinery
Console Under Selection** for a selected RectTransform under the intended Canvas.
It uses typed scene references, Undo and normal prefab instantiation, does not
auto-save, and refuses a duplicate console. It does not regenerate the existing UI,
machine or arena. Do not use it in the procedural fallback for its regression test.

## Hierarchy and prefab inventory

```text
Aurora UI                         Existing Phase 1 Canvas/scaler/raycaster
  Driving HUD                    Existing eight live widgets
  Machinery Controls             Existing organizational group
    Aurora Machinery Console     IceMachineryConsole; no full-screen input target
      Compact Console            CanvasGroup; summary + Open console
      Expanded Console           CanvasGroup
        Close console
        Vertical Auger / Conditioner / Horizontal Auger / Snow Tank
        Flood Valve / Wash Valve / Blade Wheel
        Blade Schematic          Engagement, SET/CONTACT, optional moving art
        Board Brush / Wash Pump
        Snow Breaker / Refill help
        Finish run / New session
      Design Preview - hidden in Play
      Session Confirmation       Last sibling; hidden unless confirmation is open
  UI Event System                Existing InputSystemUIInputModule
```

The console prefab is `UI/Prefabs/Machinery/Aurora Machinery Console.prefab`.
Its saved initial bounds preserve the old reference layout: compact
`(1210,752,362,88)`, expanded `(1210,288,362,552)` in top-left 1600 x 900 units.
Those are initial authored values, not runtime positioning instructions.
The existing CanvasScaler remains Scale With Screen Size, 1600 x 900, Expand.
Rotary artwork uses center anchors and its readouts use bottom anchors, keeping
the visible center aligned with the resized interaction area without runtime layout.

New templates live in `UI/Prefabs/Machinery/Widgets/`:

| Prefab | Reusable behavior |
| --- | --- |
| Aurora Action Button | Standard Button calling a supported explicit command; default service help. |
| Aurora Maintained Toggle | Standard Toggle reflecting an authoritative maintained command. |
| Aurora Momentary Button | One accepted snow-breaker stroke per click, using existing cooldown. |
| Aurora Hydraulic Lever | Top raises, bottom lowers, center neutral; release/exit neutralizes owned command. |
| Aurora Quarter Turn Valve | Flood or wash, continuous 0–100%, 90-degree operating travel. |
| Aurora Blade Wheel | Calibrated continuous depth and 3.5-turn travel, drag and scroll. |
| Aurora Bound Slider | Standard Slider using normalized flood/wash/blade range. Optional alternative presentation. |
| Aurora Collapsible Panel | Persistent compact/expanded groups and open/close commands. |

Existing `UI/Prefabs/Widgets/Aurora Status.prefab` and `Aurora Meter.prefab`
remain the reusable status and compact-meter templates. No duplicate status/meter
system was introduced. Console controls and these templates are independent saved
objects; editing a template does not silently rebuild the installed console.
Use prefab variants or replace a console child deliberately for shared inheritance.

## Binding and operation

`IceMachineryConsole` has explicit Game, Canvas, raycaster, compact/expanded groups,
compact summary, preview badge, confirmation and Controls references. The scene
instance supplies Game/Canvas references that a reusable prefab cannot own.
`IceConsoleControl` specifies Owner and Kind, its interaction RectTransform,
optional Button/Toggle/Slider/Selectable, live labels, fill and animation children.
Names and sibling order are not gameplay bindings.

| Function | Existing authority / behavior |
| --- | --- |
| Conditioner | `Sim.SetConditionerHydraulic(+1/-1/0)`; actual lift from `Sim.Conditioner.Lift`. |
| Snow tank | `Sim.SetTankHydraulic(+1/-1/0)`; actual `Sim.TankLift`, existing dump rules. |
| Vertical/horizontal auger | `Sim.Controls.VerticalAuger / HorizontalAuger`, maintained switches. |
| Flood/wash | `Sim.Controls.FloodFlow / WashValve`, independent clamped fractions. CCW opens a UI valve; CW closes. |
| Blade | `Sim.Controls.BladeDepthMm`, bounded by `Sim.Config.MaxBladeDepthMm`; CW deepens, CCW retracts; configured `BladeWheelTurns` travel. |
| Brush/pump | `Sim.Controls.BoardBrush / WashPump`, maintained commands. |
| Snow breaker | `game.OperateSnowBreaker()`, existing single stroke/cooldown/consequences. |
| Refill help | `game.Service()`, existing instructions; no instant refill. |
| Finish | `game.Finish()` and existing `Sim.TryFinish()`. Free simulation ends immediately; assisted training validates return conditions. |
| New session | Existing `game.NewSession(false,false)`, returning to startup after explicit console confirmation. |
| Compact/expanded | Existing `game.Dashboard.Compact`; Tab still uses the existing shortcut. |
| Blade schematic | Existing `IceDashboard.BladeEngagement / BladeContactMm`, actual set depth and conditioner lift. |

No independent machine-state model is stored in the UI. A gesture records only
its device, pointer, simulation instance and owned transient demand. Maintained
states, readouts, physical switch poses and indicators all read the real simulator.
External keyboard/physical changes appear at the configurable refresh interval
(default 0.1 seconds); direct UI changes refresh immediately. Text is assigned only
when its formatted value changes. Simulated hydraulic release is not delayed by
the display refresh interval.

Keyboard/physical input keeps its existing direction and calibration. Polling runs
after keyboard and physical input, before simulation stepping. Matching keyboard
input or a physical drag wins over a stale UI gesture. Canceling a UI gesture only
neutralizes a hydraulic demand it still owns; it does not erase another input
owner's demand. Pointer up/exit, lost device, focus/pause, unavailable UI, changed
simulation, mode, camera, quick look, RMB, menu or hide transitions cancel it.

Ordinary action buttons use standard click ownership: dragging from a lever or
valve onto Finish/New Session cannot activate the button on release. New Session
now has an explicit authored confirmation (`confirmNewSession=true`). This is a
narrow UI safeguard; the actual reset command is unchanged. Finish keeps its
existing direct behavior (`confirmFinish=false`), with optional confirmation
available in the presenter. Confirmation does not change pause/gameplay state.
Only its confirm/cancel targets remain usable; menu/focus/camera transitions cancel
the pending action. Its full-screen scrim owns input only while that modal is open.

## Pointer routing and visibility

`IceResurfacerGame.PointerOverAuthoredInterface` asks the console first, updating
its current group visibility before the shared-Canvas raycast, then asks the HUD.
The existing `IceUIPointerGuard` uses current graphic raycasts and retains a UI
press until mouse release. This avoids dependence on EventSystem update order and
prevents acquiring a world control halfway through a UI drag.

When `HasAuthoredConsole` is available, `IceDashboard` suppresses only the old
CompactRack/Rack drawing and its compact/expanded fixed pointer rectangles.
Moving a widget therefore moves its hit region and leaves no old console rectangle.
Moved UI also consumes relevant legacy mouse/scroll events. Already captured legacy
or physical gestures retain their release path. The HUD still suppresses only its
own old header; the nonmigrated dashboard remains enabled.

The console is visible only during an active seated run, outside pause/help/routes,
hidden-interface, results/startup and held-quicklook states. RMB/native locked cursor
disables interaction. On-foot service remains on its existing path. The HUD remains
read-only on foot and also disables its diagnostics action during the new confirmation.
No second Canvas or EventSystem is required. Decorative graphics have Raycast Target
off; solid panel/control backgrounds intentionally own their visible areas. A hidden
CanvasGroup has zero alpha and no raycast/interaction, so layouts do not duplicate
active targets. Missing required presenter references restore the legacy console
and report the binding problem.

## Preview and artwork workflow

Preview values are ordinary saved presentation data, labeled DESIGN PREVIEW:
partial conditioner, lowered tank, one active auger, partial flood/wash, representative
blade depth, deployed brush and active pump. No ExecuteAlways simulation/preview
updater runs. Runtime entry points use `Application.IsPlaying(gameObject)` so an open
Prefab Stage is not treated as a running scene. Passive selection does not edit assets.
Play replaces samples with actual values and hides the badge; exiting restores saved
Edit Mode presentation.

The saved representative values are conditioner 45% up, tank 0% up, vertical auger
on/horizontal off, flood 45%, wash 30%, blade 0.10 mm, brush/pump on. Preview uses
text and indicator fills. Mechanism artwork keeps its authored neutral/rest pose
so runtime animation can begin from that exact pose without applying an offset twice.

For Photoshop sprites and TMP:

1. Export a transparent PNG into an experiment-local artwork folder. In Unity set
   Texture Type **Sprite (2D and UI)**, Sprite Mode **Single**, appropriate alpha and
   filter settings; Apply. Use Multiple only if intentionally authoring a sprite sheet.
2. Assign the imported sprite to the target Image's Source Image or an opted-in
   shared theme slot. Turn off that Image's style `applySprite` for a local replacement.
   Keep Raycast Target off for decorative indicators/text. The root's assigned
   Interaction Area governs lever/rotary math; replacing its graphic does not alter it.
3. For scalable panels/buttons set borders in **Sprite Editor**, Apply, then choose
   Image Type **Sliced**. For a meter retain **Filled** with the desired fill method
   and origin. Keep correct proportions with the authored RectTransform/preserve aspect.
4. Set the shared TMP font roles on `UI/Themes/Aurora UI Theme.asset`. To add a font,
   import a licensed TTF/OTF, create its TMP font asset/atlas/material with Font Asset
   Creator, and assign Primary, Secondary or Numeric. Verify glyph coverage and fit.
5. Use **Tools > Aurora > UI > Apply Themes to Loaded UI** for an explicit Undo-backed
   preview, then save. It includes inactive loaded children and the open Prefab Stage;
   unopened prefab assets are not silently changed. Runtime applies enabled style
   channels once on enable.
6. For an individual override disable `applyFont`, `applyColor`, `applySprite`,
   `applyFontSize` or `applyButtonStates` as appropriate, then edit the target component.
   Alignment, material and layout are locally authored. Font size is opt-in. Shared
   state styling now accepts a standard Selectable (including Toggle/Slider), retaining
   the existing Button field as fallback for Phase 1.
7. Selectable transitions support normal/highlighted/pressed/selected/disabled color
   or sprite states. Maintained active state uses its Toggle graphic, filled indicator
   or explicitly assigned moving graphic, independently of hover/press. Customize those
   child Images and offsets; disable `animateGraphic` to retain completely static art.
8. Create a prefab variant or duplicate a template. Place it under the intended panel,
   assign Owner and supported Kind, and add it exactly once to Controls. Remove the old
   instance/list entry if replacing it. Do not manually wire an additional gameplay
   callback into Button.onClick/Toggle.onValueChanged/Slider.onValueChanged: the presenter
   installs/removes its listeners. A new command kind needs a binding extension; reskins
   and rearrangement of existing kinds do not.

`movingGraphic` and schematic children are optional animation targets, not widget
roots. Their initial authored pose is captured once, then runtime applies configured
offsets/rotation. Leave them unassigned or disable animation for an art-only variant.
Do not bind the panel/control root as an animated indicator if its layout must stay fixed.

## Files changed and protection

Existing files changed:

- `Presentation/IceResurfacerGame.cs`: console registration, shared pointer query,
  pre-step input polling and existing release/camera integration.
- `Presentation/IceDashboard.cs`: conditional console draw/hit suppression and shared
  authored pointer ownership; all unrelated legacy modules retained.
- `UI/Runtime/IceAuthoredHud.cs`: diagnostics interaction excluded by console confirmation.
- `UI/Runtime/IceUIStyle.cs` and `UI/Editor/IceUIStyleEditor.cs`: optional Selectable
  state target plus Undo/prefab override support, preserving old Button assignments.
- `Scenes/IceResurfacer_AuthoredMachine.unity`: additive console prefab instance and
  explicit references under the existing UI organizational group.

New files (with Unity metadata):

- `UI/Runtime/IceMachineryConsole.cs`, `UI/Runtime/IceConsoleControl.cs`.
- `UI/Editor/IceMachineryConsoleFactory.cs`,
  `UI/Editor/IceMachineryConsoleValidation.cs`, and
  `UI/Editor/IceMachineryPhysicalValidation.cs`.
- `UI/Prefabs/Machinery/` main console and eight templates listed above.
- This report and `Documentation/UI-Phases-1-and-2-Master-Acceptance.md`.
- Project-root `Validation/UIAuthoring-Phase2/` staging, build/run/audit scripts and evidence.
- `Validation~/UIAuthoring-Phase2/verify_assets.py` and its static audit output,
  using the retained Phase 1 reference reader.

Before editing, 471 affected source/scene/prefab/UI/metadata/documentation files were
copied to `Backups~/UIAuthoring-Phase2-20261106`; copies were hash-verified.
`baseline-sha256.json` records all 560 pre-existing experiment files outside Unity's
ignored `~` folders. Existing source changes were checked against that baseline
before editing. Asset generation, mutation tests and Play occurred in a disposable
project with separate preferences/storage. Final scene integration is additive and
guarded against concurrent file changes and an open real Wireframe Editor.

No machine/arena prefab, camera anchor, brush/roller/fill-port assembly, simulator
calibration/core, GPS recording or scoring change is part of this migration.
The procedural scene and Phase 1 visual prefab/theme/font assets are preserved.
No branch, commit or push is created.

## Validation results

Actual Unity import, serialization, Play Mode and Direct3D rendering ran in
`Validation/UIAuthoring-Phase2/AcceptanceProject`. Licensing succeeded. The existing
simulation suite also ran separately against the current unchanged Core sources.
The scripts refuse authoring tests outside the marked disposable project.

| Check | Verified automated result |
| --- | --- |
| Existing simulation regressions | **43 scenarios passed**: hydraulic hold, interlocks, physical contact, snow/water conservation, priming/pickup, breaker, brush, service, routes, scoring and operator speed. |
| Serialized asset audit | **8,476 assertions passed** across the console and eight templates: GUID/fileID/font references, 18 control/owner bindings, shared interaction-state targets, representative preview data and resize anchors. |
| Authoring persistence | Moved conditioner lever, widened flood valve from 88 x 88 to 104 x 88, changed its TMP size/bold with shared-size opt-out, and independently moved brush toggle. Unity recorded prefab overrides, saved, reopened and retained them in real Play. |
| Fresh Editor process | A separate Unity process reopened the edited scene and verified every saved position, size, style opt-out, rotary anchor and representative preview value. |
| Exact delivered scene | Copied the final normal scene into the disposable project without changing its bytes; loaded it and entered Play, checked typed references/owners, expanded via real UI handlers, and operated/released its default lever and valve. Exiting left the scene SHA-256 unchanged. |
| Controls | Actual InputSystemUIInputModule events exercised both hydraulic directions/dead zone/release/exit, independent quarter-turn valves and wheel scroll, calibrated blade travel, maintained switches, breaker, service, finish and new session. |
| Release and ownership | Pause/help/routes/hide, compact switch, camera, focus/pause callbacks, disabled presenter, missed UI mouse-up and reset release transient demands. Keyboard takeover is retained. A canceled Button press cannot fire after hide/reopen. |
| Physical/keyboard parity | Real physical picking at conditioner lifts 0, 0.5 and 1; relocated UI over a cockpit grip blocks world acquisition; moving it away restores picking. Numeric/valve/blade keys update console readings. |
| Existing camera/operator suite | Authored camera anchors, smooth E transition, overlapping E/Q/C restore, controls at three lift heights, moved/scaled mounts, brush/pump switches and horn release passed. |
| Phase 1 regression | Eight widgets/live values, diagnostics, hide/pause/help and on-foot read-only policy passed with the console installed. |
| Procedural fallback | Entered Play in the original scene, initialized simulation, preserved legacy pointer/control paths and had no authored presenters. Scene bytes remained unchanged. |
| Rendered layouts | Twelve camera PNGs: default and edited layouts, compact and expanded, each at 1366 x 768, 1600 x 900 and 1920 x 1080. These show the authored Canvas and world; mixed IMGUI composition remains manual. |
| Protection | 560 baseline files compared; only five existing source files and the additive authored-scene integration changed. All machine/arena/core assets, original scene, and Phase 1 visual assets are unchanged. |

Durable evidence is under project-root `Validation/UIAuthoring-Phase2/Evidence`:

- [Core regression output](../../../../Validation/UIAuthoring-Phase2/Evidence/Core-regression.txt).
- [Serialized asset audit](../../../../Validation/UIAuthoring-Phase2/Evidence/Static-assets.txt).
- [UI authoring and input output](../../../../Validation/UIAuthoring-Phase2/Evidence/Machinery-results.txt).
- [Physical and keyboard parity output](../../../../Validation/UIAuthoring-Phase2/Evidence/MachineryPhysical-results.txt).
- [Existing operator integration output](../../../../Validation/UIAuthoring-Phase2/Evidence/OperatorIntegration-results.txt).
- [Protection audit and exact new-file inventory](../../../../Validation/UIAuthoring-Phase2/Evidence/Protection-audit.json).
- [Additive scene integration audit](../../../../Validation/UIAuthoring-Phase2/Evidence/Scene-integration.json).

Final successful Unity runs were `Unity-acceptance-06.log`,
`Unity-persistence-final.log` and `Unity-integrated-exact.log`, each exiting 0.
The combined `Machinery-results.txt` retains the acceptance, fresh-process and
exact-scene results. The twelve `Machinery-Default-*` / `Machinery-Edited-*` PNGs
come from the final acceptance run. Edited-layout captures are disposable test
overrides; the delivered scene keeps the normal layout.

`RunCoreRegression.ps1` reproduces the existing core suite with isolated build output
and a verified copy of its deterministic route fixture. `StageAcceptanceProject.ps1`
and `RunAcceptanceProject.ps1` handle isolated Unity runs. `CompileExperiment.ps1`
compiles against installed Unity APIs. `AuditProtection.py` checks the backup hashes;
`IntegrateConsoleScene.py` preserves the pre-existing scene records while adding the
Unity-generated console records and remapping a reused stripped-object ID.

The scene transplant preserves all 14 other existing serialized records byte-for-byte;
the existing UI prefab instance changes only its added-child list. Unity's incidental
driven Canvas overrides from the disposable save are not copied into the normal scene.
The original root, prefab links, calibration and authored machine/arena objects remain.

The final 100-call binding microbenchmark averaged **0.042 ms per refresh**.
This measures binding work, not total frame time. The console adds 106 Graphics,
bringing the authored Canvas to 146, with zero automatic layout groups.
The hidden Editor's managed-allocation counter returned
zero even for a retained 4,096-byte calibration allocation; draw counters similarly
returned zero despite rendered captures. Both are **unavailable**, not zero-cost
results. No complete frame-time, allocation or draw-call regression claim is made.
Use the master checklist's comparable fallback/authored Profiler conditions.

## Limits and remaining migrations

Native focus, hardware mouse/wheel feel, OS cursor lock and final mixed
ScreenSpaceOverlay/IMGUI composition need a visible Editor/player check. Camera
captures and injected events do not claim those checks passed. Full-frame allocation,
draw-call and Canvas rebuild comparison requires valid Profiler/Frame Debugger data;
binding timing alone does not establish absence of rendering regressions.

The GPS/survey, navigation/status strip, messages/history, route manager, diagnostics,
startup/pause/help/results and on-foot service panels remain on their existing paths.
Phase 2 does not redesign them or expand into a full interface migration. The
procedural scene deliberately retains its original interface.
