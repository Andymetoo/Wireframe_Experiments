AURORA R-54 / ICE RESURFACER SIMULATOR
Unity 6000.4.1f1 / URP / 29 October 2026

Launch
Open Assets/Experiments/IceResurfacer/Scenes/IceResurfacer.unity and press Play,
or use Experiments > Ice Resurfacer > Open Launch Scene. No generation step,
Build Settings edit, package change or other experiment is required.

FREE SIMULATION / SCORED and PRACTICE permit the same machinery operation.
GUIDED TRAINING adds explicitly labeled safety assistance. The optional F5
reference route is independent, advisory, and never controls the machine.
The machine starts nose-in toward the service-room snow receiver, with 738 L
flood and 310 L wash water. S backs through the southeast gate onto ice.
The unloading zone is now a flush, load-bearing grate. Drive or walk across it;
real walls still collide. Snow passes through the grate into the receiver.

Keyboard guide
W / S                 Forward / reverse pedal; opposite demand brakes through zero.
A / D                 Steering, independent of head orientation.
CTRL                  Service brake; pedal release gives hydrostatic deceleration.
RIGHT SHIFT + W/S     Precision demand, 42% of normal pedal input.
RMB held              Manual head-look; release keeps the selected orientation.
Q held                Temporary left conditioner / board-edge inspection.
E held                Temporary seated view of the physical machinery controls.
C held                Temporary external chase camera.
LEFT SHIFT held       Temporary rearward glance over the RIGHT shoulder.
X                     Recenter forward in about 0.2 seconds.
R                     Toggle standing / sitting, preserving head orientation.
SPACE held            Temporary rise, compatible with R standing toggle.
1 held                Lower conditioner; release holds actual attained height.
RIGHT SHIFT + 1       Raise conditioner while held.
2 / 3                 Horizontal / vertical auger, independently maintained.
4                     Toggle left board brush.
5                     Toggle wash pickup pump.
6                     One manual snow-breaker stroke.
7 held                Raise dump tank; release holds attained position.
RIGHT SHIFT + 7       Lower dump tank while held.
[ / ] held            Decrease / increase flood-water valve.
RIGHT SHIFT + [ / ]   Decrease / increase independent wash-water valve.
, / . held            Fine shallower / deeper blade adjustment.
J                     Reset a conveyor jam after physical clearing / stopping.
F                     Dismount / board near the left step or rear platform.
TAB                   Expand / collapse the right-side operator console.
F1                    Operator guide; pauses the simulation.
F2                    Normal overview / shaving mask / actual material channels.
F3                    Machinery diagnostics, actual contact and footprint overlays.
F4                    Smoothed FPS / frame-time meter.
NUMPAD 7              Show / hide the selected GPS route (F5 alias).
NUMPAD 8              Open / close Route Manager (F6 alias).
NUMPAD 9              Start recording / stop and save (F7 alias).
NUMPAD 1              Toggle clean video mode: hide/restore HUD, GPS and highlights.
ESC                   Pause / resume; eye adjustment and MAX FORWARD SPEED slider.

Left Shift is exclusively a rear glance. Use RIGHT SHIFT for precision and
machinery modifiers. Releasing Q/E/C/Left Shift restores the exact saved head
rotation and eye offset, including custom RMB views. Overlaps use
C > E > Q > Left Shift priority and retain one saved normal viewpoint.

Controls and HUD
Four physical hydraulic pivots now share a transverse mounting bank:
VERTICAL AUGER / CONDITIONER / HORIZONTAL AUGER / DUMP TANK, left to right.
Hold E and use the cursor. Lift/dump levers spring to neutral and retain actual
height; augers latch. Flood and wash have adjacent independent L handles with
90-degree travel. Drag or scroll the separate 3.5-turn blade handwheel.
Hover an individual grip or wheel for a subtle amber tint and control guidance.
Dragging retains that feedback until release; camera/input transitions clear it.
The taller water stems flank the closer blade handwheel. The complete snow
breaker assembly sits beside the rightmost hydraulic lever.

