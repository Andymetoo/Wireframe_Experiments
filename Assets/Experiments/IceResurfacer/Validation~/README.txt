Ice Resurfacer validation
27 October 2026

Run actual Core source with the dependency-free .NET 9 harness:
  dotnet run --project Assets/Experiments/IceResurfacer/Validation~/IceResurfacer.Validation.csproj --configuration Release -p:UseSharedCompilation=false
Unity ignores this directory because its name ends in ~.

Results.txt records 29 deterministic scenarios: original driving, independent
conveyors, wash priming/recovery, water, brush, practice/scoring and interlocks;
physical cut/water footprints and conservation; snow-breaker assistance;
measured snow calibration; spill/jam/dump/recollection conservation; progressive
dumping; side/end/rounded board contacts; close-board water margin; and an
integrated scored route from the production default spawn.
New scenarios reproduce the rejected-dump travel latch, exercise 24 accumulated
control/restart cases at centre and board edge, hold/release hydraulics, live
interlock recovery, conserved residual shakeout and the complete water-service
lifecycle. Movement-before.txt is the captured failing pre-fix reproduction.

The complete route uses only throttle/steer/equipment commands. It never
writes poses or cells, changes tank capacity, or refills. Five nested perimeter
circuits and nine overlapping oval circuits cover the sheet before clearing,
exiting, aligning with the pit, dumping, lowering and finishing. The outer route
is 0.05 m closer to the boards than the earlier coarse-grid regression and
uses 23% flood demand. Four board-contact penalties are retained. This route
is a regression harness, not an in-game autopilot or an efficient operator plan.
Individual mechanism tests deliberately use pose fixtures and small capacities
to isolate behavior. Material balance counts actual cut depth, loose snow,
conveyor buffers, tank, pit and accounted off-ice losses; cumulative spill
throughput is not counted as a second storage reservoir.

20-yard before/after measurements compare the original source/configuration
against the current source at the same nominal blade settings and 42% throttle
from rest, then blade-out/brake/conveyor purge. The previous implementation did
not fill the tank over that distance. See Results.txt for observed volumes,
OperatorRealism.txt for the actual bite model and its calibration assumptions.
Reference blade stays 1.956 m; water distribution is 2.100 m; grid is 0.10 m.
Cut and water have separate physical bounds. Water-only cells earn no cut
coverage. Default close-board physical margin is 85 mm, rasterized margin 100 mm.

Repeatable Unity / input / rendering validation
StageUnityValidation.ps1 creates an isolated project at Validation~/UnityProject
with its own Settings, Packages and Library. Use -UpdateOnly on later runs.
If scripts are disabled locally, invoke with:
  powershell -NoProfile -ExecutionPolicy Bypass -File Assets/Experiments/IceResurfacer/Validation~/StageUnityValidation.ps1 -UpdateOnly

Run installed Unity 6000.4.1f1 against that copy with -batchmode -force-d3d11
and -executeMethod IceResurfacer.Editor.IceResurfacerEditor.RunBatch.
Do not pass -quit: the method enters real Play Mode and exits after checking.
Unity licensing must be accessible to the validation process. No license,
shared project settings, packages, or live user scene are changed by the tests.
Reports and camera captures initially land in the copy's own experiment
Validation~ directory. Copy the successful reports/images here after the run.

Unity-results.txt: saved scene, shader, spatial lane, audio and runtime errors.
Camera-results.txt: real Input System camera behavior, stance and sightlines.
Authenticity-results.txt: real game Update keyboard/mouse recovery matrix,
focus/pause/release, board recovery and actual shakeout emission.
Physical-controls-results.txt: rendered-part ray picking, both latched augers,
spring controls, independent quarter-turn valves and full blade-wheel travel.
Service-results.txt: actual F/E/WASD route around the machine, both inlet fills,
overflow, nozzle return, reboarding and travel recovery.
Operations-results.txt: actual snow visual/state behavior and CPU/allocation
sample. Rendering stress fixtures seed initial tank stock near full; ordinary
fill rate is measured by the core harness without tank writes.

Then run -executeMethod IceResurfacer.Editor.IceResurfacerEditor.BuildLayoutPlayer
against the isolated copy. It adds the probe only to a temporary scene and
builds Validation~/Build/AuroraLayout.exe. Run RunLayoutPlayer.ps1 with Width
1366 / Height 768, then Width 1920 / Height 1080. The helper starts hidden,
verifies alpha zero and a non-interactive owned window, and supplies native
messages without moving the user's OS pointer or activating the window.
It checks switches/levers, three and 3.5 complete blade turns, stops/reversal,
quarter-turn wash/flood handles, spring return, guide/pause/start/results,
compact/service layouts, clipping and captures.
Native wheel scrolling and human ergonomics remain manual checks.
Use -WindowMode Offscreen for the final capture path. Unity's hidden-window
D3D11 presentation diagnostics are counted separately by the launcher because
they bypass the managed log callback. The opt-in probe disables the Development
Console overlay for clean HUD captures; player logs retain the warnings.

Camera images: Operator-seated.png, Operator-standing-window.png,
Operator-board-side-edge.png, Operator-full-tank-window.png,
Operator-auger-discharge.png. Operation images: Snow-overflow.png,
Snow-dumping.png, Snow-dumped-pit.png. These are actual Unity camera renders;
HUD-* images separately include the player's native IMGUI framebuffer.
No simulation check proves physically authentic driving or a GPU frame budget.
