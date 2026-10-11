# Meridian 1944 implementation

Meridian is an independent, scene-owned air-combat game inside the existing Unity project. The three scenes are `Cabinet`, `HauntedHouse`, and `Meridian1944`. `VectorCabinet.GameCollection` changes scenes with `LoadSceneMode.Single`, restores normal time and cursor state, and retains no gameplay objects. Haunted House keeps its own simulation, renderer, and controls.

## Simulation boundaries

The classes under `Assets/AirCombat/Core` use `System.Numerics`, SI units, and a right/up/forward convention of +X/+Y/+Z. They have no Unity dependency. `AirCombatGame` translates keyboard or mouse input into `FlightInput`, runs a 60 Hz simulation, and presents briefing, HUD, pause, and debrief screens. Rendering never writes aircraft transforms back into the simulation.

| Source | Responsibility |
| --- | --- |
| `FlightTypes.cs` | Aircraft definitions, retained aircraft state, and pilot commands. |
| `FlightDynamics.cs` | Aerodynamic forces, attitude rates, damage effects, and terrain impact. |
| `PilotAI.cs` | Delayed tactical decisions and flight-control commands. |
| `Weapons.cs` | Ballistic projectiles, swept hit detection, impacts, and predicted lead. |
| `TerrainField.cs` | Seeded continuous terrain heights shared by collision and rendering. |
| `AirMission.cs` | Seeded starting situations, pilot ownership, simulation order, and outcomes. |
| `AirValidation.cs` | Repeatable checks on flight, combat, and mission behavior. |
| `AirRenderer.cs` | Meshes, cameras, cockpit, terrain, and bounded visual effects. |

## Flight model

Aircraft retain world-space velocity independently of their attitude. Each bounded integration step computes local airflow, angle of attack, sideslip, altitude-dependent density, and dynamic pressure. Lift follows wing orientation perpendicular to the velocity; banking therefore redirects lift and costs vertical support. Profile drag, induced drag, stall drag, sideslip damping, gravity, and power-limited propeller thrust change velocity rather than teleporting the aircraft along its nose direction.

Pilot input requests damped angular rates with reduced authority at low speed, high angle of attack, damage, or excessive speed. Automatic longitudinal trim assists keyboard flight without leveling the wings or holding altitude. Stalls reduce lift and control authority and produce a recovering nose-down moment when the pilot eases off. Sustained overspeed and excessive load damage the airframe. Damage reduces lift, thrust, and control authority. Contact with the terrain ends the aircraft's flight.

This is an accessible point-mass aerodynamic model with controlled angular dynamics, not a rigid-body engineering simulation. It deliberately omits torque, propwash, detailed control surfaces, landing gear, and engine management.

## Pilots and combat

AI pilots issue the same `FlightInput` commands as the player. Tactical decisions update approximately four to six times per second. Pilots intercept distant contacts, pursue predicted firing positions, manage throttle and turning speed, limit climbs when energy is low, and use finite firing windows with imperfect aim. Close passes, threats, and prolonged unfavorable pursuit can trigger a break or extension. Terrain avoidance and stall-speed recovery take priority. Escort attackers weigh distance, the courier objective, their current target, and a nearby threatening player.

Rounds inherit aircraft velocity, travel at finite muzzle speed, fall under gravity, and expire. Relative swept segments intersect aircraft-oriented fuselage and wing ellipsoids, reducing tunneling through fast-moving targets. The optional lead cue uses the same intercept calculation as AI guidance; it does not steer rounds or grant hits. Ammunition, damage, and outcome evaluation live in the core simulation.

The training duel ends when its opponent is destroyed or the player is lost. Courier cover ends when both interceptors are destroyed, the courier survives five minutes, or either protected allied aircraft is lost. A twelve-minute mission limit prevents an endless unfinished sortie. Both missions begin airborne.

## Procedural presentation

All visible content is generated from C# geometry and source shaders. Terrain samples the collision height field on a 150 m grid across 36 km; polygon colors, villages, trees, an airfield, and cloud banks provide geographical cues. The mesh interpolates between samples, so its surface can differ slightly from continuous collision heights. Ocean continues beyond the island.

Opaque fighter meshes have distinct wing shapes, fuselages, canopy shells, tail surfaces, geometric markings, and spinning propellers. A larger twin-engine courier shares the procedural construction approach. Aircraft dimensions follow their definitions. Cockpit view hides the player's canopy shell and rotor blades to keep the sight clear and avoid solid-blade flicker, and adds a geometric panel with animated dials. Chase view follows with damping and displays the full airframe. Camera attitude preserves aircraft roll and the true horizon.

Two resource-loaded shaders provide flat surface shading, distance haze, and a geometric sky dome without textures or global `RenderSettings` changes. Tracers, impacts, debris, and smoke use bounded object pools. The renderer disables its old hierarchy immediately when rebuilt and releases every owned runtime mesh and material during cleanup.

## Extension and limits

Add performance types through `AircraftDefinition`, mission setups and objectives through `AirMission`, and tactical decisions through `PilotAI`. Authored aircraft can later replace `AirRenderer`'s visual construction while retaining core state, controls, physics, and weapons. Existing shader and geometry code is confined to the air-combat presentation layer.

There is no takeoff, landing, cockpit interaction, fuel-system simulation, audio, campaign, disk save, or network play. Gunnery uses forgiving geometric hit volumes rather than exact mesh collision. AI has simplified situational knowledge and terrain awareness rather than a visual detection model. Seeds reproduce starting conditions and seeded decisions for an identical sequence of fixed-step inputs; they are not a cross-platform bitwise determinism guarantee. Automated and scripted runtime checks can establish consistency and working outcomes, but human playtesting must establish handling feel, difficulty, pacing, and dogfight enjoyment.