The Surface Survey defaults to the upper left; MAP - / MAP + collapses it.
The instruments occupy the top right; TAB opens the lower-right mini console.
They, keyboard shortcuts and physical 3D controls write the same state.
Both hydraulic controls agree with the physical levers: pull BACK toward the
seat (mini-lever bottom) to LOWER; push FORWARD (mini-lever top) to RAISE.
Release centers the levers and holds actual attained position. A schematic beside
the blade controls shows extension and pressure-adjusted contact, with vertical
movement exaggerated for clarity. It does not model blade pitch.
The console starts collapsed. Held inspection views clear the side UI.
The main instruments permanently report CONDITIONER UP / DOWN / RAISING /
LOWERING / PARTIALLY LOWERED from actual lift. Blade screw setting and
pressure-adjusted contact depth are separate; standing still does not shave ice.
See Documentation/HydraulicDirections.txt for the direction audit and validation.
Ordinary notifications fade after eight seconds. Serious equipment alerts stay
compact. MESSAGES opens scrollable history and an ordinary-message visibility
setting. F1 retains operating guidance. TAB also selects compact F3 diagnostics.

ESC > MAX FORWARD SPEED sets a personal 3.00-8.00 MPH target in .25 MPH steps.
PlayerPrefs saves it immediately across Play Mode and later launches. RESET TO
ORIGINAL SPEED restores the exact serialized 2.65 m/s (5.9279 MPH) calibration.
The scene asset is unchanged; acceleration, traction, reverse and braking still
govern actual speed. The run report records the selected limit. This operator
preference is not a manufacturer specification. See Documentation/HUDVideoSpeed.txt.

Machinery and visual editing
The centered tank crown is 150 mm lower; walls, window, door and cylinder
anchors share authored dimensions rather than a scaled parent. A compact blue
upright and rear-wall inlet replace the large overhead pipe. Transport, raised
bin misses, snow fill and collision agree with this geometry. The unchanged
3.54 m3 loose-equivalent stock maps into a measured 1.931 m3 tapered envelope
through an explicit 1.833:1 visual compaction ratio. No snow stock is discarded.
This ratio is an Aurora visual calibration, not a manufacturer specification.

Wheel track remains the manual's 1.37 m and tire width stays .215 m. Visible
wheels, steering pivots and tire references agree. The brush pivot is now 1.16 m
ahead of the rear axle, 46 cm forward of the cockpit-refinement baseline. Its
complete arm/roller assembly and simulated contact share that position.
Conditioner yaw is limited to 2 degrees; its actual footprints
follow it. Chassis steering and roughly 4.485 m rear-axle radius are unchanged.
The actual operator station moved 6 cm outboard without raising the eyes.
The steering hub moves 28 cm forward and its wheel plane is 68 degrees above
horizontal. The connected shafts, support, dashboard and lamp follow the hub.
Q turns slightly forward along the left working edge to retain the relocated
brush and trailing conditioner in one downward view; the seated origin stays fixed.

Use Experiments > Ice Resurfacer > Machine Geometry Editor to edit shared
parameters and rebuild a selectable unbatched preview. Tank height/offset,
conveyor height, wheel track, brush mount/extension, cockpit placement, steering
hub/tilt, blade spindle, water-stem height and snow-breaker position are exposed.
Save the launch scene normally to persist these parameters. Preview
transform dragging is temporary; rebuilding uses the shared parameters.
The preview uses the runtime builder and is removed before play/window close.
Runtime batching remains; no independently maintained prefab is introduced.

Surface survey, pooling and drying
The ice remains 520 x 1200 physical 5 cm cells. The normal survey uses a separate
130 x 300 area-weighted overview with 20 cm pixels. Initial dust and extra
crease snow are conserved but no longer make the overview white static.
Concentrated deposits remain visible. F2 and hover retain exact 5 cm data.
The overview distinguishes cut/wet, wet/uncut, cut/dry, untreated, snow and
excess water. Fractional narrow gaps contribute their actual area.

The 1.956 m blade is narrower than the 2.100 m towel plus finite water fringe.
A continuous wet seam may legitimately remain uncut. Actual water earns water
coverage; it never receives invented cutting credit. No scoring thresholds
were weakened to remove real misses.

