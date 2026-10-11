# The Vector Cabinet

Three independent, silent games in one Unity **6000.4.1f1 URP** project. All game visuals use code, opaque polygon meshes, vector lines and built-in UI. No imported creative assets, generated textures, audio, new packages, or runtime services are required.

## Launch

Run **`Builds/VectorCabinet/VectorCabinet.exe`** with its data folder and DLLs beside it. The launcher offers **Haunted House**, **WWII Air Combat** and **Hockey**, also selected with keys 1, 2 and 3. Each game has restart and return-to-cabinet buttons. Progress is kept only for the current session.

In Unity, open `Assets/GameCollection/Scenes/Cabinet.unity` and press Play, or use **Vector Cabinet > Open Launcher**. All individual game scenes also work directly. **Vector Cabinet > Build Windows Player** builds the launcher and three game scenes into the same executable. The original Haunted House build menu now builds the collection too.

## Hockey — Cold Snap

First-person, three-on-three hockey plus goalies in a rounded indoor rink. You stay **Blue #9**, with two AI teammates against three AI skaters. Blue always attacks the orange end. Regulation lasts three minutes; a tie gets one minute of sudden-death overtime, then a draw. Goals pause play and reset to a center faceoff. No icing, offsides or penalties are simulated.

Try **Open Ice** first: the clock stops and opposing skaters hold position while goalies and your teammates remain active. Practice coasting, braking, calling for passes and shooting into corners. The practice rink uses the same physical rules as a match.

| Control | Action |
| --- | --- |
| Mouse | Look and aim; momentum keeps its current direction |
| W / A / S / D | Push forward / left / backward / right relative to view |
| Shift | Stronger strides toward higher speed |
| Space | Brake using the skate edges |
| Left mouse, hold then release | Charge and shoot |
| F | Quick wrist shot |
| Right mouse / Q | Pass; nearby teammate within the aim cone receives a small lead assist |
| E | Check an opponent within reach |
| R | Request a pass from an AI teammate |
| C | First-person / chase view |
| Tab | Toggle rink map |
| H / Esc | Notes / pause, sensitivity, restart and cabinet |
| Arrow keys | Keyboard alternative for looking around |

**Skating:** release movement keys to coast. Look around while gliding; your view does not instantly redirect velocity. Speed gives you reach but requires room to stop or reverse. Use the glide-direction indicator when your head and travel direction differ. No camera bob, roll or impact shake is applied.

**Puck play:** get the puck within stick reach and in front of the body. The stick then guides it with a capped force; it is never parented to a skater. Hard turns, fast passes, collisions and checks can release it. Shots stay on the ice. Aim away from the goalie's pads and recover rebounds. The dark puck is slightly enlarged visually for readability; collision uses its smaller physical radius. A diamond/direction prompt and rink map help recover it when looking elsewhere.

**Team play:** seek a clear passing lane, then R to call. Teammates may refuse a blocked or distant pass. One player pressures or recovers while the others support or protect dangerous space. With the puck, bots carry, pass and aim for corners. Goalies track angles with reaction delays and limited movement; saves are physical pad contacts that leave rebounds.

Scores and match statistics lead to rematch and new-match options. Match seeds reproduce AI decisions for identical fixed-step inputs. There is no player switching, aerial puck simulation, stick-height control, stamina meter or full rulebook. [Architecture and limits](Documentation/cold-snap-architecture.md) describe the implementation.

## WWII Air Combat — Meridian 1944

A fictional Channel sortie in a low-polygon WWII setting. Fly the Kestrel Mk.IX against Falke fighters over a seeded island. Begin with **Training Duel**: one armed opponent and an optional lead cue. **Courier Cover** asks you to keep the unarmed Heron transport, callsign Lark, alive for five minutes, or shoot down both interceptors. Missions begin airborne. There is no takeoff or landing sequence.

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
| G | Toggle predicted lead cue |
| M | Toggle mouse stick; cursor above center pulls up |
| H | Pause and read flight notes |
| Esc | Pause, sensitivity, restart, return to cabinet |

Mouse stick and keyboard can be combined; the pause screen has sensitivity and mouse pitch inversion. Start with keyboard controls and short inputs. The cursor recenters when mouse stick is enabled. The control system never directly snaps aircraft attitude or velocity.

**Turning:** bank with A/D, release roll, then pull S. More bank needs more elevator. Hold Z to ease the wings level. Climbing spends speed; diving gains speed. Near stall, lower the nose and add power. Sustained overspeed and extreme load damage the airframe. Damaged aircraft lose power, lift and control authority.

**Gunnery:** close to about 300–600 metres and fire short bursts. Rounds travel through space, inherit aircraft velocity, drop under gravity, and collide with moving wing/fuselage volumes. The small green cue predicts an intercept; the larger amber brackets identify the aircraft. Put the gunsight on the lead cue, not the target bracket. A HIT flash confirms damage. The cue does not steer or aim your guns.

**Escort:** Lark is green on the contact scope. Amber contacts are hostiles; the scope faces with your heading and shows a 4 km radius, with farther contacts clamped to its edge. Watch the courier's condition and intervene before a pursuer gets a sustained firing position. Destroyed enemies, survival, remaining condition and hits contribute to the sortie score.

The HUD reports speed in knots, altitude in feet, heading, throttle, airframe condition, ammunition and load. Stalls, terrain proximity and overspeed show warnings. A sortie number reproduces the starting terrain, contacts and pilot decisions when the same controls and simulation steps are used. No mid-sortie save is implemented.

## Haunted House — The House That Listens

The original investigation remains: ten generated rooms, three ghost families, evidence-based deduction, finite tools and spatial resolution rituals. Its core simulation and procedural renderer are preserved. The only ordinary-play additions are buttons to return to the cabinet.

W/S move, A/D turn, E opens doors or examines, F examines, J opens the notebook, M the discovered map, and H the field guide. Read the in-game guide for investigative tools and rituals. The full original manual is in [Documentation/haunted-house-manual.md](Documentation/haunted-house-manual.md).

## Implementation and validation

`Assets/GameCollection` owns the launcher, scene switching, combined builder and opt-in integration runner. Each scene owns its controls, camera, UI, generated geometry and state. Loading a game replaces the current scene; ordinary play uses no persistent game objects. The opt-in diagnostic runner is the only persistent helper.

`Assets/AirCombat/Core` is plain C# and System.Numerics: aerodynamic forces and damped angular rates, terrain field, tactical pilots, ballistic weapons, aircraft damage and mission progression. `Presentation` adapts keyboard/mouse input, runs a fixed 60 Hz simulation and renders replaceable procedural meshes and UI. See [the architecture notes](Documentation/meridian-architecture.md) for details and limitations.

`Assets/Hockey/Core` uses the same separation with its own ice-plane simulation, skating, puck contacts, team pilots, goalkeepers and match clock. Core ticks subdivide to 120 Hz; fast puck motion uses smaller collision steps. `HockeyGame` owns input and menus; `HockeyRenderer` owns the rink, equipment animation and camera. Neither touches the older games' simulation or presentation.

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

The completed hockey delivery's executed checks, build results, preservation audit and practical limits are recorded in [hockey-verification.txt](Documentation/hockey-verification.txt). Actual release-player images are under `Logs/Hockey/VisibleStandalone`.
