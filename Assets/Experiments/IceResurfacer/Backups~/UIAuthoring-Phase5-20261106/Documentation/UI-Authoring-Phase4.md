# Aurora R-54 — UI Authoring Phase 4

Phase 4 supplies persistent, editable Welcome, Pause/Operator Settings, Operator
Guide and Results screens. Assisted training stays a welcome button. These screens
use the existing Canvas, Input System, TextMeshPro, themes and session commands.
Their starting composition follows the previous paper-panel menus. No final
artwork is supplied. Phase 5 is outside this change.

## Open and edit

Open `Scenes/IceResurfacer_AuthoredMachine.unity`. The added hierarchy is:

```text
Aurora UI                       existing Canvas, 1600×900 Expand scaler, raycaster
  Driving HUD                   existing Phase 1
  Machinery Controls            existing Phase 2
  Navigation                    existing Phase 3 (see its documentation)
  Menus
    Aurora Session Screens      IceMenuScreens; explicit game/Canvas references
      Welcome - Start Screen
      Pause - Operator Settings
      Operator Guide
      Results - Session Review
        Full-screen background - replace artwork   each screen has its own
        Editable paper panel
          Panel background - replace artwork
          independent TMP headings, values, captions and nested widget variants
        Design preview - hidden in Play
  UI Event System               existing EventSystem/InputSystemUIInputModule
```

The exact existing Phase 1–3 names and layouts are preserved. No second Canvas,
EventSystem, title scene or training state machine is added.

Select **Aurora Session Screens**. Its Inspector buttons or **Tools > Aurora > UI >
Screen Preview** show Welcome, Pause and Settings, Operator Guide, Results, or
Gameplay UI. Results and sliders have saved representative values; a visible
**DESIGN PREVIEW / SAMPLE DATA** badge identifies them. These are ordinary saved
TMP and Slider properties, so you can change the examples yourself in Edit Mode.
Play hides badges and replaces sample values from the real simulator.

**Restore Saved Presentation** restores every visibility/input value captured
before preview. Selecting another preview first restores the previous snapshot.
Saving, entering Play, assembly reload, scene close, prefab save and quitting also
restore it. Preview never constructs a Simulation, calls session commands,
writes PlayerPrefs or accesses route storage. Preview is Editor-only; selecting
Pause outside Play does not pause the game. The normal saved root shows Welcome;
the other screens remain present with their CanvasGroup alpha zero.

Move, resize, anchor and rotate the actual RectTransforms with Unity's Rect tool.
Buttons and captions are separate objects. Result labels, values and points are
separate TMP objects. Bindings use serialized references and enum kinds, not
positions, names or child order. You may reparent a button within its screen;
retain its `IceMenuAction.Screen`, Owner and Button references. Update the screen's
**Navigation Order** and **First Selection** after rearranging controls. Keep
modal backgrounds behind the controls and the menu root under the existing Menus
group. No runtime menu component writes authored RectTransforms.

Help deliberately uses a VerticalLayoutGroup, editable spacing/padding,
ContentSizeFitter, RectMask2D and ScrollRect. Each of its six text sections has the
existing `IceUITextHeight` adapter to size to edited content. Rearrange sections
in this content group; add LayoutElements for images. After editing prose or fonts
outside Play, use **Fit Edited Help Text (Undoable)** on the menu Inspector, or
**Tools > Aurora > UI > Fit Edited Help Text**, to refresh body heights immediately.
This explicit layout edit is separate from temporary preview and supports Undo.
You can replace this layout
in your own variant. Other screens use freely positioned, centered panels and
stretching full-screen backgrounds. Change those anchors/layouts as desired.

## Prefabs and bindings

All new prefabs are in `UI/Prefabs/Menus/`:

| Asset | Purpose |
| --- | --- |
| Aurora Session Screens.prefab | Nested four-screen assembly and explicit binding arrays |
| Aurora Welcome.prefab | Title, introduction, four actions, optional disabled logo |
| Aurora Pause Settings.prefab | Resume/help/new session, two settings, refill practice |
| Aurora Operator Guide.prefab | Six editable scrollable instruction sections and close action |
| Aurora Results.prefab | Seventeen individually bound report widgets and three actions |
| Aurora Menu Button.prefab | True variant of Phase 3 Aurora Route Action; menu command binding replaces route command |
| Aurora Operator Setting.prefab | True variant of Phase 2 Aurora Bound Slider; menu setting binding replaces machinery command |
| Aurora Result Row.prefab | Caption/value/points reusable row with explicit metric kind |
| Aurora Menu Panel.prefab | Shared-theme replaceable panel Image |

