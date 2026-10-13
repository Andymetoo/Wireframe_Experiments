# Operation Afterburn — verification

Executed on 12 October 2026 with Unity 6000.4.1f1, Windows x64. This records automated execution and inspected captures, not a claim of human flight playtesting.

## Delivered player

`Builds/VectorCabinetRoguelite/VectorCabinet.exe` contains the cabinet, Haunted House, Meridian 1944 and Cold Snap scenes. Select **WWII Air Combat → Operation Afterburn**, or press **6** at the flight briefing. The five standard missions remain selectable.

The prior players in `Builds/VectorCabinet` and `Builds/VectorCabinetSquadron` were not replaced. SHA256 verification covers all **376 files** in those releases plus **75 protected source, scene and settings files**. The preservation report is `Logs/Roguelite/preservation-check.txt`. Unity inserted its input settings into `preloadedAssets` during building; that source setting was restored to its original value.

## Executed checks

| Execution | Result | Evidence |
| --- | --- | --- |
| .NET flight/core suite | **844 checks, 0 failures** | `Logs/Roguelite/air-core-validation.txt` |
| Unity all-games validation | **2,099 checks, 0 failures** | `Documentation/roguelite-unity-validation.txt` |
| Final rendered standalone sequence | **428 checks, 0 failures, 0 runtime errors**, player exit 0 | `Logs/Roguelite/FinalRendered/runtime-smoke.txt`, `runtime-errors.txt` |
| Windows release build | Succeeded, **0 errors, 0 warnings** | `Documentation/roguelite-build-result.txt` |

These suites overlap; the numbers are not a count of unique scenarios. The Unity suite includes the existing flight and hockey checks and 1,000 complete seeded Haunted House investigation validations.

A clean final rebuild was byte-identical across all **188 release files** to the standalone that passed the final rendered checks (`Logs/Roguelite/final-release-check.txt`).

Roguelite coverage includes route generation over 200 seeds, finite progression, objective failure, illegal/double transitions, repairs and replacement pilots, paid workshop offers, installation/stack limits, owned ability slots, charges and exact effect expiry, projectile modifiers, persistent damage, and new-run reset. It checks all 12 kits and 144 ordered kit pairs. Baseline aircraft definitions remain unchanged, and all five ordinary mission trajectories reproduce exactly before and after campaign use within the same runtime.

Input checks send events through Unity's actual Input System using temporary devices. They verify positive, negative and fractional wheel deltas, throttle limits, frame-rate independence, idle frames, menu suppression, restart/reset, keyboard/gamepad throttle coexistence, and a bound throttle lever. Both ordinary and roguelite flight use normal `Update` for wheel tests. Keyboard and gamepad ability edges are tested alongside preservation of standard gamepad shoulder controls.

## Actual combat, separate from fixtures

Five .NET campaign trials used legal route, reward and repair choices, with bounded pilot controls and real flight/projectiles/objectives. They produced **11 sortie victories out of 15 attempted**; one complete run won and four failed. No health, position, ammunition or objective overrides were used in these combat trials.

The standalone repeated the complete winning **run seed 0**, carrying its rewards, damage and repairs through four actual sorties:

| Sortie | Mission seed | Mission | Flight time | Player rounds | Confirmed hits | Hull home |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 1071316691 | Duel | 184.2 s | 213 | 8 | 100 |
| 2 | 780525707 | Duel | 141.1 s | 155 | 9 | 100 |
| 3 | 1308398490 | Ground attack | 231.0 s | 103 | 14 | 86 |
| 4 | 561375023 | Ground attack | 242.0 s | 44 | 7 | 79 |

Its earned operation victory and reward screens are captured in `44-afterburn-earned-operation-result.png` and `44-afterburn-earned-reward-*.png`. The pilot completed roughly 13:18 of simulated flight. This is one successful route, not proof that all routes or builds have equal difficulty. The .NET run finished with 58 hull; floating-point flight outcomes are not promised to be identical across Unity/Mono and .NET runtimes.

Additional **explicitly labeled state fixtures** inspect all reward/final menus, defeat/reset and equipment presentation. These are kept separate in the log and are not counted as combat evidence. The existing rendered sequence also completes standard interception, escort and ground-attack sorties; exercises Haunted House; and plays a full physical hockey match.

## Presentation and isolation

Inspected actual player captures of the standard/roguelite selection, operations table, workshop, reward cards, active ability HUD and earned run victory. Enemy and objective labels were inspected against sky and terrain in both modes at **1280×800 and 1600×900**. The inspection led to a font-tint correction and contrast treatment for protected bomber status, followed by a rebuilt player and the final passing rendered suite.

Cross-game checks verify one enabled camera, no duplicate listeners or simulation roots, restored cursor/time state, no leftover campaign modifiers in standard missions, and cleanup of temporary input devices/settings. No project-wide input or rendering redesign was made. No creative assets, audio, new packages or online services were introduced.

## Reproduce

From `ValidationAir`, run `dotnet run --project AirCombat.Validation.csproj --configuration Release`. Its local `global.json` selects the installed SDK. In Unity, use **Vector Cabinet → Afterburn → Validate All Games** and **Build Separate Windows Player**. The build method is `VectorCabinet.Editor.RogueBuild.ValidateAndBuild` for a closed-project batch invocation.

Run the new executable with `-cabinetSmoke <absolute-output-directory> -logFile <absolute-log-path> -screen-width 1280 -screen-height 800 -screen-fullscreen 0` for the rendered sequence. It temporarily owns test input, writes screenshots and reports, and quits when complete. Keep the player visible for meaningful graphics captures.

## Still requires human playtesting

Physical mouse-wheel, gamepad and HOTAS ergonomics were not tested with human hardware interaction. Automated events do not establish flight feel, comfort or enjoyment. Escort/interception difficulty, risk/reward values, unusual five-kit builds and ability timing need player feedback; the sampled campaign win rate is diagnostic, not a balance target. Lower resolutions, ultrawide displays and other GPUs were not visually reviewed.

There is no persistent save/resume, permanent progression, landing sequence or randomized new mission family. Runs deliberately reuse the existing five mission simulations. Exiting or restarting discards the operation, as stated in the game and guide.
