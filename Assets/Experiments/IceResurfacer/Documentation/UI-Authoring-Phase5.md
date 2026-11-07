# Aurora R-54 — UI authoring Phase 5

Phase 5 completes the ordinary screen UI in
`Scenes/IceResurfacer_AuthoredMachine.unity`. Service, messages/history, physical
control hints, drive warnings, bottom actions, F4 and projected GPS strokes now
have saved editable uGUI objects. Existing systems still own their content and
commands. The procedural scene retains its legacy presentation.

Start visual work with the [authoring handbook](Aurora-UI-Authoring-Handbook.md).
The [master acceptance](UI-Phases-1-to-5-Master-Acceptance.md) contains **295 detailed
checks: all 218 earlier rows preserved verbatim and 77 new unsigned manual checks**,
plus 20 quick smoke references. Automated evidence does not sign manual rows.

Paths below are relative to `Assets/Experiments/IceResurfacer`, unless stated.
Engine: Unity 6000.4.1f1, URP, Windows. Validation date: 2026-11-06 local time
(some UTC log/capture timestamps are November 7).

Subsequent requested focus change: clicking another Editor panel or alt-tabbing
no longer pauses or opens the menu. Aurora enables
[background running](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/Application-runInBackground.html)
for its lifetime, restores the previous setting on unload, and ignores gameplay
input while unfocused. Explicit Pause remains available. Two existing camera/drive
validation fixtures now restore focus before their next input sequence. Compilation
and `Unity-Input-focus-change-01.log` passed; native alt-tab remains a manual check.
`Evidence/Focus-change-summary.json` records this follow-up separately from the
original Phase 5 acceptance snapshot below. D06's historical auto-pause expectation
is superseded by the updated policy note in the current master checklist.

## Initial inventory and final disposition

The latest Phase 1–4 documents, 218-check master, saved assets, theme, dashboard,
Game, service and physical-input sources were inspected. A real isolated Phase 4
`VerifyIntegrated` smoke passed before modifications; its log and assertions are
preserved as `Validation/UIAuthoring-Phase5/Evidence/Initial-Phase1-to-4-*`.

Categories: **1** persistent editable uGUI; **2** runtime content from editable
templates; **3** retained specialized developer tool; **4** superseded legacy path
disabled in the complete authored scene. A retained fallback is deliberate.

| Remaining presentation/source | Category and disposition |
| --- | --- |
| `IceDashboard.ServicePanel` | 1/4: seven saved fields plus editable instructions/background; old service drawing and broad on-foot y>700 hit band excluded when Phase 5 is available |
| `Guidance`, critical faults and ordinary notices | 1/4: separate routine and critical templates, same dashboard history/policy and simulation fault predicates |
| `MessageLog` and routine-message preference | 1/2/4: saved history scroll/advice/actions; bounded editable row instances; same 32-entry owner, order and timestamps |
| `DriveWarning` | 1/4: saved warning bound to the existing drive-inhibit reason and throttle condition |
| Physical-control/legacy hover hint | 1/4: saved context template reads `IcePhysicalControls.Hint`; authored machinery already owns its own controls/hints |
| `StatusStrip` and Routes/Messages/F1/Pause buttons | 1/4: saved texts/buttons call the same methods; no duplicate legacy strip/hit rectangle |
| F4 performance summary | 1/4: saved TMP/Image; detailed F3 performance diagnostics remain developer content |
| Recording and GPS status | Already Phase 3 editable status; bottom recording/distance text migrated; no second notification owner |
| `RouteSilhouetteContinuation` / `GpsMachineAlignment` screen strokes | 1/4: saved `IceGpsProjectedGraphic`, same bounded visibility/occlusion/geometry callbacks; legacy strokes retained only for fallback |
| Header, rack, survey, training cue, Welcome/Pause/Help/Results | Already Phases 1–4; per-module legacy guards retained, full authored dashboard fast return added |
| `RouteManagerPanel`, `RouteRecordingPanel`, `RouteRecordingPreview`, `TrainingGuidePanel`, `TrainingGuideView` | Ordinary route UI already replaced in Phase 3; their IMGUI helpers remain for the procedural fallback, not a second authored route window |
| `IceDashboard.Diagnostics`, F3 collision/contact overlay | 3: specialized diagnostic text/world lines, active-only old diagnostic regions; rebuilding the tools is outside the normal UI migration |
| F2 treatment/debug textures | 3 for generated diagnostic data; displayed through the existing editable survey UI, no new screen or hit region |
| `IceLayoutPlayerProbe`, `IcePerformanceProbe`, Editor validators and route-fit windows | 3: explicit development tooling, not normal session screens |
| `IceUITheme` GUI drawing helpers and `IceResurfacerGame.OnGUI` | Retained dispatch/helpers for diagnostics and fallback. Normal complete authored UI returns before legacy style/texture initialization |
| World labels from `IceWaterService`, `IceWorldBuilder` and authored machine parts | Outside screen UI: procedural TextMesh/material changes, hose/ports/world effects retain their existing owners; limitations listed in the handbook |

