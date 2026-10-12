# The Vector Cabinet

Three independent, silent games in one Unity **6000.4.1f1 URP** project. All game visuals use code, opaque polygon meshes, vector lines and built-in UI. No imported creative assets, generated textures, audio, new packages, or runtime services are required.

## Launch

Run the squadron expansion at **`Builds/VectorCabinetSquadron/VectorCabinet.exe`** with its data folder and DLLs beside it. The previous build remains at `Builds/VectorCabinet/VectorCabinet.exe`. The launcher offers **Haunted House**, **WWII Air Combat** and **Hockey**, also selected with keys 1, 2 and 3. Each game has restart and return-to-cabinet buttons. Progress is kept only for the current session.

In Unity, open `Assets/GameCollection/Scenes/Cabinet.unity` and press Play, or use **Vector Cabinet > Open Launcher**. All individual game scenes also work directly. **Vector Cabinet > Build Windows Player** builds the launcher and three game scenes into the same executable. The original Haunted House build menu now builds the collection too.

## Hockey — Cold Snap

First-person, three-on-three hockey plus goalies in a rounded indoor rink. You stay **Blue #9**, with two AI teammates against three AI skaters. Blue always attacks the orange end. Regulation lasts three minutes; a tie gets one minute of sudden-death overtime, then a draw. Goals pause play and reset to a center faceoff. No icing, offsides or penalties are simulated.

Try **Open Ice** first: the clock stops and opposing skaters hold position while goalies and your teammates remain active. Practice coasting, braking, calling for passes and shooting into corners. The practice rink uses the same physical rules as a match.

| Control | Action |
| --- | --- |
| Mouse | Look and aim; momentum keeps its current direction |
| W / A / S / D | Skate relative to view; also select high / left / low / right shot region |
| Shift | Stronger strides toward higher speed |
| Hold Space | Smooth hockey stop; overrides skating thrust while held |
| Click left mouse / F | Immediate snap shot (mouse fires on click release) |
| Hold left mouse, release | Charge and release a slapshot |
| Hold right mouse, THEN hold left mouse | Charge a wrist shot; release left to shoot; slower and more accurate than a slapshot |
| Right mouse release / Q | Pass; nearby teammate within the aim cone receives a small lead assist |
| Hold left mouse before receiving a puck | Automatic one-timer when an incoming or loose puck reaches the stick |
| E | Check an opponent within reach |
| R | Request a pass from an AI teammate |
| C | First-person / chase view |
| Tab | Toggle rink map |
| H / Esc | Notes / pause, sensitivity, restart and cabinet |
| Arrow keys | Keyboard alternative for looking around |

**Skating:** release movement keys to coast. Look around while gliding; your view does not instantly redirect velocity. Speed gives you reach but requires room to stop or reverse. Use the glide-direction indicator when your head and travel direction differ. No camera bob, roll or impact shake is applied.

**Puck play:** get the puck within stick reach and in front of the body. The stick guides it with a capped force; it is never parented to a skater. Hard turns, fast passes, collisions and checks can release it. Shots lift under gravity and bounce off ice, skates, bodies, pads, gloves, posts and the raised crossbar. The dark puck is slightly enlarged visually for readability; a short air trail, ground shadow, direction prompt and rink map help track it. Pucks above stick height cannot be collected. A puck over the glass causes a faceoff.

**Aiming:** the mouse sets an impact point on the net or rink; the bright outlined cross shows the intended target, including the WASD offset. W+A selects upper left, no keys center, S+D lower right. Hold Space while aiming to stop pushing. The shot supplies an initial velocity that compensates for gravity, not a guaranteed goal: moving skaters, distance, shot type, rebounds and goalie reactions matter. Wrist shots spread less than slapshots; long shots require a higher arc and are less reliable. Passes remain along the ice. Holding LMB before contact can one-time an incoming pass or a loose puck you skate onto; releasing after that shot does not shoot again.

**Team play:** seek a clear passing lane, then R to call. Teammates may refuse a blocked or distant pass. One player pressures or recovers while the others support or protect dangerous space. With the puck, bots carry, pass and select aimed low or high shots. They can prepare one-timers, especially for incoming teammate passes. Goalies track angles and shot heights with reaction delays and limited movement; physical pad, torso and glove saves leave rebounds.