`IceMenuScreens` registers as `Game.AuthoredMenus`, manages visibility/focus and
dispatches existing commands. `IceMenuScreen` stores kind, CanvasGroup and focus
targets. `IceMenuAction` adapts Button/Submit to a specific existing command.
`IceMenuSetting` binds a Slider to the existing speed or fallback-eye property;
its value/detail format strings and multipliers are editable. `IceResultWidget`
only formats fields from `RunReport`; no scoring logic lives in it. Its numeric
format strings, multipliers and complete/partial/practice messages are editable.

`IceMenuFactory` creates only missing new menu assets. It never regenerates earlier
UI, machine or arena assets and skips existing menu prefabs. The **Install Missing
Phase 4 Screens** command attaches the assembly to the current existing Menus group,
records Undo and leaves saving to you. It returns without duplication if installed.
The destructive validation/build entry points require a disposable-project marker.

## Preserved behavior

| Action/state | Existing authoritative behavior |
| --- | --- |
| Start scored / New scored | `NewSession(false)`: scored FreeSimulation |
| Start practice / New practice | `NewSession(true)`: unscored FreeSimulation |
| Assisted training | `NewSession(true, true, GuidedTraining)`: unscored, existing interlocks |
| Pause New session | `NewSession(false, false)`: welcome selection, no immediate run |
| Refill practice | Existing `StartServicePractice`: practice with 35%/30% water setup |
| Resume | Existing TogglePause; Escape remains owned by Game |
| Help / close | Existing ToggleHelp; F1 remains owned by Game |
| Speed | Existing SetOperatorSpeedMph, 3–8 mph, quarter-mph snap and PlayerPrefs save |
| Eye | Existing -0.08…+0.12 m fallback only; authored eye anchor remains authoritative |
| Results | Existing completed RunReport, points, penalties, speed comparison and 95% completion rule |

There is no speed-reset button. Seated-eye text explains why adjusting its fallback
value does not move an authored eye anchor. `HeadOffset` and all camera calculations
remain unchanged. Original and selected speed remain separate report values.
Practice displays no score/point breakdown. Guided training keeps its original
equipment, return and hose gates. FreeSimulation retains its permissive finish
behavior and consequences. Help wording comes from the current local dashboard,
including current machinery, camera, map, service and shortcut descriptions.

Visibility follows the existing flags, in the existing priority order: Route
Manager, Help, finished Results, unstarted Welcome, Pause, then gameplay. Hide-all
keeps its previous policy. Help does not modify the Paused flag: closing it returns
to Pause, Welcome, Results or gameplay as appropriate. No parallel session state
is introduced.

In the authored scene `IceDashboard.OnGUI` skips only the replaced session-sheet
branch when the authored modal is available. Its route, machinery, diagnostics,
service and other unmigrated UI remain. The procedural scene has no menu binding,
so all its legacy IMGUI screens remain available.

## Input and performance

A visible modal owns pointer input across the screen. The real full-screen Image
blocks raycasts, while inactive groups have alpha zero, Interactable false and
Blocks Raycasts false. Decorative graphics and TMP captions do not raycast.
The existing Game pointer ownership check also recognizes the modal before
cockpit picking. Pause/help use the existing ReleaseControls/ReleaseLook paths.

Screen/focus transitions cancel menu presses and setting drags, clear old screen
focus and restore remembered valid selection. A canceled Button press stays
canceled through hide/reopen until a fresh press. Slider cancellation is separate
from click cancellation so normal dragging remains continuous. A per-frame action
guard prevents duplicate Button and Submit callbacks. The existing EventSystem's
navigation events are enabled while a focused modal owns input and restored when
it closes. Tab/Shift+Tab use the authored list; the machinery Tab shortcut yields
while a Phase 4 modal is visible. Existing arrows/Submit use the Input System UI
module. Native cursor/focus acceptance remains separate from injected tests.

