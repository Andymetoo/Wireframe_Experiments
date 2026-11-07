# Aurora R-54 — UI authoring handbook

Open `Scenes/IceResurfacer_AuthoredMachine.unity`. Edit the saved **Aurora UI**
objects with Unity's Rect tool, TMP components and Image/Selectable Inspectors.
The simulator supplies readings and commands; it does not restore your layout.
No scene generation, machine rebake or coding step is part of normal UI design.

Paths in this handbook are relative to `Assets/Experiments/IceResurfacer`.
Use a scene copy when experimenting. Leave the original procedural scene alone
unless intentionally working on its legacy interface.

## Where everything lives

```text
Aurora UI                         Canvas, CanvasScaler, GraphicRaycaster
  Driving HUD                     IceAuthoredHud
    Identity / Speed / Machine Load / Flood Water / Wash Water
    Snow Tank / Conditioner / Blade
    Design Preview - hidden in Play
  Machinery Controls
    Aurora Machinery Console      IceMachineryConsole
      Compact Console / Expanded Console / Session Confirmation
  Surface Survey
    Aurora Surface Survey         IceSurfaceSurvey
      editable map, overlay, legend, readings and patch details
  Navigation
    Aurora GPS Status             IceGpsStatus
  Messages
    Aurora Operator Interface     IceOperatorOverlay
      Aurora Projected GPS        IceGpsProjectedGraphic; no raycast target
      On-foot water service       seven separate IceServiceReadout objects
      Bottom actions and status   Routes / Messages / Help / Pause
      Performance readout F4
      Routine operator notice     IceOperatorMessage
      Cockpit interaction hint    IceOperatorMessage
      Drive inhibit warning       IceOperatorMessage
      Operator message history    scroll view, advice and controls
        Live rows - editable layout
        Sample history - hidden in Play
      Design preview - hidden in Play
      Critical machinery notice   IceOperatorMessage; foreground default
  Menus
    Aurora Route Manager          IceRouteManager
    Aurora Session Screens        IceMenuScreens
      Welcome - Start Screen
      Pause - Operator Settings
      Operator Guide
      Results - Session Review
  Diagnostics                     reserved organization group
  UI Event System                 EventSystem / InputSystemUIInputModule
```

All main panels above are persistent scene/prefab objects. The scene instances
explicitly reference the existing Game and Canvas. Controllers can be renamed or
reordered: bindings do not infer meaning from child names or positions.

| Interface | Reusable asset location | Live content |
| --- | --- | --- |
| Eight HUD widgets | `UI/Prefabs/Widgets/` and `UI/Prefabs/Aurora UI.prefab` | Existing simulator readings and diagnostics action |
| Machinery controls | `UI/Prefabs/Machinery/`, including `Widgets/` | Existing controls, mechanism state and confirmation |
| Survey/GPS/routes | `UI/Prefabs/Navigation/` | Authoritative map textures, projected geometry and route library |
| Session screens | `UI/Prefabs/Menus/` | Existing session flags, settings and actual RunReport |
| Service/messages/status | `UI/Prefabs/Operator/Aurora Operator Interface.prefab` | Existing water-service controller and dashboard messages |
| Notice/hint/warning templates | `Aurora Operator Notice`, `Aurora Context Hint`, `Aurora Drive Warning`, `Aurora Critical Notice` in `UI/Prefabs/Operator/` | Existing content/severity/lifetime; optional icon and editable fade |
| Operator action | `UI/Prefabs/Operator/Aurora Operator Action.prefab` | Explicit existing Routes/History/Help/Pause/history actions |
| Message history row | `UI/Prefabs/Operator/Aurora Message History Row.prefab` | At most 32 reused runtime instances, newest first |
| Projected GPS continuation | `UI/Prefabs/Operator/Aurora Projected GPS.prefab` | Bounded existing occlusion/route/alignment geometry in a uGUI mesh |

Route rows and history rows are dynamic instances of editable templates. Edit the
prefab source or assign a variant on its presenter; editing one runtime clone is
not a saved design. History retains full original messages and timestamps; the
short routine banner retains the old abbreviated presentation. The message
history owner is still `IceDashboard`, not a second message database.

## Move and resize without breaking input

Select the actual panel RectTransform and use the Rect tool (`T`). Move, rotate,
resize, change anchors or set a new pivot. Child captions and value objects can
be moved independently. Save the scene to retain its prefab overrides.

Keep controls under the appropriate presenter's visibility root. For example,
keep service text/background under **On-foot water service**, and a pause button
inside its Pause screen. Reparenting under an unrelated group can give it the
wrong modal visibility even if its command reference remains correct.

Pointer regions follow enabled **Graphic** components with **Raycast Target** on,
plus their CanvasGroups and masks. They do not follow the old dashboard rectangles.
A panel background is usually the intended blocker; labels, meter fills and
decorative icons normally have Raycast Target off. An Image that is visually
transparent can still block input. Disable Raycast Target or its GameObject when
it is decoration. Do not add a full-screen invisible Image behind a nonmodal card.
Notice/hint/warning backgrounds and F4's readout are informational and start with
Raycast Target off, preserving world-control access beneath them. Service, history
and action backgrounds intentionally own their actual visible regions.

