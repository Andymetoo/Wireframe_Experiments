# Aurora R-54: visual UI authoring, Phase 3

Implemented 2026-11-06 for Unity 6000.4.1f1. Experiment-relative paths below start
at `Assets/Experiments/IceResurfacer`. Open
`Scenes/IceResurfacer_AuthoredMachine.unity` and expand **Aurora UI** in Edit Mode.

## Scope and architecture

Phase 3 adds persistent Surface Survey, GPS status and Route Manager modules to
the existing Phase 1 Canvas, GraphicRaycaster, InputSystemUIInputModule,
TextMeshPro fonts, `IceUIStyleSheet` theme and explicit-reference binding system.
There is one Canvas and one EventSystem. No package, simulation model, storage
format, route-fitting rule or machine command implementation was replaced.

The original procedural scene keeps its IMGUI presentation. In the authored
scene, `IceDashboard` suppresses only Survey/SurveyDetail, TrainingCue/recording
notice and the recording/route-panel draw path when their corresponding authored
presenter is available. Their historical survey/GPS hit rectangles are also
conditional on that same ownership. Other IMGUI panels remain available.

| Module | Persistent presentation | Existing authoritative source |
| --- | --- | --- |
| Surface Survey | Header/background, independently movable map, coverage/finish, six legend swatches/labels, F2 legend, status, collapse button, patch panel/zoom/selected cell/detail | `IceSurfaceView.OverviewTexture` / `MapTexture`, `Sim.Ice`, `game.Report`, machine/layout pose, `IceRouteGuide` map window |
| GPS | Separate route, leg/direction, instruction, deviation, progress/bar, scrollable warning, visibility/routes buttons and recording label | `IceRouteGuide.ActiveRouteName`, `Progress`, `ActiveFit`, `TrainingStatus`, `Visible`, `Recorder` |
| Route Manager | Movable/resizable window, tabs, name field, recording actions, scrollable file rows, cached preview, metadata/status, raw sample scrubber/detail, guide layers/leg/rejoin, opacity/colors, confirmations | Existing library/catalog, recorder, guide-loading/activation, raw/guide preview caches, Editor edit callback |

## Saved hierarchy and prefabs

```text
Aurora UI (existing Canvas, scaler and raycaster)
  Driving HUD                         Phase 1, retained
  Machinery Controls                  Phase 2, retained
  Surface Survey
    Aurora Surface Survey             IceSurfaceSurvey
      Header background / Header / Collapse
      Survey body
        Panel background - replace artwork
        Map frame
          Live map - resize independently   IceSurveyMap : RawImage
            Map indicators and route footprints  IceSurveyOverlay
        Coverage / Finish quality / Treatment legend / F2 mode legend / Status
      Patch inspection - independently movable
        Actual cell texture / Inspected cell border / Actual cell detail
  Navigation
    Aurora GPS Status                 IceGpsStatus
      Selected route / Leg and direction / Navigation instruction
      Lateral deviation / Route progress / Progress track
      Calibration and loading warnings - scrollable
      Show hide GPS / Open route manager / Recording status
  Menus
    Aurora Route Manager              IceRouteManager
      Window background - replace artwork
      Manager content
        Header / Active GPS / Close
        Library column
          Tabs / Recording name / Recording actions
          Saved files - scrollable / Viewport / Content
            Design preview rows - excluded from storage
        Preview and actions column
          Details and actions - scrollable / Viewport / Content
            Route preview viewport / Preview orientation
              Selected raw vehicle/conditioner markers / sample number / START / END
            GPS, load, Editor, rename/delete actions
            Selected file metadata / Recording and validation status
            Raw sample and guide diagnostic sections / Open routes folder
      Per-file confirmation
  UI Event System                     existing
```

Reusable assets are under `UI/Prefabs/Navigation/`: **Aurora Surface Survey**,
**Aurora GPS Status**, **Aurora Route Manager**, **Aurora Route Row**,
**Aurora Route Action**, **Aurora Section Header**, **Aurora Status Message** and
the explicitly labeled **Survey Design Preview.png**. Scene instances supply
Game/Canvas/raycaster references; reusable prefabs intentionally leave those empty.

## Bindings and interaction

`IceSurfaceSurvey`, `IceGpsStatus` and `IceRouteManager` register on the existing
game. Live content normally refreshes at 10 Hz, using unscaled time; assignments
skip unchanged TMP strings, fill amounts and texture references. Visibility is
synchronized before shared-Canvas pointer queries and at modal transitions.
Presenters never restore authored panel positions, anchors, dimensions, font sizes
or art. Standard layout components intentionally arrange only list/scroll content.