Settings refresh only while Pause is visible and only rewrite text after a value
or camera-anchor condition changes. Rejected late slider callbacks restore the
display from the game, and speed preference saves occur only when its snapped
value changes. Results compare RunReport fields without
boxing and rewrite labels only on entry or changed report. Visibility setters
skip unchanged values. There is no per-frame menu instantiation or layout reset.
The two visibility passes ensure existing Game changes take effect independently
of script update order. Full-frame CPU/GPU/Canvas/GC comparison still needs the
Unity Profiler on representative hardware.

## Your PNGs, sprites and fonts

1. Import a Photoshop PNG into your own Artwork folder. For backgrounds/buttons,
   set Texture Type to **Sprite (2D and UI)** and apply. Select a screen's **Full-screen
   background - replace artwork**, assign Source Image, and choose Simple/Sliced
   and Preserve Aspect to suit your composition. A full bleed design usually uses
   the saved stretch anchors and Preserve Aspect off. Set tint to white for the
   original artwork colors. For an image with different proportions, choose your
   crop/letterbox treatment explicitly.
2. On its `IceUIStyle`, disable **Apply Sprite** for a local image and **Apply Color**
   for a local tint. Otherwise theme reapplication intentionally owns those values.
   Do the same on button/panel Images. Disable **Apply Button States** for custom
   Selectable colors; edit the Button's ColorBlock/transition or sprite swap.
3. For nine-sliced art, set borders in Unity's Sprite Editor and choose Image Type
   **Sliced**. Retain the Image's raycast target for modal backgrounds and controls;
   disable it for decoration. Keep Button.Target Graphic and Slider fill/handle
   references assigned when replacing graphics.
4. Create TMP font assets through Unity's TMP font asset workflow. Assign the
   existing `UI/Themes/Aurora UI Theme.asset` font slots for shared changes. For a
   local font, disable `IceUIStyle.Apply Font` and assign the TMP Font Asset directly.
   Local font sizes are the default; enable Apply Font Size only when you want the
   theme's role size. Edit alignment, wrapping and rects directly on TMP components.
5. Use **Create > Prefab Variant** for alternative button/panel designs. Preserve
   the action/setting/metric components and their explicit references. Save scene
   overrides or apply intentional changes to your new variants after leaving Play.
   Restart Unity to verify a finished composition. No C# coordinates need changing.

## Validation record

Detailed machine-readable evidence, matching resolution captures and test logs are
under workspace `Validation/UIAuthoring-Phase4/`. The disposable Unity project has
its own company/product identity for PlayerPrefs and redirects any route writes
to validation-only storage. Test art and deliberately altered layouts stay there.

Executed on 2026-11-06 with Unity 6000.4.1f1:

| Validation actually run | Result |
| --- | --- |
| Initial Phase 1–3 integration smoke, before implementation | PASS; retained initial evidence |
| Runtime and Editor C# compilation against installed Unity APIs | PASS; existing obsolete/API and unused-envelope warnings only |
| Four saved screen roots, twelve actions, two settings, seventeen result bindings and prefab variants | PASS in real Unity Edit Mode |
| Temporary previews, exact visibility restore and automatic restore on Save | PASS; no simulation or preference changes |
| Deliberate moved/rotated/reskinned button, replacement background and resized score | PASS after save/reopen, Play and fresh Editor process |
| Scored, practice, assisted training, pause, Escape, F1, help nesting, refill and new-session actions | PASS through existing Game commands and injected real uGUI input |
| Speed range/snap, session persistence and fresh-process PlayerPrefs persistence | PASS; 6.25 mph restored in a fresh simulation |
| Seated fallback versus authored eye anchor | PASS; fallback changes without moving or double-offsetting the anchor |
| Actual scored/practice report fields, points, penalties and partial state | PASS; no UI scoring calculation |
| Keyboard Submit/focus traversal, modal raycasts, held hydraulics and interrupted press/drag/focus | PASS with injected Input System events |
| Late slider callback after Pause hides | PASS; display restored without changing the setting |
| Procedural scene legacy state paths and machinery | PASS; scene bytes unchanged |
| Reused Phase 1 HUD/cockpit input suite | PASS; 22 pass records, native cursor lock separately unverified |
| Reused Phase 2 physical/keyboard parity suite | PASS; 69 pass records, including hydraulic/valve directions and keyboard synchronization |
| Phase 3 map/GPS/route regressions | PASS; 54 pass records, dynamic rows/actions, disposable recording, compatible guide activation and unchanged Andy rejection |
| Exact integrated scene through menu and earlier-phase Play tests | PASS; every delivered scene byte retained |
| Existing deterministic simulation regression | PASS: all 43 scenarios, including an actual complete scored route |
| Four menus × three resolutions × default/edited layouts | PASS: 24 camera-rendered PNGs and action viewport bounds; six additional navigation regression captures |

