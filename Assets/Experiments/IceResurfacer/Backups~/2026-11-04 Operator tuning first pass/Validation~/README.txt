AURORA R-54 / VALIDATION
29 October 2026

Current scope and status
This Unity-ignored folder contains dependency-free core tests, the isolated
Unity project, native player helpers, actual rendered evidence and measurements.
Current behavior and integration status are documented in ../README.txt and
../Documentation/OperatorErgonomics.txt. MachineryErgonomics.txt,
SurfaceConsistency.txt and MotionContacts.txt explain the individual systems.
OperatorAuthenticity.txt and earlier reports are historical, not current results.

Current executed aggregate: PASS, 37 deterministic groups in
Operator-ergonomics-core.txt. Route-guide-fullrun.txt records the expanded
operation with deliberate overflow, recovery, stationary flood and freezing.
Integrated Unity suites PASS with zero managed runtime errors. Native HUD tests
PASS at 1366 x 768 and 1920 x 1080 with zero measured text clipping. Their hidden
D3D presentation warnings are counted separately: 11 and 6 respectively.
The final snow-recovery camera evidence passes its nonblank/content checks;
Both optimized rendered performance runs are complete (16 workloads each).
The current core and resolution-specific HUD reports retain their scope; historical
reports are not current results.
A source hook, an old passing report or an image alone does not establish a pass
for the latest revision. Preserve failed run logs as part of the investigation.

Core tests and commands
Run from the repository root with .NET 9:
  dotnet run --project Assets/Experiments/IceResurfacer/Validation~/IceResurfacer.Validation.csproj --configuration Release -p:UseSharedCompilation=false

The 37 groups cover deterministic driving/braking/reverse, actual contact,
conditioner footprints, independent machinery, priming/pickup, finite material,
jams, spill recovery, manual dumping, scoring and Free Simulation freedom.
New checks cover the area-weighted survey, finite stationary pools, phase
progression, traversable unloading grate, contact severity, limited conditioner
yaw and the generated advisory route. The physical field remains 5 cm.

Focused entry points and evidence:
- RouteGuideProbe.csproj / Route-guide-fullrun.txt: generated geometry is
  read-only; a validation driver executes departure, working passes, intentional
  augers-off cutting, later snow recovery, a five-second flood stop, return,
  unloading and results. This driver is not an in-game autopilot.
- SurfaceConsistencyProbe.csproj / Surface-consistency-results.txt: 20 cm
  aggregation over unchanged 5 cm cells, narrow wet/uncut seams, initial snow,
  stationary flood at 5/15/30 seconds, retained liquid/frozen quantities,
  conservative spreading, board retention, pickup and 0/5/10/20/30 minute aging.
- WaterPerformanceProbe.csproj / Water-performance-comparison.txt: core-only
  30,090-step sustained workload before/after exact water-phase caching, with
  paired raw results and seven complete cell-state hashes. This performance
  fixture resets the vehicle to its lane start; it is not the driven full route.
- ServiceCollisionProbe.csproj / Motion-contact-focused.txt: grate crossings
  at 0, +/-15, +/-30 and 90 degrees; actual wall/reverse recovery; normal-speed
  impacts; bounded conditioner compliance; floor/grate drainage; raised-tank
  contact and clearance. The old pit rectangle is no longer a movement barrier.
- FreedomProbe.csproj: free operation, actual landing geometry and traction.
  Append '-- route' after dotnet run options to run the normal complete route.
  Aggregate Operator-ergonomics-core.txt contains its current full-run outcome.
- IceCalibrationProbe.csproj: fractional stamps, physical width/tire alignment,
  real liquid pickup, residual grooves, freezing, brush contact and scoring mask.
- SnowCalibrationProbe.csproj / Snow-calibration-focused.txt: finite side escape,
  repeated pickup, separate failure states and source accounting. The aggregate
  checks the corrected compact conveyor outlet through shared geometry values.
  Snow-calibration-before.csv and Snow-calibration-after.txt retain the previous
  pass's 18 full-sheet apparatus comparisons, not a newly driven full operation.
