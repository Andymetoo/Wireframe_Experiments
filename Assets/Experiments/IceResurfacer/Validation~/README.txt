AURORA R-54 / VALIDATION
28 October 2026

Scope and authoritative results
This folder is ignored by Unity because its name ends in ~. It contains the
standalone core harness, isolated Unity project/build helpers, actual rendered
evidence and measurements. Current behavior is documented in ../README.txt,
../Documentation/OperatorAuthenticity.txt, OperatorCamera.txt, SnowCalibration.txt
and PitServiceCollision.txt. SurfaceServiceRefinement.txt preserves the previous
completed pass. OperatorFreedom.txt, IceCalibration.txt and Performance.txt retain
older measurements, including earlier 28 October data.
Generated reports establish the status of their own run. A source description
or older image alone does not establish that the latest runtime checks passed.

Deterministic core
Run the actual Core source with .NET 9:
  dotnet run --project Assets/Experiments/IceResurfacer/Validation~/IceResurfacer.Validation.csproj --configuration Release -p:UseSharedCompilation=false
Final executed aggregate: PASS, 35 deterministic scenario groups, recorded in
Results.txt. Final integrated Unity suites also PASS with zero managed errors;
native HUD runs also PASS at both resolutions. Both sustained performance runs
and the duration-matched comparison repeats are complete. The current pass
report distinguishes measured throughput from mechanics and human calibration.
The harness covers deterministic drive/braking/reverse, contact/guide rollers,
trailing footprints, separate conveyors, priming/pump wear, water/snow balance,
real jams and recovery, residual dumping, scoring/practice, repeated control/
resume sequences, Free Simulation versus training, hose recovery, finite film,
pickup, the shaving mask and the complete southeast route.

Specialized entry points and reports:
- FreedomProbe.csproj: free operation, actual landing geometry, traction and
  southeast gate tests; pass '-- route' after dotnet run options for FullRun.
- Freedom-results.txt: unrestricted and unconventional operation, training
  isolation, moving dismount, arbitrary/angled dump locations and material totals.
- Southeast-full-run.txt: the complete scored route from the centered nose-in
  spawn through reverse departure, resurfacing, forward return, unloading and finish.
- ServiceFreedomValidation.cs: invoked by the aggregate harness; moving/active
  dismount, centered straight reverse exit, hose departure/breakaway, return and conservation.
- ServiceValidation.cs: the retained safe service sequence in Guided Training.
- IceCalibrationProbe.csproj / Ice-visual-refinement-results.txt: real 5 cm state,
  fractional film stamps, aging, residual gouge depth, tire-aligned parallel
  passes, liquid pickup, frozen-water balance, brush footprint and scoring mask.
  Ice-calibration-results.txt retains the previous surface calibration.
- IceWidthBeforeProbe.csproj / Ice-width-before-results.txt: the captured previous
  implementation's straight-pass width and alignment measurement.
- SnowCalibrationProbe.csproj / Snow-calibration-before.csv and
  Snow-calibration-after.txt: 18 full-sheet dry apparatus cases with three blade
  settings, three conveyor configurations and first/second passes. Prescribed
  lane poses isolate machinery; these are not physically driven route tests.
  Snow-calibration-focused.txt checks normal/deep mapping, finite side escape,
  persistent recovery, distinct failure states and independent material balance.
- MinimapValidation.cs / Minimap-results.txt: actual working passes reproduce a
  wet-but-uncut seam; strict state classification and correct-overlap comparison
  preserve existing coverage thresholds. Hover magnification exposes real cells.
- ServiceCollisionProbe.csproj / ServiceCollisionValidation.cs: slow straight,
  angled and side pit approaches, contact/reverse recovery, no automatic glancing
  alignment, finite floor drainage and physical raised-tank contact/pit overhang.
  OperatorAuthenticityBefore/Pit-collision-before.txt preserves the old margin.
- Movement-before.txt: the original rejected dump-target travel-latch failure.

Full-run comparison
The current control-driven scored route records 96.67% combined treatment of
shavable ice, 95.14% combined treatment of ALL ice, 97.04% water coverage,
77.3% finish, 88.8 points, 677.0 L flood use, 0.724 m3 snow in the pit,
1787 simulated seconds, zero misuse faults and a proper southeast return.
The preceding operator-freedom route recorded 97.05% reachable / 95.16%
whole-ice coverage, 82.2% finish and 90.4 points. Earlier whole-ice coverage
was 95.18%. Reachable and whole-sheet denominators differ; current depth/aging
calibration and the driven route also changed. This is not an isolated layout A/B.

The full route only uses throttle, steering and equipment commands after the
normal spawn. It does not write poses/cells/tanks, refill, change capacities or
snap to the pit. It is a regression harness, not an in-game autopilot or an
optimal operating pattern. Individual mechanism tests use disclosed fixtures
and smaller reservoirs to isolate effects. Material balance counts actual stored
ice/snow/water in cells, conveyors, tank, pit and floor. Cumulative throughput
is not counted again as an independent storage reservoir.