Scores and match statistics lead to rematch and new-match options. Match seeds reproduce AI decisions for identical fixed-step inputs. There is no player switching, manual stick-height control, stamina meter or full rulebook. [Architecture and limits](Documentation/cold-snap-architecture.md) describe the implementation.

## WWII Air Combat — Meridian 1944

A fictional Channel sortie in a low-polygon WWII setting. Fly the Kestrel Mk.IX against Falke fighters over a seeded island. Five selectable briefings retain **Training Duel** and **Courier Cover** and add three squadron missions with two mortal wingmen, Rook and Finch. All missions begin airborne; there is no takeoff or landing sequence.

- **Fighter Interception:** stop both raid leaders before they cross the defended boundary. Escort fighters try to delay you. Designating a raider for your wingmen while you intercept the other can split the workload.
- **Bomber Escort:** deliver at least two of three Heron bombers to air cover. Bombers fly an actual route and formation. Killing attackers does not replace lost bombers or complete the route.
- **Ground Attack:** destroy four moving supply trucks. Two optional flak batteries fire physical rounds, and enemy fighters defend the convoy. Set up a shallow dive, fire a short burst, pull clear and extend before returning.

The original **Training Duel** offers one armed opponent and a lead cue. **Courier Cover** asks you to keep the unarmed transport Lark alive for five minutes or destroy both interceptors.

| Control | Action |
| --- | --- |
| W / S or Up / Down | Nose down / nose up |
| A / D or Left / Right | Roll left / right |
| Q / E | Rudder left / right |
| Shift / Ctrl, or R / F | Increase / decrease throttle |
| Space / left mouse | Fire guns |
| Hold Z | Gentle wings-level control assist |
| C | Cockpit / chase camera |
| Tab | Select next surviving hostile |
| 1 / 2 / 3 / 4 during flight | Form Up / Engage selected target / Cover Me / Regroup |
| Hold right mouse | Look around independently of the aircraft |
| V / middle mouse | Center look |
| G | Toggle predicted lead cue |
| M | Toggle mouse stick; cursor above center pulls up |
| H | Pause and read flight notes |
| Esc | Pause, sensitivity, restart, return to cabinet |

Mouse stick and keyboard can be combined; the pause screen has sensitivity and mouse pitch inversion. Start with keyboard controls and short inputs. The cursor recenters when mouse stick is enabled. The control system never directly snaps aircraft attitude or velocity.

**Turning:** bank with A/D, release roll, then pull S. More bank needs more elevator. Hold Z to ease the wings level. Climbing spends speed; diving gains speed. Near stall, lower the nose and add power. Sustained overspeed and extreme load damage the airframe. Damaged aircraft lose power, lift and control authority.

**Gunnery:** close to about 300–600 metres and fire short bursts. Rounds travel through space, inherit aircraft velocity, drop under gravity, and collide with moving wing/fuselage volumes. The small green cue predicts an intercept; the larger amber brackets identify the aircraft. Put the gunsight on the lead cue, not the target bracket. A HIT flash confirms damage. The cue does not steer or aim your guns.

**Squadron:** wingmen fly their formation slots through normal controls, manage speed, acquire visible contacts, defend themselves and recover from low energy or terrain danger. Orders allow immediate survival maneuvers. Engage uses the target selected when you issue the order; cycling targets alone does not retask the flight. Cover Me focuses on threats near you. Regroup breaks pursuit and returns to formation. Damaged or exhausted pilots may withdraw.

**Awareness:** allies are green and hostiles amber. The contact scope shows visible contacts within 4 km, with terrain occlusion. Watch bomber condition and threat notices; abandon a pursuit when the protected formation needs help. The target/waypoint guidance supplements visual tracking rather than steering the aircraft.

**Gamepad:** left stick flies, triggers control rudder, RB fires, LB levels, D-pad up/down adjusts throttle, right stick looks. X selects a target, B changes camera, A toggles lead. Hold Y and tap D-pad up/right/left/down for Form Up/Engage/Cover Me/Regroup. Start pauses; menus use D-pad/A/B. Pause offers sensitivity, deadzone, inversion and optional Input System flight-stick binding paths. Settings last for the current Air Combat scene. Physical controller and HOTAS testing remains necessary; see [input guide](Documentation/meridian-input-guide.txt).

