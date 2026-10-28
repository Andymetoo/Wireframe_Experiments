AURORA R-54 / ICE RESURFACER SIMULATOR
Unity 6000.4.1f1 / URP / 28 October 2026

Launch
Open Scenes/IceResurfacer.unity and press Play, or use
Experiments > Ice Resurfacer > Open Launch Scene.
The saved scene builds its owned arena and machine at runtime. No generation
command, Build Settings change, or other experiment is required.

Choose FREE SIMULATION / SCORED or FREE SIMULATION / PRACTICE. Both permit the
same machinery operation anywhere. Scored sessions measure physical results
and misuse; practice shows the surface review without a score. GUIDED TRAINING
is an assisted practice variant: its safety restrictions explicitly identify
training assistance. It uses the operator guide, not a scripted lesson campaign.

Normal sessions start with 738 L flood water and 310 L wash water. ESC > REFILL
PRACTICE starts a NEW practice session with 35% flood and 30% wash water.

Complete keyboard guide
W / S                 Forward / reverse pedal. Opposite demand brakes through zero.
A / D                 Steering, independent of head orientation.
CTRL                  Service brake. Release pedal for hydrostatic deceleration.
SHIFT with W/S        Precision demand, 42% of normal pedal input.
RMB held              Manual cockpit head look. Release preserves orientation.
Q                     Smooth persistent left conditioner / board-edge view.
E                     Smooth persistent view of the physical control station.
C                     Smooth persistent rear shoulder view of the fresh strip.
X                     Return to forward view in about 0.2 seconds.
R                     Toggle standing / sitting; head direction is preserved.
SPACE held            Temporary standing while held; compatible with R toggle.
1 held                Lower conditioner; release holds attained position.
SHIFT+1 held          Raise conditioner; release holds attained position.
2                     Toggle horizontal auger.
3                     Toggle vertical auger.
4                     Toggle left board brush.
5                     Toggle wash pickup pump.
6                     One manual snow-breaker plunger stroke.
7 held                Raise dump tank; release holds attained position.
SHIFT+7 held          Lower dump tank; release holds attained position.
[ / ] held            Decrease / increase flood-water valve.
SHIFT+[ / SHIFT+]     Decrease / increase wash-water valve.
, / . held            Fine shallower / deeper blade adjustment.
J                     Inspect/reset a conveyor jam once physically cleared/stopped.
F                     Dismount / board.
TAB                   Toggle compact HUD.
F1                    Operator guide; pauses the simulation.
F2                    Cycle normal ice, shaving mask, actual affected-cell channels.
F3                    Drive, machinery-load and performance diagnostics.
F4                    Toggle smoothed FPS / frame-time meter; visible by default.
ESC                   Pause / resume; eye-height adjustment is in the pause panel.

Q/E no longer adjust the blade; use comma/period. R no longer recenters; use X.
Preset views remain relative to the machine after releasing the key. RMB can
continue from any preset. Mouse movement without RMB controls the cockpit
cursor. Seated eye adjustment is modest: -8 to +12 cm around the authored mount.

Physical controls
Press E to view the inboard station, then use the cursor. The four black-knob
levers operate conditioner, horizontal auger, vertical auger and dump tank.
Drag hydraulic levers toward the seat to raise, forward to lower. They spring
to neutral on release and hold their actual attained position. Auger levers
latch independently. Wash and flood have separate L handles with 90-degree
travel: drag from 9 o'clock CLOSED to 6 o'clock OPEN. Water follows partial
openings. Drag/scroll the blade handwheel through its 3.5-turn range; click the
snow-breaker plunger for a stroke. HUD, keyboard and physical controls all write
the SAME machinery state. A mouse gesture never captures keyboard throttle.

On foot
F can leave a moving, raised or operating machine in Free Simulation. The
machine keeps its equipment settings; the released pedal allows it to coast.
The exit uses clear ground beside the machine and avoids boards and the pit.
Mouse movement looks freely WITHOUT RMB; WASD walks; E or click interacts with
the object at the center of view. F boards near the left step or accessible rear
operator platform. Pause/guide releases the cursor. Boarding restores the seated
head view and ordinary cockpit cursor. A moving dismount can incur misuse.

Southeast service room and water
The diagonal opening is in the southeast rounded corner of the unchanged
60 x 26 m rink. A 5.4 m doorway joins a 12 m wide backstage room with a marked
parking area, offset snow pit, workshop fittings and right-side water station.
The parking reference faces northwest onto the rink. There is no docking snap.

For an orderly refill, stop and stow equipment, press F, and walk around the rear
of the machine to the wall hose on its right. E takes the nozzle. E connects the
blue ICE WATER or teal WASH WATER rear fill neck. E opens/closes the wall supply.
Each tank fills separately; capacity is enforced and excess visibly overflows.
Close supply, E to uncouple, then face the highlighted holder and E to return the
hose. The holder tolerates a broad view and a 2.65 m approach. F boards again.

Free Simulation allows pressurized uncoupling and departure with a connected
hose. Water continues spilling until the wall supply actually closes. Pulling
past the physical 11 m reach releases a breakaway coupling, leaves a loose nozzle
and records a misuse warning. E retrieves the nozzle from the floor. Boarding
while carrying it also leaves it on the floor, rather than locking boarding or
remotely stowing it. The coupling is an explicit fictional recovery mechanism.
See Documentation/WaterService.txt for geometry, accounting and source details.

