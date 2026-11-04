# Aurora R-54 — operator realism audit

2026-11-04 · Unity 6000.4.1f1 URP · latest local `Assets/Experiments/IceResurfacer`

## 1. Executive assessment

Aurora already reproduces much of the actual work: separate shave/wash/flood footprints, finite reservoirs, independent conveyors, retained conditioner snow, side overflow, recollection on ice, progressive dumping, hose filling, overflow, hydrostatic slowing, articulated hydraulics and a separately deployed board brush. These are implemented systems, not proposed features. The authored machine, arena, calibration, Operator Tuning, camera work and GPS recordings were treated as authoritative.

The largest operator-facing gaps are the simplified drive station, absent physical brush/pump switches and horn, instantaneous brush contact compliance, limited mechanical significance of the snow breaker, and service/reset shortcuts. Water and snow generally balance within measured floating-point error. This is a volume-based simulator, not a complete density, thermal, engine or hydraulic simulation.

Two bounded water corrections were implemented: the pickup pump now requires its horizontal-conveyor drive, and it can prime from existing standing water with the blade retracted. No tank capacities, flow calibration, snow expansion, collision model or authored geometry were changed.

**The restrained augers-off effect is largely explained by the existing calibration and rendering.** Normal `.35` blade setting means `.056 mm` effective bite. The conditioner retains 80 L before shedding. At 1 m/s on the measured fresh lanes, both stopped augers begin spilling after approximately 111 m; steady spill is only .736 L/s split between two shallow windrows. Increasing visual activity need not increase snow inventory. Detailed results and limitations follow.

**Validation boundary:** deterministic source-level operating cycles and compilation against installed Unity assemblies passed. An isolated Unity Editor could not initialize licensing and never reached Play Mode. This report does not claim fresh rendered, mouse-interaction, prefab-runtime-binding or subjective driving proof.

## 2. Evidence and current configuration

### Primary documents and model distinctions

Page references below use **printed chapter-page / one-based PDF page**. A statement about 526 is not automatically a statement about 546 or fictional Aurora.

