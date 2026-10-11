# The Vector Cabinet

Two independent, silent games in one Unity **6000.4.1f1 URP** project. All game visuals use code, opaque polygon meshes, vector lines and built-in UI. No imported creative assets, generated textures, audio, new packages, or runtime services are required.

## Launch

Run **`Builds/VectorCabinet/VectorCabinet.exe`** with its data folder and DLLs beside it. The launcher offers **Haunted House** and **WWII Air Combat**. Each game has restart and return-to-cabinet buttons. Progress is kept only for the current session.

In Unity, open `Assets/GameCollection/Scenes/Cabinet.unity` and press Play, or use **Vector Cabinet > Open Launcher**. Both individual game scenes also work directly. **Vector Cabinet > Build Windows Player** builds all three scenes into the same executable. The original Haunted House build menu now builds the collection too.

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

Run the offline .NET 8 harnesses from their directories:

```powershell
Push-Location Validation
dotnet run --project HauntedHouse.Validation.csproj --configuration Release
Pop-Location
Push-Location ValidationAir
dotnet run --project AirCombat.Validation.csproj --configuration Release
Pop-Location
```

In Unity, **Vector Cabinet > Validate Both Games** runs the air checks and 1,000 complete haunted investigations. The project-local automation mailbox `Library/CabinetCommand.txt` accepts `validate`, `play-smoke`, `stop`, `build` and `build-dev`. The opt-in standalone argument `-cabinetSmoke OUTPUT_DIRECTORY` runs scene integration checks and captures screenshots, then exits. Ordinary play never starts it. The original development argument `-houseSmoke OUTPUT_DIRECTORY` still routes directly to the haunted investigation walkthrough.

Validation reports are in `Documentation`; rendered captures and diagnostic logs are under `Logs/AirCombat`. Automated controls verify mechanics and integration. They do not establish subjective flight feel, mission difficulty, historical fidelity, or long-term replay value; these need human playtesting.