A practical resurfacing run
Enter through the southeast doorway with equipment stowed for clearance. Lower
the conditioner while creeping, start both conveyors and use about 0.35 mm nominal
blade setting as an initial calibration. Open wash, allow roughly ten seconds
for prime, then start pickup. Flood flow should suit speed; close it before long
stops. The manual valves are not an automatic speed-compensated water system.

Use the outside tire edge against the previous visible wet boundary as an
alignment cue, then check Q/C and the survey for the actual strip. The blade
remains 1.956 m, towel 2.100 m, body 2.130 m, wheel track 1.370 m and tire width
0.300 m. The finite water fringe uses the same water volume and cannot cross
boards. F2 distinguishes unreachable shaving rim from water and surface finish.

Pickup can progressively remove still-liquid excess through its actual squeegee
strip if the pump is primed, functioning and the wash reservoir has space. It
cannot remove frozen water or instantly erase a bad patch. Repeat passes may
help. Dry pump operation causes wear; a new session currently restores pump
condition. Water filling does not repair machinery wear.

Watch actual snow stock, conveyor congestion and load. Slow down/reduce bite
before overload; shut down and clear a real jam before J. The brush cleans only
where its deployed left-side disc reaches, moving snow toward the conditioner.
Return under manual control, approach the pit using the visible outlet, and
hold 7 to dump. Snow follows the outlet trajectory: off-center or angled snow
may hit the pit, ice, or floor. Briefly lower then raise to shake out residue;
SHIFT+7 lowers the tank. No transform or heading snaps toward the pit.

Free Simulation allows finishing anywhere; the result reflects the remaining
surface and equipment state. A safe return and proper disposal earn handling
credit. No particular driving route is required. Guided Training adds labeled
safe-operation checks before dumping, service and finishing.

Scores and diagnostics
The result separates combined treatment coverage (40 points), whole-sheet
surface quality (30), water distribution (10), avoiding excessive water (10),
water-use efficiency (5), and tank disposal / safe return (5). Recorded misuse
deducts 1.2 per fault, capped at 30, with per-fault cooldowns. Missed patches are
reported separately. Practice retains mechanics but displays no scored result.
95% combined treatment of reachable ice marks a complete sheet.

The configurable 15.24 cm perimeter exclusion applies to shaving/combined
coverage only. Water distribution and finish still measure the whole sheet;
the excluded rim is not automatically credited. F2 shows the mask and actual
cut/wash/flood cells. The deterministic tire-alignment test measures zero dry or
unscored gaps, both before and after this pass; the reported foot-wide gap was
not reproduced in straight parallel passes. The new finite fringe increased
the measured strong wet band from 2.0 to 2.2 m at the test grid phase. Turning
alignment and boundary recognition still need operator playtesting.

Estimated Machine Load is combined cutting, conveyor stock, running augers and
congestion demand. Hover its meter or open F3 for the breakdown. F3 also names
real drive inhibition/contact, raw input, accepted demand, traction, focus and
cursor state. Free Simulation has no raised-tank or attached-hose travel lock.
F4 updates text four times per second; F3 adds CPU work, GPU timing when available,
GC allocation and draw-call diagnostics. Whole-frame before/after measurements
and their environment are in Documentation/Performance.txt; a core timing alone
is not an FPS claim.

Evidence and limits
Documentation/OperatorFreedom.txt explains the current architecture and evidence.
Documentation/IceCalibration.txt contains world-space width, mask and pickup tests.
The aggregate core harness passed all 32 scenario groups; Results.txt records
the outcome. Runtime and performance validation are separate measurements.
Validation~/ contains deterministic reports, actual Unity captures, native HUD
checks at 1366x768 / 1920x1080, and performance reports. Consult the final report
files for executed outcomes; historical 25-27 October documents retain earlier
measurements and intentionally superseded controls/layout restrictions.

The current pass received the traditional control-station photograph and the
user's tall seated-view photograph. They guide the inboard station, tapered
body and sightlines. The mentioned annotated conditioner and board-gap pictures
were not present. The Aurora is an original fictional machine, not a traced or
manufacturer-certified Model 546 replica. Its approximately 4.35 m visible
assembly is not the published 4.04 m manufacturer equipment envelope.

Driving remains a deterministic kinematic model with restrained traction slip,
not a tire/Rigidbody solver. The configurable forward maximum is 2.65 m/s
(9.54 km/h); hydrostatic response, grip and load are calibration choices. Ice
freezing is time-based, water spread is finite redistribution, snow is stored
volume, and hose routing is geometric. There is no rollover, personal-injury,
thermal-slab, granular-snow or full fluid simulation. Audio is synthesized.
Human operator feel, wet-edge recognition in turns, hose comfort, camera presets,
body proportions and audio balance remain important manual checks.

The latest control-driven southeast route records 97.05% reachable coverage
(95.16% against all ice, versus the prior 95.18% baseline), 82.2% finish and
90.4 points, then returns and dumps without a pose reset. See
Validation~/Southeast-full-run.txt for water, time, disposal and scoring details.

Validation and ownership
Run the dependency-free .NET checks:
  dotnet run --project Assets/Experiments/IceResurfacer/Validation~/IceResurfacer.Validation.csproj -p:UseSharedCompilation=false
For Unity validation while this project is open, StageUnityValidation.ps1 creates
an isolated project under Validation~/UnityProject. See Validation~/README.txt.
All source, materials, launch assets, docs and evidence remain in this experiment.
Unrelated games, project settings, packages and build-scene lists are preserved.
No branch, commit or push is part of this work.
