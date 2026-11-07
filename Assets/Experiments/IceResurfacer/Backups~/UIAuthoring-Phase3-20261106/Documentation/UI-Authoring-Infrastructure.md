# Aurora R-54: visual UI authoring, Phase 1

Implemented 2026-11-06 for Unity 6000.4.1f1. Open
`Assets/Experiments/IceResurfacer/Scenes/IceResurfacer_AuthoredMachine.unity`.

## Start authoring

1. In **Edit Mode**, expand **Aurora UI > Driving HUD**. Select **Flood Water**.
2. Use the Rect tool (`T`) to move and resize its RectTransform. Its background,
   Label, Value, Detail, Meter Background and Fill are real saved UI objects.
   Each indicator moves independently. Keep widgets beneath Driving HUD, including
   any subgroups you create, so its visibility CanvasGroup applies to them.
3. Select a child text object to edit its TMP font size, alignment, material and
   RectTransform independently. These are not reset by the data binding.
4. For a local font, color or sprite override, first disable the corresponding
   **Apply Font / Apply Color / Apply Sprite** channel on that object's
   **Ice UI Style** component, then edit the TMP or Image component. Font size is
   local by default. Disable **Apply Button States** before locally changing a
   button's transition/state graphics. Removing a style component also leaves
   all its visual properties locally authored.
5. Save the scene. These are normal prefab-instance overrides. Use Unity's
   Overrides/Apply workflow only when you want a change to become the reusable
   prefab's default. Enter Play: the same saved objects receive live readings.

Do not run a scene-generation or asset-generation command to author this HUD.
There is no runtime layout builder, name-based widget lookup, layout group,
ContentSizeFitter or script that restores RectTransforms. The initial construction
script in `Validation~/UIAuthoring-Phase1/create_assets.py` is a one-time development
artifact that refuses to overwrite its output; it is not the editing workflow.

## Approach and scope

uGUI matches this project's required Scene/Inspector/RectTransform authoring
workflow and existing GameObject/prefab workflow. It was already installed
(`com.unity.ugui` 2.0.0), with TextMeshPro supplied by that package. Input System
1.19.0 was also installed. No package dependency was added. UI Toolkit would
introduce UXML/USS authoring and a second layout model without an advantage for
this pilot's freely positioned, sprite-based GameObject widgets.

The existing top header is the only migrated module. The starting palette follows
`IceUITheme`; this is infrastructure and placeholder typography, not a proposed
final redesign. The old meters had captions and numbers but did not actually draw
their ratio as a bar. The pilot adds editable fill Images as well as the numbers.
The original procedural launch scene continues to use its old header.

## Existing UI inventory

The source audit excluded historical `Backups~` and `Validation~` copies.
`IceResurfacerGame.OnGUI()` calls the non-component `IceDashboard`. The dashboard
uses a centered, uniformly fitted 1600 x 900 IMGUI matrix and hardcoded rectangles.
`PointerOverInterface()` duplicates panel regions for physical-control picking.
`IceUITheme` supplies static colors, GUI styles and temporary generated textures.
There were no existing screen Canvases, EventSystems, UI prefabs, UI Toolkit
documents or imported TMP font assets in the experiment before this pass.