- MinimapValidation.cs: known working passes reproduce actual wet-but-uncut
  slivers and compare correctly overlapped passes without granting cutting credit.
- ServiceFreedomValidation.cs and ServiceValidation.cs: free moving/active exits,
  persistent equipment, hose breakaway/recovery, ordinary refill sequences and
  material balance. Both are included in the aggregate harness.

Run the focused water performance reproduction with .NET 9:
  dotnet run --project Assets/Experiments/IceResurfacer/Validation~/WaterPerformanceProbe.csproj --configuration Release -p:UseSharedCompilation=false

Water-performance-before.txt and Water-performance-after.txt record 29,999
measured steps after warmup. Mean core time fell from 1.948 to 1.086 ms (44.2%);
the last 5,000-step window fell from 2.697 to 1.674 ms. All seven state hashes,
active-cell counts and reported water totals match exactly. The optimization
preserves exact exponential phase physics, flow order, thresholds, cadence and
work budget; two lazy cache arrays add approximately 5 MB at the default grid.
All 37 aggregate core groups pass on the optimized source. These .NET timings
exclude Unity, rendering and UI and are not FPS or native-player measurements.
Both optimized native benchmarks are complete; the rendered performance section
below records the executed results separately from these .NET core timings.

Individual fixtures may seed poses or stocks to isolate behavior. The complete
routes use only throttle, steering and equipment commands after the default spawn;
there are no later pose/cell/tank writes, refills, capacity changes or docking snaps.
Cumulative throughput is not counted again as stored material. Material balance
sums actual ice, conveyors, tank, receiver and floor stocks.

Current complete-operation results
The normal driven route reports 96.97% reachable combined treatment, 95.43%
whole-ice combined treatment, 97.33% water coverage, 77.6% finish and 89.0 points.
It uses a reported 677.0 L flood, unloads 0.729 m3 to the receiver, runs for
1,787 simulated seconds and records zero misuse with a proper southeast return.

The generated-route operation retains the 95% completion requirement and reports
95.69% reachable treatment, 96.12% water coverage, 79.6 points and seven deliberate
misuse events. It unloads 0.8099 m3 after 1,791 seconds. Augers-off cutting spills
0.049787 m3; the same deposit cells later lose 0.024935 m3 across 1,538 cells.
Some snow remains physically deposited. Snow source balance error is
-0.00000194 m3. Stationary flooding releases 9 L in five seconds and reaches
10.276 mm peak liquid depth. Final field water is 137.7363 L liquid plus
548.2549 L frozen; material is not removed merely to make the view look dry.

The long-run water audit integrates the exact finite per-tick flow in double
precision: 685.989337 L versus 685.991194 L retained, a 0.001858 L difference.
Existing float counters separately report 686.198303 L released and 686.305481 L
from tank depletion. This approximately 0.03-0.05% cumulative rounding is disclosed;
it is not silently represented as an exact tank-to-field balance.
Guide visibility and manual deviation are separate native UI checks. Core route
completion demonstrates feasible control-driven geometry, not human driving feel.

Historical distinction
OperatorErgonomicsBefore/ preserves the current pass's starting images, reports
and prior performance data. The preceding OperatorAuthenticity pass reported
35 core groups, 96.67% reachable / 95.14% whole-ice coverage, 88.8 points and
0.724 m3 receiver snow. Its Unity/native/performance successes apply to that
revision. Earlier OperatorAuthenticityBefore/, SurfaceServiceBefore/ and
OperatorFreedomBefore/ folders retain still older baselines. Reachable and
whole-sheet denominators differ, and geometry/route/optics changed between
passes; these route figures are not an isolated layout A/B comparison.

Isolated Unity validation
StageUnityValidation.ps1 creates Validation~/UnityProject with its own Settings,
Packages and Library. Run without -UpdateOnly initially; use -UpdateOnly later:
  powershell -NoProfile -ExecutionPolicy Bypass -File Assets/Experiments/IceResurfacer/Validation~/StageUnityValidation.ps1 -UpdateOnly