The survey RawImage uses the existing overview in normal mode and MapTexture in
both F2 modes. It never calls Texture.Apply, creates cell objects, or maintains an
ice-state cache. The map includes the original service extent (+12 m X / -13 m Z)
and keeps +Z upward. Physical texture bounds use actual grid dimensions/cell size.
`RenderedRect`, the authored RectTransform, Canvas event camera and map world
extent drive pointer conversion. Aspect preservation letterboxes the map inside
its authored rectangle; letterbox/service-room positions do not inspect ice.
RawImage UV crop/flip coordinates are accounted for in picking. Keep UV Rect
`(0,0,1,1)` for the normal whole-rink/service overlay composition.

Hover shows the actual cell and a 16-cell patch (clamped for small fields). Click
pins it; the patch close button clears it. The patch samples the existing detailed
MapTexture through UVs; its border tracks the selected row/column. Collapse hides
the body and patch and their raycast regions, and retains the dashboard collapse
state. Sparse route/service/machine/footprint lines use one uGUI Graphic mesh,
because standard uGUI has no polyline component. This mesh contains no cell grid;
its colors, line/route widths and marker size are Inspector properties.

GPS instructions preserve the old turn, pace and lateral/conditioner-offset
calculations. Showing/hiding calls `IceRouteGuide.Toggle`; Numpad 7/F5 still use
that same method. World-space rendering, 24.384 m lookahead, yellow heading line,
depth testing, progression, calibration and clearance code are unchanged. A
hidden-GPS status and its Show button stay accessible; world guidance visibility
continues to follow the existing rules. Long load/calibration messages scroll.

The manager calls existing route operations. Its tabs use the existing
Library/Raw/Training panel state, preserving calls from Editor menus and other
code. It retains record, stop without saving, stop/save, save copy, refresh, raw
load/recovery, guide load/activation, built-in reference/test selection, alias
rename, confirmed deletion, folder opening, Editor editing, sample inspection,
preview layers, explicit leg rejoin and GPS color/opacity actions. **Edit route in
Editor** invokes the existing `EditRouteRequested` workflow; no runtime route
editor or automatic compatibility bypass was added.

`IceRouteRow` binds the full saved path explicitly. Object name, caption, sibling
index, scrolling and row order never select a file. Library rename changes the
existing alias metadata, leaving the recorded file unchanged. Confirmation
captures both path and route identity and rechecks identity before writing.
Refresh preserves selection by path; removed selections cancel confirmation.
Rows are pooled and rebound only on open, refresh, tab changes or saves, rather
than every frame. Recovery files remain inspectable but cannot activate as GPS.
Existing strict load/fit/readiness checks run at activation.

Button listeners bind once and are removed on destruction/disable as appropriate.
`IceUIClickGuard` cancels stale presses across hiding, row rebinding and drags.
File-producing operations also reject repeated callbacks in the same frame and
within a 0.4-second save interval. Preview objects have no storage identity or save
callback. Only explicit operator recording/library actions write files.

Shared pointer ownership uses `IceUIPointerGuard` against current authored
graphics. Ordinary decoration has Raycast Target off; visible panel backgrounds,
maps, scroll viewports and controls deliberately receive pointer events. The
manager retains existing modal screen ownership and releases machine controls/
look on open/close. Closing clears selected TMP fields and pending confirmation.
Text focus is recognized immediately from EventSystem selection as well as TMP
focus, before TMP's deferred activation. This prevents global/route shortcuts
from executing while typing. Driving widgets respect pause/help/on-foot/quicklook,
RMB and hide-all rules. No physical-control picking/drag algorithm changed.

## Authoring without C#

1. Work in Edit Mode. Move/resize module roots with Unity's Rect tool. Save normal
   prefab-instance overrides. Do not regenerate the machine, arena or UI to edit
   appearance. **Tools > Aurora > UI > Install Missing Phase 3 Panels** is an
   additive, Undo-backed recovery/installation command; existing panels are skipped.
2. Survey: move **Map frame** or **Live map - resize independently** separately
   from header/labels. Change Preserve Map Aspect on `IceSurveyMap` deliberately;
   keep it on for correct physical proportions. Choose top-left anchors for a
   fixed dock or stretched anchors for a map that grows with its panel. Overlay
   projection follows the map; do not independently offset the overlay unless you
   intend an art offset. Patch panel position and size are independent. The Edit
   Mode Preview Rink Size is representative; match it to the Game calibration
   when you want the preview aspect to exactly match the current live rink.
3. GPS: route, leg, instruction, deviation, progress, warning and recording labels
   are independent objects referenced by the presenter. You can reparent them into
   different groups beneath its visibility root. Keep the warning's standard
   ScrollRect/viewport/mask when lengthy messages must stay contained. Prefer
   top-left anchors for a docked card, or appropriate edge/center anchors for your
   own composition. No presenter resets them in Play.