- **M526:** supplied [526 Operating Instructions PDF](<../Assets/Experiments/IceResurfacer/Documentation/526 Operating Instructions English.pdf>), cover DM-20077, rev 3/10/25, 98 pages. Existing extracted text was located in the same directory; additional older readable extracts exist under `Validation~`.
- **M546:** no original 546 manual was found in the experiment Documentation directory. The current manufacturer-linked [546 manual](<../Assets/Experiments/IceResurfacer/Validation~/OperatorRealismAudit-20261104/Evidence/546 Operating Instructions English 2025-03-12.pdf>) was downloaded through the [manufacturer owner area](https://zamboni.com/owners-area/?folder=546), DM-54620, rev 03/12/2025, 90 pages. Its full extraction and relevant rendered pages are retained locally.
- **S546:** [manufacturer specification sheet, rev 06/2021](https://zamboni.com/wp-content/uploads/specs/546_specs.pdf), pp. 1–2. It supports 738 L flood, 310 L wash, 1.956 m blade, 1.96 m wheelbase, 2.83 m³ actual bin and 3.54 m³ compacted capacity. Its options list distinguishes AWS speed-related water metering, automatic snow breaker, board brush, wash equipment and other fittings.
- M546 capacity table **xiv / PDF15** distinguishes **273 L wash** from **310 L with the plastic wash-tank option**. Aurora's 738/310 L combination is intentional and defensible. The 526's 727/273 L figures are not a reason to change it.
- The 546's **two conveyor handles and pull-back-to-lower conditioner** are directly documented at **7-6 / PDF66**. Applying the 526 station arrangement indiscriminately would produce false defects.
- The manual's brush-operation contact roller belongs to the **brush arm**. The separate front chassis guide roller need not contact boards continuously on straight passes. The operator's clarification supersedes contrary assumptions in older project notes.
- Full-valve flood `.72 L/s`, wash recovery `.644`, snow expansion `3.1`, retention, bite response, hydraulic timing and fill speed are Aurora/operator calibrations. The manuals do not establish those rates.

Extraction caution: the 526 PDF uses unusual font encoding. Fresh page images were checked for important conclusions. Both current PDFs visibly specify **140°F / 60°C maximum flood-water fill temperature** (526 **7-10 / PDF69**, 546 **7-11 / PDF71**). The older `526-paged-reference.txt` says 160°F / 70°C; it must not override the actual PDF. No temperature implementation was added. [Provenance, SHA-256 hashes and page mapping](../Assets/Experiments/IceResurfacer/Validation~/OperatorRealismAudit-20261104/Evidence/manual-provenance.txt) documents the evidence.

### Actual local settings

The authored scene has enabled `IceOperatorTuning`, separate from its underlying calibration. Audit measurements parse the frozen scene YAML and apply those settings; they do not substitute `new SimulatorConfig()` defaults.

| Setting | Authored runtime | Procedural/default configuration |
|---|---:|---:|
| Flood / wash / loose-equivalent snow capacity | 738 L / 310 L / 3.54 m³ | Same |
| Maximum flood flow | 1.8 × .4 = **.72 L/s** | 1.8 L/s |
| Maximum wash supply | 1.05 L/s | Same |
| Direct wash-return fraction | **.644** | .82 |
| Loose-snow pickup limit | **50 mm** | 6 mm |
| Shavings expansion | 3.1 × 1 | 3.1 |
| Horizontal / vertical transport limit | 12 / 10 L loose snow per second | Same |
| Conditioner / vertical retention | 80 / 55 L loose snow | Same |
| Overflow particle density / size | ×3 / ×1.25 | ×1 / ×1 |

Sources: `Scenes/IceResurfacer_AuthoredMachine.unity` component 1003 and calibration; `Core/OperatorTuningSettings.Apply`; `Presentation/IceOperatorTuning.CreateRuntimeConfig`; `Core/SimulatorConfig`. Existing mechanical alignment, board-brush rig, recorded routes, calibrated guides and GPS presentation remain in place.

## 3. Manual-to-implementation comparison

Code references are relative to [the experiment](../Assets/Experiments/IceResurfacer). **G** means gameplay, **V** visuals, **A** sound/interaction. Severity describes the realism gap, not a certification of machine safety. Confidence concerns the evidence; subjective feel remains unverified. “Accurate” is reserved for narrowly verified values or directions.

### Operator station and mechanics

| System / manufacturer behavior | Local implementation and assessment | Classification; impact; severity / confidence | Recommended action |
|---|---|---|---|
| Hydrostatic slowing plus separate brake: M526 7-2/PDF61; M546 7-2/PDF62 | `Simulation.UpdateVehicle`: pedal release decelerates, brake slows faster, reversal brakes through zero; snow/load affects acceleration. Rates are calibration. | **Good approximation**; G; medium / high | Retain; operator-calibrate response rather than infer braking numbers from prose. |
| Independent engine speed, transmission selection and accelerator: same sections; 526 2200–2500 RPM versus 546 2400–2800 RPM | `OperatorControls`, `IceResurfacerGame.Update`: W/S combines direction and demand; no engine governor, neutral-start sequence, parking brake or selectable transmission ratio. | **Intentional simplification**; G/A; high / high | Add a separate drive-station pass with optional F/N/R, governor and pedal input while retaining shortcuts. |
| Power steering and relief at stops: M526 7-14/PDF73; M546 7-16/PDF76 | `Simulation.UpdateVehicle` turns toward keyboard demand and self-centers on release. No steering-stop hydraulic load/relief. | **Intentional simplification**; G/A; medium / high | Offer retained steering angle or analog wheel input; review relief feedback later. |
| Conditioner pull back lowers; forward raises: M546 7-6/PDF66, 10-2/PDF87 | `IcePhysicalControls.UpdateInput/Release/Animate`, `Simulation.Tick`: hold spool, neutral on release, timed lift. `IceConditionerLinkage` and cylinder posing already connect authored parts. | **Good approximation**; G/V; low / high | Preserve direction and geometry. Native interaction still needs Unity verification. |
| Fully lowered conditioner applies working down pressure/relief: M526 7-6/PDF65, 9-7/PDF86; M546 7-6/PDF66 | `ProcessIce` derives pressure from lift fraction; no hydraulic pressure, spring load, chassis squat or end-stop relief. `UpdateConditioner` keeps housing square to chassis. | **Intentional simplification**; G/V/A; medium / high | Add shared working-pressure/load state only in a later bounded pass. |
| Two independent conveyor handles pulled back: M546 7-6/PDF66 | `OperatorControls`, `IcePhysicalControls`, `ConveySnow`: independent H/V switches and finite stages. Source/visual rotors stop instantly, including jam. 526 optional reversing arrangement differs. | **Good approximation** of 546; G/V; low / high | Keep independence. Optional coast-down requires one rotor state shared by sound, visuals and throughput. |
| Counterclockwise blade wheel raises blade: M526 9-3,9-5/PDF82,84; M546 9-5/PDF82 | `IcePhysicalControls.ChangeBlade/TurnDelta`, `SimulatorConfig.EffectiveBladeDepth`, `Game.UpdateMachine`: correct sense, 3.5-turn calibrated range, nonlinear bite, blade translated from authored rest. No angle/leveling/wear state. | **Intentional simplification**; G/V; medium / high | Retain calibration. Manufacturer screw travel differs by model and is not actual shaving depth. |
| Independent manual wash and flood valves: M526 7-9/10; M546 7-10/11 | Physical drag, quarter-turn limits and keyboard inputs share `WashValve`/`FloodFlow`. Exact quadrant/handle clocking is not established by cited documents. | **Good approximation**; G/V; low / high for independent metering, **Insufficient evidence** for exact orientation | Keep authored valve positions and orientation. |
| Manual snow breaker clears intake-corner accumulation: M526 7-9/PDF68; M546 7-10/PDF70 | `StrokeSnowBreaker` transfers at most 4 L of existing H stock into available V space; visible stroke and audio exist. No adhered corner stock makes routine strokes necessary. | **Intentional simplification**; G/V; medium / high | Add finite corner buildup and mass-preserving release; automatic breaker is optional. |
| Horn, pump/brush switches and lights: M526 6-2,6-5/PDF53,56; M546 6-6/PDF60 | `IcePhysicalControls.Control` covers hydraulic levers, valves, blade and breaker. Pump/brush work through keys 5/4 and HUD, without physical switches; no horn input/state/audio found. | **Missing feature**; G/A; high / high | Add movable optional switch bindings and momentary horn in a station pass. Do not regenerate dashboard. |
| Seated operation and reach: M526 7-1/PDF60; M546 7-1/PDF61 | `Game.SelectCameraView/UpdateLook`, `IceChaseCamera`, `WaterService.TryDismount/TryBoard`: authored eye, quicklooks, standing/chase inspection, walk-around service and explicit boarding. Standing view can still drive. | **Good approximation** for access; G/V; medium / high source, low ergonomic proof | Preserve camera work. Treat standing/chase driving as sandbox assistance; obtain operator runtime judgment. |
| Optional brush extends, lowers, spins and sweeps before flood; its arm roller contacts boards with spring action: M526 7-10/PDF69; M546 7-11/12/PDF71/72 | `SurfaceProcessing.UpdateBoardBrush/BrushEdge` conservatively relocates existing snow toward the conditioner. Contact instantly projects the disc inward; `IceBoardBrushRig.Pose` retains commanded arm angle and changes link length. No spring deflection/velocity/return state. | **Good approximation** of sweeping; **Missing feature** for compliance; G/V; high / high | Keep authored pivot, reach and common visible/contact center. Add bounded dynamic compliance in the proposed brush pass. |
| Separate chassis guide wheel identified in M526 fig.5.1/PDF46 and M546 fig.5.1/PDF47; operator specifies primarily turn assistance | Authored scene guide X=-1.0009999 m, Z=2.444 m, radius .09 m. `Simulation.UpdateVehicle` models contact and travel; `Game.UpdateMachine` rotates it. It neither steers automatically nor requires continuous straight contact. | **Good approximation** of intended role; G/V; low / high source, unverified runtime corner feel | Preserve manual placement. Do not transfer brush-arm roller contact requirements to this front guide wheel. |

### Water, cutting and snow

| System / manufacturer behavior | Local implementation and assessment | Classification; impact; severity / confidence | Recommended action |
|---|---|---|---|
| Separate flood/wash tanks and fills: M546 xiv/PDF15, fig.5.2; S546 | `WaterService.ConnectHose/TickWaterService`, `IceWaterService`, `IceWaterFillPort`: separate ports, gauges, routed hose, independent finite stocks. 738/310 L matches the selected 546 arrangement. | **Accurate** capacities; **Good approximation** workflow; G/V; low / high | Preserve. Exact Aurora neck location is fictional ergonomics. |
| Filling/overflow | `TickWaterService`, `DepositRunoff`, `EnvironmentField.TickWater`: finite supply, capacity-limited storage, tracked overflow, floor flow/drains, loose nozzle and drive-away breakaway. 12 L/s and breakaway are fictional. | **Intentional simplification**; G/V; low / high | Keep accelerated service; do not describe breakaway as standard manufacturer equipment. |
| Wash valve first, at least ten seconds, then pickup; observe screen return: M526 7-9/PDF68; M546 7-10/PDF70 | `ProcessIce`: gravity supply, ten-second prime, shared finite pump budget, recirculation, liquid-only pickup and dry wear. No actual screen debris or impeller-temperature model. | **Good approximation**; G; medium / high | Preserve calibration; later add visible return/filter condition and service. |
| Horizontal conveyor supplies pump power: explicit M526 7-11/PDF70; M546 7-10 requires horizontal turning | **Before:** switch alone powered recovery and dry damage. **After:** `WashPumpRunning` requires switch + H on + no conveyor jam; `ProcessIce` and `IceMachineAudio.SetState` use it. | **Suspected defect — corrected**; G/A; high / high for 526, medium for universal 546 drivetrain | Selected 526-derived architecture for Aurora; switch latch is preserved. |
| External standing-water pickup with blade out and an initially empty wash tank: M526 7-11/PDF70; M546 7-12/PDF72 | **Before:** only supplied wash could build prime. **After:** moving forward, lowered, blade retracted, actual liquid present can prime over five seconds; finite removal fills wash tank. | **Suspected defect — corrected**; G; high / high | Five-second timing is explicitly 526-derived; 546 gives no such delay and calls for both augers. |
| Regulate flood at overlaps; close before lift: M526 7-10/PDF69; M546 7-11/PDF71 | `FindFootprint/FloodCellWeight/ProcessIce`: finite trailing towel footprint, fractional edge samples, narrow fringe, runoff and pooling when stopped. Manual valve remains open until operator closes it. | **Good approximation**; G/V; low / high | Keep .72 L/s trial. Automatic speed regulation is optional AWS, not a missing universal requirement. |
| Distribution towel, squeegee seal and water quality | `ProcessIce` uses ideal contact widths; `IceField` tracks wash treatment, liquid/frozen film, gouges and overwater. No towel saturation, dirty filter, torn squeegee, temperature, contaminants or heat balance. Manual squeegee inspection: 526 7-9; 546 7-10. | **Intentional simplification**; G/V; medium / high | Small filter/seal fault pass before any thermal solver. |
| Shave → collect → wash → flood sequence: M526 7-4/PDF63; M546 fig.7.3 | `ProcessIce`, `IceField.ApplyBladeCut`: actual grid removal produces expanded shavings; loose snow is removed separately; pickup ahead of new trailing flood prevents reclaiming its own fresh forward film. | **Good approximation**; G/V; low / high | Preserve spatial order and source ledgers. |
| Actual cutting and surface finish | `ApplyBladeCut/UpdateQuality`: cumulative cuts, residual gouges, reverse scoring, finite supplied film, overcut penalty. Underlying ice slab is effectively unlimited; no paint/slab depletion or real blade wear. | **Intentional simplification**; G/V; medium / high | Add blade-condition consequences before physical ice-slab/thermal complexity. Warnings about wear do not establish a wear simulation. |
| Conveyors remove shavings; full bins can plug: M526 7-6/PDF65; M546 7-6/PDF66 | `ConveySnow`: 12/10 L/s limits, 80/55 L retained stocks, full-bin rejection, congestion, six-second jam threshold. Numbers are not manufacturer measurements. | **Good approximation**; G/V; medium / high | Retain; calibrate throughput/retention with operator observations. |
| Augers forgotten or stopped | `ConveySnow`, `UpdateSnowExitPositions`, `DepositEscapedSnow`: H off holds trough, V off backs up throat, both side exits deposit real stock. No manufactured tank-fill bonus. | **Good approximation**; G/V; medium / high; exact onset **Insufficient evidence** | See measured delay below. A gradual, volume-conserving escape rule needs operator calibration. |
| Persistent snow recollection on ice | `IceField.DepositSnow`, `ProcessIce`: real snow cells lose only the amount conveyed; subsequent passes recover windrows. Authored 50 mm pickup already exceeds default 6 mm. | **Good approximation**; G/V; low / high | Do not reimplement or boost mass. Test rendering thresholds separately. |
| Snow presentation | `IceDepositedSnowView.Rebuild/Sample`, `IceSnowView.Update`: real field drives sparse mesh; 10 cm spacing/four-cell averaging; triangles below 1 mm omitted. Existing particle multipliers affect appearance only. | **Good approximation**, with visual underrepresentation risk; V; medium / high source, unverified runtime | Preserve field heights; consider finer threshold/normal/grain treatment backed by screenshots. |
| Compaction and tank filling | `SnowTankM3` stores loose-equivalent volume; `TankSnowPhysicalCapacityM3`/`IceSnowView` fit it to authored shell. S546 distinguishes 2.83 actual from 3.54 compacted m³. No dynamic density/compaction. | **Intentional simplification**; G/V; medium / high | Keep capacities. Any future melting requires a parallel water-equivalent/mass inventory. |
| Dump progressively; conveyors off, level position, no raised travel: M526 7-13/PDF72; M546 7-14/15/PDF74/75 | `UpdateTank/ComputeDumpTrajectory/DepositDump`: progressive gravity/momentum, pit misses, persistent floor deposits, 2.5% adhesion and shakeout; conveyor contents remain separate. Free mode permits misuse; training adds assistance. | **Good approximation**; G/V; low / high | Retain. Residual percentages are Aurora calibration, not manual data. Support stand/slope rollover are absent. |
| Snow left off ice | `EnvironmentField.Deposit` has no snow removal. `DepositDump` message implies off-pit deposits may be recollected, but only ice cells are collectible. | **Missing feature**; G; medium / high; message is a **Suspected defect** | Future manual cleanup transfers floor stock to pit. Do not reward scraping the floor with a lowered conditioner; manuals require raising before exit. |
| Frozen slush/foreign-object jams and washout: M526 7-6/7,7-12; M546 7-7/8,7-13 | Current jam arises from full-bin congestion; no frozen throat snow/foreign objects. `TryClearConveyorJam` resets anywhere after stopped/augers-off/blade-out/lowered nonfull tank checks; no ignition/key/service state. | **Intentional simplification**; G; high / high | Add a short coherent bay washout workflow. Where fitted/enabled, reversing does not clear snow jams; 526 explicitly labels reverse optional. |

### Operational nuances

The dedicated wash sections specify pump-off then valve-closed at completion. The 526 leaving-ice overview reverses that order. This documentary inconsistency does not justify a rigid universal interlock. Free simulation appropriately preserves operator commands and consequences; training restrictions should remain labeled assistance.

Towel behavior is currently an ideal distribution kernel, not a cloth-fluid interaction. Water can still flow while the conditioner is raised; the simulator does not automatically repair an operator's open-valve mistake. Screen return, dirty-water rejection, squeegee leaks and temperature effects remain substantial authenticity opportunities.

Pump wear grows immediately at `.007/s` while driven and unprimed. This is a gameplay wear proxy, not manufacturer damage timing; the 526 excess-water procedure warns about dry operation exceeding 30 seconds. The drive correction preserves that existing calibration.

## 4. Quantitative implications and the restrained snow overflow

### Flood and wash calibration

At .72 L/s, a nominal full 738 L tank lasts **1,025 seconds (17:05)** at full opening. Ten minutes uses nominally 432 L, leaving 306 L. At the procedural default 1.8 L/s, duration is 410 seconds (6:50). Both are intentional configurations; neither rate is claimed as a manufacturer specification.

Ignoring overlap and the narrow fringe, average film thickness is `flow / (width × speed)` in mm. For the 2.10 m distribution width, full authored flow gives about **.343 mm at 1 m/s**, **.129 mm at 2.65 m/s**. Its `.30 mm` good-film reference corresponds to approximately 1.14 m/s. Slower corners and repeated passes can pool; faster passes can receive too little water. These are implications, not a request to overwrite the trial flow.

The ten-minute full-wash/full-flood cut supplied **630.000 L** through wash, recirculated **398.958 L** directly and recovered **169.251 L** from the surface. Wash tank remaining was **248.188 L (80.061%)**. Thus `.644` is a direct-return parameter, **not the total retained fraction of the complete circuit**. Total return was about 90.19% of gross wash supply in this particular cycle. Repeated wet passes, blade setting, speed, available pickup water, pump damage and tank space alter that result. This explains why the operator's 80%-remaining experiment can coexist with a .644 direct-return setting.

### Snow production and spill visibility

At normal setting `.35`, actual bite is `.056 mm`. With 3.1 expansion, shaving one square metre creates only **.1736 L loose snow**, plus whatever loose snow was already there. The full-sheet apparatus began with **.297111 m³** loose snow and shaved **.083892 m³ solid**, producing **.260064 m³** expanded shavings. Its final **.557173 m³ tank**, 15.739% of capacity, follows those sources; filling faster would require a deeper cut, more initial rink snow or a justified density change.

Fresh-lane apparatus, 1 m/s, authored snow tuning:

| Blade setting / augers | First side escape | Settled total escape | Retention before escape |
|---|---:|---:|---:|
| .35 / both off | 110.599 s, 110.6 m | .7355 L/s | 80 L |
| .35 / H on, V off | 187.902 s, 187.9 m | .7355 L/s | 80 + 55 L |
| .80 / both off | 19.5 s, 19.5 m | 4.4318 L/s | 80 L |
| .80 / H on, V off | 32.9 s, 32.9 m | 4.4318 L/s | 80 + 55 L |

These are prescribed production-processing sweeps, not a vehicle route or universal timing. The full-sheet raster includes different cells/overlaps and began spilling at 149.4 s. Transient peaks from recollecting deposited material are much higher than the fresh-lane steady rate.

At .7355 L/s and 1 m/s, each side receives approximately .368 L per metre. A .16 m radius deposit kernel gives a roughly .32 m wide ridge: **about 1.15 mm mean depth**, approximately 1.95 mm ideal center depth before smoothing. That is close to the snow mesh's 1 mm triangle threshold. Particle density is already ×3; the short approximately .214 s fall leaves only about 43 simultaneous clumps per side at steady normal flow. This is a numerical explanation of subdued presentation, not fresh framebuffer proof.

`SpilledSnowM3`, `TotalSnowCollectedM3` and loose pickup are **transfer counters**, not independent sources. A repeated off-auger sweep registered .710 m³ cumulative spilling with only about .523 m³ total stock. Adding those transfers to current stock would incorrectly report created snow. The source is initial loose snow plus integrated actual shaving times expansion.

## 5. Ten highest-impact remaining realism gaps

The two corrected water defects were high priority; the list below ranks the remaining work after this pass.

1. **Independent engine/transmission/pedal operation.** Present shortcuts omit the distinction between engine RPM, hydraulic work and ground speed. High confidence; substantial operator impact.
2. **Physical station controls: pump, brush and horn.** Existing functions lack hands-on switches; horn is wholly absent. High confidence.
3. **Board-brush spring behavior.** Contact projects the disc inward instantly and changes visible link length. No bounded compression, damping or return exists. High confidence; affects corner feel and appearance.
4. **Meaningful jam/service recovery.** J resets congestion without off-ice shutdown/washout; no ignition lifecycle or screen servicing. High confidence.
5. **Snow-breaker purpose and throat buildup.** Normal flow almost empties buffers each tick, leaving the authentic repeated stroke habit with little mechanical benefit. High confidence.
6. **Normal-cut snow loading and overflow onset/presentation.** 80 L retention and shallow bite explain the long delay; actual retention and loading need operator judgment. High confidence in computation, medium in desired calibration.
7. **Wash return, filter and squeegee condition.** Ideal return fraction lacks debris, clogged filter, torn seal and obvious visual return feedback. High confidence.
8. **Blade setup, wear and hydraulic down pressure.** No tilt/leveling/feathering/pressure or wear consequences; current wheel range is a gameplay mapping. High confidence.
9. **Persistent off-ice cleanup.** Mis-dumped snow persists without a legitimate cleanup action; existing message overstates recollection. High confidence.
10. **Thermal/density and freezing behavior.** Exponential 420 s freezing, fixed expansion and visual compaction cannot reproduce hot/cold water, wet slush, melting or snow-density changes. High confidence; larger scope, lower immediate priority than controls.

## 6. Corrections, validation and preservation

### Implemented changes

**1. Pump-drive dependency.** `Core/Simulation.cs` exposes `WashPumpRunning` from switch, horizontal auger and jam state. `Core/SurfaceProcessing.cs` uses this actual drive state for pumping, wet-prime retention and dry damage. `Presentation/IceMachineAudio.cs` uses it for pickup sound. Switching off the drive leaves the pump switch latched; it does not silently change the operator's setting. A jam also removes drive. Exact pump drivetrain across every 546 serial revision remains unverified; this is the documented 526 mechanism chosen for Aurora, consistent with the 546 normal-wash prerequisites.

**2. Standing-water priming.** `SurfaceProcessing.ProcessIce` samples existing liquid before removal even with pump off when the lowered conditioner moves forward with the blade retracted. Five seconds of that wet contact establishes a separate pickup prime. Dry interruption decays it; raised, stopped, reversing, blade-down and fully frozen cases cannot establish it. Normal supplied-wash priming remains ten seconds. Existing pump throughput, residual liquid, tank-space limits and source-before-pickup order remain intact. `Simulation.Refill` resets both prime states.

These corrections add no material source, tank capacity, tuning control, authored child object or unrelated diagnostic UI.

Changed production files:

- [Core/Simulation.cs](../Assets/Experiments/IceResurfacer/Core/Simulation.cs)
- [Core/SurfaceProcessing.cs](../Assets/Experiments/IceResurfacer/Core/SurfaceProcessing.cs)
- [Presentation/IceMachineAudio.cs](../Assets/Experiments/IceResurfacer/Presentation/IceMachineAudio.cs)

Validation changes: new `Editor/OperatorWaterRealismValidation.cs` (and metadata); existing `Editor/OperatorMaterialValidation.cs`, `Editor/IceCalibrationRuntimeValidation.cs`, `Validation~/Program.cs`, `Validation~/IceCalibrationValidation.cs`, `Validation~/SurfaceConsistencyValidation.cs` explicitly enable horizontal drive in tests that require pumping. Assertions were retained. `Validation~/IceResurfacer.Validation.csproj` includes the new operating-sequence tests as the 42nd scenario group.

### Tests and measured results

All evidence below is fresh for this pass. Full logs, frozen source copies, scene configuration, new test runner and manual evidence are in [OperatorRealismAudit-20261104](../Assets/Experiments/IceResurfacer/Validation~/OperatorRealismAudit-20261104).

| Test / evidence | Result |
|---|---|
| New water scenarios, [before](../Assets/Experiments/IceResurfacer/Validation~/OperatorRealismAudit-20261104/water-before.log) / [after](../Assets/Experiments/IceResurfacer/Validation~/OperatorRealismAudit-20261104/water-after.log) | H off returned **4.305 L/5 s before, 0 after**; H on **4.305 L/5 s both**. Stopped/jammed drive returned 1.722 L/2 s before, 0 after. Switch state preserved. |
| Dry impeller consequences | Unpowered switch added .014 wear in 2 s before, **0 after**. Powered dry operation still added .014. |
| External-water operating sequence | Empty wash tank, supplied valves closed, blade out, forward wet pass: before **0 L recovered + .070 damage**; after **11.890001 L/10 s, no damage**, sheet/tank water residual **−.000048 L**. Positive fixture waits six seconds to clear the five-second threshold. |
| Negative priming / capacity cases | Stopped, blade down, frozen, interrupted, raised and reverse cases did not recover water and still incurred driven dry wear. Full tank removed **0 L**; water residual −.000001 L. |
| Aggregate suite | Baseline **41/41**; final **42/42** deterministic groups, including actual recorded-route fixtures, contacts, service, full cut/return/dump and new water sequences. [Final log](../Assets/Experiments/IceResurfacer/Validation~/OperatorRealismAudit-20261104/final-core.log). |
| Authored driven generated-reference cut | **1775.97 s**, **95.7323% coverage**, returned to service bay; flood **31.5727 L**, wash **301.2017 L**, snow tank **.718177 m³**. Snow residual **+.00005160 m³**, water **−.147170 L**. Same before/after. This slow automated route is a representative integration cycle, not an experienced operator's ten-minute shift. |
| Ten-minute authored full flood/wash cut | Flood used **431.987 L**, flood remaining **305.958 L**; wash remaining **248.188 L**; no pump damage. Snow residual **−.00000127 m³**, water **−.060522 L**. Same before/after. |
| Full-sheet normal cut, augers on | **.557173 m³** tank; no spill; all sampled surface loose snow removed. Snow residual **−.00000158 m³**; water **−.044790 L**. |
| Full-sheet normal cut, augers forgotten | Tank **0**, conditioner **.080000 m³**, ice snow **.468326 m³**; remaining snow also includes off-ice deposits. Snow residual **−.00000106 m³**. Peak local surface snow **62.06 mm**; cumulative spilling .931117 m³ includes repeat transfers. |
| Recollection | Finite 10 L ordinary windrow: **zero remaining in measured band**. Separate off-then-crossing-sweeps test recovered **406.176 L** into transport, residual **−.001198 L loose snow**. |
| High cutting load and real tank capacity | .80 setting/.600 mm bite full sheet collected **3.083510 m³ (87.105%)**. Extended 1300 m snow apparatus reached **3.540 m³**, rejected snow and jammed; residual **−.156889 L loose snow** over ~5,132 L total stock. |
| Tank dump | 3.083510 m³ emptied from tank; pit received **3.083372 m³**; residual **−.00013804 m³**. Separate trough/conveyor stock is not silently dumped. |
| Both service refills with overflow | Supply **779.999983 L**; tank gain **601.500000 L**; overflow **178.502913 L**; final tanks **738 / 310 L**. Supply meter residual **−.002931 L**, physical stock residual **+.002740 L**. |
| Brush/guide/corner coverage | Existing deterministic brush snow relocation, board clipping, roller glancing contact, authored-outline contacts and complete rounded-rink route passed. Dynamic spring behavior is absent; no fresh visual corner claim. |
| Offline Unity-reference compile | All Core/Presentation/Editor sources compiled against installed Unity 6000.4.1f1 assemblies: **0 errors, 45 warnings** (existing obsolete API/serialization-field warnings). [Compile log](../Assets/Experiments/IceResurfacer/Validation~/OperatorRealismAudit-20261104/working-unity-reference-compile.log). |
| Actual Unity run | Isolated Editor log: **licensing initialization failed**, LicenseClient connection refused/retries. No Play Mode or rendered verification reached. [Unity log](../Assets/Experiments/IceResurfacer/Validation~/OperatorRealismAudit-20261104/baseline-unity.log). |

[Full authored-cycle log](../Assets/Experiments/IceResurfacer/Validation~/OperatorRealismAudit-20261104/working-material.log) and [snow probe log](../Assets/Experiments/IceResurfacer/Validation~/OperatorRealismAudit-20261104/Snow/results.txt) distinguish prescribed apparatus poses from driven paths. Apparatus sweeps establish material behavior; they do not prove vehicle clearance or input usability.

Balance definitions:

- **Snow residual:** current ice snow + tank + H/V buffers + pit + environment snow − initial loose snow − integrated expanded shavings. Recovered/spilled throughput is not added again. Snow litres here mean loose-equivalent volume, not liquid-water litres or kilograms.
- **Water residual:** flood tank + wash tank + retained ice water (liquid **and frozen**) + environment water + drained water − initial water − external service supply. Wash recirculation is internal. Phase change does not delete water.
- The underlying shaved slab is a metered source, not a finite slab inventory. Tests establish conservation within that abstraction. Small residuals arise from float stores and accumulation; they are reported rather than called exact zero.

### Preservation and reproducibility

Development used separate Baseline/Working source copies and an isolated Unity project under the audit directory. Original source/asset hashes were captured before production edits. Promotion compares each destination to its starting hash and refuses an unexpected change. Authored prefab, scenes, mesh/material assets, arena, camera/tuning scripts and route/GPS data were not edited or regenerated. No branch, commit or push was made.

The suite initially wrote its existing hardcoded `Surface-consistency-results.txt` output into the main project. Only the known generated line was restored to its baseline; subsequent execution used the isolated project as working directory. No authored asset was involved.

Hash-checked promotion verified **388 other original production/test files unchanged**, including the captured authored assets and recordings. Nine existing source/test/project files and one new test were copied byte-identically from the validated Working copy. See [preservation.txt](../Assets/Experiments/IceResurfacer/Validation~/OperatorRealismAudit-20261104/preservation.txt) and [promotion-sha256.csv](../Assets/Experiments/IceResurfacer/Validation~/OperatorRealismAudit-20261104/promotion-sha256.csv) for exact checks. The original nine files also have verified backups in `BeforePromotion`.

Evidence is retained locally; ignored copied projects/build outputs are not a new project dependency. For a repeat isolated aggregate run, use the staged Unity project as the working directory (the harness writes some relative result files):

```powershell
# From the main project directory:
$audit = Join-Path (Get-Location) 'Assets/Experiments/IceResurfacer/Validation~/OperatorRealismAudit-20261104'
Push-Location "$audit/Validation~/UnityProject"
dotnet run --project "$audit/Working/Validation~/IceResurfacer.Validation.csproj" -c Release
Pop-Location
dotnet run --project "$audit/WaterRunner/WaterRunner.csproj" -c Release -p:SourceCopy=Working
dotnet run --project "$audit/Snow/Snow.csproj" -c Release
dotnet build "$audit/CompileUnity/CompileUnity.csproj" -c Release
```

## 7. Small future passes and operator questions

### Proposed implementation passes

| Pass | Concrete scope | Acceptance evidence |
|---|---|---|
| Physical station completion | Optional movable pump/brush switches using existing picking and authoritative booleans; shared momentary horn action/audio; release on focus loss, pause and dismount. Preserve authored controls and procedural fallback. | Mouse/keyboard parity, occlusion after moving controls, horn release, no prefab regeneration; Unity screenshots and interaction tests. |
| Brush compliance | Add bounded deflection and velocity along the current articulation, damped return and board penetration stop. One resulting center drives snow sweep, head, arm and link; retain authored rest/pivot/reach. | Straight and corner contact, release, deployment against board, no penetration, stable fixed-step response, conserved swept snow; operator approves stiffness/travel. |
| Meaningful short service loop | Add engine/service state; optional bay screen-empty and auger washout actions. Flush actual retained snow using metered water to tracked floor/drain/pit. Replace magical J reset with a coherent short recovery. | Jam survives premature reset, safe service clears it, all material accounted, normal cut adds seconds rather than a checklist. |
| Snow-breaker and overflow calibration | Finite adhered corner stock, stroke releases stock, gradual side shedding from available trough stock. Reuse existing deposited-snow chunks; improve shallow ridge visibility without height/mass inflation. | Before/after operator timing at normal/deep bites; H-off/V-off/full-bin cases; no new snow; render/performance comparison. |
| Wash condition | Debris/filter restriction and squeegee leakage affect finite return; visible return cue; short service restores condition. Keep experimental flow/recovery available. | Ten-minute normal cut, clogged/no-return case, extra liquid left on sheet, refill/clean recovery and bounded mass errors. |
| Drive/blade refinement | Optional F/N/R and governor independent of pedal, retained/analog steering; later blade sharpness/down-pressure state with audible end-stop relief. | Recorded ordinary shift with operator; no route/collision calibration changes; hydraulic and water/snow work respond plausibly to load. |
| Later density/thermal work | Parallel solid/water-equivalent inventory before snow melting, hot-water energy or dynamic compaction. | Mass and energy balances across freezing, shaving, mixing, melting and disposal. Larger separate project, not required for current corrections. |

Routine operator gameplay worth representing: checking visible return, emptying collected screen debris after a cut, flushing accumulated auger snow, noticing damaged squeegee/towel or worn blade, and cleaning a mis-dump. Blade changing/sharpening/leveling, hydraulic pressure adjustments, pump overhaul and engine diagnostics should remain qualified-maintenance actions or optional workshop content, not mandatory chores every resurfacing. Follow the selected machine's manual and local training for who performs each real-world task.

### Questions requiring actual operator judgment

1. For your ordinary shift, what actual shaved depth and initial hockey snow load feel right? Should the displayed `.35` remain a control setting despite only `.056 mm` effective bite?
2. With both augers forgotten, how many metres before the conditioner visibly sheds, and does snow first escape gradually or after a noticeable packed reservoir? The present 80 L threshold is measurable but undocumented.
3. At normal travel speed, is .72 L/s the right full-open upper range, or should your normal valve position use only part of its travel? Preserve the experiment until driven comparison.
4. Is 80% wash remaining still the target after a normal cut with this actual route, overlap and pickup behavior? The current ten-minute apparatus meets it; a slow repeated route returns much more water.
5. What brush compression travel, return speed and corner response match your experience? The front guide roller will retain its turn-oriented role and authored position.
6. How much mechanical snow-breaker benefit should an operator notice when neglecting it? Should ordinary snow accumulate at the intake, or only wet/heavy cuts?
7. Would a few seconds of meaningful screen/washout service be welcome, and which actions should remain optional sandbox shortcuts?
8. In licensed Unity, do the current eye position, control-view reach, wheel direction, hydraulic release and visible return provide the cues you need? Source inspection cannot settle these ergonomic questions.

The next mandatory verification step for the implemented code is a licensed Unity run of both authored and procedural scenes, checking pump sound/drive state and the standing-water procedure through real controls. No authored geometry adjustment is justified by this audit alone.
