# Operation Afterburn

A four-sortie air-combat roguelite inside **WWII Air Combat**. Fly the existing combat missions, bring a damaged aircraft home, and decide whether your next supply crates belong in repairs, a replacement wingman or an experimental field kit.

The seven standard missions remain separately selectable. Haunted House and Hockey still use the shared cabinet launcher. Phase 1.1 launches from `Builds/VectorCabinetAfterburn11/VectorCabinet.exe`; earlier build folders remain separate. In Unity, use **Vector Cabinet > Afterburn 1.1 > Build Separate Windows Player** for this destination.

## Start a run

1. Select **WWII Air Combat** in the cabinet.
2. Choose **Operation Afterburn / Roguelite**, or press **6** at the standard briefing.
3. On the operations table, inspect three route choices. Prepare your aircraft, then click a route or press **1–3** to launch it.
4. Win the sortie, collect its supplies, and choose **one** reward. Repeat until the fourth victory breaks the blockade.

An objective failure or your aircraft's destruction ends the operation. Equipment and supplies belong only to that run. Retry its seed to revisit the operation, or choose a new seed for fresh mission and reward combinations. Route choices also influence later generation.

There is **no persistent save or permanent stat progression**. Leaving for standard missions, returning to the cabinet, quitting or starting a new operation ends the current run. The pause menu's restart action restarts the whole operation with its original seed; it does not erase one failed encounter while keeping its equipment.

## Pick the right assignment

Each stop offers three assignments drawn from the existing flight encounters:

| Assignment | Actual objective | Tactical consideration |
| --- | --- | --- |
| Fighter sweep | Defeat a fighter alone. | Smaller haul, no wingmen; handling equipment appears in salvage. |
| Courier gamble | Protect the courier or eliminate its attackers. | Solo protection duty favors timely interception and accurate guns. |
| Cloud convoy | Deliver at least two surviving bombers. | Your flight must stay close enough to protect the formation. |
| Valley interdiction | Destroy four trucks through fighter and flak cover. | Gun power helps, but weight and poor pullout performance are dangerous. |
| Raid breaker | Stop both raiders before either crosses the boundary. | Climb, speed and coordinated target selection matter more than chasing escort fighters. |
| The ace's challenge | Defeat Voss and survive. | Escorts are secondary. Deny his energy advantage and avoid following a slow climbing lure. |
| Squadron clash | Defeat a majority of the enemy fighters and account for every incoming flight. | Your squadron and the independent Blue pair face successive formations; conserve ammunition and regroup between contacts. |

The table shows each route's risk and supply reward. A heavily armed aircraft may suit a ground strike; an agile but fragile build may prefer a fighter engagement. All routes use physical flight and projectiles.

The four stages now change the tactical problem rather than quietly increasing enemy health, power or gun damage:

| Stage | Three route choices | Escalation |
| --- | --- | --- |
| 1 — Opening moves | Fighter sweep / Courier gamble / Valley interdiction | A solo duel, a two-attacker protection mission, or a risky early strike with Rook and Finch. |
| 2 — Contested skies | Cloud convoy / Raid breaker / The ace's challenge | Bomber protection, a crossing interception, or Voss with one escort. |
| 3 — Pressure front | Valley interdiction / Squadron clash / The ace's challenge | The strike gets a fresh defending fighter; the battle has five fighters in two flights; Voss brings two escorts. |
| 4 — Final offensive | Cloud convoy / Squadron clash / The ace's challenge | A fresh flight joins the bomber attack; the battle grows to seven fighters in three flights; Voss and two escorts begin with a greater height advantage. |

Voss flies a stock Falke with ordinary hull and flight limits. His advantages are quicker decisions, more disciplined defensive reactions, predictive gunnery and willingness to extend when a climb would lose energy. Rook and Finch can help isolate him; no special weapon or upgrade is required.

Squadron clash adds two independent Blue-flight allies to your squadron. In the ordinary battle, defeat **three of five** enemies; in the finale, defeat **four of seven**. Every incoming flight must physically enter the contested sector or already be destroyed before victory. Reserve aircraft approach in formation from outside friendly visual range rather than appearing on your tail. Blue flight is local support, separate from the persistent Rook/Finch roster.

