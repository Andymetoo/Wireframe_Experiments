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

The machine starts centered in the service room, nose toward the snow pit and
rear toward the rink. Hold S to back straight through the southeast doorway;
W approaches the pit. Normal sessions start with 738 L flood water and 310 L wash water. ESC > REFILL
PRACTICE starts a NEW practice session with 35% flood and 30% wash water.

Complete keyboard guide
W / S                 Forward / reverse pedal. Opposite demand brakes through zero.
A / D                 Steering, independent of head orientation.
CTRL                  Service brake. Release pedal for hydrostatic deceleration.
SHIFT with W/S        Precision demand, 42% of normal pedal input.
RMB held              Manual cockpit head look. Release preserves orientation.
Q held                Temporary left/down conditioner and board-edge inspection.
E held                Temporary view of the physical control station.
C held                Temporary external chase camera behind the whole machine.
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
F3                    Drive/load diagnostics and footprint/pit contact overlay.
F4                    Toggle smoothed FPS / frame-time meter; visible by default.
ESC                   Pause / resume; eye-height adjustment is in the pause panel.

For collision inspection, combine TAB with F3: compact diagnostics stay at the
left and leave the actual pit/footprint outlines visible in the scene. TAB again
restores the full machinery/performance panel.

Q/E no longer adjust the blade; use comma/period. R no longer recenters; use X.
Releasing Q/E/C restores the exact head pose saved before inspection, including
custom RMB angles. Overlapping keys use C > E > Q priority and preserve that
same saved pose. X explicitly recenters. Mouse movement without RMB controls
the cockpit cursor. Seated eye adjustment is -8 to +12 cm around the authored mount.

Physical controls
Hold E to view the inboard station, then use the cursor. Viewed from the seat,
the four black-knob levers run LEFT TO RIGHT: vertical auger, conditioner lift,
horizontal auger, dump tank. Their physical pivots and mouse targets follow this order.
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
60 x 26 m rink. A 5.4 m doorway joins an 11 m wide backstage room with a marked
parking area, centered snow pit, workshop fittings and water station. The parking
reference faces southeast into the room toward the pit. The wall supply is on
the opposite room wall from the earlier layout, beside the machine's fixed
right-side fillers. The left operator station and machine-side controls have
not been mirrored. The centered rear aisle gives a straight reverse route onto
the rink. There is no docking snap.

For an orderly refill, stop and stow equipment, press F, and walk around the rear
of the machine to the wall hose on its right. E takes the nozzle. E connects the
blue ICE WATER or teal WASH WATER rear fill neck. E opens/closes the wall supply.
Each tank fills separately; capacity is enforced and excess visibly overflows.
Close supply, E to uncouple, then face the highlighted holder and E to return the
hose. The holder tolerates a broad view and a 2.65 m approach. F boards again.
The service HUD shows used hose length out of 11 m and TAUT near its limit.
Aimed fixtures beyond arm reach report distance; blocked movement names the
machine, wall, pit edge or hose. Looking upward can extend the held nozzle;
walking back toward slack remains possible even while it is over the limit.

Free Simulation allows pressurized uncoupling and departure with a connected
hose. Water continues spilling until the wall supply actually closes. Pulling
past the physical 11 m reach releases a breakaway coupling, leaves a loose nozzle
and records a misuse warning. E retrieves the nozzle from the floor. Boarding
while carrying it also leaves it on the floor, rather than locking boarding or
remotely stowing it. The coupling is an explicit fictional recovery mechanism.
See Documentation/WaterService.txt for geometry, accounting and source details.

A practical resurfacing run
Back out through the southeast doorway with equipment stowed for clearance;
choose the forward route once clear of the gate. Lower
the conditioner while creeping, start both conveyors and use about 0.35 mm nominal
blade setting as an initial calibration. Open wash, allow roughly ten seconds
for prime, then start pickup. Flood flow should suit speed; close it before long
stops. The manual valves are not an automatic speed-compensated water system.

