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
Release pedal: hydrostatic deceleration. SPACE: stronger service brake.
Hold SHIFT with W/S: precision speed (42% demand, about 4.5 km/h forward).
Hold RMB: smooth head look, including rear/down with a shoulder lean.
The seat and complete operator station are on the vehicle's LEFT. Look down
and to the RIGHT to inspect the trailing conditioner; the main body is right.
Release RMB: return to the forward view; mouse cursor operates instruments.
Mouse: drag amber levers; rotate handwheels clockwise/counterclockwise, or
scroll over them. Continuous values drive both the mechanisms and 3D controls.
1: conditioner up/down; 2: horizontal auger; 3: vertical auger; 4: left brush;
5: wash pickup pump; 6: one snow-breaker plunger stroke;
Q/E: shallower/deeper cut. These supplement the mouse.
F1: operator guide (pauses the run). ESC: pause. Focus loss also pauses.

A complete working run
1. Drive through the south gate with conditioner raised, blade out, tank down.
   Once the entire conditioner is on ice, creep forward and lower it. Start
   both conveyors, then adjust the blade to about 0.35 mm.
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
5. For exit, retract the blade, stop flood flow and let both conveyors empty.
   Turn both conveyors off. Stop wash pickup pump, close wash valve and stow
   brush. Fully raise the conditioner BEFORE crossing the south threshold.
6. Drive toward the snow pit at the FAR end of the service bay, centered and
   facing south. Stop near the pit, unload the snow tank, wait until empty,
   then lower it. Finish Run evaluates the actual sheet. Refill
   replenishes water and repairs pump wear for another pass. An unsafe exit
   must be corrected by re-entering the rink and performing a proper exit.

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
mechanism/control animations, six original synthesized sound layers.

Scope and calibration still requiring work
This is a playable prototype, not a manufacturer-certified training simulator.
Driving is a deterministic kinematic model, not a tire-friction/Rigidbody
solver. Turning radius, acceleration, load, water rates, freeze time and
scoring need human playtesting. Ice freezing is an elapsed-time approximation;
there is no thermal slab model, fluid solver, granular snow compaction, engine
fuel/emissions, hose elasticity, blade deformation, maintenance workshop or
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
Blade and towel geometry now share the simulation's 1.956 m working width;
the surrounding conditioner housing is 2.076 m wide within the 2.13 m vehicle
envelope. The earlier 1.6 m water emitter now spans that full working width.
The corrected seated lens is 64 degrees instead of 76, giving roughly 25%
more apparent width without changing the correct 77-inch cut or faking water
coverage. See Documentation/LayoutCorrection.txt for measurements and rationale.

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