On foot, the service panel blocks world clicks at its actual displayed rectangle.
E still acts on the existing aimed service target. A moved-away or hidden service
panel cannot reserve the old bottom-screen band. Modal menus continue to own the
whole screen and release momentary machinery controls through the existing Game.

Preserve Button.Target Graphic, Slider fill/handle references and explicit action,
setting or metric kinds when replacing artwork. On session screens, update
**First Selection** and **Navigation Order** after rearranging controls. Native
Tab/Shift+Tab uses that order; arrows/Submit use the existing Input System module.
Ordinary driving controls retain their existing navigation policy and shortcuts.

Clicking another Editor panel or alt-tabbing does not pause Aurora or open its
menu. The simulation runs in the background while gameplay input and mouse look
release on focus loss. Use Esc or Pause explicitly when you want to pause.

Help and message/route lists intentionally use layout groups. Edit spacing,
padding, LayoutElement row sizes and scroll viewport/masks there. A layout group
controls its children's positions; remove/disable it or make a freely positioned
subgroup if you want a different composition. For edited help prose/fonts use
**Fit Edited Help Text (Undoable)**. For a larger history font, resize the history
row prefab's LayoutElement and text rectangle to fit its wrapped content.

The projected GPS graphic has editable widths, border colors, opacity, tint,
material and sample points. It can follow the existing guide's color or use a
local route color. Keep its default full-view anchors for a physically aligned
overlay. Moving/resizing that viewport intentionally moves/rescales the drawn
projection; it never changes the route or vehicle. Its geometry and machine
occlusion still come from the existing GPS systems. It never captures a pointer.

## Shared theme and local overrides

There is one shared theme: **UI/Themes/Aurora UI Theme.asset**. Typography assets
live in `UI/Typography/Fonts/` and `UI/Typography/Resources/Fonts & Materials/`.
The included Liberation Sans SDF is placeholder typography; its license/source
remain in the project.

Each `IceUIStyle` explicitly selects its theme, font role, color role and optional
sprite/state channels. Primary, Secondary and Numeric roles can use different
TMP assets. Existing objects keep their current choices and intentional local
overrides. New service quantity fields use Numeric; message text uses the existing
Primary/Secondary conventions. The GPS projection's world-guidance colors and the
drive warning's existing alert face are deliberate local defaults, both editable.

After changing the theme, choose **Apply Theme to Loaded UI** in its Inspector or
**Tools > Aurora > UI > Apply Themes to Loaded UI**. This is an explicit Undo-backed
operation on opted-in loaded objects, including inactive UI and the open Prefab
Stage. It does not silently rewrite every unopened prefab. Open another prefab
and apply there to preview its inherited theme. Styles apply once on runtime enable.

For a local font, turn **Apply Font** off on that object's style, then assign its
TMP Font Asset. For local tint/sprite/state graphics turn **Apply Color**, **Apply
Sprite** or **Apply Button States** off before editing that channel. Font sizes
are local by default; enable Apply Font Size only when you want the theme role's
size. Alignment, wrapping, RectTransforms, content margins and hierarchy remain
authored values. Shared spacing/padding fields are design references, not a runtime
layout engine. No theme operation changes panel coordinates.

## Photoshop PNGs, nine-slicing, buttons and TMP fonts

1. Export a PNG from Photoshop into your own project Artwork folder. Choose
   **Sprite (2D and UI)** in its importer and **Single** for a single graphic.
   Apply, then assign it to Image.Source Image or a shared theme sprite slot.
2. For a full-screen menu image, use its **Full-screen background - replace artwork**
   object. Keep stretch anchors, choose your intended crop/letterbox treatment
   with Preserve Aspect, and use white Image tint for the original artwork colors.
   Disable the style's Apply Sprite/Color channels for a local replacement.
3. For scalable panels/buttons, set Sprite Editor borders, apply and choose Image
   Type **Sliced**. Preserve intended padding and button Target Graphic references.
   A sprite replacement does not automatically resize your RectTransform.
4. Customize hover/pressed/selected/disabled behavior on Button/Selectable using
   Color Tint or Sprite Swap. Use shared theme state slots with Apply Button States
   on, or turn it off for a local transition/ColorBlock/SpriteState. Check all states
   in Play, including disabled controls and the currently selected menu button.
5. Import TTF/OTF into your own typography folder, create a TMP font asset through
   TMP's Font Asset Creator, save its font/atlas/material assets, then assign the
   shared role or local TMP component. Check glyph coverage, line wrapping and
   all three target resolutions after a font change.
6. Use **Create > Prefab Variant** for alternative notices, buttons, controls or
   panels. Preserve required components and explicit references. Assign your new
   row/template variant to the owning presenter when changing dynamic content.

The optional notice icon and welcome logo start disabled. Enable them and supply
your own art. Their positions and raycast behavior are normal authored properties.
No final artwork or alternate aesthetic is imposed by this migration.

