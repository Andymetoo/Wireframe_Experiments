AURORA R-54 / ICE RESURFACER SIMULATOR
Independent Unity 6000.4.1f1 + URP prototype

Launch
Open Scenes/IceResurfacer.unity and press Play. Alternatively use
Experiments > Ice Resurfacer > Open Launch Scene. Select Scored Run or
Unscored Practice. The original arena and machine are assembled on entering
Play Mode; the saved scene is the launch entry point, not an empty scene
requiring a generation command. No Build Settings change is required.

Quick controls
W / S: forward / reverse pedal demand. Opposite demand brakes through zero.
A / D: steer. Steering does not turn the operator's head.
Release pedal: hydrostatic deceleration. CTRL: stronger service brake.
Hold SHIFT with W/S: precision speed (42% demand, about 4.5 km/h forward).
Hold RMB: smooth head look, including rear/down with a shoulder lean.
The seat and complete operator station are on the vehicle's LEFT. Look down
and rear-left for the outer conditioner edge; rear-right shows the main trail.
Release RMB: keep the exact head direction relative to the machine; mouse
cursor returns to its instrument position. Mouse movement alone never turns
the head. R: smoothly recenter to forward in 0.2 seconds (E remains blade depth).
Hold SPACE: smoothly raise eyes 0.48 m with a small 0.10 m forward lean.
Release SPACE: return to seated height, preserving head direction both ways.
WASD and instruments work while seated or raised. Pause, guide and focus loss
preserve head direction; only R or a new session intentionally recenters.
Mouse: look down at the real station, release RMB, then grab physical controls.
The four black-knob levers operate conditioner, horizontal auger, vertical auger
and dump tank. Pull toward the seat to raise; push forward to lower. Conditioner
and dump levers spring to neutral on release and hold their attained position.
Auger levers latch RUN/OFF independently. Matching HUD controls write exactly
the same machine state. TAB switches between the full rack and compact HUD.
Wash and flood use independent L-shaped valves with 90-degree travel: drag from
9 o'clock CLOSED to 6 o'clock OPEN. Water follows actual partial valve settings.
Rotate or scroll the blade wheel for fine adjustment; mechanical end stops
remain 3.5 turns apart. Click the physical plunger for a down-and-return stroke.
Hold 1: lower conditioner; Shift+1: raise conditioner (changed from toggle).
2: horizontal auger; 3: vertical auger; 4: left brush;
5: wash pickup pump; 6: one snow-breaker plunger stroke;
Q/E: shallower/deeper blade screw setting. Blade wheel travel: 3.5 full turns.
J: jam inspection/reset, only stopped with blade out and BOTH augers off,
tank lowered and enough capacity available. Dump first if the tank is full.
F: safely dismount in bay / board at left step. On foot: WASD walk, RMB look,
E or click interacts with hose, fill ports and wall supply. E still deepens
the blade while seated. F3: optional drive diagnostics, hidden by default.
F1: operator guide (pauses the run). ESC: pause. Focus loss also pauses and
releases spring controls. Mouse gestures never capture keyboard throttle.

A complete working run
1. Drive through the south gate with conditioner raised, blade out, tank down.
   Once the entire conditioner is on ice, creep forward and lower it. Start
   both conveyors, then set the blade wheel near 0.35 mm (0.056 mm effective bite).
2. Begin at precision speed. Open the wash valve to roughly 25%, wait at
   least 10 seconds for priming, then start the pickup pump. Start flood flow
   around 32% at precision speed; faster travel needs more flow. These are
   prototype starting points, not manufacturer operating specifications.
3. Make overlapping clockwise laps with the boards on your LEFT. Work inward
   and watch the actual wet edge and survey for missed strips. Reduce blade
   and water in goal/crease areas and on repeated overlaps. The left board
   brush clears perimeter snow only when it actually contacts the edge.