| UI group | Existing implementation | Editable asset boundary | Dynamic content and code retained |
| --- | --- | --- | --- |
| Persistent driving HUD | `Header`, `StatusStrip` | Independent identity, speed, load, flood, wash, snow, conditioner and blade widgets; later bottom navigation strip | Simulator readings, formatting and existing commands. Only the top header is migrated now. |
| Compact/expanded machinery controls | `CompactRack`, `Rack`, `MaintainedLever`, `HydraulicLever`, `WaterHandle`, `Dial`, `Toggle`, `BladeSchematic` | Console/rack frames, controls, toggles, dial art and schematic viewport prefabs | Existing command directions, limits, hydraulic press/release ownership and schematic geometry. Physical machine controls remain independent. |
| Surface survey/minimap | `Survey`, `SurveyDetail`, `MapKey`, `MapPoint`, `DrawRouteMap`, `DrawFootprintMap` | Frame, RawImage, collapse button, legend template, coverage labels, cell tooltip | Existing surface textures, map projection, ice-cell selection, vehicle/conditioner markers and route geometry can remain generated inside an editable viewport. |
| GPS and route management | `IceRouteGuide`, `TrainingCue`, `RouteManagerPanel`, `RouteRecordingPanel`, `TrainingGuidePanel`, `RouteRecordingPreview`, `TrainingGuideView`, `RouteLibraryCatalog` | Advisory card, manager/modal panels, route-row templates, inputs, tabs, buttons, preview viewport | Library population, recording, storage, recovery, fitting, progress, preview paths and status remain in existing code. No route file changes. |
| Pause/settings/help | `Pause`, `Help`, game pause/help and preference methods | Panels, buttons, sliders and instructional text | Pause ownership, saved speed preference/snapping, eye-height and existing commands. Static help prose can become serialized content. |
| Startup/end of run | `Welcome`, `Results`, `ReportLine` | Mode buttons, report cards and report-row templates | `NewSession`, report values and simulator scoring remain authoritative. |
| Notifications/status | `Guidance`, `DriveWarning`, `NextAction`, `SnowStatus`, tooltip, history, recording notice | Warning/toast panels, tooltip, history dialog and message-row prefab | Message priority, history limit, expiry/fade and state-derived advice. Later separate message collection from IMGUI drawing. |
| Diagnostics/debug | `Diagnostics`, performance badge, `IcePerformanceMeter`, `IceCollisionOverlay`, F2/F3/F4 | Diagnostic metric-row templates, sheets and badge | Profiling/contact/collision geometry and debug renderers can remain code-driven. The migrated load button invokes the existing F3 diagnostics action. |
| On-foot water service | `ServicePanel`, `IceWaterService.Status/Prompt/HandleInput` | Status, instructions and prompt panel | Walking, cursor ownership, E interactions, F boarding, hose/supply simulation and physical service targets stay in existing code. |

World-space machinery/station `TextMesh` labels are outside this screen-UI pass.
The fuller pre-change audit is preserved in
`Validation~/UIAuthoring-Phase1/Inventory.md`.

## Persistent assets and hierarchy

All new presentation assets live under the experiment's `UI/` folder:

```text
UI/
  Prefabs/Aurora UI.prefab
  Prefabs/Widgets/Aurora Meter.prefab
  Prefabs/Widgets/Aurora Diagnostics Button.prefab
  Prefabs/Widgets/Aurora Status.prefab
  Themes/Aurora UI Theme.asset
  Artwork/Placeholder White.png
  Typography/Fonts/                     (Liberation Sans source and OFL license)
  Typography/Resources/                (TMP settings, SDF fonts, style/line data)
  Typography/Shaders/                   (required TMP mobile shader/includes)
  Runtime/                             (bindings, theme and pointer ownership)
  Editor/                              (authoring Inspectors and isolated validation)
```

The authored scene contains a real prefab instance, with its Game reference
overridden to the existing `IceResurfacerGame` component:

```text
Aurora UI                         Canvas, CanvasScaler, GraphicRaycaster
  Driving HUD                     IceAuthoredHud, CanvasGroup
    Identity
    Speed
    Machine Load                  Button -> existing ToggleDiagnostics()
    Flood Water
    Wash Water
    Snow Tank
    Conditioner
    Blade
    Design Preview - hidden in Play
  Machinery Controls
  Surface Survey
  Navigation
  Messages
  Menus
  Diagnostics
  UI Event System                 EventSystem, InputSystemUIInputModule
```

The six future module groups are empty organizational containers; their existing
IMGUI counterparts remain active. Driving HUD contains the actual pilot objects.
There is no full-screen Image/raycast target. Solid widget backgrounds block
pointer acquisition; decorative text, fills and the preview badge do not.

The eight pilot children are ordinary editable children of the UI prefab.
The widget prefabs are independent reusable templates; they are not nested sources
for the pilot children. Editing a template therefore does not unexpectedly alter
the pilot. Shared themes provide the common styling link.

## Shared styling, typography and artwork