Use the installed Unity 6000.4.1f1 executable against that isolated project, with
-batchmode -force-d3d11 and:
  -executeMethod IceResurfacer.Editor.IceResurfacerEditor.RunBatch
Do not add -quit: the method enters actual Play Mode and exits after validation.
The helper requires the local Unity license. It does not switch or stop the
original open Editor, edit its project settings/packages, or alter build scenes.
The completed Operator-ergonomics-unity-8.log records all suites passing with
zero final runtime errors. Earlier numbered logs retain the investigation.
Reports and images first land in the
copy's own experiment Validation~ directory; promote the final passing evidence
here after checking that it corresponds to the source under review.

Runtime evidence map:
- Unity-results.txt: launch, assets, shaders, spatial lane, audio and managed logs.
- Camera-results.txt: Q/E/C/Left Shift holds, exact custom-view restoration,
  overlap priority, external chase, stance/recenter, wheel steering and sightlines.
- Layout-results.txt: transverse controls, authored geometry/capacity, scene
  lighting and shared-parameter preview checks.
- Physical-controls-results.txt: actual ray picking/gestures, latched augers,
  spring hydraulics, independent L valves and full 3.5-turn blade-wheel travel.
- Authenticity-results.txt: actual Update input, Right Shift machinery/precision,
  Left Shift camera independence, drive recovery, pause/focus and shakeout.
- Service-results.txt: on-foot route, both fills, overflow, nozzle return,
  boarding, hose reach/breakaway and moving dismount with equipment preserved.
- Service-geometry-results.txt: traversable grate, actual contact severity,
  shared tank/cylinder geometry, finite floor water and snow surface orientation.
- Operations-results.txt: finite conveyors, strong side escape, sparse 3D ground
  deposits, recovery and no duplicate collection, dump/pit pile normals/volume.
- Ice-calibration-runtime-results.txt: real-cell/GPU/map agreement, fractional
  footprint, pickup, initial overview, grooves, finite pools and timed drying.

Native HUD verification -- PASS at both resolutions
Build in the isolated project using:
  -executeMethod IceResurfacer.Editor.IceResurfacerEditor.BuildLayoutPlayer
This creates the owned development player at Validation~/Build/AuroraLayout.exe.
Run each resolution serially:
  powershell -NoProfile -ExecutionPolicy Bypass -File Assets/Experiments/IceResurfacer/Validation~/RunLayoutPlayer.ps1 -WindowMode Offscreen -Width 1366 -Height 768
  powershell -NoProfile -ExecutionPolicy Bypass -File Assets/Experiments/IceResurfacer/Validation~/RunLayoutPlayer.ps1 -WindowMode Offscreen -Width 1920 -Height 1080

The helper operates only its hidden child window. It does not move the user's
OS pointer or activate the open Editor. It checks real IMGUI lever/valve/wheel
interactions, compact and expanded side consoles, persistent UP/DOWN/partial
status, history, quicklooks, survey, F5 route, modals and measured text clipping.
HUD-player-results-<resolution>.txt and HUD-layout-issues-<resolution>.txt record
the completed passing runs with zero measured text clipping at both resolutions.
Native hidden-window D3D warnings are 11 at 1366 x 768 and 6 at 1920 x 1080,
reported separately from managed errors. HUD-* PNGs are actual framebuffers. Capture FPS labels
include screenshot/automation waits and are not benchmark measurements.

Rendered performance -- final measurements complete
Final sustained mean/p95: 9.210/23.783 ms at 1366x768; 7.818/23.297 ms at
1920x1080. Both have zero managed errors, with 34/43 separately counted native
presentation warnings. Large accumulated-water regions remain costlier than
the earlier feature baseline. PerformanceOperatorErgonomics/Performance-summary.txt
records all 16 scenarios, allocation, measured correction and raw evidence.

Build the separate opt-in benchmark in the isolated project using:
  -executeMethod IceResurfacer.Editor.IcePerformanceBuild.Build
