# The House That Listens

A silent, first-person haunted-house investigation for Unity 6 URP. Every visible room, object and apparition is built from code-generated geometry. There are no imported art assets, textures, audio, media services or runtime network calls in the game.

## Launch

Open `Assets/HauntedHouse/Scenes/HauntedHouse.unity` and press **Play**. Alternatively use **Haunted House > Open Game Scene**. Enter a case number or choose **New Case** on the title screen.

The current Windows build is `Builds/VectorCabinet/VectorCabinet.exe`; choose Haunted House in the shared launcher. Keep its data directory and DLLs beside the executable. **Haunted House > Build Windows Player** now builds the complete collection. This project uses Unity **6000.4.1f1** and its existing URP/input packages.

## Play

You are an investigator in a ten-room house. The dead are bound to a personal object and an unfinished act. Compare accounts, test the presence, identify the authentic keepsake, and physically perform the appropriate observance.

| Control | Action |
| --- | --- |
| W / S or Up / Down | Step forward / backward |
| A / D or Left / Right | Turn 90 degrees; no turn cost |
| Q / C | Sidestep left / right |
| E | Open the door ahead, otherwise examine the room |
| F | Examine the room, regardless of facing |
| T | Read the resonance meter; consumes a charge |
| V | Communicate with the presence |
| L | Shutter or light the lantern |
| Space | Wait one action |
| X | Spend salt for three further protected actions |
| G / P | Take or exchange / place a keepsake |
| R | Mark or attune the current room |
| B | Attempt release |
| J / Tab | Evidence notebook and recent field observations |
| M | Map explored rooms |
| H / F1 | Field guide |
| Esc | Pause, resume, restart, or withdraw at the entrance |

All actions also have mouse buttons. Rooms can be examined from any tile inside them. Reading stops time; recovered evidence remains in your notebook. An attempted step into a wall or closed door costs no action. Doors require manual opening.

The meter narrows the keepsake candidates by metal; the written engraving account distinguishes the original. Three objects share overlapping descriptions. An exchanged or displaced object returns to its original table, so an incorrect offering cannot destroy the solution.

Threats warn before striking. The correct response costs no consumable. Salt gives breathing room when you have not understood the warning. The Vestibule is safe; waiting there reduces dread, but injuries do not heal. Three wounds end a case. There is no combat.

The three haunting families have different motives, triggers and resolutions. Their complete instructions are discoverable in the house. A successful resolution, death, or voluntary withdrawal produces an ending with replay options. The case number reproduces the original house and mystery. This version keeps an ongoing attempt in memory only; it does not save mid-investigation across application exits.

## Structure

- `Assets/HauntedHouse/Core/Scenario.cs`: immutable-by-convention scenario truth and navigable grid.
- `ScenarioGenerator.cs`: deterministic xorshift generation; house topology, history, objects and supporting evidence.
- `Investigation.cs`: action-driven state, ghost behavior, equipment, reversible object handling, rituals and endings. No Unity dependencies.
- `ScenarioValidator.cs`: reachability, role constraints and consistency of the actual generated clue text.
- `SessionValidation.cs`: executable investigations, evidence-derived deductions, mistake recovery, danger and terminal-state checks.
- `Presentation/HouseRenderer.cs`: replaceable procedural presentation; opaque depth-writing architecture, vector meshes, furniture, doors and entities.
- `Presentation/HauntedHouseGame.cs`: composition, input, notebook, discovered map, field guide and all game screens.
- `Presentation/RuntimeSmoke.cs`: explicit `-houseSmoke` development-build walkthrough and screenshots; inactive in ordinary play.
- `Editor/HouseEditorTools.cs`: editor menus, build entry point and local automation mailbox.

Add a behavior family by extending the scenario's facts and evidence, its simulation conditions, and the validators. Replace the renderer to use authored visuals without changing the simulation. Existing rendering is deliberately isolated from mystery generation; it cannot decide whether a ritual succeeds.

## Validation

From the project directory in PowerShell:

```powershell
Push-Location Validation
dotnet run --project HauntedHouse.Validation.csproj --configuration Release
Pop-Location
```

The dependency-free harness uses .NET 8, pinned by `Validation/global.json`, and offline package sources. It checks 10,000 generated scenarios, repeat generation, deliberate corruptions, and 1,000 complete gameplay scenarios. Exact output is in `Documentation/validation-results.txt`. A nonzero exit means failure.

In Unity, **Haunted House > Validate 1000 Cases** checks both generated facts and the actual simulation. To batch-build with this project closed in other editors:

```powershell
& 'G:\UnityInstalls\6000.4.1f1\Editor\Unity.exe' -batchmode -nographics `
  -projectPath $PWD -executeMethod HollowHouse.Editor.HouseEditorTools.ValidateAndBuild `
  -quit -logFile Logs/house-build.log
```

For a rendered development-player walkthrough, use **Haunted House > Build Development Player**, then pass `-houseSmoke` and a writable screenshot directory. The runner traverses the house, opens the journal/map and completes each of the three rituals, then exits. It records ending state and runtime exceptions. It is an integration check, not an estimate of human completion time.

## Current scope

Ten room identities, three behavior families, three overlapping keepsakes per case, finite tools, three spatial ritual structures and reproducible cases form the complete initial loop. Motives and clues are authored templates parameterized by shared scenario facts; these are coherent combinations rather than unrestricted generated stories. The 10–15-minute target, tension, and replay longevity still need human playtesting.