4. Watch tank volume, conveyor buffer and load. Excess cutting, a stopped
   auger or a full tank backs up snow and eventually spills it onto the ice.
   Close flood flow before stopping. Dry wash-pump operation causes wear.
   Hold Space and look forward/down through the black-framed tank opening to
   inspect actual snow. Continued full-tank feeding can jam the conveyors.
   Stop both augers before inspecting/resetting a physical jam with J.
5. For exit, retract the blade, stop flood flow and let both conveyors empty.
   Turn both conveyors off. Stop wash pickup pump, close wash valve and stow
   brush. Fully raise the conditioner BEFORE crossing the south threshold.
6. Drive toward the snow pit at the FAR end of the service bay, centered and
   facing south. Stop near the pit, unload the snow tank, wait until empty,
   briefly lower and raise it to release adhered residue, then hold LOWER
   until fully stowed. Finish Run evaluates the actual sheet. Refill using
   the physical hose station; normal sessions still start full. An unsafe exit
   must be corrected by re-entering the rink and performing a proper exit.

Water servicing
Normal sessions start with 738 L flood water and 310 L wash water. For a refill
test, ESC > REFILL PRACTICE replaces the run with a new practice session having
35% flood and 30% wash water.
1. Stop inside the bay. Raise conditioner, retract blade, turn augers, brush,
   pickup pump and both water valves off; fully lower the snow tank.
2. Press F. Walk around the machine to the right wall hose.
3. Aim at the holder and E to take the nozzle. Walk to the blue ICE WATER or
   teal WASH WATER inlet; aim and E to couple. The 11 m hose must reach.
4. Return to the wall valve and E to open. The selected tank fills progressively
   at 12 L/s. The other tank is unchanged. Watch the sight tubes/header/repeater.
5. At capacity, excess exits the overflow standpipe and drains visibly, with a
   distinct splash layer. Close wall supply with E before uncoupling.
6. E at the connected inlet to disconnect. Connect the other inlet if needed,
   or E at the wall holder to return the nozzle. Return to the left step and F.
An attached hose inhibits travel and names the recovery steps. No invisible
tank top-up is performed. Documentation/WaterService.txt explains assumptions.
Water service does not repair mechanical wear; pump condition resets with a
new session. The former instant top-up/repair shortcut is no longer a player
control. A full maintenance workshop remains outside this prototype.

Movement reliability
A refused dump request used to leave a nonzero target while the tank was fully
down. The drive interpreted that target as a raised tank indefinitely. The
new drive checks actual tank elevation and live service interlocks; a rejected
request clears its target. Ordinary controls never gate keyboard throttle.
Live warnings explain raised tank, hose, brake and board contact. F3 shows raw
keyboard input, requested/accepted demand, velocity, brakes, interlock reason,
focus, mouse capture and ground-model status. It is opt-in only.
Validation~/Movement-before.txt records the pre-fix failed reproduction.

Scoring and recovery
Coverage 45 points; whole-sheet finish 35; avoiding overwatering 10;
tank emptying 5; proper exit 5. Operator mistakes deduct 1.2 each with a
per-fault cooldown. Coverage requires cutting, water and snow removal in the
actual conditioner footprint. Whole-sheet finish includes missed areas.
95% coverage counts as a complete sheet; returning earlier gives a partial
result. Practice retains mechanical consequences and feedback but no score.
The survey shows untreated grey areas, processed green areas and excess-water
amber areas. It is a survey of the simulation, not a suggested driving path.
New Session resets only this experiment. There is no persistent save system.

Implemented
Fixed-step deterministic simulation with separate input, presentation and
instrument code; rounded rink/body boundaries; finite acceleration, coasting,
braking, steering slew and load response; a lagging, articulated conditioner;
continuous equipment commands; separate snow conveyors and finite reservoirs;
wash priming/recovery/wear; snow accumulation, overflow and visible particles;
spatial scratch/snow/cut/wash/flood/quality state; wet-to-dry appearance;
sensitive crease regions; threshold/exit and tank-handling rules; scored and
practice sessions; original mesh-built arena and machine, lighting, URP ice,
mechanism/control animations, ten original machine sound loops with mechanical
transients, plus fill/overflow/coupling sound at the service station.