The source audit covered every runtime experiment C# file containing `GUI`,
`GUILayout` or `OnGUI`, plus world text and object creators. No ordinary remaining
screen was reclassified as a developer tool merely to avoid migration.

## Saved components and bindings

The added assembly is under **Aurora UI / Messages / Aurora Operator Interface**.
It uses the existing Canvas, 1600×900 Expand scaler, GraphicRaycaster, Input System
module and EventSystem. There is no new Canvas, input stack, theme or message bus.

`IceServiceReadout` separates hose/connection, reach/tautness, supply percentage,
actual flow, overflow, targeted prompt and current notice. `IceWaterService` still
owns the hose, target, supply, routing and effects; the UI does not simulate water.
F/E, walking, camera, flood/wash capacities, supply and breakaway behavior remain
the existing code. Only its click-ownership predicate changed to consult all
visible authored graphics at the same world-aim screen point. E remains independent.

`IceDashboard.ObserveMessages` preserves the original active-driving observation
gate, session clear, same-notice deduplication, timestamps, 32-entry retention and
eight-second routine lifetime. `CriticalMessage` extracts the original priority:
jam, tank full, pump damage, then deep snow. Routine-off never suppresses it.
Normal banners retain the previous abbreviated text; history retains full messages.
Visual fade duration is editable without extending the original lifetime.

`IceOperatorOverlay` refreshes visible values at 10 Hz, updates text only when
different and refreshes history only on its owner's revision. Rows grow only to
the existing 32-entry limit, then reuse/hide. Button listeners bind/unbind with
the presenter. Existing click cancellation prevents an interrupted press firing
after reopening. Service pointer ownership observes intervening releases even
when no world-picking call occurred on the release frame.

`IceGpsProjectedGraphic` stores bounded strokes in an array and uses the existing
source visibility queries. It dirties its mesh only when geometry/color changes.
Widths, colors/borders, opacity, tint, material and Edit Mode sample points are
editable. Keep its full-view RectTransform for physical alignment; moving that
viewport intentionally moves the projected drawing. It never raycasts.

No controller restores RectTransform positions, fonts, sprites, margins, layout
spacing or button artwork every frame. Factory coordinates only initialize missing
assets. The normal Editor installer adds a missing assembly once, with Undo; it
does not save, regenerate earlier UI or replace existing assets.

## Visibility and input ownership

| State | Phase 5 behavior |
| --- | --- |
| Welcome, Pause, Help, Results, Route Manager | Ordinary Phase 5 groups hidden; no Phase 5 action/world-pointer ownership; existing modal controller retains precedence |
| Active driving, compact or expanded | Status/actions, applicable routine/critical/hint/drive warning and optional F4; history only when requested |
| Active on foot | Service and optional F4; driving strip/history/notices hidden; actual-target prompt and service effects remain authoritative |
| Numpad 1 video mode | All ordinary Phase 5 groups/projection hidden; service simulation and E remain functional |
| RMB/free look or lost focus | Existing Game input policy retained; no interactive driving UI; click gestures canceled on visibility/focus transitions |
| F3 diagnostics | Existing developer diagnostics still draw; their old regions are active only with diagnostics enabled |

Service, history and action graphics intentionally block their visible regions.
Informational notice/hint/warning/F4 artwork and decorative labels/icons default
to Raycast Target off. The Game synchronizes earlier presenters before the final
Phase 5 shared-Canvas raycast, avoiding stale visibility during dismount/modal
transitions. No old bottom strip, history rectangle or on-foot screen band remains
active when its authored replacement is available.

If a presenter becomes unavailable, its guarded legacy equivalent can provide
recovery. The original `Scenes/IceResurfacer.unity` contains no authored dependency;
its serialized bytes are unchanged. The complete authored scene uses no ordinary
legacy drawing while all seven saved modules are available and F3/audit is off.

