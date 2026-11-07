# AURORA R-54 — final integration pass

2026-11-04. Local Unity 6000.4.1f1 project. This report supersedes the straightaway-roller advice in the older ControlsBrushRoller and AuthoredMachine notes.

The existing machine, arena, mesh assets, materials, snow presentation, water calibration and GPS data were retained. The remaining brush, camera and cockpit features are implemented. Deterministic and static geometry evidence is available; rendered appearance, real mouse interaction, audio listening and native Editor persistence still require licensed Unity acceptance.

## A — inventory and preservation

The latest snow pass was already present: volume-normalized broad caps with shared gradient normals, sparse mesh cleanup, bounded delivery-driven bin spout, and **Bin spout visibility (x)**. These were validated without editing their implementation.

Existing systems retained: authored machine binding, conditioner-mounted valves/handwheel, hydraulic articulation, independent conveyor controls, fitted collision outline, board-brush rig, finite snow stocks, wash pump priming/dependencies, Operator Tuning, recordings and calibrated guides, 24.384 m GPS lookahead and the yellow rear-axle heading line extending six metres beyond the nose.

Outstanding findings at entry:

- The guide still carried the prior 174 mm outboard extension; no subsequent manual tuck-in was present.
- Brush authoring exposed geometry and previews, but lacked explicit saved endpoints and spring state.
- The seated eye was 210 mm left of seat centre. E used calculated yaw/lean rather than its own persistent anchor.
- The steering-column foot was approximately 23.505 mm above the floor.
- No physical brush switch, wash pickup switch or horn existed.

The user confirmed the previous coding passes had finished. A SHA-256 verified copy of all **502** current experiment files was made before edits at `Validation~/FinalIntegration-20261104/Before/`, with `baseline-manifest.json`. This includes pre-existing uncommitted snow work and route data; Git HEAD was not treated as the baseline. No branch, commit or push was made.

Only two pre-existing authored assets changed: `Machine/AuroraR54.prefab` and `Scenes/IceResurfacer_AuthoredMachine.unity`. The scene differs only in `GuideRollerX`. The prefab preserves every original object/component and changes nine existing serialized documents, adding 52 documents for anchors, supports, controls and their bindings. Original renderer, mesh and collider documents are unchanged. All new internal references resolve. The preservation audit compares exact bytes against the local snapshot.

## B — focused snow integration

No snow mesh, shader, spout, tuning or physical snow algorithm was redesigned. Existing **Bin spout visibility (x)** remains available and still controls cosmetic density only.

Fresh executed evidence under `Validation~/FinalIntegration-20261104/`:

| Check | Result |
|---|---|
| Both augers off, ordinary .35 setting, prescribed fresh lane | **18.4 m / 20.12 yd**, authored and procedural |
| Actual driven authored onset | **17.9133 m / 19.5902 yd**, unchanged |
| Actual driven procedural onset | **17.8491 m / 19.5200 yd**, unchanged |
| Ordinary 10 L streak, saved 6 and 50 mm pickup | Zero remaining snow and zero white cells after aligned collection |
| Isolated 30 mm mound, roughness .7 | Approximately **36.259 mm** broad cap; volume integral preserved |
| Board/corner reconstruction | Maximum tested integral error **1.15e-11 m³** |
| Spout sampler, .05 / .7355 / 10 L/s | **17 / 264 / 3599** requested samples over two seconds; native pool separately capped |
| Empty, jammed, full, raised tank or vertical conveyor off | Zero accepted delivery and zero new spout samples |
| Thirty-minute active snow soak | Zero managed allocation; balance residual **−0.029508 L** over approximately 2,039 L initial plus generated snow |

The spout continues to read actual accepted tank delivery, not blade setting or an always-on emitter. Existing rejection, missed-bin and dumping cues remain separate. Pool bounds remain 360 live spout fragments, 64 requested samples per update, and 2,520 maximum particles across all pools.

The centered standing eye was checked against **9,120** authored triangles/conservative primitive surfaces. All **8/8** sampled spout points have clear static rays from machine-space eye `(-.610, 2.778, -.696)`. This is geometric evidence for an empty lowered tank, not proof of rendered brightness, motion or visibility through accumulated bin snow. No speculative snow-shape correction was made.

Evidence: `snow-checks.log`, `onset.log`, `inspection-audit.json`, `inspection.log`.

## C — roller and brush

### Compact front guide roller