Ice calibration interpretation
Blade remains 1.956 m, towel 2.100 m, body 2.130 m, wheel track 1.370 m and tire
width 0.215 m. The outside tire edge is 0.7925 m from the centerline. At the test
0.050 m grid phase, the strong wet band is 2.300 m. The preceding 0.100 m
implementation measured 2.200 m after finite spreading and its baseline 2.000 m.
The current GPU shader-response mask spans 2.367 m at its 60% display threshold
across the physical 2.100 m towel. This is not an objective perceived-width
guarantee under every reflection angle. Correctly tire-aligned straight tests
at 1.9425 m centers have zero dry/unscored cells between passes. Wider 2.05 m
spacing reproduces 80 wet-but-uncut cells in the inspected strip. The wet fringe
can conceal missed cutting, while the overview can exaggerate/omit subpixel
slivers. Both effects are reported without filling gaps or relaxing treatment.
Turning and visible-edge recognition still require operator playtesting.
VisualIceRefinement.txt explains physical thresholds, real
520 x 1200 samples, fractional edge stamps, residual depth and aging. The
420-second phase constant retains 48.95% liquid at 5 minutes and 5.74% at 20;
liquid plus frozen water is conserved.

The new film uses limited existing water volume and leaves all non-ice cells
dry. A 0.1524 m mask excludes physically inaccessible shaving perimeter only;
water/finish still assess the complete sheet. Pickup removes only real liquid
within its contact region, under pump and wash-tank limits. Frozen material
cannot disappear. CPU timings printed by this core harness exclude Unity, UI
and graphics and are not FPS measurements.

Unity / actual input / rendering
StageUnityValidation.ps1 creates an isolated project at Validation~/UnityProject
with its own Settings, Packages and Library. Run without -UpdateOnly the first
time; use -UpdateOnly subsequently:
  powershell -NoProfile -ExecutionPolicy Bypass -File Assets/Experiments/IceResurfacer/Validation~/StageUnityValidation.ps1 -UpdateOnly
Run installed Unity 6000.4.1f1 against THAT copy with -batchmode -force-d3d11
and -executeMethod IceResurfacer.Editor.IceResurfacerEditor.RunBatch.
Do not add -quit: the method enters actual Play Mode and exits after its checks.
The validator needs access to the local Unity license. The original open Editor,
its scene, project settings, packages and build-scene list remain untouched.
Reports and images first land in the copy's own experiment Validation~ directory;
copy the successful final reports/captures here after the run.

Runtime report map:
- Unity-results.txt: launch/assets/shader/spatial lane/audio and managed errors.
- Camera-results.txt: persistent RMB; held Q/E/C and exact saved-pose restoration;
  overlap priority; external chase; X, R/Space, actual wheel articulation/sightlines.
- Authenticity-results.txt: actual game Update input, drive recovery, release,
  focus/pause, contact recovery and shakeout emission.
- Physical-controls-results.txt: ray picking, latched augers, held hydraulics,
  independent quarter-turn valves, and full 3.5-turn blade-wheel travel.
- Service-results.txt: actual F/E/W route around the southeast machine room,
  free mouse look, both fills/overflow, broad highlighted return, boarding,
  reverse hose breakaway and moving dismount with equipment state preserved.
  Every walk step checks continuity/body clearance; the taut-hose test looks up
  to extend the nozzle and retreats with S without a reset. Reach/obstruction
  messages and the persistent hose-length display are exercised.
- Operations-results.txt: snow transfer, actual finite trough, two pooled side
  streams, persistent deposits and recovery/no duplicate material.
- Ice-calibration-runtime-results.txt: tire overlap, scoring mask, before/after
  pickup, GPU water-response width, residual-groove lanes and timed wet aging,
  rendered from actual cells by IceCalibrationRuntimeValidation. This pass adds
  strict map/world seam agreement and identical-water/pose reflection A/B images.
The final integrated run passes every suite with zero managed runtime errors.
Camera checks include exact restore/stance/chase behavior and actual wheel
geometry/sightlines (124 checks); relocated physical controls pass 40 checks.
Service-geometry-results.txt verifies the six pit approaches, real raised-bin
pivots and visible conserved floor runoff (55,290 changed same-camera pixels).
Some rendering stress fixtures deliberately seed near-full initial snow stock.
The complete core route does not. Check final generated results for runtime
status; a pending rerun must not be summarized as a successful final run.