Use the outside tire edge against the previous visible wet boundary as an
alignment cue, then check Q/C and the survey for the actual strip. The blade
remains 1.956 m, towel 2.100 m, body 2.130 m, wheel track 1.370 m and tire width
0.215 m (526 LT215/85R16 nominal section). The finite water fringe uses the same
water volume and cannot cross
boards. F2 distinguishes unreachable shaving rim from water and surface finish.

Pickup can progressively remove still-liquid excess through its actual squeegee
strip if the pump is primed, functioning and the wash reservoir has space. It
cannot remove frozen water or instantly erase a bad patch. Repeat passes may
help. Dry pump operation causes wear; a new session currently restores pump
condition. Water filling does not repair machinery wear.

Above the normal .35 nominal setting, actual blade bite increases progressively
to .600 mm at the .80 maximum; .35 still removes .056 mm. This extreme is an
operator-review calibration, not a manufacturer cut-depth specification. The
3.54 m3 tank and 3.1 snow expansion are unchanged. Repeated deep cuts produce
more snow, load and cumulative unevenness; there is no routine paint penalty.

Watch actual snow stock, conveyor congestion and load. Slow down/reduce bite
before overload; shut down and clear a real jam before J. The brush cleans only
where its deployed left-side disc reaches, moving snow toward the conditioner.
With the horizontal auger off, the finite trough fills and snow escapes from
both sides into recoverable windrows. Vertical-off congestion, a full tank,
jam and raised-bin upper misses have distinct states and emission locations.
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
unscored gaps at 1.9425 m between pass centers. A wider 2.05 m spacing reproduces
a wet-looking but uncut seam: water extends farther than the physical blade.
The current 5 cm grid contains
520 x 1200 real samples, with fractional water at footprint edges and smooth
surface reconstruction. Its strong wet raster is 2.30 m; the GPU-visible wet
response spans 2.367 m in the measured view, covering the actual 2.10 m towel.
The thin fringe uses the same finite water supply. Turning alignment and
boundary recognition still need operator playtesting. Normal survey colors now
explain uncut, cut/dry, wet/uncut, loose snow, excess water and resurfaced cells.
Hover the survey for an exact 16 x 16 cell enlargement and measured treatment
deficits. The overview can omit or exaggerate subpixel slivers; the inspector
shows actual cells. No gap filling or relaxed coverage requirements are applied.

New ice starts dull and scored by skate grooves of differing physical depths.
Shallow cutting can leave deeper grooves; deeper or repeated cuts remove more
ice and produce more snow. Wet film gradually settles: with the configurable
420-second freeze constant, about half remains liquid after five minutes and
about 6% after twenty. Fresh, dry-shaved and settled ice keep distinct finishes.
See Documentation/VisualIceRefinement.txt for calibration and measured examples.

Estimated Machine Load is combined cutting, conveyor stock, running augers and
congestion demand. Click its meter or open F3 for the explanation and breakdown.
The model starts at 13% idle demand; each running auger adds 8%/9%, and blade
speed/depth, snow in the conveyors and congestion add the rest. At 100% load,
the load multiplier allows 65% of normal acceleration (snow mass and tire grip
apply separately). It changes drive/engine sound, and above 100% warns of
overload. It does not change the configured top speed or impose a drive lock.
Jams follow actual accumulated snow and conveyor capacity. This is an estimated
mechanical-demand indicator, not measured engine horsepower or fuel consumption.
F3 also draws accepted/rejected footprints, visible/collision pit boundaries,
contact point and normal. It names the rejected pose and actual movement reason,
raw input, accepted demand, traction, focus and
cursor state. Free Simulation has no raised-tank or attached-hose travel lock.
F4 updates text four times per second; F3 adds CPU work, GPU timing when available,
GC allocation and draw-call diagnostics. Whole-frame before/after measurements
and their environment are in Documentation/OperatorAuthenticity.txt; a core timing alone
is not an FPS claim.