The historical pre-extension model provided the compact position. Only the roller group and bracket were corrected; the bracket's inner attachment remains at machine X `−.632 m`. The visible roller and scene collision point now agree.

| Measurement | Before | After |
|---|---:|---:|
| Guide centre X | −1.001 m | **−.827 m** |
| Guide centre Y / Z | .530 / 2.444 m | Unchanged |
| Physical radius | .090 m | Unchanged |
| Bracket length | .369 m | **.195 m** |
| Outermost roller X magnitude | 1.091 m | **.917 m** |
| Relationship to 1.066 m conditioner envelope | 25 mm outside | **149 mm inside** |

A coarse whole-wheel cylinder initially suggested interference. That was a false positive: it combined the hub width with the larger tread radius. The tighter 1 mm axial-strip swept tyre envelope, covering all wheel roll angles and 401 sampled steering positions, gives **6.70 mm minimum clearance**. Direct fixed-spin primitive-surface sampling gives 8.54 mm. No extra forward relocation or enlarged invisible roller was needed. This is sampled static geometry evidence, not a native collision capture.

Executed driving checks cover neutral straightaways, shallow 2–15° glances, harder 25/45° impacts, square head-on stops, reverse recovery, lowered conditioner and service-gate travel. The authored parallel fixture has zero roller contact/rotation and travels 13.25 m at full speed or 7.95 m at resurfacing speed, with the conditioner 25 mm clear. The tested corner has roller contact on **419/450** steps, 21.41 m of rolling, final speed 2.19 m/s and minimum board clearance 20.24 mm. The original procedural configuration was tested separately and remains operable with its existing roller geometry.

No vehicle-contact algorithm or driving dimensions were retuned. Old authored straight-contact success conditions and the Inspector's outboard-extension action were removed. The compact roller is a corner guide; the wider conditioner/body can meet a straight board before it does.

Evidence: `roller-validation.txt`, `RollerHardwareProbe.cs`, `RollerProbe.csproj`, and updated `Validation~/ContactDrivingValidation.cs`.

### Board brush endpoints and compliance

The existing rig now stores two authored endpoints. Initial poses come from the current rig. Existing pivot X/Z `−.548 / 1.3 m`, .38849553 m arm, .192 m radius and 86.32715° stowed / 0° working angles remain intact until the operator enables saved endpoints.

Author in Prefab Mode:

1. Select **Board brush articulated arm / left side**.
2. Click **Edit STOWED**, move/rotate the head using the dedicated Scene handles or Inspector coordinates, then save that endpoint.
3. Repeat **Edit WORKING** and save.
4. Enable **Use saved endpoints in Play**. Use the deployment slider or 0/25/50/75/100% preview buttons to inspect articulation.
5. Save the prefab. Endpoint data persists; transient previews return to stowed on save. Changes use Undo.

Both endpoints must fit one horizontal arm length within 2 mm. A mismatch reports the length errors. Adjust pivot/arm length or use **Project both endpoints onto arm circle**, then save both again. Height and head orientation can differ at the endpoints. Runtime uses a private configuration copy derived from approved endpoints; it does not overwrite the saved scene calibration. Alignment diagnostics respect this authority.

Three controls on the same rig govern compliance:

- **Compliance travel (m): .12** on Aurora.
- **Spring return (rad/s): 12**.
- **Damping ratio: 1**, critically damped return.

Core simulation owns compression and velocity. A board constraint follows the curved-board normal; the bounded damped return restores the deployed rest position. The visible head and snow sweep use the same `BrushWorldX/Z`. Snow is redistributed conservatively beneath that head. The longer procedural brush uses .45 m fallback travel to retain its existing clearance behavior.

**11/11** focused deterministic checks passed: endpoint feasibility, insufficient reach, light compression, full travel, release, curves, material conservation, fixed-step repeatability, procedural behavior, actual driving at 1.59/2.65 m/s and allocation. An 80 mm compression returns to 77.003 mm on the first 20 ms release step, then effectively zero after three seconds without overshoot. The curved-board fixture's largest numerical penetration is .00152 mm. Forty-eight swept cells conserve snow within 2.35e-12 m³.

Real limit: if an edited reach requires more compression than available travel, the brush can overlap the kickplate. The Inspector warns and `BrushTravelLimited` reports this; no invisible travel or remote sweeping is supplied. Adjust the authored reach or travel. Current Aurora's approximately 62.5 mm parallel-board overlap fits within its 120 mm travel.