The HUD/cockpit fixture temporarily moves navigation presentation out of its sight
lines, keeping registrations active, then restores it. The separate Phase 3 tests
exercise actual map/GPS locations and moved-panel pointer ownership. All recording,
copy, rename and deletion operations use disposable validation storage. The Andy
guide fixture is an unchanged copy; originals are neither rewritten nor revalidated.

The final unchanged-results presentation benchmark averaged **0.00239 ms/call**
across 1,000 calls, excluding rendering, simulation and other UI. Those calls did
not increment the result-label refresh count or create objects. Earlier matching
runs measured 0.00245–0.00254 ms/call. Unity's allocation counter reported zero even
for a retained 4,096-byte calibration allocation, so it is **unavailable**, not
evidence of zero allocation. A full-frame performance comparison remains pending.

Complete-report wording also has an explicit representative formatter fixture;
that fixture is not claimed as a physically completed Unity training run. The
43-scenario Core suite supplies independent completed scored-run/scoring evidence.
Development failures and corrections remain in the root validation logs; final
successful runs are enumerated in `Evidence/Validation-summary.json`.

Manual checks remain **NOT TESTED** in
[the 218-check Phases 1–4 master acceptance](UI-Phases-1-to-4-Master-Acceptance.md).
It preserves all 148 previous rows exactly and adds 70 Phase 4 rows. Native device
focus/cursor behavior, end-to-end complete guided driving, custom external fonts,
subjective readability and full-frame Profiler comparison need operator sign-off.
Camera-rendered uGUI captures do not certify the combined legacy IMGUI composition.

## Protected work and exact change scope

Before edits, 639 existing experiment files were copied and SHA-256 verified in
`Backups~/UIAuthoring-Phase4-20261106`. A separate manifest protects all 31 route
storage files. No branches, commits or pushes were made.

Only these existing files are changed by Phase 4:

- `Presentation/IceResurfacerGame.cs`: menu registration/availability, fallback-eye
  disclosure, menu pointer ownership, modal Tab policy and immediate presentation refresh.
- `Presentation/IceDashboard.cs`: suppress replaced legacy screen drawing when the
  authored modal owns it; retain all other legacy paths.
- `Scenes/IceResurfacer_AuthoredMachine.unity`: one additive nested menu instance
  and the existing UI instance's added-child list; original non-UI records retained.
- `UI/Editor/IceUIInputValidation.cs` and `IceMachineryPhysicalValidation.cs`:
  their existing safety guards also accept the marked Phase 4 disposable project,
  allowing the unchanged Phase 1/2 test bodies to run against the integrated UI.

New experiment files are the nine menu prefabs above, runtime sources
`IceMenuScreen.cs`, `IceMenuAction.cs`, `IceMenuSetting.cs`, `IceResultWidget.cs`,
`IceMenuScreens.cs`; Editor sources `IceMenuFactory.cs`, `IceMenuPreview.cs`,
`IceMenuValidation.cs`; this report and the consolidated checklist, plus Unity
metadata. New workspace validation scripts/evidence live in `Validation/UIAuthoring-Phase4`.
The final protection audit enumerates exact paths and hashes, including metadata.

Machine/AuroraR54, arena, camera anchors, fill ports, board brush, calibration,
Phase 1–3 prefabs/layouts, physics, core scoring and saved routes are not regenerated
or edited. The prior incompatible Andy guide must keep its GuideRollerX rejection.
Serialized integration is guarded against concurrent file changes and performed
only while the working project is closed. If you have already opened/edited it,
use the additive Install Missing command in your Editor rather than replacing
your scene externally.

## Remaining Phase 5 scope

Unmigrated diagnostics/detail popups, on-foot/service prompts, cockpit/diegetic
presentation and any other remaining procedural overlays retain their current
implementations. Their migration, new gameplay features, final artwork and a new
visual design are outside Phase 4.
