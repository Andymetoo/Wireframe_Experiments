Ice Resurfacer deterministic simulation validation
=================================================

Run from the repository root with the installed .NET 9 SDK:
  dotnet run --project Assets/Experiments/IceResurfacer/Validation~/IceResurfacer.Validation.csproj --configuration Release

The project has no NuGet dependencies. It compiles the actual Core source directly.
Unity ignores this directory because its name ends in ~. Build products stay here.
Results.txt contains the successful observed output on 2026-10-25.

Nineteen scenarios check finite configuration, deterministic fixed stepping,
hydrostatic coasting and braking, reversal, body/rink collision, conditioner
tracking, trailing spatial coverage and missed strips, conditioner lift,
independent conveyors and snow overflow/load/capacity, gravity wash feed and
pickup priming/recirculation, finite flood water and pooling, left brush contact,
exit/tank interlocks, continuous controls, unscored practice, and a complete run.
The correction pass adds physical cutting/wet-width measurements, per-tick
turning footprint bounds, water/snow mass balance, and manual snow-breaker
capacity/interlock/conservation checks.

Measured width
The physical cut remains 1.956 m (approximately 77 inches), against a 2.130 m
vehicle envelope (approximately 84 inches). At the existing 0.20 m cell size,
the rasterized straight cut/wet span is 1.80-2.00 m depending on lateral cell
alignment. The measurement covers several offsets and configurable working
widths. Cutting and water use the same cells. Every changed cell in a working
turn is checked against the articulated conditioner's physical footprint.
There is no shader-only widening, increased nominal blade, or changed score
area. A 25% physical increase would make a 2.445 m blade, wider than the cited
vehicle. The corrected camera perspective supplies apparent enlargement while
the builder now follows the same calibrated working width as simulation.

Manual plunger
StrokeSnowBreaker transfers up to the configured 0.004 m3 of existing throat
snow from the horizontal buffer into available vertical-buffer capacity. It
requires the tank and conditioner lowered and the vertical conveyor running;
safe strokes without transferable snow may still animate. This optional
manual assist never creates snow, empties the tank, or changes the previous
automatic conveyor behavior. Its transfer volume is a calibration assumption,
not a manufacturer specification or a granular bridging solver.

The complete-run test starts at the production default spawn in the service bay.
It drives exclusively through throttle, steering, and equipment controls. It
never writes the vehicle pose, conditioner pose, or ice cells; never enlarges
capacities or changes geometry; and never refills. A pure-pursuit route follows
five nested perimeter circuits and nine overlapping long oval circuits, then
clears its conveyors, raises the conditioner, crosses the exit threshold, aligns
with the far snow pit, stops, unloads, lowers the tank, and finishes.

Observed complete-run result with default configuration and seed 546:
  Coverage 95.06%; finish quality 79.6%; score 90.6/100.
  Flood water used 700.7 L; snow dumped 2.92 m3.
  Elapsed simulation time 1750 seconds; operator mistakes 0.
  Complete=true, proper exit=true, one completed snow dump.

The small mechanism tests intentionally use explicit pose fixtures to isolate
individual effects. Only the full-run test demonstrates integrated driving and
completion. The route is a regression harness, not an in-game autopilot or a
claim that this overlapping pattern is an efficient real operator's route.
The full-run test does not use wash water; priming and recovery are checked in
their dedicated scenario. No human ergonomics, audio quality, tire dynamics,
frame-time budget, or visual realism claims follow from these pure-core tests.
Unity compilation, shader validation, and scene smoke checks are documented
separately in the experiment's main documentation.

Repeatable Unity and HUD validation
----------------------------------
StageUnityValidation.ps1 makes an isolated project under Validation~/UnityProject,
with its own Settings, Packages and Library. Run it with PowerShell's explicit
-ExecutionPolicy Bypass -File switch if script execution is disabled locally.
Use -UpdateOnly after the first copy. The staging script disables only the
copy's Editor Search startup indexing to avoid a Unity first-use/Play Mode race.
It never edits the original project's preferences, settings or open scene.

Run the installed Unity 6000.4.1f1 executable against that copy with -batchmode,
-force-d3d11 and -executeMethod IceResurfacer.Editor.IceResurfacerEditor.RunBatch.
Do not pass -quit: the method enters real Play Mode and exits after its checks.
Camera-only inspection images and reports are initially written to the copy's
own experiment Validation~ directory.

Then invoke -executeMethod IceResurfacer.Editor.IceResurfacerEditor.BuildLayoutPlayer
against the copy. This explicitly attaches the probe only to a temporary scene;
the normal launch scene never contains it. The development player is produced
in Validation~/Build. RunLayoutPlayer.ps1 launches it hidden, makes only its own
window fully transparent and click-through before showing it without activation,
and tests one requested resolution per process. Mouse messages go only to that
player's own native window, exercising OnGUI without moving the OS cursor,
activating a window, or manipulating the user's editor.
Screenshots come from the actual player framebuffer, including all instruments.

The launcher uses Direct3D11 BitBlt presentation for its transparent test window,
a per-process switch; the project retains its existing rendering configuration.
Unity reference: https://docs.unity.com/en-us/engine/6000.7/manual/unity-editor/command-line-arguments/player
Normal scene/graphics/input checks use the project's unmodified Direct3D11 path.
Use separate invocations of RunLayoutPlayer.ps1 with -Width 1366 -Height 768
and -Width 1920 -Height 1080. Each writes a resolution-specific result and
text-fit report. Combined HUD-player-results.txt and HUD-layout-issues.txt
contain both final observed runs. The disposable build uses its own product
identity so its window preferences stay separate from the original project.
HUD player validation checks both exact client sizes, continuous lever/dial input,
switches, plunger, guide, pause/resume, service/finish guards, session reset, and
scored/practice launch. Text metrics are audited alongside saved visual captures.