Scope and calibration still requiring work
This is a playable prototype, not a manufacturer-certified training simulator.
Driving is a deterministic kinematic model, not a tire-friction/Rigidbody
solver. Turning radius, acceleration, load, water rates, freeze time and
scoring need human playtesting. Ice freezing is an elapsed-time approximation;
there is no thermal slab model, fluid solver, granular snow compaction, engine
fuel/emissions, hose elasticity, blade deformation, a full maintenance workshop or
dynamic rollover. Original synthesized audio is functional rather than a
recording of a real machine. Art and reflections can receive further polish.

The original fictional model uses reference proportions with ergonomic
adjustments, not a traced Model 546 mesh: its visible towel/body assembly spans
about 4.35 m rather than the published 4.04 m equipment envelope. The authored
arena/shader use the default 60 x 26 m geometry. Changing rink dimensions or
machine geometry in Core also requires changing the authored visual builder.
Behavioral values in the launch component's Calibration fields can be tuned
without changing other games or project settings. Start a new session after
changing calibration values.
The blade remains 1.956 m. Towel, emitter and physical flood footprint span
2.100 m inside a 2.120 m housing and 2.130 m vehicle envelope. The 0.10 m grid
resolves the close-board wet edge. Flood outside the blade earns no cutting
coverage credit. A 61-degree lens plus actual water spread gives about 14%
additional apparent wet half-width per side compared with the previous pass.
Snow volume now follows the actual effective bite, not the nominal screw
setting. The chosen 0.16 bite fraction is an explicit calibration assumption,
not a manufacturer specification. Full-depth cutting still increases load,
snow, scoring damage and overcutting relative to a normal setting.
See Documentation/OperatorRealism.txt for retained measurements and
Documentation/Authenticity.txt for the 27 October 2026 reliability/control pass.
The photograph mentioned in the request was not in the supplied attachment;
the original station geometry follows the written mechanical layout.

Files and validation
Core/                 Plain C# state and mechanics, no Unity dependencies.
Presentation/         Scene builder, input/camera, instruments, audio and ice.
Resources/, Shaders/  Owned material templates and shaders retained in builds.
Scenes/               Saved launch scene with explicit owned asset references.
Editor/               Explicit scene-opening and validation tools only.
Documentation/        Manufacturer sources, facts and disclosed assumptions.
Validation~/          Standalone tests, results and Unity-rendered captures.

Run pure C# checks with .NET 9 SDK:
  dotnet run --project Assets/Experiments/IceResurfacer/Validation~/IceResurfacer.Validation.csproj -p:UseSharedCompilation=false
Unity asset check: Experiments > Ice Resurfacer > Validate Assets.
For the repeatable graphics/Play Mode check, close this project in the Editor
and invoke the Unity 6000.4.1f1 executable with:
  -batchmode -projectPath <repository> -executeMethod IceResurfacer.Editor.IceResurfacerEditor.RunBatch -logFile <log-path> -force-d3d11
Do not add -quit: the validation exits itself after Play Mode checks.
See Validation~/Unity-results.txt and Validation~/Results.txt for recorded
outcomes and the scope of checks. The correction pass also captures actual
IMGUI at 1366x768 and 1920x1080, plus external and seated machine views.
Mouse ergonomics, subjective sound and final driving calibration need human
playtesting even when automated checks pass.
The headless input check uses virtual keyboard/mouse devices and a transient
InputSettings clone so that the absent Game View focus does not discard input;
it restores the original settings and never saves this clone to the project.
If this project is open in Unity, StageUnityValidation.ps1 creates an isolated
validation copy under Validation~/UnityProject; pass that copy as projectPath.
The copy has its own settings and Library and does not touch the open session.

Ownership
All experimental source, assets, guides and validation live in this folder.
The adjacent IceResurfacer.meta is Unity's required folder identity. There
are no references to existing games, external downloaded art or wireframe
presentation. Existing scenes, packages, project settings and build scene
lists are preserved. Nothing has been committed or pushed.
