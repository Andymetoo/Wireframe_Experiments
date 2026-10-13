# The Vector Cabinet

Three independent, silent games in one Unity **6000.4.1f1 URP** project. All game visuals use code, opaque polygon meshes, vector lines and built-in UI. No imported creative assets, generated textures, audio, new packages, or runtime services are required.

## Launch

Run **`Builds/VectorCabinetAfterburn11/VectorCabinet.exe`** with its data folder and DLLs beside it. Earlier releases remain in `Builds/VectorCabinetRoguelite`, `Builds/VectorCabinetSquadron` and `Builds/VectorCabinet`. The launcher offers **Haunted House**, **WWII Air Combat** and **Hockey**, also selected with keys 1, 2 and 3. Each game has restart and return-to-cabinet buttons. Progress is kept only for the current session.

In Unity, open `Assets/GameCollection/Scenes/Cabinet.unity` and press Play, or use **Vector Cabinet > Open Launcher**. All individual game scenes also work directly. **Vector Cabinet > Afterburn 1.1 > Build Separate Windows Player** builds the current collection into its separate Phase 1.1 folder; the older build menu items retain their earlier destinations.

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

A fictional Channel sortie in a low-polygon WWII setting. Fly the Kestrel Mk.IX against Falke fighters over a seeded island. Seven selectable briefings include the original **Training Duel** and **Courier Cover**, three squadron assignments, **Ace Hunt**, and **Channel Air Battle**. Rook and Finch join the squadron missions and can be lost. All missions begin airborne; there is no takeoff or landing sequence.

- **Fighter Interception:** stop both raid leaders before they cross the defended boundary. Escort fighters try to delay you. Designating a raider for your wingmen while you intercept the other can split the workload.
- **Bomber Escort:** deliver at least two of three Heron bombers to air cover. Bombers fly an actual route and formation. Killing attackers does not replace lost bombers or complete the route.
- **Ground Attack:** destroy four moving supply trucks. Two optional flak batteries fire physical rounds, and enemy fighters defend the convoy. Set up a shallow dive, fire a short burst, pull clear and extend before returning.
- **Ace Hunt:** defeat Voss, a disciplined energy fighter with an escort. His stock aircraft obeys normal flight limits; better decisions, defensive reactions and gun discipline make the difference. Escorts are secondary objectives.
- **Channel Air Battle:** your squadron and the independent Blue pair face five fighters arriving in two flights. Break the offensive by destroying three and surviving until both enemy flights reach the sector or are defeated. Reinforcements approach physically from beyond friendly visual range.

In the larger battles, pilots return toward the contested sector after a long pursuit and open separation when a sustained turn produces no firing opportunity. Use those extensions to regain speed, choose another target or bring Rook and Finch back together before the next flight arrives.

The original **Training Duel** offers one armed opponent and a lead cue. **Courier Cover** asks you to keep the unarmed transport Lark alive for five minutes or destroy both interceptors.

Briefing shortcuts **1–5** select the original missions, **7** selects Ace Hunt, **8** selects Channel Air Battle, and **6** opens Afterburn. Press Enter to launch the selected standard assignment. Interception now presents a crossing opportunity: cut onto the raid's diagonal track before committing to a tail chase; escort fighters are distractions from the two raid leaders.

| Control | Action |
| --- | --- |
| W / S or Up / Down | Nose down / nose up |
| A / D or Left / Right | Roll left / right |
| Q / E | Rudder left / right |
| Mouse wheel up / down; Shift / Ctrl, or R / F | Increase / decrease throttle (wheel: 4% per notch) |
| Space / left mouse | Fire guns |
| Hold Z | Gentle wings-level control assist |
| C | Cockpit / chase camera |
| Tab | Select next surviving hostile |
| 1 / 2 / 3 / 4 during flight | Form Up / Engage selected target / Cover Me / Regroup |
| Hold right mouse | Look around and behind either shoulder; release returns forward |
| Hold X | Quick rear view; release returns forward, overriding RMB while held |
| V / middle mouse | Instantly center look |
| G | Toggle predicted lead cue, enabled by default |
| M | Toggle mouse stick; cursor above center pulls up |
| H | Pause and read flight notes |
| Esc | Pause, sensitivity, restart, return to cabinet |