Evidence and limits
Documentation/OperatorAuthenticity.txt records this pass and validation status.
Documentation/SnowCalibration.txt records the 18 full-sheet machinery experiments.
Documentation/OperatorCamera.txt and PitServiceCollision.txt cover detailed changes.
Documentation/SurfaceServiceRefinement.txt preserves the preceding completed baseline.
Documentation/VisualIceRefinement.txt explains resolution, wear and settling.
OperatorFreedom.txt and IceCalibration.txt retain the preceding measurements.
The updated aggregate core harness passes all 35 scenario groups, including
pit/raised-tank contact and finite floor drainage. The final integrated Unity run
passes every suite with zero managed errors, including exact held-camera return,
actual wheel sightlines, 40 physical-control checks and visible finite puddles.
Final native HUD runs pass at 1366x768 and 1920x1080 with no measured text
clipping or managed errors. Both 14-workload rendered performance runs,
29,999-interval sustained samples and prior-duration repeats are complete.
Matched active mean/p95 is 3.609/5.982 ms at 1366 and 3.352/5.780 ms at 1920;
sustained means are 4.402/5.062 ms. Some workloads regress; the slower 1920
whole-wet trace is preserved. See OperatorAuthenticity.txt for all data and limits.
Consult OperatorAuthenticity.txt and generated Results.txt for final status.
Runtime and performance validation are separate measurements.
Validation~/ contains deterministic reports, actual Unity captures, native HUD
checks at 1366x768 / 1920x1080, and performance reports. Consult the final report
files for executed outcomes; historical 25-27 October documents retain earlier
measurements and intentionally superseded controls/layout restrictions.

The earlier control-station and tall seated-view photographs guide the inboard
station, tapered body and sightlines. The supplied annotated 526 station photo
confirms the physical hydraulic order and relative water-valve/blade-wheel layout.
The Aurora is an original fictional machine, not a traced or
manufacturer-certified Model 546 replica. Camera-results.txt measures 4.175 m
aligned assembly length including fittings, compared with the published 4.04 m
manufacturer envelope. Accurate blade and track do not imply an exact CAD replica.

Driving remains a deterministic kinematic model with restrained traction slip,
not a tire/Rigidbody solver. The configurable forward maximum is 2.65 m/s
(9.54 km/h); hydrostatic response, grip and load are calibration choices. Ice
freezing is time-based, water spread is finite redistribution, snow is stored
volume, and hose routing is geometric. There is no rollover, personal-injury,
thermal-slab, granular-snow or full fluid simulation. Audio is synthesized.
Human operator feel, wet-edge recognition in turns, hose comfort, inspection cameras,
body proportions and audio balance remain important manual checks.

The current control-driven route reverses out from nose-in parking, works the
sheet, then returns forward and dumps without a pose reset: 96.67% reachable
coverage, 95.14% whole-ice coverage, 97.04% water coverage, 77.3% finish and
88.8 points. It uses 677 L over 1,787 simulated seconds, deposits 0.724 m3 into
the pit and records zero misuse. See Validation~/Results.txt; the standalone
Southeast-full-run.txt is refreshed with the final integrated evidence set.
These results supersede the earlier 28 October route's 97.05% reachable /
95.16% whole-ice coverage, 82.2% finish and 90.4 points; changed calibration and
driven route mean this is not an isolated comparison of the service layout.

Validation and ownership
Run the dependency-free .NET checks:
  dotnet run --project Assets/Experiments/IceResurfacer/Validation~/IceResurfacer.Validation.csproj -p:UseSharedCompilation=false
For Unity validation while this project is open, StageUnityValidation.ps1 creates
an isolated project under Validation~/UnityProject. See Validation~/README.txt.
All source, materials, launch assets, docs and evidence remain in this experiment.
Unrelated games, project settings, packages and build-scene lists are preserved.
No branch, commit or push is part of this work.
