# Cold Snap: implementation notes

Cold Snap is the hockey scene in The Vector Cabinet. Skaters move on the ice plane while the puck has independent height and vertical velocity for lofted shots, gravity, bounces and goal crossings under the bar. The first-person 3D presentation is generated from that simulation. No imported artwork, generated textures, audio or external services are used.

## Ownership and boundaries

| File or area | Responsibility |
| --- | --- |
| `Assets/Hockey/Core/HockeyTypes.cs` | Input commands and plain skater/puck state; vector helpers. |
| `Skating.cs` | Acceleration, coasting, edge grip, braking, facing, stun and board contact. |
| `RinkPhysics.cs` | Rounded rink boundary, goal-frame obstacles and skater separation. |
| `PuckPhysics.cs` | Finite stick guidance and striking impulses, ice drag, gravity, substepped collisions, posts, crossbars, netting, pad/glove rebounds and goal crossing. |
| `HockeyShots.cs` | Distinct shot speeds and spread; bounded, gravity-compensated launch velocities toward a supplied 3D target. |
| `ShotGesture.cs` | Frame-independent mouse-button ordering, tap/hold shot selection, wrist-shot chords and consumed one-timer releases. |
| `HockeyAI.cs` | Delayed tactical decisions, support lanes, carrier pressure, defensive cover and goalie positioning. |
| `HockeySession.cs` | Team composition, faceoffs, clock, goals, overtime, statistics and dead-puck recovery. |
| `PhysicsValidation.cs`, `HockeyValidation.cs` | Repeatable physics and complete-match diagnostics. |
| `ShotGestureValidation.cs` | Button-sequence regression checks for snap/slap/wrist selection, passes, one-timer consumption and reset behavior. |
| `Assets/Hockey/Presentation/HockeyGame.cs` | Unity input, fixed-step accumulator, camera intent, HUD, briefing, pause and results. |
| `HockeyRenderer.cs` | Generated rink and equipment meshes, procedural posing and cameras. |
| `Assets/Hockey/Shaders/Resources/ColdSnapSurface.shader` | Opaque vertex-color rendering with simple directional shading. |

The core references `System` and `System.Numerics`, not Unity. Horizontal positions are `Vector2`: X is rink width and Y is rink length. Presentation maps these to Unity X/Z; `PuckState.Height` supplies the puck's center height. Core facing uses radians, with zero toward the positive length axis; the rendering `SetView` API takes Unity yaw/pitch in degrees.

`HockeyGame` owns one `HockeySession` and one child renderer. The shared launcher loads scenes with `LoadSceneMode.Single`. Hockey does not create production `DontDestroyOnLoad` objects, persistent input subscriptions or audio listeners. The renderer parents every generated object to its own scene root and explicitly releases its meshes and material when rebuilding or being destroyed. It does not change global `RenderSettings`.

## Skating and the stick

Human and AI skaters submit the same `HockeyInput` structure. Movement supplies bounded acceleration rather than directly assigning speed or position. Releasing movement leaves momentum with low coasting drag. Active turns apply capped lateral edge force; holding brake overrides thrust and exponentially reduces speed without reversing it. Backward and lateral travel have different speed limits, and body facing turns at a bounded rate which slows with speed. Contact with boards and other players changes position and velocity through the same core collision rules.

The camera looks independently of velocity. Turning the view changes the direction of subsequent pushes and stick aim, but does not rotate existing momentum. First-person view deliberately adds no stride bob, roll or automatic steering. A separate HUD glide vector communicates travel direction. Chase view is optional; its position is constrained inside the rounded boards.

The puck is never parented or teleported to a player during ordinary play. A nearby, facing skater can apply a bounded spring/damping force toward a stick-handling point ahead of the body. `ControllerId` describes this local interaction; it is not an ownership constraint. Speed, distance, height, cooldowns, impacts and opponent touches can break control. Stick guidance and strikes have separate low-height reach limits, and the stick cannot collect a puck through the net. A shot or pass applies a finite velocity impulse and temporarily prevents immediate recapture. Player passes can gently lead a teammate inside the aiming cone.

Snap, slap, wrist and one-timer shot profiles differ in speed and spread. A targeted shot supplies a horizontal target and height; the launch calculation compensates for gravity with capped vertical velocity and deterministic aiming error. It supplies an initial velocity, not a guaranteed hit. The resulting trajectory still encounters players, pads, gloves, posts, crossbars, the ice and netting. The renderer reads the actual airborne position. A small ground-projection disc and a short geometric trail help distinguish height from distance. Puck spin/wobble is cosmetic; the core does not model rigid-body angular momentum.