Interception begins with a closer, diagonal crossing opportunity while retaining normal aircraft speeds. Cut across toward the raid's track, gain a firing position and split the raid leaders with your wingmen. Chasing escort fighters or settling into a long tail chase still risks losing the objective.

At the standard briefing, **1–5** select Fighter Interception, Bomber Escort, Ground Attack, Training Duel and Courier Cover. **7** selects Ace Hunt, **8** selects Channel Air Battle, and **6** opens Afterburn. Enter launches the selected standard mission.

## Repairs compete with equipment

Your hull condition persists between sorties. Rook and Finch retain their damage, and a lost wingman stays absent until replaced. Solo assignments do not erase the condition of pilots who stayed behind.

| Preparation | Cost | Result |
| --- | --- | --- |
| Repair your aircraft | 2 supplies | Restore up to 35 hull. |
| Repair a surviving wingman | 1 supply | Restore up to 40 hull. |
| Recruit a lost wingman | 3 supplies | Assign a fresh pilot and aircraft. |
| Workshop refit | 3 supplies | Install the displayed field kit; one offer per later stop. |

Fuel, ammunition and tactical charges replenish free at launch. Rewards always include a repair alternative: restore 40 player hull, 25 hull to surviving wingmen, and gain one supply. That choice gives up an equipment or ability reward; it cannot revive a lost pilot.

## Give the aircraft a character

There are twelve field kits, with up to five distinct installations and two levels per kit. A second level compounds its benefits and drawbacks. Installed kits remain for the run; you cannot shed their weight before an awkward assignment. The hangar shows mass, power, wing area, roll response and gun statistics.

Examples of useful tradeoffs:

- **20 mm conversion:** heavier, slower-firing guns with more damage per round and less ammunition. Combine with **deep ammunition bays** for endurance, accepting further mass and drag.
- **Armored bathtub:** reduced incoming gun damage, heavier mass and slower roll. Armor does not make a terrain collision survivable.
- **Stripped airframe + two-stage supercharger:** rapid acceleration and stronger control response, paid for with greater vulnerability and a lower safe dive speed.
- **Extended wings:** better lift, lower stall speed and less induced drag, but slower rolls and more parasite drag. **Racing fairings** push toward the opposite priorities.
- **Bench-harmonized guns:** much tighter dispersion at the expense of per-round damage. **High-velocity feed** reduces the lead required, but ammunition disappears faster.
- **Water injection kit:** longer emergency boosts and an extra charge per level, with added mass and drag. Boost still wears the airframe.
- **Flying workshop:** automatic repairs after victories, with the weight of its equipment aboard. It cannot repair a failed operation or restore dead wingmen.

Build names such as *Heavy hitter* or *Knife-edge racer* summarize combinations. Their performance comes from actual mass, thrust, lift, drag, control and weapon changes, not from a separate arcade movement system.

## Two tactical buttons, no cockpit card menu

You begin with **Emergency boost** in slot one. Acquire other abilities as rewards, then click the slot buttons in the hangar to cycle through owned cards. New cards fill an empty slot, then replace slot two; displaced cards remain owned and can be re-equipped.

**Keyboard:** tap **T** for slot one or **Y** for slot two. **5/6** remain aliases. **Gamepad:** hold the **Y/north button**, then press **LB** for slot one or **RB** for slot two. These are separate keyboard and controller bindings; a keyboard ability needs only one key.

Holding a button does not repeatedly spend charges. The HUD reports remaining uses, active duration and concise reasons for rejected activations. Both slots can be active together, but an active slot cannot consume another use until it expires. Pausing or browsing cards cannot activate an ability.

| Ability | Effect | Constraint |
| --- | --- | --- |
| Emergency boost | 15 seconds of increased engine power and thrust. | Two uses; costs 3 hull each time and requires more than 15 hull. Water injection improves duration and uses. |
| AP belt | 9 seconds of stronger, faster gunfire. | Two uses; still consumes physical ammunition and requires a firing solution. |
| Combat flaps | 12 seconds of increased lift and pitch response with a lower stall speed. | Two uses; much more drag and a reduced safe maximum speed. Deploy for a turn, not a fast dive. |
| Flight rally | 20 seconds of Cover Me, improved wingman engine power and tighter gun dispersion. | One use; requires surviving wingmen in the current sortie. It summons nobody. |