4. Manager: resize/move its centered root or change its anchor/pivot. The two
   columns stretch to their authored proportional split; their lists scroll
   vertically. At substantially narrower widths, change the column split or
   stack the sections using ordinary RectTransforms/layout components. Its
   authored 1448 x 820 reference size and tested 1260 x 770 override fit all three
   requested near-16:9 resolutions through the existing scaler.
5. Edit **Aurora Route Row** in Prefab Mode (or make a variant and assign Row
   Prefab). Rearrange its children, change preferred height on LayoutElement,
   backgrounds, selection indicator and Selectable hover/pressed states. Change
   spacing/padding on the file Content's VerticalLayoutGroup. That layout controls
   row root sizes/positions; edit the children freely. Keep Button/title/metadata/
   validation/selection references assigned. Long captions ellipsize; full metadata
   is available in the detail pane. Runtime rows come from this saved prefab.
   Raw-preview sample markers and START/END labels are also saved objects beneath
   the cached texture. Their anchors follow existing preview projection while
   their artwork, dimensions and local label offsets remain editable.
6. Action buttons can be reordered under their standard horizontal/vertical
   groups, moved out into freely positioned subgroups or reskinned. Retain
   `IceRouteAction.Owner`, Kind and Button, and the manager Actions array. Do not
   add a second gameplay listener to Button.onClick. The standalone action prefab
   supplies artwork and the same binding; assign Owner/Kind and add the instance
   to Actions when using it in another layout.
7. Shared styling is unchanged: disable a channel on **Ice UI Style** before a
   local font/color/sprite/Selectable-state override. Font size is local by
   default. Theme application is the existing explicit Undo-backed command.
   `IceUITextHeight` only updates preferred height of wrapping scroll text when
   its content, width or size changes; disable/remove it for a fully fixed design.
8. Export Photoshop art as PNG, import as **Sprite (2D and UI)** and assign it to
   an Image or shared theme slot. Use sliced sprites for borders/panels. Keep
   decorative raycasts disabled and retained input targets intentional. Use TMP
   Font Asset Creator for imported TTF/OTF and assign your font/material normally.

Edit Mode previews are saved presentation data, with three clearly labeled sample
rows and a representative treatment PNG. No ExecuteAlways simulation or route
writer runs. Play hides preview-only rows/badges and replaces live readings.

## Validation and performance

Detailed, current results are in project-root `Validation/UIAuthoring-Phase3/Evidence`
and the successor [master acceptance checklist](UI-Phases-1-to-3-Master-Acceptance.md).
The original Phase 1/2 checklist is preserved unchanged. Test scripts require a
marked isolated copy, separate preferences and `Validation~/` route storage.

The existing deterministic simulation suite passed all **43 scenarios**. Unity
import, serialization, authoring changes, save/reopen and real Play/input-module
checks ran successfully. Six edited-layout captures cover Survey/GPS and Route
Manager at 1366 x 768, 1600 x 900 and 1920 x 1080. Camera captures include uGUI
and the world; they do not include the remaining IMGUI composition.

| Automated validation | Verified result / practical scope |
| --- | --- |
| Unity authoring | Moved/resized map, repositioned GPS, narrowed manager to 1260 x 770, changed local TMP size/bold and background; saved, reopened and used the same objects in Play. |
| Survey input | Actual InputSystemUIInputModule clicks inspected three known physical cells after resize/move; correct patch/values, hover/pin, letterbox rejection, collapse/expand and all F2 textures passed. Live simulator changes updated the existing texture. |
| GPS | Live route/leg/instructions, authored visibility button, Numpad 7/F5 and existing world guidance passed. Strict mismatch rejection retained. |
| Manager/recording | Actual Record/Stop-save clicks, duplicate suppression, raw load/sample annotations, stable 150+ file selection/scroll/reorder, tabs and TMP focus ownership passed. Real injected wheel events scrolled the file viewport. |
| File actions and Editor workflow | Disposable source/copy/alias hashes verified; separate compatible test guide activated, rename left raw bytes unchanged, Save copy published once, Delete required confirmation and removed only its captured copy; existing Editor edit callback invoked. |
| Invalid data | Malformed files could not activate/copy; incomplete recovery could be inspected but could not activate GPS. An unchanged Andy guide copy with mismatched GuideRollerX was rejected without revalidation or rewrite. |
| Input coexistence | Canceled save press could not fire after hide/reopen. Physical lever picking remained available; relocated survey blocked it only at the new authored location. Moving it away restored access. RMB released survey ownership. |
| Regression/persistence | Phase 1 live widgets and Phase 2 real machinery command passed smoke checks. Procedural scene initialized, kept keyboard controls, contained no authored modules and retained its bytes. Fresh Editor reopening and exact delivered-scene Play passed. |
| Delivered assets | Additive scene installation preserved 19 other serialized records byte-for-byte. Only the existing UI instance's added-child list changed. Existing console child retained. |