`ShotGesture` distinguishes a short left click (snap on release, up to .18 seconds) from a held slapshot. Right mouse passes on release, leaving room for the ordered RMB-then-LMB wrist chord. A chord consumes the pending pass. Holding left before puck arrival readies a one-timer; the session requires actual stick range, low enough puck height and closing relative motion. One-timer counts are attributed to individual skaters so a teammate's shot cannot consume the human's held gesture. Releasing after an automatic shot cannot shoot again. Pausing, faceoffs and restarting cancel incomplete gestures.

The input adapter resolves its aim against the goal plane, ice or rounded rink boundary. WASD adds a coarse horizontal/vertical target offset while retaining its skating role; holding Space suppresses thrust for stationary aiming. The outlined cross displays this resolved impact target. The camera does not tilt to mimic the necessary launch angle: `HockeyShots` calculates that independently using gravity and travel time. It includes deterministic range-dependent spread and inherited skating momentum.

Puck integration adds small substeps according to distance travelled so hard shots cannot simply step across posts. Boards reflect with energy loss; net sides and backs damp more strongly. Collision shapes vary with puck height: low goalie pads are wider than the torso, and the reaction hand has its own height and lateral reach. Crossing the scoring plane inside the posts requires the whole puck to pass the line below the crossbar. The goal frame and net read the same 2.2m height constant as the core. The visible puck is slightly enlarged: an 18cm silhouette around a 12.5cm collision radius improves first-person readability. Goal and pad collision tests use the smaller physical radius.

The controller advances at 60Hz; the session subdivides to at most 1/120 second, with puck substeps inside that. Presentation reads the resulting state and never supplies collision transforms back to the simulation. Capped catch-up time prevents a long stalled frame from generating an arbitrarily large update.

## Team decisions and match flow

Three skaters and one goalkeeper represent each team. The human remains Blue #9. Teammates #7 and #12 fill wing and supporting/defensive roles. Blue always attacks the orange end.

AI decisions are sampled after short reaction delays. A selected nearby player recovers a loose puck or pressures an opposing carrier while other skaters seek outlet lanes or protect receivers and the slot. On attack, the wing advances toward the far side and the supporting player stays behind the puck. A carrier can pass into a clear lane, answer the human's pass request, avoid pressure or shoot toward an open corner. Goalkeepers reposition against the puck angle and react to a projected shot line after their ordinary decision interval. They move through the same skating system and stop shots through physical pad, torso or glove contact; saves can leave playable rebounds. The visible save glove uses the core's delayed hand height and reach rather than instantly tracking the puck.

Pilots choose snap, wrist or slap profiles based on distance and pressure. They can ready one-timers for approaching teammate passes, intercept the arrival point and strike through the same session/contact rules. AI target height decreases at very close range, and pilots abandon a readied attack after the puck passes the goal plane. These are tactical choices; the human can still attempt a steep shot. A puck leaving above the glass produces an announced faceoff.

The normal match has three minutes of playing time. Goals pause play and lead to a center faceoff. A tie enters one minute of sudden death; if no one scores, a draw is a valid final result. Practice stops the clock and parks opposing skaters while teammates and goalies remain active. There are no offsides, icing, penalties or line changes. Skater bodies remain simplified upright collision volumes; this is not a full ragdoll or stick-blade rigid-body simulation.

A puck that remains nearly stationary for an extended period causes an announced faceoff and increments `StuckResets`. This is an explicit dead-play recovery, not a hidden relocation during play. Match statistics count attempted strikes, passes released, qualifying goalkeeper rebounds, checks, possession turnovers and goals. They support diagnosis rather than claiming regulation scoring semantics.

## Replacement and verification

Authored character or rink models can replace `HockeyRenderer` without changing the core. Its public entry points are `Build(session)`, `Sync(session, dt)`, `SetView(yawDegrees, pitchDegrees, mode)` and `ViewCamera`. Visual poses derive from speed, facing, stun, stick cooldown, puck position/height and goalie hand state. A future animation system can read those same fields. Goal height, mouth, depth, post radius and barrier heights are read from shared collision constants. Changes to the rink footprint should update both collision constants and geometric construction together.

Seeded pilot randomness and fixed time steps support reproducible matches within a runtime. Human inputs remain part of the replay conditions; using the same seed alone does not reproduce a differently played match. The standalone .NET harness in `ValidationHockey` links the exact core source files. Unity/runtime integration diagnostics additionally exercise input, rendering, scene transitions and cleanup. Automated match outcomes can expose invalid states or inert AI, but do not establish that skating feel, first-person readability or scoring difficulty are enjoyable. Those require direct playtesting.