## Shared visual system and authoring

The sole shared asset remains `UI/Themes/Aurora UI Theme.asset`. New actions and
notice panels are true variants of existing menu assets, which themselves inherit
the earlier reusable controls. Primary/Secondary/Numeric fonts, semantic colors,
panel/button sprites and Selectable state channels use existing `IceUIStyle`.
New Reach/Supply/Flow fields opt into Numeric. Existing meter geometry/fills and
local typography choices were inspected and preserved rather than rewritten.

Channel opt-ins permit local fonts, sizes, sprites, tints and button transitions.
The drive warning's original alert face and GPS world-guidance colors are explicit
editable local defaults. Shared theme application is explicit and Undo-backed;
it respects disabled style channels and does not move objects. Old prefabs, theme,
fonts, meter art and layout overrides remain byte-identical to the Phase 5 baseline.

Operator Preview provides driving, water service, history and warning samples.
It coordinates with Phase 4 Screen Preview and restores transient CanvasGroup
values before Save, Play, reload, close or quit. It does not run service gameplay,
alter history/PlayerPrefs or write routes. The handbook includes PNG import,
nine-slicing, TMP font assets, variants, state art, anchors and recovery procedures.

## Asset protection and exact changes

Before editing, **677 experiment files** were copied and SHA-256 verified under
`Backups~/UIAuthoring-Phase5-20261106`; all **31 route hashes** were recorded.
Tests use `Validation/UIAuthoring-Phase5/AcceptanceProject`, with a required marker,
isolated application identity/preferences and disposable route storage. Main
machine/arena/anchors/ports, physics/tuning/Core and existing UI assets are unchanged.

Serialized integration checks running Unity project paths, lock ownership and
expected hashes. It installed 17 new prefab/metadata files and additively attached
one root. All 30 unrelated existing scene records are byte-identical. In the old
UI instance, only `m_AddedGameObjects` changed; an existing unused ID/reference was
preserved and the new stripped reference remapped to avoid collision. The final
scene SHA-256 is `e0ea510c5a50981d0f53ab8c26045a25efccc1b2d8daa2a62901e8330d675c74`.

The final two new-prefab raycast corrections were separately backed up, guarded
against concurrent edits, copied with both projects closed and hash-verified.
No earlier prefab was overwritten. Installation refuses a changed baseline;
the additive Undo-backed Editor installer is the recovery path for another scene.
No branch, commit or push was created.

Exactly 15 baseline files changed:

| Files | Change |
| --- | --- |
| `Scenes/IceResurfacer_AuthoredMachine.unity` | Additive saved Phase 5 instance/references only |
| `Presentation/IceDashboard.cs` | Shared existing message observation/policy, guarded retired drawing/hit regions, authored fast return |
| `Presentation/IceResurfacerGame.cs` | Presenter registration, visibility/raycast integration and message observation |
| `Presentation/IceWaterService.cs` | Full authored pointer exclusion for clicks |
| `Presentation/IceRouteGuide.cs` | Projection callback and explicit temporary query disposal |
| `Presentation/RouteSilhouetteContinuation.cs`, `Presentation/GpsMachineAlignment.cs` | Emit unchanged qualified segments to the authored graphic; query lifecycle cleanup |
| `UI/Runtime/IceAuthoredHud.cs`, `IceMachineryConsole.cs`, `IceSurfaceSurvey.cs`, `IceGpsStatus.cs` | Skip automatic content formatting while hidden; retain visibility/input polling and explicit RefreshNow |
| `UI/Editor/IceMenuPreview.cs` | Restore operator preview before taking preview ownership |
| `UI/Editor/IceUIInputValidation.cs`, `IceMachineryPhysicalValidation.cs` | Also permit marked Phase 5 validation path; existing test behavior unchanged |
| `Editor/IceServiceValidation.cs` | Optional assertion observer and geometry-capture switch; original default workflow unchanged |

New runtime scripts under `UI/Runtime/`: `IceOperatorOverlay`, `IceOperatorAction`,
`IceOperatorMessage`, `IceServiceReadout`, `IceMessageHistoryRow`,
`IceGpsProjectedGraphic`. New `UI/Editor/` scripts: `IceOperatorFactory`,
`IceOperatorPreview`, `IceOperatorValidation`, `IceOperatorEditorLifecycle`.
Each has a Unity-generated `.cs.meta`.