## Anchors, pivots and resolution

The existing CanvasScaler uses **Scale With Screen Size**, reference **1600×900**,
match mode **Expand**. At 1366×768, 1600×900 and 1920×1080 the scale is approximately
0.8533, 1 and 1.2. At other aspects the logical Canvas expands along the spare axis;
anchors decide where panels go. There is no runtime script restoring old coordinates.

Change anchor presets deliberately: Unity's modifier options determine whether
position/pivot also change. A centered panel should usually use centered anchors
and pivot; a full-screen backdrop stretches with offsets. Stretched child labels
and fills adapt to resizing, while fixed anchors keep fixed dimensions. Inspect
actual bounds and typography after changes. Do not move the Canvas or add another
active Canvas/EventSystem just to reposition a panel.

## Preview without running the simulator

- Select **Aurora Session Screens**, or use **Tools > Aurora > UI > Screen Preview**,
  for Welcome, Pause/Settings, Help, Results or gameplay UI. Assisted training is
  a selection on Welcome, not an invented separate state.
- Select **Aurora Operator Interface**, or use **Tools > Aurora > UI > Operator
  Preview**, for driving/status, on-foot service, history or warnings.
- **Restore Saved Presentation** returns the exact previous CanvasGroup/input
  values. The two transient preview tools coordinate ownership. Save, Play, script
  reload, scene/prefab close/save or quitting restore them automatically.
- Representative readings are normal saved TMP/Slider properties. **DESIGN PREVIEW /
  SAMPLE DATA** badges identify them. Play hides sample-only rows/badges and supplies
  real values. Preview does not dismount, fill water, create history, save preferences
  or write routes.

Earlier console/map/GPS preview values remain editable normal prefab data. Their
existing authoring tools still work; the new preview workflow does not regenerate
or permanently disable them. In Scene view use 2D/frame the Canvas. The runtime
camera is created by the simulator, so a stopped Game view may not show the world.

## Save, reopen and recover

Save a scene override for a one-off composition. Apply an override to a prefab or
your own variant only when it should become that asset's default. Exit Play before
saving design work. Live values are overwritten by bindings; change static captions,
formats, graphics and layout in Edit Mode. Do not copy live simulated values back
to saved components.

| Symptom | Check/recovery |
| --- | --- |
| Font/sprite/tint changes back on Play | Turn off that IceUIStyle channel, or intentionally change its shared asset |
| Panel invisible in Edit Mode | Use the appropriate preview; inspect CanvasGroup alpha and restore previews |
| Clicking world controls is blocked | Inspect enabled Image/Graphic raycast targets, masks and CanvasGroups at the actual location |
| Button moved but action wrong | Check explicit Owner, Screen/Kind, Button reference and visibility parent; sibling order does not bind commands |
| Reordered menu has awkward keyboard traversal | Update its First Selection/Navigation Order and Selectable navigation |
| Row changes disappear on the next run | Edit the row prefab/variant assigned to the presenter, not a runtime clone |
| Help/history text overlaps after enlargement | Fit edited help text or resize the history row's LayoutElement/text rect and list spacing |
| A migrated presenter reports missing bindings | Restore assigned Game/Canvas/raycaster/content references; disabled/unavailable modules can restore legacy fallback |
| Missing Phase 5 assembly in an existing edited scene | Use Tools/Aurora/UI/Install Missing Phase 5 Operator Interface; it adds once, with Undo, and does not save or replace your scene |

Do not run machine/arena rebakes, calibration transfers or regeneration tools for
UI design. Verified migration backups are under `Backups~/UIAuthoring-Phase5-20261106`.
If your scene changed during an external integration attempt, preserve your scene
and use the additive Editor installer rather than overwriting it.

## What remains code-driven

F2 treatment/debug textures and F3 machinery/contact diagnostics remain specialized
developer rendering. The diagnostic panels retain their legacy code and active-only
hit regions; F3's world collision outlines are custom geometry. F4's ordinary summary
is now an editable player UI object; detailed profiler content remains in diagnostics.
Standalone development probes and route-fitting Editor windows remain tools.

The procedural fallback retains all its original IMGUI screens and controls. They
are not the authoring path for the complete authored scene. Disabling an authored
module can restore its legacy equivalent for recovery.

World geometry/effects are separate from screen UI: dynamic ice/surface textures,
loose/deposited snow, water/floor wetness, hose routing and nozzle effects, particles,
audio, 3D route/heading lines, some procedural world TextMesh labels and the runtime
camera remain generated or updated by their existing systems. Authored machine,
arena, labels, anchors and fill ports retain their existing prefab editing workflow.
Runtime text/material overrides on some world labels limit direct Scene-view styling;
no unrelated geometry was migrated or rebaked here. Screen-space GPS continuation
and alignment strokes have editable uGUI styling while retaining their source geometry.

Use [the 295-check master acceptance](UI-Phases-1-to-5-Master-Acceptance.md), starting
with **MASTER: TEST THIS NOW**. Automated captures and injected input do not replace
your native device, cursor/focus, final artwork or full-frame Profiler acceptance.
