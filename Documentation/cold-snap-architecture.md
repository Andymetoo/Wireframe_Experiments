# Cold Snap: implementation notes

Cold Snap is the hockey scene in The Vector Cabinet. The simulation is an ice-plane model with a first-person 3D presentation: puck height is fixed, but skating, collisions, passing, rebounds and goal crossings are simulated. No imported artwork, generated textures, audio or external services are used.

## Ownership and boundaries

| File or area | Responsibility |
| --- | --- |
| `Assets/Hockey/Core/HockeyTypes.cs` | Input commands and plain skater/puck state; vector helpers. |
| `Skating.cs` | Acceleration, coasting, edge grip, braking, facing, stun and board contact. |
| `RinkPhysics.cs` | Rounded rink boundary, goal-frame obstacles and skater separation. |
| `PuckPhysics.cs` | Finite stick guidance and striking impulses, ice drag, substepped collisions, posts, netting, pad rebounds and goal crossing. |
| `HockeyAI.cs` | Delayed tactical decisions, support lanes, carrier pressure, defensive cover and goalie positioning. |
| `HockeySession.cs` | Team composition, faceoffs, clock, goals, overtime, statistics and dead-puck recovery. |
| `PhysicsValidation.cs`, `HockeyValidation.cs` | Repeatable physics and complete-match diagnostics. |
| `Assets/Hockey/Presentation/HockeyGame.cs` | Unity input, fixed-step accumulator, camera intent, HUD, briefing, pause and results. |
| `HockeyRenderer.cs` | Generated rink and equipment meshes, procedural posing and cameras. |
| `Assets/Hockey/Shaders/Resources/ColdSnapSurface.shader` | Opaque vertex-color rendering with simple directional shading. |

The core references `System` and `System.Numerics`, not Unity. Positions are `Vector2`: X is rink width and Y is rink length. Presentation maps these to Unity X/Z. Core facing uses radians, with zero toward the positive length axis; the rendering `SetView` API takes Unity yaw/pitch in degrees.

`HockeyGame` owns one `HockeySession` and one child renderer. The shared launcher loads scenes with `LoadSceneMode.Single`. Hockey does not create production `DontDestroyOnLoad` objects, persistent input subscriptions or audio listeners. The renderer parents every generated object to its own scene root and explicitly releases its meshes and material when rebuilding or being destroyed. It does not change global `RenderSettings`.

## Skating and the stick

Human and AI skaters submit the same `HockeyInput` structure. Movement supplies bounded acceleration rather than directly assigning speed or position. Releasing movement leaves momentum with low coasting drag. Active turns apply capped lateral edge force; braking applies a stronger force. Backward and lateral travel have different speed limits, and body facing turns at a bounded rate which slows with speed. Contact with boards and other players changes position and velocity through the same core collision rules.

The camera looks independently of velocity. Turning the view changes the direction of subsequent pushes and stick aim, but does not rotate existing momentum. First-person view deliberately adds no stride bob, roll or automatic steering. A separate HUD glide vector communicates travel direction. Chase view is optional; its position is constrained inside the rounded boards.

The puck is never parented or teleported to a player during ordinary play. A nearby, facing skater can apply a bounded spring/damping force toward a stick-handling point ahead of the body. `ControllerId` describes this local interaction; it is not an ownership constraint. Speed, distance, cooldowns, impacts and opponent touches can break control. A shot or pass applies a finite velocity impulse and temporarily prevents immediate recapture. Player passes can gently lead a teammate inside the aiming cone. Shots stay on the ice.

Puck integration adds small substeps according to distance travelled so hard shots cannot simply step across posts. Boards reflect with energy loss; net sides and backs damp more strongly. Skaters present small collision discs and goalies have wider pad capsules. Crossing the scoring plane inside the posts requires the whole puck to pass the line. The visible puck is slightly enlarged: an 18cm silhouette around a 12.5cm collision radius improves first-person readability. Goal and pad collision tests use the smaller physical radius.

The controller advances at 60Hz; the session subdivides to at most 1/120 second, with puck substeps inside that. Presentation reads the resulting state and never supplies collision transforms back to the simulation. Capped catch-up time prevents a long stalled frame from generating an arbitrarily large update.

## Team decisions and match flow

Three skaters and one goalkeeper represent each team. The human remains Blue #9. Teammates #7 and #12 fill wing and supporting/defensive roles. Blue always attacks the orange end.

AI decisions are sampled after short reaction delays. A selected nearby player recovers a loose puck or pressures an opposing carrier while other skaters seek outlet lanes or protect receivers and the slot. On attack, the wing advances toward the far side and the supporting player stays behind the puck. A carrier can pass into a clear lane, answer the human's pass request, avoid pressure or shoot toward an open corner. Goalkeepers reposition against the puck angle and react to a projected shot line after their ordinary decision interval. They move through the same skating system and stop shots through physical pad contact; saves can leave playable rebounds.

The normal match has three minutes of playing time. Goals pause play and lead to a center faceoff. A tie enters one minute of sudden death; if no one scores, a draw is a valid final result. Practice stops the clock and parks opposing skaters while teammates and goalies remain active. There are no offsides, icing, penalties, line changes, aerial pucks or height-based stick collisions.

A puck that remains nearly stationary for an extended period causes an announced faceoff and increments `StuckResets`. This is an explicit dead-play recovery, not a hidden relocation during play. Match statistics count attempted strikes, passes released, qualifying goalkeeper rebounds, checks, possession turnovers and goals. They support diagnosis rather than claiming regulation scoring semantics.

## Replacement and verification

Authored character or rink models can replace `HockeyRenderer` without changing the core. Its public entry points are `Build(session)`, `Sync(session, dt)`, `SetView(yawDegrees, pitchDegrees, mode)` and `ViewCamera`. Visual poses currently derive from speed, facing, stun, stick cooldown and puck position. A future animation system can read those same fields. Changes to rink dimensions should update both collision constants and geometric construction together.

Seeded pilot randomness and fixed time steps support reproducible matches within a runtime. Human inputs remain part of the replay conditions; using the same seed alone does not reproduce a differently played match. The standalone .NET harness in `ValidationHockey` links the exact core source files. Unity/runtime integration diagnostics additionally exercise input, rendering, scene transitions and cleanup. Automated match outcomes can expose invalid states or inert AI, but do not establish that skating feel, first-person readability or scoring difficulty are enjoyable. Those require direct playtesting.