It creates Validation~/PerformanceBuild/AuroraPerformance.exe. For example:
  powershell -NoProfile -ExecutionPolicy Bypass -File Assets/Experiments/IceResurfacer/Validation~/RunPerformancePlayer.ps1 -WindowMode Offscreen -Width 1366 -Height 768 -EnduranceFrames 30000 -OutputFolder PerformanceOperatorErgonomics
Repeat serially at -Width 1920 -Height 1080. The output folder is explicit so
current data does not overwrite earlier comparison reports. Record the actual
measured interval count from each CSV, rather than assuming it equals the
requested frame budget. Use duration-matched repeats when comparing shorter
historical workloads, and keep both the long trace and the matched repeat.

The current probe has 16 workloads: driving and active cutting in both HUD modes,
overflow, camera changes, walking, hose fill/spill, dumping/shakeout, sustained
operation, whole-sheet wetness, real side escape, chase, stationary pooling and
the advisory route. New pooling/route scenarios have no equivalent old baseline.
Frame time includes the whole player; CPU_work excludes IMGUI/render. Core test
timings are not FPS. Work allocation is separate from whole-frame GC allocation.
The development Mono player uses D3D11, VSync off and a hidden offscreen window,
with one 20 ms simulation tick per rendered frame. This measures workload
throughput, not focused-window input latency or the user's Editor frame rate.

Do not run Unity builds, native HUD tests, performance players or other heavy
validation concurrently. Hidden-window D3D presentation warnings bypass Unity's
managed log callback; launchers count them separately in report footers. Preserve
logs for each reported run. A zero managed-error count does not imply zero native
warnings. PerformanceOperatorAuthenticity/ and its Matched folder contain the
previous completed pass; PerformanceSurfaceService/ and earlier folders retain
older measurements. Do not carry their timings forward as current results.

Current capture targets
OperatorErgonomicsBefore/ is the before set. After captures include:
- Operator-seated/standing-window, Camera-preset-Q/E/C/LeftShift/X,
  Cockpit-hydraulic-controls, and Operator-steering-left/center/right with
  corresponding -inspection views.
- Conditioner-straight/curved; Grate-traversal-0deg/30deg/90deg;
  Contact-slow-direct/hard-direct/fast-glance/service-wall; raised tank/glass.
- Minimap-initial-overview, Minimap-area-overview, exact treatment map and
  wet/uncut seam world/material/GPU comparisons.
- Ice-stationary-flood-00s/05s/15s/30s and
  Ice-wet-age-00min/05min/10min/20min/30min.
- Snow-disabled-augers-both-sides, windrows-before/after-recovery,
  Snow-dumping, Snow-dumped-pit and Service-floor-snow.
- HUD-<resolution> normal, console, operator, compact, messages, conditioner states,
  survey, route, quicklooks, diagnostics, help/pause/results and service.
These suffix groups denote separate PNGs; see OperatorErgonomics.txt for names.
All geometry/surface images come from actual Unity cameras. Some diagnostic views
hide the machine to expose the physical surface; native HUD images retain IMGUI.

Custom geometry validation
Machine Geometry Editor uses shared parameters and the runtime builder, but an
edit does not automatically reshape every collision hull. Revalidate custom
wheel-track settings at full left/right steering, including the swept tire
clearance, body/board clearance and operator alignment reference. The default
1.37 m track is validated; static width validation alone does not establish that
an arbitrary wider configuration clears at full lock. Rebuild the preview and
rerun applicable geometry/contact checks before treating custom settings as proven.

Final preservation and interpretation
AuditErgonomicPreservation.py checks source metadata/GUIDs, experiment-only changes
and hashes of current source/assets against the staged project. Its output is
Preservation-results.txt. An audit before final staging is not a final built-source
match. No branch, commit, push, shared-package or original-settings edit is part
of this work. The original Editor remains available to the user.
Automated evidence does not establish manufacturer authenticity, final operator
comfort, real tire/suspension response, sound quality or stability beyond the
measured workload. The main report records remaining human calibration questions.