Select **UI/Themes/Aurora UI Theme.asset** to edit primary, secondary and numeric
TMP fonts, text/panel/accent/warning/danger colors, optional panel/button/meter
sprites and button-state sprites/colors. The included Liberation Sans SDF font is
the package's placeholder font, with its source/license retained locally. The
white sprite is a replaceable neutral fill graphic, not final artwork.

After a theme edit, press **Apply Theme to Loaded UI** in its Inspector, or choose
**Tools > Aurora > UI > Apply Themes to Loaded UI**. This deliberate operation is
Undo-backed and updates opted-in channels on loaded scenes and the open Prefab
Stage, including inactive widgets. Save the affected scene/prefab afterward.
Unopened prefabs are not silently rewritten. Open each prefab you want to preview
with the revised theme and apply there. During Play, styles apply once on enable.

Theme assignment does not change anchors, positions, dimensions, text alignment
or hierarchy. Shared font size is an optional channel and starts off.
`commonSpacing`, `commonPadding` and `borderThickness` are Inspector reference
conventions for manual design; they intentionally do not perform automatic layout
or draw borders. Use sliced sprites or explicit border Images for real borders.
An unassigned shared font/sprite leaves the local fallback intact.

To use Photoshop artwork, export a PNG with transparency into an experiment-local
art folder. In Unity's importer choose **Sprite (2D and UI)**, then assign it to
the desired Image or theme sprite slot. Use the Sprite Editor to define borders
and Image Type **Sliced** for scalable panels. For meter fills retain Image Type
**Filled**, Fill Method **Horizontal**, Origin **Left**; replacing the sprite
does not change the live fraction. Preserve `Raycast Target = false` on decorative
graphics. The visible widget background is the intentional pointer-blocking area.

Import your own TTF/OTF into `UI/Typography/Fonts` (or another experiment-local
folder), use TMP's Font Asset Creator, save the resulting font/atlas/material
assets locally, and assign a font role in the theme. Check character coverage
and text fit at all three target resolutions. Materials remain editable per text
component; a shared font replacement may change its associated default material.

## Binding system and simulator ownership

`IceAuthoredHud` holds explicit references to Game, Canvas, visibility group,
raycaster, preview badge and widgets. The prefab leaves Game unassigned; the
authored scene instance supplies it. Objects may be renamed and reordered without
breaking bindings. Keep the widget list synchronized when adding/replacing widgets;
use the binding Inspectors to catch missing references.

`IceHudWidget` connects static authored labels and live Value/Detail TMP text,
optional Fill Image and Action Button. Static captions are not rewritten. The
identity prefix is also an editable field. No duplicate simulator state exists.

| Widget | Authoritative source |
| --- | --- |
| Elapsed time | `Sim.Time` |
| Speed/direction | `abs(Sim.Vehicle.Speed) * 3.6`, speed sign |
| Machine load | `Sim.MachineLoad`; Action calls `game.ToggleDiagnostics()` |
| Flood | `Sim.FloodWaterLiters / Sim.Config.FloodCapacityLiters` |
| Wash | `Sim.WashWaterLiters / Sim.Config.WashCapacityLiters` |
| Snow | `Sim.SnowTankM3 / Sim.Config.SnowCapacityM3` |
| Conditioner | `IceDashboard.ConditionerStatus(Sim)` and `Sim.Conditioner.Lift` |
| Blade | `Sim.Controls.BladeDepthMm`, `IceDashboard.BladeContactMm(Sim)` |

Start refreshes immediately. Subsequent live content refreshes default to 10 Hz
using unscaled time; visibility updates every LateUpdate and immediately before
pointer queries. Text is assigned only when the formatted reading changes. Fill
amounts are clamped to 0–1. Invalid widget references/capacities show `UNAVAILABLE`
and a diagnostic rather than a plausible preview reading. Missing controller/input
infrastructure hides the authored HUD and restores the old header path.

Bindings update live text, fill amounts and interaction/visibility only. They
never write gameplay state except the existing diagnostics command. The theme
component independently applies explicitly selected appearance channels.

## Interaction and state ownership

When `game.HasAuthoredHud` is true, `IceDashboard` skips only `Header()` and its
old `(408,24,1164,108)` mouse rectangle. The rest of its drawing and hit regions
remain on their existing paths. An inactive/unavailable authored controller
restores the legacy header. The procedural scene has no UI instance and no
authored owner, so its old header remains available.