Mouse stick and keyboard can be combined; the pause screen has sensitivity and mouse pitch inversion. Start with keyboard controls and short inputs. The cursor recenters when mouse stick is enabled. The control system never directly snaps aircraft attitude or velocity.

RMB free-look permits 165 degrees to either side with bounded vertical motion. RMB/X withhold mouse steering and LMB gunfire; keyboard steering and Space guns remain usable. Releasing returns forward smoothly and safely restores normal mouse flight. An LMB press held through a look gesture must be released before mouse gunfire resumes.

**Turning:** bank with A/D, release roll, then pull S. More bank needs more elevator. Hold Z to ease the wings level. Climbing spends speed; diving gains speed. Near stall, lower the nose and add power. Sustained overspeed and extreme load damage the airframe. Damaged aircraft lose power, lift and control authority.

**Gunnery:** close to about 300–600 metres and fire short bursts. Rounds travel through space, inherit aircraft velocity, drop under gravity, and collide with moving wing/fuselage volumes. The green cue predicts an intercept; amber brackets identify the aircraft. Put the outlined gunsight on the lead cue, not the target bracket. Actual aircraft damage triggers a brief X; a longer amber confirmation identifies destruction. The cue starts enabled in both flight modes and never steers or aims your guns.

**Squadron:** wingmen fly their formation slots through normal controls, manage speed, acquire visible contacts, defend themselves and recover from low energy or terrain danger. Orders allow immediate survival maneuvers. Engage uses the target selected when you issue the order; cycling targets alone does not retask the flight. Cover Me focuses on threats near you. Regroup breaks pursuit and returns to formation. Damaged or exhausted pilots may withdraw.

**Awareness:** allies are green and hostiles amber. Outlined labels retain their colors without floating backplates. The contact scope shows visible contacts within 4 km, with terrain occlusion. Each living ground objective also has a numbered marker, radar diamond and off-screen/ridge guidance; known raid leaders receive persistent guidance too. Destroyed objectives disappear. These mission markers identify assigned objectives without exposing unrelated hidden aircraft. Watch bomber condition and threat notices; abandon a pursuit when the protected formation needs help.

Success or destruction leaves approximately three seconds to see the aftermath before results. Falling aircraft and smoke are presentation effects; finalized scores and outcomes cannot change during the hold. Enter or gamepad A continues immediately, and Esc can pause, restart or leave.

**Gamepad:** left stick flies, triggers control rudder, RB fires, LB levels, D-pad up/down adjusts throttle, right stick looks. X selects a target, B changes camera, A toggles lead. Hold Y and tap D-pad up/right/left/down for Form Up/Engage/Cover Me/Regroup. Start pauses; menus use D-pad/A/B. Pause offers sensitivity, deadzone, inversion and optional Input System flight-stick binding paths. Settings last for the current Air Combat scene. Physical controller and HOTAS testing remains necessary; see [input guide](Documentation/meridian-input-guide.txt).

The HUD reports speed in knots, altitude in feet, heading, throttle, airframe condition, ammunition and load. Stalls, terrain proximity and overspeed show warnings. A sortie number reproduces the starting terrain, contacts and pilot decisions when the same controls and simulation steps are used. No mid-sortie save is implemented.

### Operation Afterburn — roguelite

Choose **WWII Air Combat**, then **Operation Afterburn** (or **6** on its briefing). All seven standard missions remain available. Survive four sorties: choose among three assignments, bring home supplies, select one reward, and prepare for the next flight. Hull damage and wingman losses persist; ammunition, fuel and tactical charges replenish. Each later stop offers a fixed workshop refit competing with repairs and replacement pilots for supplies.