The HUD reports speed in knots, altitude in feet, heading, throttle, airframe condition, ammunition and load. Stalls, terrain proximity and overspeed show warnings. A sortie number reproduces the starting terrain, contacts and pilot decisions when the same controls and simulation steps are used. No mid-sortie save is implemented.

## Haunted House — The House That Listens

The original investigation remains: ten generated rooms, three ghost families, evidence-based deduction, finite tools and spatial resolution rituals. Its core simulation and procedural renderer are preserved. The only ordinary-play additions are buttons to return to the cabinet.

W/S move, A/D turn, E opens doors or examines, F examines, J opens the notebook, M the discovered map, and H the field guide. Read the in-game guide for investigative tools and rituals. The full original manual is in [Documentation/haunted-house-manual.md](Documentation/haunted-house-manual.md).

## Implementation and validation

`Assets/GameCollection` owns the launcher, scene switching, combined builder and opt-in integration runner. Each scene owns its controls, camera, UI, generated geometry and state. Loading a game replaces the current scene; ordinary play uses no persistent game objects. The opt-in diagnostic runner is the only persistent helper.

`Assets/AirCombat/Core` is plain C# and System.Numerics: aerodynamic forces and damped angular rates, terrain field, tactical pilots, ballistic weapons, aircraft damage and mission progression. `Presentation` adapts keyboard/mouse/gamepad or configured axes into device-independent intentions, runs a fixed 60 Hz simulation and renders replaceable procedural meshes and UI. See [the architecture notes](Documentation/meridian-architecture.md) and [squadron design notes](Documentation/squadron-design-and-validation.txt).

**Vector Cabinet > Squadron > Validate All Games** executes the combined checks in Unity. **Vector Cabinet > Squadron > Build Separate Windows Player** writes the expansion folder while preserving the prior build. The air harness includes bounded complete bot sorties, orders and perception checks, formation/rejoin trials, terrain recovery and physical ground-gunnery tests. Automated victories establish working objectives, not human-rated flight feel or balance.

The [final squadron verification report](Documentation/squadron-verification.txt) records 2,031 passing Unity checks, 343 passing rendered standalone checks, build results and remaining human/hardware testing.

`Assets/Hockey/Core` uses the same separation with its own skating, airborne puck simulation, puck contacts, team pilots, goalkeepers and match clock. Core ticks subdivide to 120 Hz; fast puck motion uses smaller collision steps. `HockeyGame` owns input and menus; `HockeyRenderer` owns the rink, equipment animation and camera. Neither touches the older games' simulation or presentation.

Run the offline .NET 8 harnesses from their directories:

```powershell
Push-Location Validation
dotnet run --project HauntedHouse.Validation.csproj --configuration Release
Pop-Location
Push-Location ValidationAir
dotnet run --project AirCombat.Validation.csproj --configuration Release
Pop-Location
Push-Location ValidationHockey
dotnet run --project Hockey.Validation.csproj --configuration Release
Pop-Location
```

In Unity, **Vector Cabinet > Validate All Games** runs hockey and air checks plus 1,000 complete haunted investigations. **Validate Hockey** runs just the hockey regressions and nine complete bot matches. The project-local automation mailbox `Library/CabinetCommand.txt` accepts `validate`, `validate-hockey`, `play-smoke`, `stop`, `build` and `build-dev`. The opt-in standalone argument `-cabinetSmoke OUTPUT_DIRECTORY` runs scene integration checks and captures screenshots, then exits. Ordinary play never starts it. The original development argument `-houseSmoke OUTPUT_DIRECTORY` still routes directly to the haunted investigation walkthrough.

Validation reports are in `Documentation`; current three-game integration captures and logs are under `Logs/Hockey`, with earlier flight captures preserved under `Logs/AirCombat`. Automated controls verify mechanics and integration. They do not establish subjective skating or flight feel, human difficulty, historical fidelity, or long-term replay value; these need human playtesting.

The initial hockey delivery is recorded in [hockey-verification.txt](Documentation/hockey-verification.txt). The current airborne-shot expansion is recorded in [hockey-shots-verification.txt](Documentation/hockey-shots-verification.txt), with release-player captures under `Logs/HockeyShots/Standalone`.