Eight prefabs under `UI/Prefabs/Operator/`, with metadata: **Aurora Operator
Interface**, **Aurora Operator Notice**, **Aurora Critical Notice**, **Aurora
Context Hint**, **Aurora Drive Warning**, **Aurora Operator Action**, **Aurora
Message History Row**, **Aurora Projected GPS**. Three new documents are this
report, the handbook and Phase 1–5 master. No final artwork was created.

Validation scripts/evidence live under `Validation/UIAuthoring-Phase5/`; disposable
project/compiler/build directories are ignored. `Prepare`, `Stage`, `SyncSources`,
`Run`, `SelectTestScene`, `IntegrateScene`, `InstallVerifiedAssets`,
`FinishVerifiedAssets`, `CompileExperiment`, `RunCoreRegression`, `CreateChecklist`,
`AuditProtection` and `CollectEvidence` record reproducible guarded steps. The
experiment `.gitignore` excludes generated validation outputs. Complete exact file
paths/hashes and the Phase 5 baseline source patch are in `Evidence/Protection-audit.json`
and `Evidence/Existing-source-changes.patch`.

## Automated validation and rendered evidence

Final accepted Unity runs must exit **0**, not merely print PASS before shutdown.
`Validation/UIAuthoring-Phase5/Evidence/Validation-summary.json` lists accepted
logs, assertion-record counts and capture filenames. Counts include repeated
regressions and are not distinct manual acceptance checks.

| Validation | Actual result/evidence |
| --- | --- |
| Initial Phase 1–4 smoke | PASS before Phase 5 changes; copied initial log/assertions |
| Existing deterministic Core suite | PASS, 43 scenarios; `Core-regression-latest.log` |
| Runtime and Editor static compilation | PASS, existing warnings retained; `Compile-final.log`; actual Unity import also exercised |
| Deliberately edited authoring run | PASS, `Unity-Acceptance-05.log`; moved service/prompt, resized critical warning, local TMP font asset and replacement PNG; real service/fault events |
| Fresh-process persistence | PASS, `Unity-Restart-01.log`, and final `Unity-Projection-render-02.log` independently reopens edited archive after Play |
| Exact delivered integration | PASS, `Unity-Delivered-final-02.log`; full service/messages/input/visibility/captures; exact scene bytes preserved |
| Dedicated projected GPS render/lifecycle | PASS, `Unity-Projection-render-02.log`; nonzero existing occlusion fixture, segment/budget parity, no unchanged-mesh dirties, zero remaining temporary query colliders after Play, clean process exit |
| Phase 4 menu regression | PASS, `Unity-Menus-regression-01.log`; existing modes/settings/help/results and click cancellation |
| Phase 1/2 input and physical cockpit regression | PASS, `Unity-Input-regression-01.log`; actual Input System handlers, moved widgets, keyboard parity, hold/drag/release and injected RMB |
| Phase 3 route/storage regression | PASS, `Unity-Routes-regression-01.log`; strict Andy rejection, compatible disposable guide, alias/copy/delete/recovery/error/scroll actions and physical picking |
| Phase 3 survey/GPS integration regression | PASS, `Unity-Navigation-regression-01.log`; navigation bindings, map interaction, modal ownership and six combined renders |
| Procedural fallback | PASS in authoring run; no saved authored root, F dismount, same history, Pause/Help, unchanged procedural scene bytes |
| Asset/route/checklist audit | PASS, `Protection-audit.json`: all 677 backup hashes verify, 662 baseline files unchanged, 15 expected changes only, 31 routes and 218 unchanged historical rows |

Source `git diff --check` passes. The whole tracked diff reports trailing spaces
in Unity's empty serialized YAML scalar fields (including earlier phase changes);
those serialized bytes were preserved instead of normalizing unrelated assets.

The service suite walks with injected WASD and existing world handlers, aims at
real holders/ports/valves, takes/returns the nozzle, fills flood and wash, produces
actual finite-tank overflow, tests taut recovery, driven breakaway and moving
dismount. It uses the prior service validation's isolated setup and aim helpers;
it does not replace walking with teleports during the interaction sequence. The
observer verifies live prompt, flow, supply, hose and notice fields against that
state. Actual dry-pump simulation steps produce pump damage/mistakes and critical
content; routine timing boundary tests deliberately control only the UI clock.

Additional checks cover moved service panel versus the actual centered aim point,
old y>700 band absence, hidden groups, E while UI hidden, six real button commands,
interrupted presses, bounded history, preview restoration, explicit fonts/sprites,
missing bindings and scene persistence. Test setup and accelerated simulation are
fixtures, not a claim of a native uninterrupted end-to-end drive.