Evidence and native acceptance instructions: `Brush/notes.txt`, `Brush/results.txt`. The native Undo/save/reload hook compiled but did not run.

## D — camera, mounting foot and physical controls

Open `Machine/AuroraR54.prefab` in Prefab Mode. Under **Left-hand operator station**, the new **Ice Operator Station** component references the two camera anchors and three moving control parts.

**Driver Eye Anchor** remains under the seat and starts at seat-local `(0, .73, .14)`, identity rotation. It moves the viewpoint 210 mm right onto the seat centre and 50 mm forward; neutral world height stays 2.298 m because the old hidden .06 m offset is now included in the anchor. The seat itself is untouched. Edit anchor position and rotation directly. Standing, RMB look, Q, rear glance, chase and dismount continue through the existing camera state system.

**Controls View Anchor** is separately editable under the seat. Its initial seat-local position is `(.18, .86, −.05)`, Euler rotation `(44, 69, 0)` degrees: higher and right of the driver eye, with a rearward offset that clears the raised valves. Static full-machine checks frame **51/51** sampled grip/rim/crank/hub points with unobstructed rays at 0/50/100% conditioner lift. This is geometric evidence, not a rendered/input pass. Hold E to blend to that exact transform; release to restore the saved view. **Controls enter seconds** and **Controls return seconds** default to .24 s. The authored anchor bypasses old automatic E yaw and shoulder lean. RMB can look around from the inspection view. Board/wall head constraints remain active, so a deliberately placed anchor outside the accessible space can be constrained.

The column foot extends downward by 23.5 mm while preserving its top. Its centre moves down 11.75 mm and cylinder Y scale grows from .0375 to .04925. The wheel, column and existing control positions are unchanged; residual floor gap is approximately .005 mm.

Three new saved assemblies sit left of the wheel on a small support rail, using existing materials and built-in primitive meshes:

- **Board brush switch / movable assembly**: maintained switch reading/writing the existing board-brush state; key 4 and HUD remain synchronized.
- **Horn button / movable assembly**: momentary button, bounded two-tone generated clip through the existing positional machine-audio architecture. No suitable existing horn asset/system was found. Release, pause, focus loss, dismount and reset clear the horn.
- **Wash pickup pump switch / movable assembly**: maintained switch reading/writing the existing pump state; key 5 and HUD remain synchronized. Priming, horizontal-auger dependency and dry-pump consequences are unchanged.

Move whole assembly parents to rearrange them. Their named moving grips retain renderers and remain outside static batching. Picking and hover/highlight use actual grip geometry; the picking ray conversion was corrected for nonuniform scale. The initial moving pivots are assigned on Ice Operator Station. Keep those references when reorganizing the hierarchy. The normal seated RMB look down/left reaches the new panel; E concentrates on the existing right-hand controls.

The procedural builder supplies equivalent camera/control functionality without rewriting the procedural scene. A native menu, **Experiments > Ice Resurfacer > Validation > Operator anchors and cockpit controls (Play Mode)**, checks anchor binding, transitions/restoration, existing controls at multiple conditioner heights, clicks, state animation, horn release, batching and moved/scaled picking. It compiled but has not run here.

Camera evidence: `camera-controls-audit.json` and `.log` were regenerated from the final applied prefab; `OperatorIntegrationEvidence.txt` records the supporting checks. The initial generated procedural view is fitted once from its own actual control grips; saved authored anchors are never automatically refitted.

## E — regression, performance and limits

**43/43 deterministic simulation scenarios passed** (`aggregate.log`). Runtime and Editor assemblies compile separately against installed Unity references with zero errors (`compile.log`); obsolete API and existing unassigned-field warnings remain. Compilation is not a native Unity import or Play result.

The current authored ten-minute wash fixture remains **248.1882 L / 310 L**, with .72 L/s maximum flood and .644 wash recovery. Water-stock residual is .060522 L across the float transactions. No capacities, Operator Tuning values or water quantities were changed (`water.log`).

Offline CPU evidence: 100,000 brush updates allocate zero managed bytes, approximately .000258 ms/update in the recorded run. Snow geometry, spout sampler and active snow soak also allocate zero managed bytes in their focused checks. These timings are not Unity frame times. Native mesh upload/draw cost, GPU timing, actual audio and total camera/control allocation remain unmeasured.