Stopped flooding now retains and redistributes actual liters. Full flow for
5 / 15 / 30 seconds releases 9 / 27 / 54 L; tested wet areas grow to
2.13 / 4.335 / 7.16 m2. Boards retain liquid; no visual water is added.
Primed pickup can recover still-liquid excess into available wash capacity.
Gross lateral-transfer liters count repeated cell crossings, not new water.

The physical 420-second freeze constant remains. Fresh liquid, settling film
and mostly frozen polished ice have different optical responses driven by
actual phase quantities. No separate cosmetic timer exists. Untreated ice
retains grooves of differing depths. Scene-specific ambient RGB 214/228/245
brightens the arena; previous lighting is restored when the experiment unloads.

Snow and practical operation
With augers off, the finite trough fills and snow escapes both sides. Bounded
clump particles show discharge; sparse 3D height meshes use the same ground
snow as shader, survey, pickup and accounting. Deep windrows may need repeated
passes because loose pickup is limited to 6 mm and finite conveyor throughput.
Full tank, stopped augers, jams and raised-bin misses retain distinct causes.
Pit/floor snow uses irregular volume-based surfaces with upward winding.

Back out with S, then choose the forward route. Lower while creeping, engage
both augers, and start near .35 mm nominal blade setting. Open wash, prime for
about ten seconds, then enable pickup. Set manual flood flow to speed/overlap
and close it before long stops. Follow the previous wet edge with the outside
tire and check Q/C/survey. F5 shows one reference rear-axle route: departure,
perimeter, overlapping passes, turnarounds and return. Its 1.60 m pass spacing
and 4.60 m arcs are advisory; deviation has no penalty or restriction.

Normal .35 mm setting retains .056 mm calibrated bite. Deeper settings
progressively reach .600 mm at the .80 maximum. This is Aurora calibration,
not manufacturer screw-travel conversion. Repeated deep cuts make more snow,
load and unevenness; no routine paint-damage model was added.
Return and dump manually toward the grate. Actual outlet trajectory determines
where snow lands. Brief lower/raise movements release residue; Right Shift+7
lowers fully. Board/wall audio follows normal contact velocity and distinguishes
rolling/rubbing from body impacts, with cooldowns to avoid repeated audio spam.

Water service
F steps off; the machine keeps its equipment settings and can coast. On foot,
mouse look is free, WASD walks and E/click operates the aimed fixture.
Walk around the rear to the right-side wall hose. Take the nozzle, couple blue
FLOOD WATER or teal WASH WATER, then operate supply. Close supply, uncouple,
return the nozzle and F to board. Each tank is finite. ESC > REFILL PRACTICE
starts a new session with partly empty tanks. Hose reach is 11 m with explicit
reach/obstruction feedback. Free Simulation allows pressurized uncoupling and
attached-hose departure; exceeding reach breaks the fictional recovery coupling
and spills water. Floor water drains through the actual drain/grate with accounting.

Scores and Machine Load
Scores measure combined treatment, finish, water distribution, overwater,
efficiency and handling. 95% reachable treatment marks a complete sheet; no
route is mandatory. The 15.24 cm perimeter exclusion applies only to shaving /
combined coverage; water and finish measure the whole rink.
Machine Load estimates 13% idle plus cutting, augers, snow and congestion.
At 100% load its acceleration multiplier is 65%; it also changes synthesized
sound. It is not horsepower or fuel use and does not impose a travel lock or
change configured top speed. Click its meter or use F3 for the breakdown.

Recording an operator route
Choose FREE SIMULATION / PRACTICE. NUMPAD 8 opens Route Manager. Type a name,
RECORD NEW ROUTE and drive normally. NUMPAD 9 stops and saves automatically;
STOP & SAVE does the same in the manager. LOAD SAVED ROUTE shows readable names,
dates, lengths and previews. Select a take, USE AS GPS, then NUMPAD 7 shows it.
No Editor conversion or file-extension knowledge is required. Existing raw files
remain unchanged; an editable fit is saved separately. Diagnostics / Advanced
retains full raw inspection, footprint analysis and optional manual recovery.
Colors advance every 40 m, reverse travel is orange, START/END are marked, and
the gray trace shows the actual conditioner. This view never smooths the data.
The machine keeps running while the panel is open; park before using it.