**51 Phase 5 captures**: eight combined states × three resolutions × default and
edited layouts (48), plus three dedicated GPS projection renders. Sizes are
1366×768, 1600×900 and 1920×1080. States: DrivingCompact, SurveyGPS,
ExpandedWarning, History, Routes, Pause, Results, OnFootOverflow. DrivingCompact
already has GPS visible in this fixture, so its SurveyGPS image can match. Captures
include the world and complete authored Canvas, not isolated individual panels.
Camera-render captures omit IMGUI composition; legacy suppression is checked by
availability/draw guards and input tests, with native mixed rendering still manual.

Representative renders at all three sizes were visually inspected. New default
service/warning/history text and actions are legible; Pause/Results/Routes hide
ordinary overlays. History intentionally covers part of GPS as the old nonmodal
window did. F4's background touches/overlaps the service panel's lower-left edge,
without covering service text in the tested defaults. The deliberately widened
edited warning reaches slightly into the expanded-console edge; its foreground
priority is retained. These are editable composition decisions, not reasons to
silently rearrange the user's UI. Larger fonts/longer messages require row height
and spacing review. Final artwork and every rendered state remain unsigned manual
acceptance, even where an automated image exists.

## Performance and lifecycle

Clear avoidable work was removed: four existing hidden presenters no longer
schedule text formatting; normal complete authored OnGUI does not initialize
legacy styles/instrument textures; unchanged history does not instantiate rows;
unchanged GPS geometry does not rebuild its mesh. Graphic raycasting reuses the
existing shared Canvas and pooled pointer helper. No runtime private material or
texture was added by Phase 5 UI; capture textures are released by the harness.

Final isolated microbenchmarks (`Unity-Delivered-final-02.log` assertions):

| Operation | Measured milliseconds/call |
| --- | --- |
| Phase 5 visibility, visible compact / expanded | 0.00159 / 0.00158 |
| Phase 5 visibility, hidden compact / expanded | 0.00092 / 0.00094 |
| Authored GPS query + cached segment sink, dedicated render run | 0.31542 |
| Existing GPS visibility/alignment query reference, same run | 0.40313 |

These are short warmed loops on this machine, excluding simulation/rendering.
GPS uses the same bounded queries; timing variation is not a demonstrated GPU or
whole-frame speedup. The retained 4096-byte managed-allocation calibration reported
zero, so that counter is **unavailable** here. No zero-allocation claim follows.
Canvas mesh dirty callbacks and hierarchy bounds were actually asserted, but
full-frame Canvas/TMP costs, draw calls, batching, GC and CPU/GPU profiling still
require the native Profiler under comparable visible/hidden workloads. Captured
F4 FPS reflects batch cadence, not a native performance benchmark.

One development GPS capture printed passing assertions and then crashed during
native Unity shutdown (`Unity-Projection-render.log`, exit -1073741819). The stack
involved cleanup of the isolated GPS query MeshColliders. That run is **rejected**.
Query disposal now deactivates/destroys owned temporary objects before Editor Play
physics teardown, is idempotent and runs on disable/quit/reload/ExitingPlayMode.
Clean reruns assert no retained query colliders and require process exit 0. This
changes temporary presentation-query ownership, not vehicle or ice collision
geometry. Async scene unload is retained after owned-object cleanup; Unity's
[UnloadSceneAsync documentation](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/SceneManagement.SceneManager.UnloadSceneAsync.html)
describes scene-object destruction. The observed crash is not asserted to be a
known external Unity defect. Earlier failed development logs are retained rather
than counted as final passes.

## Remaining manual acceptance

Use **MASTER: TEST THIS NOW** first. Native mouse/cursor locking, focus/alt-tab,
OS drag/release/DPI behavior and visible Editor or standalone input remain manual;
the background Editor reports CursorLockMode.None. Injected relative look motion
and UI exclusion passed. F2/F3 mixed rendering and complete guided/scored native
runs also need operator acceptance. Core completed-run scenarios and representative
result formatters do not certify a full manual drive.

Review custom artwork/fonts/button states at every target size, long history and
route scrolls, physical aiming after visual edits, on-foot cursor restoration and
full-frame profiling. World geometry/effects/audio/camera/procedural TextMesh
styling remain outside this screen-UI migration, as listed in the handbook. No
remaining known functional migration blocker requires a redesign.