`IceUIPointerGuard` queries the current GraphicRaycaster directly with reused
pointer data/results, avoiding dependence on whether EventSystem.Update already
ran this frame. A press acquired by a UI graphic retains ownership until release.
The obsolete top-header rectangle no longer catches clicks after a widget moves.
When a moved widget overlays an old IMGUI control, its mouse events are consumed
before the legacy controls handle them. An already captured IMGUI drag keeps its
existing release path. Physical drags likewise retain their existing owner after
crossing UI; no physical-control directions, picking or drag math were changed.

One persistent EventSystem uses InputSystemUIInputModule. Its empty serialized
actions use the installed module's standard `AssignDefaultActions()` behavior.
UI navigation events are disabled and the diagnostics button has Navigation=None,
so normal driving shortcuts are not repurposed as selected-button actions.
RMB/locked-cursor look makes the HUD noninteractive while keeping it visible.
On-foot cursor locking and E service interaction retain their existing behavior.
The on-foot header is explicitly read-only, including if a background Editor
cannot grant the service controller's requested native cursor lock.

The authored header is visible during active driving, quicklook and on-foot play.
It hides for Numpad 1/video mode, pause, help, route manager, startup and results,
matching the old header's state policy. The future groups do not acquire a new
visibility policy in this pass.

Unity references used for this integration:
[Input System 1.19 UI support](https://docs.unity.cn/Packages/com.unity.inputsystem%401.19/manual/UISupport.html)
and [Application.IsPlaying](https://docs.unity.cn/6000.0/Documentation/ScriptReference/Application.IsPlaying.html).
Runtime guards use the object's playing-world membership so opening a prefab
while the Editor is playing cannot run its HUD/theme bindings against that asset.

## Edit Mode preview and resolution

Preview text and fill amounts are normal saved presentation content: flood 738 L,
wash 310 L, snow 25%, speed 5.0 km/h, conditioner Raised, and representative load,
elapsed time and blade data. The **DESIGN PREVIEW** badge is visible outside Play.
No ExecuteAlways simulation or preview updater runs, so merely inspecting or
previewing the Canvas cannot dirty the scene or change gameplay state. Editing a
preview label yourself is a deliberate normal authored edit. Play hides the badge
and replaces readings with actual simulation values, then Unity restores the saved
Edit Mode content on exit. Do not copy live component values back into the scene
unless you intend to change its preview.

Use the Scene view's 2D mode and frame **Aurora UI** to inspect the overlay without
Play. The Game view may have no world camera before Play because this simulator
creates its driving camera at runtime; the Canvas is still inspectable in Scene
view. Use the Game view's resolution selector for **1366 x 768**, **1600 x 900** and
**1920 x 1080**, and inspect the Canvas bounds/preview at each aspect and size.

CanvasScaler uses **Scale With Screen Size**, reference **1600 x 900**, screen
match **Expand**. At these near-16:9 sizes its scale is the minimum width/height
ratio, approximately 0.8533, 1 and 1.2. It preserves each widget's design dimensions
in reference units. At other aspect ratios the logical canvas expands along the
extra dimension; anchors decide how widgets respond. This is not a runtime
layout-repositioning script.

Pilot roots begin anchored/pivoted to the top-left. To dock the flood meter at the
bottom-left, change its anchor preset (use Unity's modifier choices intentionally),
then set its offsets and pivot. Left/right-stretched labels and meter backgrounds
inside it adapt to resizing; every child RectTransform is independently editable.
For a fixed-width child, change its anchors. Optional layout groups may be added
by you to a subgroup; no automatic layout group is imposed by this system.

## Reusable widgets and additional panels

Create a **Prefab Variant** of Aurora Meter, Aurora Diagnostics Button or Aurora
Status using Unity's Project window. Edit its child layout and style channels in
Prefab Mode. Drag the variant under Driving HUD, assign its widget to the
controller's Widgets list, and select its supported data Kind. The meter template
starts as Flood; the diagnostics button starts as Load. The controller supplies
their live simulator binding and diagnostics listener at runtime. Keep one
reference per widget and remove an old instance if replacing it.

For a purely decorative element, add an Image or TMP text and leave Raycast Target
off. A new data kind or simulator command needs a small binding extension, but
fonts, images, positioning and existing kinds do not need C# edits. Additional
screen modules should use their own presenter, visibility group and reusable
templates, explicitly suppressing only their equivalent legacy draw/hit code
once complete. Reuse the existing Canvas/EventSystem; do not add a second active
EventSystem or a full-screen transparent blocker for an ordinary panel. Test modal
ownership separately for future menus. Keep physical machinery/world geometry
out of presentation prefabs.

## Subsequent migration priorities

1. Bottom navigation/status strip, notices, tooltips and history. Reuse buttons,
   text and repeated message-row templates before introducing machinery drags.
2. Pause/settings/help and startup/results. Establish modal ownership, sliders,
   preferences and report-row templates with the existing commands.
3. Survey/minimap and GPS advisory card. Embed existing textures/projected paths
   in an editable viewport, with independent legends and tooltip layout.
4. Route Manager and saved-route rows, then recording/training panes. Preserve
   storage, recordings, fit/progress algorithms and operator route files.
5. Compact/expanded machinery controls. Explicitly test press/drag/release,
   device loss, focus loss, RMB handoff and coexistence with cockpit controls.
6. On-foot service and remaining diagnostics. Keep world-space service and debug
   renderers code-driven while making their screen panels editable.

Each module must pass relocation, save/reopen, live binding and input ownership
checks before its legacy equivalent is removed. The procedural scene's fallback
remains until an equivalent migration is intentionally supplied there.

## Protection and validation evidence

Before changes, affected source/scene files and metadata were copied to
`Backups~/UIAuthoring-Phase1-20261106`. Its `baseline-sha256.json` records hashes for
149 pre-existing source, scene, prefab and configuration assets. The authored
scene change adds a root UI prefab instance and its explicit simulator reference;
it does not regenerate or resave existing machine/arena content. No branch,
commit or push was created.

Validation scripts and logs live at project-root `Validation/UIAuthoring-Phase1`.
`StageAcceptanceProject.ps1` makes a separate project, and
`RunAcceptanceProject.ps1` launches Unity there. The destructive authoring test
requires both a disposable validation path and `UIAuthoringValidationProject.txt`;
it refuses to run in the normal project. It modifies only the isolated flood meter.
`CompileExperiment.ps1` performs a separate source compile against the installed
Unity APIs. Development audits are under experiment
`Validation~/UIAuthoring-Phase1`.

### Executed results

The final isolated Unity run (`Unity-acceptance-final4.log`) and the subsequent
fresh-process reopen both exited successfully. These were actual Unity import,
Edit Mode serialization and Play Mode tests, not a replacement managed simulator.
The isolated project uses a separate company/product identity to keep its route
storage and preferences separate from the operator's normal project.

| Check | Executed result |
| --- | --- |
| C# compilation | Installed Unity 6000.4.1f1 API compilation and actual Unity compilation passed. Existing unrelated obsolete-API/serialization warnings remain. |
| Asset and protection audit | 3,092 structural assertions passed: GUID/fileID references, eight widgets, 24 TMP labels, initial bounds at all three sizes, preview fills, intentional raycast targets, 149 baseline hashes and exact preservation of the original authored-scene content. |
| Authoring persistence | In isolated Edit Mode, moved Flood to `(80,-680)`, resized it to `280 x 120`, disabled its shared-color channel, changed its background, recorded prefab overrides, saved and reopened. Position, size, anchors/pivot, color and style opt-out survived. |
| Real Play Mode | The saved widget retained its transform and color. Its text/fill read actual simulator water/capacity and changed after simulation water use. The preview badge hid. |
| Legacy replacement | The active authored owner selects the header-suppression branch. The original flood position had no uGUI hit and no obsolete legacy-header hit region; the moved position did. No separate visual proof of the mixed IMGUI/uGUI composition is claimed by the camera captures below. |
| Input ownership | Injected Input System press/release invoked diagnostics through the actual UI module. A UI press over a real cockpit hydraulic grip did not acquire or command it. Moving UI away restored access. Physical lever and flood-valve drags worked; an existing physical drag retained ownership when crossing UI; UI capture persisted outside its panel until release. |
| State transitions | Injected Numpad 1 hid/restored the HUD through `game.Update`; pause/help hid it. Actual simulator dismount/boarding kept the on-foot header visible/read-only and restored seated interaction. |
| RMB runtime behavior | Injected RMB entered the existing look path, disabled HUD interaction, changed camera rotation with relative input and restored UI interaction on release. Native OS cursor locking is a separate limitation below. |
| Exit Play and restart | Reopening after Play restored saved transforms, color, style override and design preview text/badge. A fresh Unity process reopened the same saved scene and passed the persistence checks again. |
| Procedural scene | Opened and entered Play; simulation initialized, no authored HUD existed, and the legacy header hit region remained. Its scene bytes were unchanged. |
| Rendered resolution checks | Captured default and moved layouts at 1366 x 768, 1600 x 900 and 1920 x 1080. Inspected scaling, text, fill bars, moved-panel bounds and the gap at its previous location. These camera captures temporarily use ScreenSpaceCamera and exclude IMGUI; the shipped Canvas remains ScreenSpaceOverlay. |
| Performance scope | Final 100-call refresh sample averaged **0.010 ms/call**. The Canvas contains **40 graphics and zero automatic layout groups**. This measures binding work, not total frame time or a legacy-vs-authored rendering comparison. |

Durable results and six PNGs are copied to project-root
`Validation/UIAuthoring-Phase1/Evidence/`. In particular:

- [Authoring/persistence results](../../../../Validation/UIAuthoring-Phase1/Evidence/UIAuthoring-results.txt)
- [Injected input results](../../../../Validation/UIAuthoring-Phase1/Evidence/UIInput-results.txt)
- [Default 1600 x 900 camera capture](../../../../Validation/UIAuthoring-Phase1/Evidence/AuthoredHUD-Default-camera-1600x900.png)
- [Moved/resized/reskinned 1366 x 768 camera capture](../../../../Validation/UIAuthoring-Phase1/Evidence/AuthoredHUD-Moved-camera-1366x768.png)

The dark test background is deliberately confined to the disposable project; it
is a persistence test, not a visual recommendation or change to the normal scene.
Button tint in the immediate captures can reflect its transition after the input
test. The normal scene retains the initial full top strip and existing palette.

### Checks still requiring a visible Editor/player

Licensing and Direct3D rendering succeeded. The remaining limits are specific to
hidden batch execution and capture/profiling methods:

1. **Native mouse lock/focus and hardware interaction.** The hidden Editor reported
   `Cursor.lockState=None` despite the real RMB handler setting `Looking=true` and
   correctly disabling UI interaction. Verify native lock, cursor restoration,
   focus loss/recovery and real mouse dragging in a visible Editor or standalone.
   Injected events are not claimed as native desktop clicks.
2. **Final mixed UI composition.** Inspect the normal ScreenSpaceOverlay HUD with
   legacy survey/console/messages at the three Game view resolutions. Camera PNGs
   show the authored Canvas and scene but cannot establish the appearance of IMGUI
   or every modal/quicklook overlap. A mouse-driven Scene-view authoring session
   remains useful; the automated test edited the same serialized properties via
   Unity APIs rather than physically dragging editor handles.
3. **Full-frame allocation and draw-call comparison.** This Mono allocation counter
   returned zero even for a retained 4,096-byte calibration allocation, and Editor
   draw counters returned zero despite rendered PNGs. Those counters were marked
   unavailable, not zero-allocation/zero-draw results. Formatting can allocate at
   each 10 Hz refresh. Use a visible Profiler and Frame Debugger at a steady parked
   pose to compare the authored controller enabled versus disabled (legacy fallback),
   then driving and hidden-HUD states. Check GC Alloc, Canvas.BuildBatch, UI rebuilds,
   batches and SetPass calls. No measured full-frame regression claim is made.

Unity also reported the pre-existing malformed
`Documentation/SnowRecoveryAndContact.txt.meta`; it was left untouched. First-import
package API updates occurred only in the disposable project's Library cache.