Native HUD verification
Run -executeMethod IceResurfacer.Editor.IceResurfacerEditor.BuildLayoutPlayer
against the isolated project. It adds a probe to a temporary scene and builds
Validation~/Build/AuroraLayout.exe. Run RunLayoutPlayer.ps1 with -Width 1366
-Height 768, then -Width 1920 -Height 1080; -WindowMode Offscreen is the capture
path. The helper uses only its owned hidden/non-activating window, sends native
messages and does not move the user's OS pointer or change the open Editor.
It checks real IMGUI interactions, spring/latching controls, valve and handwheel
ranges, guide/pause/welcome/results/compact/service layouts and text clipping.
HUD-player-results-<resolution>.txt and HUD-layout-issues-<resolution>.txt record
those outcomes. HUD-* PNGs are actual native framebuffers, not mockups.
Run isolated builds and native probes serially. A first 1366 run during an
overlapping build showed automatic-pause behavior, likely from focus interference;
unchanged-binary serial reruns passed with no measured clipping or managed errors. HUD capture
FPS labels include synchronous scripted ticks, captures and automation waits;
they are not benchmark results or focused-window latency measurements.

Performance measurements
RunPerformancePlayer.ps1 drives the separate opt-in performance player at
1366x768 and 1920x1080. PerformanceOperatorAuthenticity/ contains the completed
14-workload runs with 29,999-interval sustained operation. The prior-duration
repeats in PerformanceOperatorAuthenticityMatched/ also completed; refer to
../Documentation/OperatorAuthenticity.txt for the full measurements and limits.
The matched repeats retain the old 1,499-interval long_session budget. They
do not replace the longer traces: the slower 1920 whole-wet result remains
reported, with its cause unresolved. All four benchmark runs report zero managed
errors; native presentation warnings are 73/84 extended and 18/18 matched.
PerformanceSurfaceService/ holds the PREVIOUS completed pass, including its
late-session wet-sheet workload. ../Documentation/SurfaceServiceRefinement.txt
records those 12 scenarios and their measurement limits. The new benchmark adds
real side escape and actual chase rendering; the maximum-cut workload also
changes with the calibrated .600 mm bite, so it is not identical old physics.
PerformanceBefore/, PerformanceAfter/ and PerformanceEndurance/ retain the prior
operator-freedom comparison described in the historical Performance.txt.
OperatorAuthenticityBefore/ preserves this pass's starting images/reports and
PerformanceSurfaceService data. SurfaceServiceBefore/ preserves the prior pass
starting images and the first
5 cm timing trace, Performance-5cm-before-bounded-updates-1366x768.csv, before
additional update-cost work. OperatorFreedomBefore/ retains still earlier images.
The current hidden-player comparison is a separate measurement task; do not
infer improved FPS from passed mechanics tests or preliminary individual CSVs.
F4 shows smoothed FPS/frame time at 4 Hz; F3 shows measured CPU work, available
GPU timing, last-frame allocation and draw calls during normal play.

Unity's hidden-window Direct3D11 presentation warnings bypass the managed log
callback. The launchers count them separately in report footers and preserve
player logs. The opt-in layout probe hides the Development Console overlay for
clean captures without suppressing the log. Neither hidden-window performance
nor a zero managed-error count establishes focused-window input latency.

Evidence and remaining manual checks
OperatorAuthenticityBefore/ contains this refinement's before-view evidence;
SurfaceServiceBefore/ contains the preceding surface/service baseline;
OperatorFreedomBefore/ contains the preceding pass's comparison images. Current
operator images include Operator-seated.png, Operator-standing-window.png,
Operator-board-side-edge.png and Camera-preset-*.png. Southeast-entrance.png,
Southeast-service-room.png and Service-*.png show the actual room/service flow.
Ice overlap/mask/pickup and snow-operation images show simulated material state.
Ice-water-response-top.png is the thresholded shader-response measurement.
Ice-wear-before-top.png, Ice-wear-after-top.png, Ice-wear-before-oblique.png and
Ice-wear-after-oblique.png compare untreated, shallow, deeper and wet cutting
lanes. Ice-wet-age-00min.png,
Ice-wet-age-05min.png, Ice-wet-age-15min.png and Ice-wet-age-25min.png show actual
elapsed-time aging; the measurement views hide machinery to expose the surface.
All are actual Unity camera renders; HUD captures separately include IMGUI.

The supplied traditional station and tall seated photos guide ergonomics. The
annotated 526 photograph confirms left-to-right VERTICAL / CONDITIONER /
HORIZONTAL / DUMP, with independent water valves and blade handwheel. New
Operator-steering-left/center/right.png captures show actual tire visibility;
matching -inspection.png captures use a modest down-glance from the same seat.
HUD-<resolution>-survey.png verifies exact-cell hover inspection in final native runs.
HUD-<resolution>-pit-contact.png and -pit-overlay.png verify full diagnostics
and the compact TAB + F3 contact panel, which leaves world-center outlines visible.
The same-state Ice-reflection-before/after.png pair isolates optical polish;
Snow-disabled-augers-both-sides.png and Snow-windrows-before/after-recovery.png
show actual simulated side escape and collection. These Unity captures are from
the successful final integrated run. Final native HUD runs also pass at both
resolutions with zero measured clipping/managed errors and 11 separately counted
native presentation warnings each. Performance is a separate measurement.
Automated tests do not establish manufacturer authenticity, subjective sound,
real tire response, hose weight, long-term stability beyond the measured window,
or final ergonomic comfort. Actual operator playtesting remains necessary.