Editor files: Assets/Experiments/IceResurfacer/RouteRecordings~/*.route.json.
After leaving Play Mode, use Experiments > Ice Resurfacer > Route Recordings,
or enter Play again, NUMPAD 8 and select the saved file. Standalone builds use
Application.persistentDataPath/IceResurfacer/Routes; the panel shows the full path.
The same portable JSON format works in both. Per-take recovery drafts are
checkpointed every ten real seconds and finalized on normal shutdown/Play exit.
An abnormal process crash can lose the interval since the latest checkpoint.
The reference and labeled demo remain explicit choices. Documentation/RouteOperator.txt
gives the three-action workflow. RouteRecording.txt documents the preserved schema.

Editable routes and live training guidance
Experiments > Ice Resurfacer > Training Route Editor opens any saved .route.json.
Edit the derived curve/markers and inspect separate vehicle, conditioner and
blade/water footprints. Save Corrected COPY preserves the exact original raw
source in a portable .guide.json. Red invalid sections must be repaired before
live use. Editor guides live under RouteRecordings~/Guides; standalone guides
under persistentDataPath/IceResurfacer/Routes/Guides. Copy files there and Refresh.
In Play, Route Manager loads raw recordings directly as GPS or selects corrected
guides. NUMPAD 7 toggles guidance. The next 12.192 m (40 ft) is shown in the world
and Surface Survey, crossing leg boundaries continuously. Magenta guidance has
a dark border and a restrained continuation across visible machine surfaces;
actual walls and stands occlude it. For a major
detour, select a leg and explicitly Rejoin; this changes only the guide cursor.
The original F5 reference remains the default. The labeled perimeter/loop test
fixture is selected explicitly and is not an actual operator recording.
Documentation/TrainingGuide.txt explains editing, persistence, controls and limits.

Machine audio
Physical machinery uses full 3D audio at its actual components: engine/chassis,
blade edge, separate augers, moving hydraulic cylinders, water outlets/pickup,
connected fill inlet, actual spill/contact points and individual snow outlets.
The original procedural clips and machinery-state volume/pitch responses remain.
Arena ventilation stays non-positional. The existing single camera listener
follows sitting, standing, chase and walking; camera movement adds no Doppler.
Physical sound has full gain within 2.5 m, about 25% at 10 m, 12.5% at 20 m
and 6.25% at 40 m, with a long tail fading out at 120 m. These are source-distance
amplitude gains, not a master volume control. Twenty-seven sources are created
once, with original clips shared by the split water, hydraulic and snow emitters.
Documentation/SpatialAudio.txt records Unity DSP measurements and remaining
listening calibration. Validation~/SpatialAudio contains results and WAV probes.

Validation and limits
Documentation/CockpitRefinement.txt records the preserved cockpit changes and checks.
Validation~/Cockpit-comparisons.html pairs actual before/after Unity captures.
Documentation/OperatorErgonomics.txt records the preceding integrated validation,
captures and measured performance. MachineryErgonomics.txt, SurfaceConsistency.txt
and MotionContacts.txt explain individual changes. OperatorAuthenticity.txt and
older reports retain historical measurements and superseded layouts/bindings.
Validation~/OperatorErgonomicsBefore preserves the starting evidence.

Run core checks:
  dotnet run --project Assets/Experiments/IceResurfacer/Validation~/IceResurfacer.Validation.csproj --configuration Release -p:UseSharedCompilation=false
StageUnityValidation.ps1 prepares a separate disposable Unity project. The user
Editor, other experiments, settings and packages are preserved. No Git branch,
commit or push is part of this work.

This is kinematic machinery with bounded water redistribution and bulk-snow
reconstruction, not manufacturer CAD, tire physics, thermal slab modeling or
full fluid/granular dynamics. Particle clumps have no individual collision.
Human operator review remains valuable for reach, cutting calibration, snow
intensity, drying contrast and sound balance.