The final 200-call binding microbenchmarks measured **0.0111 ms per survey+GPS
refresh**, and **0.1872 ms per manager refresh with 160 files**. These include
formatting/binding work, excluding rendering and file-list refresh. The survey
adds 35 Graphics, GPS 18, and the tested manager has 912 including its pooled
rows and hidden previews. The retained 4,096-byte allocation calibration returned
zero, so this Editor's allocation counter is **unavailable**, not evidence of
zero allocations. No full-frame, draw-call, Canvas rebuild or GC regression claim
is made. Profile comparable authored/fallback/hidden conditions using the manual
checklist. Existing file-catalog parsing remains synchronous on explicit refresh.

Durable evidence: [validation output](../../../../Validation/UIAuthoring-Phase3/Evidence/Phase3-results.txt),
[Core regression](../../../../Validation/UIAuthoring-Phase3/Evidence/Core-regression.txt),
[summary and capture inventory](../../../../Validation/UIAuthoring-Phase3/Evidence/Validation-summary.json),
[protection audit](../../../../Validation/UIAuthoring-Phase3/Evidence/Protection-audit.json),
and [scene integration](../../../../Validation/UIAuthoring-Phase3/Evidence/Scene-integration.json).
Final successful runs are `Unity-acceptance-annotations.log`,
`Unity-commands-annotations.log`, `Unity-persistence-annotations.log` and
`Unity-integrated-annotations.log`; each must exit 0. Older development/import/
infrastructure attempts remain under project-root validation logs and are not
passing acceptance evidence.

One existing guide copy, `Andy_Try_1___recorded_track__20261103_050845_a58dcab3`,
has `GuideRollerX = -1.00099993` while this authored scene reports `-0.827000022`.
The existing strict loader rejects that mismatch. Neither geometry nor saved
guides were changed to make this test pass. Successful activation was tested with
a separately recorded straight-route fixture using the current simulation config.
The established Editor revalidation/save-new-copy workflow remains available for
your deliberate use.

## Protection, exact changes and remaining work

Before changes, **594 existing experiment files** were backed up and SHA-256
verified under `Backups~/UIAuthoring-Phase3-20261106`; a separate manifest protects
all **31 saved recording/guide/draft files**. Existing local Phase 1/2 changes were
the baseline. No commits, branches or pushes were made.

Existing source changes are limited to `Presentation/IceDashboard.cs`,
`Presentation/IceResurfacerGame.cs` and `Presentation/IceRouteGuide.cs`: conditional
legacy ownership, explicit registration, shared pointer synchronization and text
focus/cursor integration. The authored scene receives three additive prefab
instances under the existing UI. Unrelated serialized scene records and existing
Phase 1/2 layouts are retained. The exact hash audit/new-file inventory is
`Evidence/Protection-audit.json` and scene-record audit is `Evidence/Scene-integration.json`.

New runtime sources: `UI/Runtime/IceNavigationUI.cs`, `IceSurveyMap.cs`,
`IceSurfaceSurvey.cs`, `IceSurveyOverlay.cs`, `IceGpsStatus.cs`, `IceRouteRow.cs`,
`IceRouteAction.cs`, `IceRouteManager.cs`, `IceUIClickGuard.cs`, `IceUITextHeight.cs`.
New Editor sources: `UI/Editor/IceNavigationFactory.cs`, `IceNavigationValidation.cs`.
New assets are the Navigation folder listed above, Unity metadata, this report and
the successor checklist. Project-root `Validation/UIAuthoring-Phase3/` contains
isolated staging/run/compile/regression/integration/audit tools and evidence.

No machine/arena prefab, camera anchor, brush/roller/fill-port assembly, tuning,
Core simulation/scoring code, calibration or original procedural scene is changed.
Saved operator routes/guides, including Andy_Try_1, retain their original hashes.

Native hardware mouse/wheel feel, OS focus/cursor lock, full visible-Editor
authoring acceptance and final mixed IMGUI/uGUI composition still need the
operator's manual sign-off. Whole-frame Profiler/Frame Debugger comparisons of GC,
Canvas rebuilds and rendering remain manual; binding timings do not certify those.
The pooled file list is tested with 150+ entries; extremely large libraries may
benefit from virtualization in a future phase. Catalogue refresh still uses the
existing eager file-reading implementation. No new per-cell UI is created.

Remaining migrations: bottom navigation/status strip, notifications/history,
pause/help/startup/results, diagnostics, on-foot water service and other legacy
overlays. Existing Editor route-authoring tools and world-space GPS remain on
their existing invocation/rendering paths.