GPS recordings/guides and presentation source are byte-preserved. **GuideRollerX is a strict compatibility field**, so guides calibrated to the extended roller require the existing **Revalidate for active scene** workflow and saving a new copy. Do not disable the check or overwrite old recordings. Read-only full fitting of eight saved guides found four Ready with zero boundary/curvature/source errors. Four older Andy_Try_1 versions already failed the current pre-integration body outline; the compact roller reduces their boundary-error samples from 31 to 23 and introduces none. The newest valid guide lengths remain 131.581 m and 1003.196 m. See `saved-guides-validation.txt`.

An isolated Unity project was created under `Validation~/FinalIntegration-20261104/Validation~/UnityProject`. The owned Unity process stalled at **Licensing is not yet initialized** and was stopped. No native validator, Play Mode capture or screenshot ran. Existing open Unity processes were not stopped. `unity-native.log` and `unity-attempt-status.txt` record the boundary.

## Files changed

Existing assets: `Machine/AuroraR54.prefab`; `Scenes/IceResurfacer_AuthoredMachine.unity` (`GuideRollerX` only).

Existing runtime scripts:

- `Core/BoardBrushArticulation.cs`, `Core/IceCalibrationConfig.cs`, `Core/SurfaceProcessing.cs` — endpoint feasibility and brush spring only.
- `Presentation/IceBoardBrushRig.cs` — endpoint persistence, derived configuration and shared head posing.
- `Presentation/IceResurfacerGame.cs` — anchor transitions, shared state, private runtime brush configuration.
- `Presentation/IceMachineRig.cs`, `Presentation/IceWorldBuilder.cs` — authored binding, batching exclusions and procedural equivalents.
- `Presentation/IcePhysicalControls.cs`, `Presentation/IceMachineAudio.cs` — three controls, geometry picking and horn.

New runtime component: `Presentation/IceOperatorStation.cs` and `.meta`.

Editor scripts: `IceBoardBrushRigEditor.cs`, `IceMechanicalAlignment.cs`, `IceMachineRigEditor.cs`, `IceControlsBrushRollerValidation.cs`, `IceCameraValidation.cs`; new `IceOperatorIntegrationValidation.cs` and `.meta`. Validation also updates `Validation~/ContactDrivingValidation.cs` and adds the evidence/harness files under `Validation~/FinalIntegration-20261104/`.

This report and its `.meta` are new. The exact final inventory is in `preservation-audit.json`. The completed snow pass's previously uncommitted files are unchanged by this integration.

## Safe inspection and acceptance

Changes are already applied locally. Nothing needs to be rebaked. Let Unity import the changed scripts/prefab, open the authored scene and inspect the compact guide, centred seat view and new switch panel. If the Editor has unsaved in-memory prefab/scene edits, compare them before saving over disk; the backup and exact deltas remain available.

For isolated acceptance, run `Stage.ps1` from the evidence directory using PowerShell with the required local script execution policy. It copies the current experiment into the disposable project and does not copy back. Use `-UpdateOnly` after source changes. Never replace the main project wholesale from that copy. `ApplyAssets.py` records the narrow asset construction and refuses application if current files differ from the verified baseline or last application hash. Restore only selected files from `Before/` after comparing any later user changes.

In licensed Unity, check both scenes separately:

1. Inspect straight/corner roller contact and tire clearance; drive glances, a square stop, reverse recovery and the service gate.
2. Save both brush endpoints, use Undo/Redo, save/reopen the prefab, preview intermediate deployment, then drive against straight and curved boards and release contact.
3. Adjust the two anchors, test held E and overlapping Q/C/rear inputs, standing, RMB and dismount; confirm exact release restoration and seat clearance.
4. Click all three new controls; verify hover, maintained detents, horn press/release/focus loss, and existing valves/handwheel at lowered, half and raised conditioner positions. Run the new Play Mode menu.
5. Stand and look through the inspection opening during actual delivery, then stop delivery. Inspect mound shape/recollection, bin filling and dumping. Run the existing snow mesh validation/native snow harness.
6. Revalidate the desired saved guide into a new copy, inspect GPS at 80 ft lookahead and its heading line in the modified camera views, then measure native frame time, allocations and GPU cost.

The unresolved limits are rendered/operator acceptance, strict saved-guide revalidation, and intentionally reported brush overtravel for incompatible future endpoint edits. No speculative visual approval is claimed.