Twelve field kits change the existing aerodynamic and weapon simulation. Heavy cannon, armor, extended wings, a stripped airframe, racing fairings and other equipment have distinct tradeoffs. Up to five kit types can be installed, each up to Mk II. Two tactical slots hold **Emergency boost**, **AP belt**, **Combat flaps** or **Flight rally**. On keyboard, press **T / Y** (or **5 / 6**). On gamepad, hold the **Y/north button**, then press **LB / RB**. The HUD separates these bindings and reports active duration, remaining uses and rejected activations. Emergency boost costs hull; flaps sacrifice speed; AP bursts consume ammunition faster; rally needs surviving wingmen.

The four stages now have distinct encounter decks: **Opening moves** offers a duel, courier escort or risky ground strike; **Contested skies** introduces bomber duty, crossing interception and the ace; **Pressure front** adds a reinforced ground strike, squadron battle or better-supported ace; **Final offensive** offers reinforced bomber protection, a seven-fighter battle in three waves, or the ace's high-altitude formation. Enemy airframes use stock performance, hull and weapons: escalation comes from opponents, geometry, reinforcements and pilot behavior instead of hidden stat inflation.

An objective failure or crash ends the run. Retry the seed, start a fresh operation, return to standard missions, or exit to the cabinet. Restart during flight restarts the entire operation. There is no save/resume or permanent stat progression. Seeded routes and rewards replay with the same choices; choosing another seed gives a different operation. [Full guide and architecture](Documentation/operation-afterburn.md).

In both flight modes, mouse wheel throttle changes the existing command in discrete steps and is ignored in menus. Contact labels and the stronger gunsight use contrasting outlines. Briefing, equipment and ability descriptions have larger body text with adjusted wrapping.

[Phase 1.1 verification](Documentation/afterburn11-verification.txt) records the current build, simulation and rendered checks, preservation results and remaining balance questions. The [original Afterburn verification report](Documentation/operation-afterburn-verification.md) remains the record of the earlier release.

## Haunted House — The House That Listens

The original investigation remains: ten generated rooms, three ghost families, evidence-based deduction, finite tools and spatial resolution rituals. Its core simulation and procedural renderer are preserved. The only ordinary-play additions are buttons to return to the cabinet.

W/S move, A/D turn, E opens doors or examines, F examines, J opens the notebook, M the discovered map, and H the field guide. Read the in-game guide for investigative tools and rituals. The full original manual is in [Documentation/haunted-house-manual.md](Documentation/haunted-house-manual.md).

## Implementation and validation

`Assets/GameCollection` owns the launcher, scene switching, combined builder and opt-in integration runner. Each scene owns its controls, camera, UI, generated geometry and state. Loading a game replaces the current scene; ordinary play uses no persistent game objects. The opt-in diagnostic runner is the only persistent helper.

`Assets/AirCombat/Core` is plain C# and System.Numerics: aerodynamic forces and damped angular rates, terrain field, tactical pilots, ballistic weapons, aircraft damage and mission progression. `Presentation` adapts keyboard/mouse/gamepad or configured axes into device-independent intentions, runs a fixed 60 Hz simulation and renders replaceable procedural meshes and UI. See [the architecture notes](Documentation/meridian-architecture.md) and [squadron design notes](Documentation/squadron-design-and-validation.txt).

**Vector Cabinet > Afterburn 1.1 > Validate All Games** executes the current combined checks in Unity. **Vector Cabinet > Afterburn 1.1 > Build Separate Windows Player** writes the new expansion folder while preserving earlier builds. The air harness includes bounded complete bot sorties, orders and perception checks, formation/rejoin trials, terrain recovery, physical ground gunnery, expanded encounters and run progression. Automated victories establish working objectives, not human-rated flight feel or balance.

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