Emergency boost and AP belt can create an attack window; using them while out of position wastes their limited duration. Combat flaps can help a slow turn but also make an energy deficit worse. Squadron orders **1–4** and target selection **Tab** remain available throughout combat.

## Flight controls and readable contacts

All existing flight controls remain: **W/S** nose down/up, **A/D** roll, **Q/E** rudder, **Space/LMB** guns, **Z** level assist, **Shift/Ctrl** or **R/F** throttle. The **mouse wheel** now changes throttle by four percentage points per notch in both standard missions and Afterburn. Fractional wheel movement is preserved; menus and card screens do not move the throttle.

Gamepad D-pad throttle and optional bound throttle levers remain supported. A stationary bound lever permits wheel or keyboard adjustments; moving the lever takes command again. The existing throttle instrument shows the current setting.

Hold **RMB** to turn your head left, right, up, down and substantially behind either shoulder. Release it to return forward in roughly a quarter-second. Hold **X** for a quick rear view; X takes precedence if both controls overlap, and releasing it returns forward before held RMB resumes. Head movement stops at 165 degrees to either side, 80 degrees up and 70 degrees down. **V**, middle mouse or right-stick click centers instantly.

Looking does not rotate the aircraft. RMB/X withhold mouse steering and LMB gunfire; keyboard steering and deliberate Space gunfire remain available. If LMB was held during a look gesture, release it before clicking to fire again. Mouse-stick control recenters safely on return. Full sensitivity and optional flight-stick bindings are in [the input guide](meridian-input-guide.txt).

Contact labels use stable team/objective colors with contrasting outlines and a restrained shadow, without floating dark backplates. The outlined gunsight is stronger against the sky. The lead cue starts enabled in both standard missions and Afterburn; **G** toggles it. It predicts an intercept, never aims your guns.

Each surviving mission-critical vehicle has its own numbered marker and radar diamond, with directional and distance guidance when off-screen or behind a ridge. Nearby labels are separated instead of silently dropping objectives. Destroyed objectives disappear. Assigned raid leaders also retain guidance; ordinary hidden aircraft still obey contact range and terrain occlusion.

A brief **X** at the gunsight confirms actual projectile damage to an aircraft. A longer amber confirmation identifies destruction. Tactical damage abilities use the same hit events, so merely placing the crosshair over a target cannot trigger confirmation.

The outcome remains visible for approximately **three seconds** before rewards or results cover the view. Aircraft wrecks keep their momentum, fall and trail geometric smoke; a destroyed player's aircraft gets an outside view. The simulation result is already final, so this presentation cannot change the winner or carried damage. **Enter/gamepad A** skips the hold; **Esc** pauses it and provides restart/exit controls. Briefing, equipment and ability descriptions use larger text with adjusted wrapping.

## Implementation and validation boundaries

`RogueRun` owns the planning, flight, reward and terminal states, seeded choices, four stage decks, supplies, persistent losses and ability timers. `RogueCatalog` describes equipment; `RogueLoadout` rebuilds session-owned aircraft and weapon parameters from a clean baseline. Temporary effects expire through the same fixed-step clock used for the sortie.

Each assignment creates an `AirMission`. `AirEncounterOptions` supplies isolated reinforcement and starting-position pressure; `AirEncounters` owns expanded encounters and physical incoming flights. Existing flight dynamics, pilots and weapons remain authoritative. Per-run definitions are cloned before modification, and standard sorties construct fresh state. `AirRogueliteUI` supplies the operation screens while `AirInputAdapter` delivers flight controls and mode-gated abilities. `AirViewControls` changes only the viewing angles. The aftermath uses renderer snapshots and its own presentation clock, never another live combat simulation.

Automated coverage is in `RogueValidation` and `AirInputValidation`, with integration exercised through the cabinet diagnostics. Execution results belong in the accompanying verification report; the presence of test code alone is not a passed test. Human playtesting remains necessary for route difficulty, upgrade combinations, controller comfort and whether one more run feels worthwhile.
