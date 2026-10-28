AURORA R-54 / VALIDATION
28 October 2026

Scope and authoritative results
This folder is ignored by Unity because its name ends in ~. It contains the
standalone core harness, isolated Unity project/build helpers, actual rendered
evidence and measurements. Current behavior is documented in ../README.txt and
../Documentation/OperatorFreedom.txt. Earlier 25-27 October documentation keeps
historical metrics; it is not the current control or southeast-layout guide.
Generated reports establish the status of their own run. A source description
or older image alone does not establish that the latest runtime checks passed.

Deterministic core
Run the actual Core source with .NET 9:
  dotnet run --project Assets/Experiments/IceResurfacer/Validation~/IceResurfacer.Validation.csproj --configuration Release -p:UseSharedCompilation=false
Current aggregate result: PASS, 32 deterministic scenario groups, recorded in
Results.txt. It covers deterministic drive/braking/reverse, contact/guide rollers,
trailing footprints, separate conveyors, priming/pump wear, water/snow balance,
real jams and recovery, residual dumping, scoring/practice, repeated control/
resume sequences, Free Simulation versus training, hose recovery, finite film,
pickup, the shaving mask and the complete southeast route.

Specialized entry points and reports:
- FreedomProbe.csproj: free operation, actual landing geometry, traction and
  southeast gate tests; pass '-- route' after dotnet run options for FullRun.
- Freedom-results.txt: unrestricted and unconventional operation, training
  isolation, moving dismount, arbitrary/angled dump locations and material totals.
- Southeast-full-run.txt: the complete scored route from the production southeast
  spawn through entry, resurfacing, return, angled unloading and finish.
- ServiceFreedomValidation.cs: invoked by the aggregate harness; moving/active
  dismount, permissive hose operation, departure/breakaway, return and conservation.
- ServiceValidation.cs: the retained safe service sequence in Guided Training.
- IceCalibrationProbe.csproj / Ice-calibration-results.txt: tire-aligned parallel
  passes, liquid pickup, frozen-water balance, brush footprint and scoring mask.
- IceWidthBeforeProbe.csproj / Ice-width-before-results.txt: the captured previous
  implementation's straight-pass width and alignment measurement.
- Movement-before.txt: the original rejected dump-target travel-latch failure.

Full-run comparison
The current control-driven scored route records 97.05% combined treatment of
shavable ice, 95.16% combined treatment of ALL ice, 97.25% water coverage,
82.2% finish, 90.4 points, 674.8 L flood use, 0.723 m3 snow in the pit,
1759 simulated seconds, zero misuse faults and a proper southeast return.
The prior whole-ice coverage baseline was 95.18%. Compare 95.16% with that figure;
the 97.05% result uses the new reachable mask. The score weights also changed.

The full route only uses throttle, steering and equipment commands after the
normal spawn. It does not write poses/cells/tanks, refill, change capacities or
snap to the pit. It is a regression harness, not an in-game autopilot or an
optimal operating pattern. Individual mechanism tests use disclosed fixtures
and smaller reservoirs to isolate effects. Material balance counts actual stored
ice/snow/water in cells, conveyors, tank, pit and floor. Cumulative throughput
is not counted again as an independent storage reservoir.

Ice calibration interpretation
Blade remains 1.956 m, towel 2.100 m, body 2.130 m, wheel track 1.370 m and tire
width 0.300 m. The outside tire edge is 0.835 m from the centerline. At the test
0.100 m grid phase, the strong wet band is 2.200 m after finite spreading versus
2.000 m before. Both correctly tire-aligned straight tests have zero dry and
unscored cells between passes. The reported foot-wide gap was not reproduced by
that test; turning, perspective and which visible edge is followed still require
operator playtesting. Do not describe the test as proving that reported visual
problem impossible. IceCalibration.txt explains thresholds and raster edges.

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
- Camera-results.txt: persistent RMB, Q/E/C/X presets, R/Space, eye/sightline tests.
- Authenticity-results.txt: actual game Update input, drive recovery, release,
  focus/pause, contact recovery and shakeout emission.
- Physical-controls-results.txt: ray picking, latched augers, held hydraulics,
  independent quarter-turn valves, and full 3.5-turn blade-wheel travel.
- Service-results.txt: actual F/E/W route around the southeast machine room,
  free mouse look, both fills/overflow, broad highlighted return, boarding,
  driven hose breakaway and moving dismount with equipment state preserved.
- Operations-results.txt: snow transfer and pooled visual/state behavior.
- Ice-calibration-runtime-results.txt: tire overlap, scoring mask and before/
  after pickup rendered from actual cells by IceCalibrationRuntimeValidation.
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

Performance measurements
RunPerformancePlayer.ps1 drives the separate opt-in performance player at
1366x768 and 1920x1080. PerformanceBefore/ and PerformanceAfter/ hold CSV results
from their respective builds; OperatorFreedomBefore/ preserves the captured
previous implementation used for comparison. Refer to ../Documentation/Performance.txt
for build/run details, scenario definitions, final measurements and limits.
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
OperatorFreedomBefore/ contains the preserved before-view evidence. Current
operator images include Operator-seated.png, Operator-standing-window.png,
Operator-board-side-edge.png and Camera-preset-*.png. Southeast-entrance.png,
Southeast-service-room.png and Service-*.png show the actual room/service flow.
Ice overlap/mask/pickup and snow-operation images show simulated material state.
All are actual Unity camera renders; HUD captures separately include IMGUI.

The supplied image pair is the traditional hydraulic station and the tall
seated viewpoint. Mentioned conditioner/board-gap annotations were not attached.
Automated tests do not establish manufacturer authenticity, subjective sound,
real tire response, hose weight, long-term stability beyond the measured window,
or final ergonomic comfort. Actual operator playtesting remains necessary.
