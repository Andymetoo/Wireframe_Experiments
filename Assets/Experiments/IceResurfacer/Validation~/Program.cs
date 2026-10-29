using System;
using System.Collections.Generic;
using IceResurfacer.Core;

internal static class Program
{
    static int passed;
    static void Main()
    {
        Test("configuration rejects nonfinite physics values", Configuration);
        Test("fixed-step deterministic across render cadence", Determinism);
        Test("hydrostatic coast and service braking", Coasting);
        Test("reversal brakes through zero", Reversal);
        Test("rejected dump command cannot latch forward drive off on ice", RejectedDumpDriveRecovery);
        Test("repeated machinery stop/interact/resume at centre and boards", MachineryRecovery);
        Test("spring-centred hydraulics hold position and preserve tank interlocks", HydraulicHold);
        Test("visible contact inhibit recovers by reversing clear", ContactRecovery);
        Test("board collision contains machine", Boundaries);
        Test("conditioner follows inside turn", Tracking);
        Test("only actual trailing footprint is processed", Footprint);
        Test("77-inch working width matches cut and wet cell extents", PhysicalWidth);
        Test("turning cut and water remain inside the articulated footprint", TurnFootprint);
        Test("footprint conserves water and collected snow material", MaterialBalance);
        Test("conditioner lift removes blade contact", Lift);
        Test("separate augers, overflow, load and finite tank", Snow);
        Test("manual snow breaker conserves material and respects interlocks", SnowBreaker);
        Test("wash valve gravity feed and pickup priming", Wash);
        Test("stationary flood pooling and finite water", Flood);
        Test("left brush cleans board contact", Brush);
        Test("exit and tank interlocks prevent instant finish", Exit);
        Test("continuous controls and practice report", Controls);
        Test("measured 20-yard snow calibration and excessive bite", SnowCalibration);
        Test("full tank spills persist, conserve snow, jam and can be recovered", SpillRecovery);
        Test("dump transfers snow progressively with operating interlocks", ProgressiveDump);
        Test("guide roller preserves shallow tangential travel without penetration", GuideRoller);
        Test("close board water edge uses physical distribution width", PerimeterWater);
        Test("Free Simulation consequences, geometry, traction and guided separation", FreedomValidation.Run);
        Test("southeast hose freedom, breakaway and recovery", ServiceFreedomValidation.Run);
        Test("tire overlap, liquid recovery, perimeter mask and finite spread", IceCalibrationValidation.Run);
        Test("snow production, side escape and recoverable deposits", SnowCalibrationValidation.Run);
        Test("truthful minimap states and measured wet-but-uncut slivers", MinimapValidation.Run);
        Test("visible pit contact, reverse recovery and finite floor drainage", () => ServiceCollisionValidation.Run());
        Test("complete scored southeast route, return and angled unload", FreedomValidation.FullRun);
        Test("water service state, capacities, conservation and recovery", ServiceValidation.Run);
        Console.WriteLine("PASS: " + passed + " deterministic simulation scenarios.");
    }

    static void Test(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception e) { Console.Error.WriteLine("FAIL " + name + ": " + e.Message); Environment.Exit(1); }
    }
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static void Near(float a, float b, float tolerance, string message) { Assert(Math.Abs(a - b) <= tolerance, message + " (" + a + " vs " + b + ")"); }
    static void Tick(Simulation s, float seconds) { for (int i = 0; i < (int)Math.Round(seconds / .02f); i++) s.Step(.02f); }
    static void Place(Simulation s, float x, float z, float heading = 0)
    {
        if(Math.Abs(x)<.001f&&Math.Abs(z+38.5f)<.001f)
        { s.Layout.ToWorld(s.Layout.PitSide,s.Layout.PitOutward-3.85f,out x,out z);heading=s.Layout.OutwardHeading; }
        s.Vehicle.X = x; s.Vehicle.Z = z; s.Vehicle.Heading = heading; s.Vehicle.Speed = 0;
        s.Conditioner.Heading = heading;
        s.Conditioner.X = x - (float)Math.Sin(heading) * (s.Config.HitchBehindAxle + s.Config.DrawbarLength);
        s.Conditioner.Z = z - (float)Math.Cos(heading) * (s.Config.HitchBehindAxle + s.Config.DrawbarLength);
    }
    static void Prepare(Simulation s)
    {
        s.Controls.ConditionerTarget = 0; s.Conditioner.Lift = 0;
        s.Controls.BladeDepthMm = .35f; s.Controls.HorizontalAuger = true; s.Controls.VerticalAuger = true;
        s.Controls.FloodFlow = .62f;
    }
    static void Stow(Simulation s)
    {
        s.Controls.Throttle = 0; s.Controls.Brake = true; s.Controls.BladeDepthMm = 0;
        s.Controls.FloodFlow = 0; s.Controls.WashValve = 0; s.Controls.WashPump = false; s.Controls.BoardBrush = false;
        Tick(s, 2);
        s.Controls.HorizontalAuger = false; s.Controls.VerticalAuger = false; s.Controls.ConditionerTarget = 1;
        Tick(s, 3);
    }

    static void Configuration()
    {
        foreach (var field in typeof(SimulatorConfig).GetFields())
        {
            if (field.FieldType != typeof(float)) continue;
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var c = new SimulatorConfig(); field.SetValue(c, invalid);
                bool rejected = false; try { c.Validate(); } catch (ArgumentException) { rejected = true; }
                Assert(rejected, "nonfinite config accepted: " + field.Name);
            }
        }
        var geometry = new SimulatorConfig { CellSize = 0 };
        bool rejectedGeometry = false; try { geometry.Validate(); } catch (ArgumentException) { rejectedGeometry = true; }
        Assert(rejectedGeometry, "zero cell size accepted");
    }

    static void Determinism()
    {
        var a = new Simulation(); var b = new Simulation(); Place(a, 0, -15); Place(b, 0, -15);
        a.Controls.Throttle = b.Controls.Throttle = .5f;
        for (int i = 0; i < 500; i++) { a.Step(.02f); b.Step(.01f); b.Step(.01f); }
        Near(a.Vehicle.Z, b.Vehicle.Z, .000001f, "frame cadence affected position");
        Near(a.Time, b.Time, .000001f, "simulation clocks differ");
        for (int i = 0; i < a.Ice.Cells.Length; i++) Near(a.Ice.Cells[i].Scratch, b.Ice.Cells[i].Scratch, 0, "seed changed");
    }
    static void Coasting()
    {
        var a = new Simulation(); Place(a, 0, -15); a.Controls.Throttle = .7f; Tick(a, 5);
        float speed = a.Vehicle.Speed, z = a.Vehicle.Z; a.Controls.Throttle = 0; a.Step(.02f);
        Assert(a.Vehicle.Speed > speed * .95f && a.Vehicle.Z > z, "release stopped immediately");
        float before = a.Vehicle.Speed; Tick(a, .5f); float coastLoss = before - a.Vehicle.Speed;
        a.Vehicle.Speed = before; a.Controls.Brake = true; Tick(a, .5f);
        Assert(before - a.Vehicle.Speed > coastLoss * 1.5f, "service brake should stop faster");
        Tick(a, 4); Near(a.Vehicle.Speed, 0, .001f, "brake did not stop");
    }
    static void Reversal()
    {
        var s = new Simulation(); Place(s, 0, 0); s.Vehicle.Speed = 1.5f; s.Controls.Throttle = -1;
        s.Step(.02f); Assert(s.Vehicle.Speed > 1.4f, "reverse jumped instantly"); Tick(s, 3); Assert(s.Vehicle.Speed < -.1f, "reverse never engages");
    }
    static void RejectedDumpDriveRecovery()
    {
        var s = new Simulation(mode:SimulationMode.GuidedTraining);
        s.Controls.Throttle = -.65f; Tick(s, 20);
        Assert(s.HasEnteredRink, "fixture must drive from bay onto rink");
        s.Controls.Throttle = 0; s.Controls.Brake = true; Tick(s, 2);
        Assert(Math.Abs(s.Vehicle.Speed) < .001f, "fixture must stop");
        s.Controls.TankLiftTarget = .5f; Tick(s, .2f);
        Near(s.TankLift, 0, .00001f, "unsafe tank request must not lift on rink");
        s.Controls.Brake = false; s.Controls.Throttle = 1;
        float z = s.Vehicle.Z,x=s.Vehicle.X; Tick(s, 1);
        Console.WriteLine("  rejected dump recovery: throttle=" + s.Controls.Throttle + ", brake=" + s.Controls.Brake + ", tank=" + s.TankLift + ", target=" + s.Controls.TankLiftTarget + ", speed=" + s.Vehicle.Speed + ", message=" + s.LastMessage);
        float progress=(s.Vehicle.X-x)*(float)Math.Sin(s.Vehicle.Heading)+(s.Vehicle.Z-z)*(float)Math.Cos(s.Vehicle.Heading);
        Assert(progress > .1f && s.Vehicle.Speed > .2f, "rejected dump target immobilized a fully lowered machine");
    }
    static void MachineryRecovery()
    {
        Action<Simulation>[] interactions = {
            s => s.Controls.BladeDepthMm = .8f,
            s => s.SetConditionerHydraulic(-1),
            s => s.Controls.HorizontalAuger = true,
            s => s.Controls.VerticalAuger = true,
            s => s.Controls.BoardBrush = true,
            s => s.Controls.FloodFlow = .37f,
            s => s.Controls.WashValve = .43f,
            s => s.Controls.WashPump = true,
            s => s.StrokeSnowBreaker(),
            s => s.SetTankHydraulic(1),
            s => { s.SetConditionerHydraulic(-1); s.Controls.HorizontalAuger = s.Controls.VerticalAuger = true; s.Controls.BladeDepthMm = .8f; },
            s => { s.Controls.WashValve = .35f; s.Controls.WashPump = true; s.Controls.FloodFlow = .45f; s.Controls.BoardBrush = true; }
        };
        foreach (float x in new[] { 0f, -11.865f })
        {
            var s = new Simulation(); Place(s, x, -14);
            for (int i = 0; i < interactions.Length; i++)
            {
                s.Controls.Throttle = .42f; s.Controls.Brake = false; Tick(s, 1);
                s.Controls.Throttle = 0; s.Controls.Brake = true; Tick(s, 1);
                Near(s.Vehicle.Speed, 0, .001f, "stop before interaction " + i);
                interactions[i](s); Tick(s, .3f);
                s.SetConditionerHydraulic(0); s.SetTankHydraulic(0);
                s.Controls.Throttle = 1; s.Controls.Brake = false;
                float start = s.Vehicle.Z; Tick(s, 1);
                Assert(s.Vehicle.Z > start + .08f && s.Vehicle.Speed > .12f,
                    "machinery interaction " + i + " blocked travel at x=" + x + ": " + s.DriveInhibitReason);
                RequireContained(s);
                // Close water before the next stop; maintained auger/blade/load
                // states intentionally remain to exercise cumulative combinations.
                s.Controls.FloodFlow = s.Controls.WashValve = 0; s.Controls.WashPump = false;
            }
        }
        Console.WriteLine("  24 accumulated machinery interactions resumed through normal throttle at centre and 85 mm board-margin fixtures.");
    }
    static void HydraulicHold()
    {
        var s = new Simulation(mode:SimulationMode.GuidedTraining); Place(s, 0, 0);
        s.SetConditionerHydraulic(-1); Tick(s, .4f);
        Assert(s.Conditioner.Lift > .5f && s.Conditioner.Lift < .9f, "conditioner teleported or moved opposite command");
        float height = s.Conditioner.Lift; s.SetConditionerHydraulic(0); Tick(s, 1);
        Near(s.Conditioner.Lift, height, .000001f, "neutral conditioner drifts");
        s.SetConditionerHydraulic(1); Tick(s, 1); s.SetConditionerHydraulic(0);
        Near(s.Conditioner.Lift, 1, .000001f, "raise travel/end stop");
        s.Controls.HorizontalAuger = true; s.Controls.VerticalAuger = true; Tick(s, 1);
        Assert(s.Controls.HorizontalAuger && s.Controls.VerticalAuger, "maintained auger positions released themselves");
        s.Controls.HorizontalAuger = s.Controls.VerticalAuger = false;
        Place(s, 0, -38.5f, (float)Math.PI);
        s.SetTankHydraulic(1); Tick(s, .5f); s.SetTankHydraulic(0);
        float lift = s.TankLift; Tick(s, 1);
        Near(s.TankLift, lift, .000001f, "neutral dump cylinder drifts");
        s.Controls.Throttle = 1; Tick(s, 1);
        Near(s.Vehicle.Speed, 0, .000001f, "actual raised tank failed travel interlock");
        Assert(s.DriveInhibitReason.Contains("LOWER"), "raised tank fails actionable explanation");
        s.Controls.HorizontalAuger = s.Controls.VerticalAuger = true; Tick(s, .02f);
        Assert(!s.Controls.HorizontalAuger && !s.Controls.VerticalAuger, "augers operated with raised tank");
        s.Controls.Throttle = 0; s.SetTankHydraulic(-1); Tick(s, 1); s.SetTankHydraulic(0);
        Near(s.TankLift, 0, .000001f, "dump lower end stop");
        s.Controls.Throttle = -1; Tick(s, 1);
        Assert(s.Vehicle.Speed < -.2f && s.DriveInhibitReason.Length == 0, "lowered tank does not release drive: speed="+s.Vehicle.Speed+" pose="+s.Vehicle.X+","+s.Vehicle.Z+" reason="+s.DriveInhibitReason);
        s.Controls.Throttle = 0; s.Controls.Brake = true; Tick(s, 1);
        s.Controls.HorizontalAuger = true; s.SetTankHydraulic(1); Tick(s, 1);
        Near(s.TankLift, 0, .000001f, "tank lifted with operating auger");
        s.SetTankHydraulic(0);
        var edge = new Simulation(mode:SimulationMode.GuidedTraining); Place(edge, 0, -38.5f, (float)Math.PI);
        edge.Controls.Brake = true; edge.Controls.Throttle = 1; edge.SetTankHydraulic(1); Tick(edge, .1f);
        Near(edge.TankLift, 0, .000001f, "simultaneous W and dump raise must not start a lift");
        edge.Controls.Throttle = 0; edge.SetTankHydraulic(1); Tick(edge, .02f); edge.SetTankHydraulic(0);
        Assert(edge.TankRaised && edge.TankLift < .02f, "first physical cylinder movement must count as raised");
        edge.Controls.Brake = false; edge.Controls.Throttle = 1; Tick(edge, .02f);
        Near(edge.Vehicle.Speed, 0, .000001f, "early lift travel window allowed movement");
        edge.Controls.Throttle = 0; edge.SetTankHydraulic(-1); Tick(edge, .1f); edge.SetTankHydraulic(0);
        Assert(!edge.TankRaised, "tiny initial lift cannot be recovered by LOWER");
        edge.Controls.HorizontalAuger = true; edge.SetTankHydraulic(1); Tick(edge, .02f);
        Near(edge.TankLift, 0, .000001f, "running auger allowed first hydraulic tick");
        edge.Controls.HorizontalAuger = false; edge.SetTankHydraulic(0);
        Assert(edge.TryDismount(), "fixture cannot safely dismount at the pit");
        edge.SetTankHydraulic(1); Tick(edge, .1f);
        Near(edge.TankLift, 0, .000001f, "on-foot operator could raise dump tank");
        Assert(edge.TakeHose() && edge.ConnectHose(WaterFillPort.Flood, 6) && edge.TryBoard(), "hose safety fixture failed");
        edge.SetTankHydraulic(1); Tick(edge, .1f);
        Near(edge.TankLift, 0, .000001f, "connected hose allowed dump tank raising");
    }
    static void ContactRecovery()
    {
        var s = new Simulation(); Place(s, 0, 27.45f); s.Controls.Throttle = 1; Tick(s, 2);
        Assert(s.BoardContact && s.Vehicle.Speed == 0 && s.DriveInhibitReason.Contains("Reverse"), "direct contact did not explain recovery");
        float z = s.Vehicle.Z; s.Controls.Throttle = -1; Tick(s, 1);
        Assert(s.Vehicle.Z < z - .1f && s.Vehicle.Speed < -.2f, "board contact latched reverse");
        s.Controls.Throttle = 0; s.Controls.Brake = true; Tick(s, 1);
        Assert(s.DriveInhibitReason.Contains("CTRL"), "brake has no recovery hint");
        s.Controls.Brake = false; s.Controls.Throttle = 1; Tick(s, .3f);
        Assert(s.Vehicle.Speed > .05f, "braking or reversal latched drive");
    }

    static void Shakeout(Simulation s)
    {
        for (int i = 0; i < 8 && s.SnowTankM3 > .000001f; i++)
        {
            s.SetTankHydraulic(-1); Tick(s, .36f);
            s.SetTankHydraulic(1); Tick(s, .36f);
            s.SetTankHydraulic(0); Tick(s, .5f);
        }
        s.Controls.HydraulicHoldControls = false;
    }
    static void Boundaries()
    {
        var s = new Simulation(); Place(s, 0, 25); s.Controls.Throttle = 1; Tick(s, 10);
        Assert(s.Vehicle.Z <= 27.5f && s.Mistakes > 0, "machine passed end boards");
        Assert(!s.Navigable(0,-31)&&s.Navigable(s.Layout.GateX,s.Layout.GateZ),"southeast gate opening mismatch");
        Assert(!s.Ice.IsInside(12, 29), "rounded corner accepted outside cell");
    }
    static void Tracking()
    {
        var s = new Simulation(); Place(s, 0, -10); s.Controls.Throttle = .4f; s.Controls.Steer = 1; Tick(s, 5);
        Assert(s.Conditioner.Heading < s.Vehicle.Heading && s.Conditioner.Heading > 0, "conditioner did not trail steering");
        float dx = s.Vehicle.X - s.Conditioner.X, dz = s.Vehicle.Z - s.Conditioner.Z;
        Assert(dx * dx + dz * dz > .8f, "conditioner teleported to axle");
    }
    static void Footprint()
    {
        var s = new Simulation(); Place(s, 0, -10); Prepare(s); s.Controls.Throttle = .5f; Tick(s, 9);
        int behind = s.Ice.IndexAt(0, -4), missed = s.Ice.IndexAt(3, -4), ahead = s.Ice.IndexAt(0, s.Vehicle.Z + 2);
        Assert(s.Ice.Cells[behind].Covered && s.Ice.Cells[behind].Scratch < .25f, "trail did not resurface");
        Assert(!s.Ice.Cells[missed].Covered && s.Ice.Cells[missed].CutDepthMm == 0, "missed strip changed");
        Assert(s.Ice.Cells[ahead].CutDepthMm == 0, "processed ahead of conditioner");
        Assert(s.Ice.Wetness(behind, s.Time) > .3f, "fresh ice not wet");
        Assert(s.GetReport().Coverage > 0 && s.GetReport().Coverage < .1f, "coverage area wrong");
    }
    static void PhysicalWidth()
    {
        // A finite-area cell is either inside or outside the physical blade. The
        // rasterized span can differ by one cell, but cannot silently lose 25%.
        float smallest = float.MaxValue, largest = 0;
        foreach (float width in new[] { 1.65f, 1.956f, 2.13f })
        foreach (float offset in new[] { 0f, .05f, .10f, .15f })
        {
            var s = new Simulation(new SimulatorConfig { ConditionerWidth = width });
            Place(s, offset, -10); Prepare(s); s.Controls.Throttle = .5f; Tick(s, 9);
            int row = s.Ice.IndexAt(0, -4) / s.Ice.Columns;
            int cutColumns = 0, wetColumns = 0;
            for (int col = 0; col < s.Ice.Columns; col++)
            {
                var cell = s.Ice.Cells[s.Ice.Index(col, row)];
                bool cut = cell.CutDepthMm > .001f, wet = cell.FloodMm > .001f;
                if (cut) cutColumns++; if (wet) wetColumns++;
                if(cut)Assert(wet||width>s.Config.WaterDistributionWidth,"cut within water spread was left dry");
                if(wet)Assert(Math.Abs(s.Ice.CellX(col)-offset)<=s.Config.WaterDistributionWidth*.5f+s.Config.WaterFilmSpreadMeters+s.Ice.CellSize*.5f+.0001f,"water escaped finite distribution plus fractional edge cells");
                if (cut) Assert(Math.Abs(s.Ice.CellX(col) - offset) <= width * .5f + .0001f,
                    "processed a cell centre beyond the blade edge");
            }
            float span = cutColumns * s.Ice.CellSize;
            Near(span, width, s.Ice.CellSize + .001f, "physical width was not propagated to processing");
            Near(wetColumns*s.Ice.CellSize,s.Config.WaterDistributionWidth,s.Ice.CellSize*2+s.Config.WaterFilmSpreadMeters*2+.001f,"water spread escaped finite film plus two partially covered edge cells");
            if (width == 1.956f) { smallest = Math.Min(smallest, span); largest = Math.Max(largest, span); }
        }
        var reference = new SimulatorConfig();
        Near(reference.ConditionerWidth, 77f * .0254f, .001f, "default blade no longer matches 77-inch reference");
        Near(reference.VehicleHalfWidth * 2, 84f * .0254f, .004f, "vehicle width no longer matches 84-inch reference");
        Console.WriteLine("  physical blade=1.956m, water=2.100m; rasterized cut=" + smallest.ToString("F2") + "–" + largest.ToString("F2") + "m at " + reference.CellSize.ToString("F2") + "m cells");
    }

    static void TurnFootprint()
    {
        var s = new Simulation(); Place(s, 0, -10); Prepare(s);
        s.Controls.Throttle = .4f; s.Controls.Steer = 1;
        var previousCut = new float[s.Ice.Cells.Length];
        var previousFlood = new float[s.Ice.Cells.Length];
        int touched = 0;
        bool articulated = false;
        for (int tick = 0; tick < 360; tick++)
        {
            s.Step(.02f);
            float sin = (float)Math.Sin(s.Conditioner.Heading), cos = (float)Math.Cos(s.Conditioner.Heading);
            articulated |= Math.Abs(s.Conditioner.Heading - s.Vehicle.Heading) > .08f;
            for (int i = 0; i < s.Ice.Cells.Length; i++)
            {
                var cell = s.Ice.Cells[i];
                bool processed = cell.CutDepthMm > previousCut[i] + .000001f || cell.FloodMm > previousFlood[i] + .000001f;
                if (processed)
                {
                    float dx = s.Ice.CellX(i % s.Ice.Columns) - s.Conditioner.X;
                    float dz = s.Ice.CellZ(i / s.Ice.Columns) - s.Conditioner.Z;
                    bool newlyCut=cell.CutDepthMm>previousCut[i]+.000001f;
                    float bound=newlyCut?s.Config.ConditionerWidth:s.Config.WaterDistributionWidth+s.Config.WaterFilmSpreadMeters*2;
                    float cellMargin=newlyCut?0:s.Ice.CellSize*.7071068f;
                    Assert(Math.Abs(dx * cos - dz * sin) <= bound * .5f + cellMargin + .0001f,
                        "turning treatment escaped its actual lateral blade/water extent");
                    float fore=dx*sin+dz*cos;
                    Assert(newlyCut?Math.Abs(fore)<=s.Config.ConditionerLength*.5f+.0001f:
                        Math.Abs(fore-s.Config.FloodContactFore)<=s.Config.FloodContactLength*.5f+cellMargin+.0001f,
                        "turning treatment stamped at the vehicle instead of trailing conditioner");
                    touched++;
                }
                previousCut[i] = cell.CutDepthMm; previousFlood[i] = cell.FloodMm;
            }
        }
        Assert(touched > 500 && articulated, "test did not exercise a working articulated turn");
    }

    static void MaterialBalance()
    {
        // One wet footprint, before appreciable freezing: all dispensed litres
        // must be represented by cell area * depth, including the wash supply.
        var wet = new Simulation(); Place(wet, 0, -5);
        wet.Controls.FloodFlow = .32f; wet.Controls.WashValve = .25f; wet.Step(.02f);
        double liquidLiters = 0;
        foreach (var cell in wet.Ice.Cells) liquidLiters += cell.FloodMm * wet.Ice.CellSize * wet.Ice.CellSize;
        Near((float)liquidLiters, wet.TotalFloodUsedLiters + wet.TotalWashUsedLiters, .00001f,
            "dispensed water and spatial water volume differ");

        // A real working pass, with conveyors operating and no overflow. Snow
        // from initial loose material plus expanded shaved ice must all remain
        // in either the horizontal buffer, vertical buffer or tank.
        var s = new Simulation(); Place(s, 0, -10); Prepare(s); s.Controls.Throttle = .4f;
        double originalSnow = 0;
        foreach (var cell in s.Ice.Cells) originalSnow += cell.SnowMm;
        Tick(s, 12);
        double remainingSnow = 0, shavedDepth = 0;
        foreach (var cell in s.Ice.Cells) { remainingSnow += cell.SnowMm; shavedDepth += cell.CutDepthMm; }
        float produced = (float)((originalSnow - remainingSnow + shavedDepth * s.Config.SnowExpansionRatio) * s.Ice.CellSize * s.Ice.CellSize * .001);
        float stored = s.HorizontalSnowM3 + s.VerticalSnowM3 + s.SnowTankM3;
        Near(stored, produced, .00001f, "cut/loose snow volume differs from conveyor and tank material");
        Assert(s.EscapingSnowRate == 0 && s.SnowTankM3 > 0, "test did not establish ordinary no-spill conveyance");
    }

    static void Lift()
    {
        var s = new Simulation(); Place(s, 0, -10); s.Controls.BladeDepthMm = .9f; s.Controls.Throttle = .5f; Tick(s, 6);
        Assert(s.GetReport().Coverage == 0, "lifted conditioner processes ice");
        int i = s.Ice.IndexAt(0, -8); Near(s.Ice.Cells[i].CutDepthMm, 0, 0, "lifted blade cut");
        s.Controls.VerticalAuger = true; s.Controls.ConditionerTarget = 0; Tick(s, 3); s.Controls.ConditionerTarget = 1; int faults = s.Mistakes; Tick(s, .1f);
        Assert(s.Mistakes > faults, "raising with vertical auger did not register");
    }
    static void Snow()
    {
        var c = new SimulatorConfig { ConditionerSnowCapacityM3 = .003f, VerticalSnowCapacityM3 = .002f, SnowCapacityM3 = .006f };
        var s = new Simulation(c); Place(s, 0, -15); Prepare(s); s.Controls.Throttle = .5f;
        s.Controls.HorizontalAuger = false; s.Controls.VerticalAuger = false; Tick(s, 5);
        Assert(s.HorizontalSnowM3 > 0 && s.SnowTankM3 == 0 && s.Mistakes > 0, "augers-off failed to accumulate");
        float load = s.MachineLoad; s.Controls.HorizontalAuger = true; Tick(s, 1);
        Assert(s.VerticalSnowM3 > 0 && s.SnowTankM3 == 0, "horizontal auger bypassed vertical");
        s.Controls.VerticalAuger = true; Tick(s, 10);
        Near(s.SnowTankM3, c.SnowCapacityM3, .00001f, "capacity-limited tank wrong");
        Assert(load > .7f && s.SpilledSnowM3 > 0, "overload/spill not observable");
        Assert(s.SnowTankM3 <= c.SnowCapacityM3, "tank overflow stored beyond capacity");
    }
    static void SnowBreaker()
    {
        var s = new Simulation(mode:SimulationMode.GuidedTraining); Place(s, 0, -12); Prepare(s);
        s.Controls.Throttle = .4f; s.Controls.HorizontalAuger = false; Tick(s, 8);
        Assert(s.HorizontalSnowM3 > s.Config.SnowBreakerStrokeM3, "test did not accumulate throat material");
        float horizontal = s.HorizontalSnowM3, vertical = s.VerticalSnowM3;
        float total = horizontal + vertical + s.SnowTankM3;
        Assert(s.StrokeSnowBreaker(), "valid manual stroke rejected");
        Near(horizontal - s.HorizontalSnowM3, s.Config.SnowBreakerStrokeM3, .000001f, "manual stroke transfer differs from calibration");
        Near(s.VerticalSnowM3 - vertical, s.Config.SnowBreakerStrokeM3, .000001f, "stroke bypassed vertical conveyor");
        Near(s.HorizontalSnowM3 + s.VerticalSnowM3 + s.SnowTankM3, total, .000001f, "manual stroke created or destroyed snow");
        Assert(s.SnowBreakerStrokes == 1, "successful stroke not counted");
        s.Controls.VerticalAuger = false;
        Assert(!s.StrokeSnowBreaker() && s.SnowBreakerStrokes == 1, "stroke allowed without vertical conveyor");
        s.Controls.VerticalAuger = true; s.Conditioner.Lift = 1;
        Assert(!s.StrokeSnowBreaker() && s.SnowBreakerStrokes == 1, "stroke allowed with raised conditioner");

        var capacity = new Simulation(new SimulatorConfig { VerticalSnowCapacityM3 = .001f });
        Place(capacity, 0, -12); Prepare(capacity); capacity.Controls.HorizontalAuger = false;
        capacity.Controls.Throttle = .4f; Tick(capacity, 8);
        Assert(capacity.StrokeSnowBreaker(), "capacity-limited stroke rejected");
        Near(capacity.VerticalSnowM3, .001f, .000001f, "stroke exceeded available vertical capacity");
        float waiting = capacity.HorizontalSnowM3;
        Assert(capacity.StrokeSnowBreaker(), "safe no-transfer stroke should still move handle");
        Near(capacity.HorizontalSnowM3, waiting, 0, "full vertical buffer lost throat material");
    }

    static void Wash()
    {
        var s = new Simulation(); Place(s, 0, -5); s.Conditioner.Lift = 0; s.Controls.ConditionerTarget = 0; s.Controls.WashValve = .5f;
        float initial = s.WashWaterLiters; Tick(s, 5); Assert(s.WashWaterLiters < initial - 2, "gravity valve does not flow");
        s.Controls.WashPump = true; Tick(s, 1); Assert(s.PumpDamage > 0 && s.Mistakes > 0, "dry pump not penalized");
        s.Controls.WashPump = false; Tick(s, 5); Assert(s.WashPrimeSeconds >= 10, "prime timer failed");
        float before = s.WashWaterLiters; s.Controls.WashPump = true; Tick(s, 2);
        Assert(before - s.WashWaterLiters < .4f, "primed recovery pump not recirculating");
    }
    static void Flood()
    {
        var s = new Simulation(new SimulatorConfig { FloodCapacityLiters = 20 }); Place(s, 0, 0); s.Controls.FloodFlow = 1; Tick(s, 15);
        Near(s.FloodWaterLiters, 0, .0001f, "water tank not finite");
        int i = s.Ice.IndexAt(s.Conditioner.X,s.Conditioner.Z+s.Config.FloodContactFore); Assert(s.Ice.Cells[i].OverwaterMm > 1 && s.Mistakes > 0, "pooling at the actual trailing towel has no consequence");
        Assert(s.GetReport().OverwaterFraction > 0, "overwater scoring missing");
    }
    static void Brush()
    {
        var a = new Simulation(); var b = new Simulation(); Place(a, -11.7f, -10); Place(b, -11.7f, -10);
        a.Conditioner.Lift = b.Conditioner.Lift = 0; a.Controls.ConditionerTarget = b.Controls.ConditionerTarget = 0;
        a.Controls.Throttle = b.Controls.Throttle = .5f; a.Controls.BoardBrush = true; Tick(a, 7); Tick(b, 7);
        int i = a.Ice.IndexAt(-12.8f, -6);
        Assert(a.Ice.Cells[i].SnowMm < b.Ice.Cells[i].SnowMm, "left edge brush does not remove snow");
    }
    static void Exit()
    {
        var s = new Simulation(mode:SimulationMode.GuidedTraining); Assert(!s.TryFinish(), "training can finish at initial spawn");
        Place(s, 0, 0); s.Step(.02f); s.Controls.TankLiftTarget = 1; Tick(s, 1); Near(s.TankLift, 0, 0, "tank lifted on ice");
        s.Controls.TankLiftTarget = 0; Place(s, 0, -28, (float)Math.PI); s.Controls.Throttle = .3f; s.Controls.ConditionerTarget = 0; s.Conditioner.Lift = 0;
        Tick(s, 9); Assert(!s.ProperExit, "lowered threshold crossing accepted");
        Assert(!s.TryFinish(), "unsafe exit completed");
    }
    static void Controls()
    {
        var s = new Simulation(null, true); Place(s, 0, 0); s.Controls.ConditionerTarget = .4f; Tick(s, 2);
        Near(s.Conditioner.Lift, .4f, .00001f, "continuous lift rounded");
        s.Controls.FloodFlow = .37f; Tick(s, 1); Near(s.TotalFloodUsedLiters, .666f, .001f, "continuous flood not honored");
        Assert(s.GetReport().Practice && s.GetReport().Score == 0, "practice scored");
    }

    static float IceSnow(Simulation s)
    {
        double volume=0;
        foreach(var cell in s.Ice.Cells)volume+=cell.SnowMm*s.Ice.CellSize*s.Ice.CellSize*.001;
        return (float)volume;
    }
    static float ShavedSnow(Simulation s)
    {
        double volume=0;
        foreach(var cell in s.Ice.Cells)volume+=cell.CutDepthMm*s.Ice.CellSize*s.Ice.CellSize*.001*s.Config.SnowExpansionRatio;
        return (float)volume;
    }
    static Simulation MeasuredLane(float setting)
    {
        var s=new Simulation();Place(s,0,-15);Prepare(s);
        s.Controls.BladeDepthMm=setting;s.Controls.Throttle=.42f;s.Controls.FloodFlow=0;
        while(s.Vehicle.Z<-15+18.288f)s.Step(.02f);
        s.Controls.Throttle=0;s.Controls.Brake=true;s.Controls.BladeDepthMm=0;Tick(s,5);
        return s;
    }
    static void SnowCalibration()
    {
        float[] legacy={.047101f,.098188f};float[] settings={.35f,.8f};
        float[] after=new float[2];
        for(int i=0;i<2;i++)
        {
            var s=MeasuredLane(settings[i]);after[i]=s.SnowTankM3;
            Console.WriteLine("  20-yard lane setting="+settings[i]+"mm: before="+legacy[i].ToString("F6")+" after="+after[i].ToString("F6")+"m3 ratio="+(after[i]/legacy[i]).ToString("F3")+" fill="+(after[i]/s.Config.SnowCapacityM3).ToString("P3"));
            if(i==0)Assert(after[i]<legacy[i]*.34f&&after[i]>legacy[i]*.15f,"normal snow calibration changed");
            Near(s.Config.EffectiveBladeDepth(settings[i]),i==0?.056f:.60f,.000001f,"effective cutting calibration differs from the measured model");
            Near(s.Config.SnowCapacityM3,3.54f,0,"compacted capacity changed to hide fill");
            Near(s.SnowTankM3,ShavedSnow(s)+.0f+(IceSnow(new Simulation())-IceSnow(s)),.00001f,"calibration snow not sourced from actual cut and loose snow");
        }
        Assert(after[1]>after[0]*1.5f,"deep blade lost its extra material load");
        var normal=new Simulation();var deep=new Simulation();Place(normal,0,-15);Place(deep,0,-15);Prepare(normal);Prepare(deep);
        normal.Controls.Throttle=deep.Controls.Throttle=.42f;deep.Controls.BladeDepthMm=.8f;
        normal.Vehicle.Speed=deep.Vehicle.Speed=1.26f;normal.Step(.02f);deep.Step(.02f);
        Assert(deep.MachineLoad>normal.MachineLoad+.35f,"fully lowered blade lost excessive mechanical load");
    }
    static void SpillRecovery()
    {
        var s=new Simulation(new SimulatorConfig{SnowCapacityM3=.002f,ConditionerSnowCapacityM3=.001f,VerticalSnowCapacityM3=.001f});
        Place(s,0,-15);Prepare(s);s.Controls.BladeDepthMm=.8f;s.Controls.FloodFlow=0;s.Controls.Throttle=.42f;
        float initial=IceSnow(s);bool verticalSpray=false;
        for(int i=0;i<1100;i++){s.Step(.02f);verticalSpray|=s.VerticalSpillRate>0;}
        Assert(verticalSpray&&s.SpilledSnowM3>.002f&&s.ConveyorJammed,"full tank failed progressive spray/spill/jam");
        Near(IceSnow(s)+s.HorizontalSnowM3+s.VerticalSnowM3+s.SnowTankM3+s.OffIceSnowM3,initial+ShavedSnow(s),.00002f,"overflow created or destroyed snow");
        Assert(!s.TryClearConveyorJam()&&!s.StrokeSnowBreaker(),"running jam can be cleared unsafely");
        int snowiest=0;
        for(int i=0;i<s.Ice.Cells.Length;i++)if(s.Ice.Cells[i].SnowMm>s.Ice.Cells[snowiest].SnowMm)snowiest=i;
        float x=s.Ice.CellX(snowiest%s.Ice.Columns),z=s.Ice.CellZ(snowiest/s.Ice.Columns);
        Assert(s.Ice.Cells[snowiest].SnowMm>1,"spill not persistent spatial snow");
        Stow(s);Place(s,0,-38.5f,(float)Math.PI);
        s.Controls.TankLiftTarget=1;Tick(s,10);s.Controls.TankLiftTarget=0;Tick(s,6);
        Assert(s.TryClearConveyorJam(),"stopped inspection/reset after dumping rejected");
        float before=IceSnow(s);Place(s,x,z+1.1f);Prepare(s);s.Controls.BladeDepthMm=0;s.Controls.FloodFlow=0;
        s.Controls.Brake=false;s.Controls.Throttle=.3f;Tick(s,1);
        Assert(IceSnow(s)<before&&s.SnowTankM3>0,"dumped machine cannot recollect its spill");
        Near(IceSnow(s)+s.HorizontalSnowM3+s.VerticalSnowM3+s.SnowTankM3+s.DumpedSnowM3+s.OffIceSnowM3,initial+ShavedSnow(s),.00003f,"spill/dump/recollection material balance failed");
    }
    static void ProgressiveDump()
    {
        var s=MeasuredLane(.8f);float tank=s.SnowTankM3;
        Stow(s);Place(s,0,-38.5f,(float)Math.PI);s.Controls.TankLiftTarget=1;
        Tick(s,3);Near(s.SnowTankM3,tank,.000001f,"snow disappeared before tank opened");
        float before=0,after=0;bool flow=false;
        for(int i=0;i<350;i++)
        {
            before=s.SnowTankM3;s.Step(.02f);after=s.SnowTankM3;
            Assert(after<=before&&before-after<=s.Config.DumpM3PerSecond*.02f+.000001f,"dump teleported contents");
            if(before>after){flow=true;Assert(s.TankLift>.8f&&s.DumpSnowRate>0,"dump volume lost without visible transfer rate");}
            Near(after+s.DumpedSnowM3,tank,.000001f,"dump transfer broke conservation");
        }
        Assert(flow&&after>0&&s.ResidualSnowM3>0,"initial dump must leave physically adhered residual snow");
        float stuck=after; Tick(s, 2); Near(s.SnowTankM3,stuck,.000001f,"stationary residue vanished");
        Shakeout(s);
        Assert(s.TankShakeoutCount>0&&s.SnowTankM3==0,"controlled hydraulic shakeout did not clear residual snow");
        Near(s.DumpedSnowM3,tank,.000001f,"shakeout created or destroyed snow");
        s.Controls.TankLiftTarget=0;Tick(s,6);Near(s.TankLift,0,0,"tank failed to stow");
    }
    static void RequireContained(Simulation s)
    {
        float sn=(float)Math.Sin(s.Vehicle.Heading),cs=(float)Math.Cos(s.Vehicle.Heading);
        for(int row=0;row<3;row++)for(int col=0;col<3;col++)
        {
            float x=(col-1)*s.Config.VehicleHalfWidth,z=-s.Config.BodyRear+(s.Config.BodyFront+s.Config.BodyRear)*row*.5f;
            Assert(s.Navigable(s.Vehicle.X+cs*x+sn*z,s.Vehicle.Z-sn*x+cs*z),"accepted penetrating body pose");
        }
        float rx=s.Vehicle.X+cs*s.Config.GuideRollerX+sn*s.Config.GuideRollerZ,rz=s.Vehicle.Z-sn*s.Config.GuideRollerX+cs*s.Config.GuideRollerZ;
        for(int i=0;i<8;i++)Assert(s.Navigable(rx+(float)Math.Cos(i*Math.PI/4)*s.Config.GuideRollerRadius,rz+(float)Math.Sin(i*Math.PI/4)*s.Config.GuideRollerRadius),"guide penetrated boards");
        s.Ice.BoardDistance(rx,rz,out float nx,out float nz);
        Assert(s.Navigable(rx-nx*s.Config.GuideRollerRadius,rz-nz*s.Config.GuideRollerRadius),"guide circle penetrated between sampled points");
    }
    static void GuideRoller()
    {
        var s=new Simulation();Place(s,-11.6f,-12,-.08f);s.Controls.Throttle=.42f;s.Vehicle.Speed=1;
        float start=s.Vehicle.Z;bool contact=false;
        for(int i=0;i<400;i++){s.Step(.02f);RequireContained(s);contact|=s.GuideRollerContact;}
        Assert(contact&&s.GuideRollerTravel>0&&s.Vehicle.Z-start>6&&s.Vehicle.Speed>.5f,"shallow guide contact stopped/stuck vehicle");
        Console.WriteLine("  glancing board pass: travel="+(s.Vehicle.Z-start).ToString("F2")+"m retained speed="+s.Vehicle.Speed.ToString("F2")+"m/s roller="+s.GuideRollerTravel.ToString("F2")+"m");
        var end=new Simulation();Place(end,-4,28.65f,(float)Math.PI*.5f-.08f);end.Controls.Throttle=.42f;end.Vehicle.Speed=1;
        for(int i=0;i<150;i++){end.Step(.02f);RequireContained(end);}
        Assert(end.Vehicle.X>-2,"end-board tangent did not carry machine");
        var corner=new Simulation();float offset=6.97f/(float)Math.Sqrt(2);
        Place(corner,-4.5f-offset,21.5f+offset,(float)Math.PI*.25f);corner.Vehicle.Speed=1;corner.Controls.Throttle=.42f;
        float startX=corner.Vehicle.X;
        for(int i=0;i<150;i++){corner.Step(.02f);RequireContained(corner);}
        Assert(corner.Vehicle.X>startX+2,"rounded-board shallow contact stuck machine");
        var impact=new Simulation();Place(impact,0,26.8f);impact.Vehicle.Speed=2;impact.Controls.Throttle=1;Tick(impact,1);
        Assert(impact.Vehicle.Speed<.2f&&impact.Mistakes>0,"direct impact lost substantial deceleration");
    }
    static void PerimeterWater()
    {
        var s=new Simulation();Place(s,-11.865f,-12);Prepare(s);s.Controls.Throttle=.42f;
        for(int i=0;i<350;i++){s.Step(.02f);RequireContained(s);}
        float physicalMargin=s.Vehicle.X+s.Config.RinkWidth*.5f-s.Config.WaterDistributionWidth*.5f;
        Assert(physicalMargin>=.05f&&physicalMargin<=.102f,"physical board pass margin outside 2-4 inch target");
        float edge=float.MaxValue;
        for(int col=0;col<s.Ice.Columns;col++)
        {
            var c=s.Ice.Cells[s.Ice.Index(col,s.Ice.IndexAt(0,-8)/s.Ice.Columns)];
            if(c.FloodMm>.01f)edge=Math.Min(edge,s.Ice.CellX(col)-s.Ice.CellSize*.5f+s.Config.RinkWidth*.5f);
        }
        Assert(edge>=0&&edge<=.101f,"spatial wet edge has a large dry strip or crosses boards");
        foreach(var cell in s.Ice.Cells)if(!cell.IsIce)Near(cell.FloodMm,0,0,"water went through rounded boards");
        Console.WriteLine("  close-board physical margin="+(physicalMargin*1000).ToString("F0")+"mm; rasterized wet edge="+(edge*1000).ToString("F0")+"mm");
    }

    readonly struct Point
    {
        public readonly float X, Z;
        public Point(double x, double z) { X = (float)x; Z = (float)z; }
    }
    sealed class Route
    {
        public readonly List<Point> Points = new List<Point>();
        public double X, Z = -34, Heading;
        public Route() { Points.Add(new Point(X, Z)); }
        public void Line(double length)
        {
            int n = (int)Math.Ceiling(Math.Abs(length) / .12);
            for (int i = 0; i < n; i++) { X += Math.Sin(Heading) * length / n; Z += Math.Cos(Heading) * length / n; Points.Add(new Point(X, Z)); }
        }
        public void Turn(double angle, double radius)
        {
            int n = (int)Math.Ceiling(Math.Abs(angle * radius) / .12);
            double da = angle / n, sign = Math.Sign(angle);
            for (int i = 0; i < n; i++)
            {
                X += radius / sign * (Math.Cos(Heading) - Math.Cos(Heading + da));
                Z += radius / sign * (Math.Sin(Heading + da) - Math.Sin(Heading));
                Heading += da; Points.Add(new Point(X, Z));
            }
        }
        public void Shift(double dx)
        {
            double a = Math.Acos(1 - Math.Abs(dx) / 10);
            Turn(Math.Sign(dx) * a, 5); Turn(-Math.Sign(dx) * a, 5);
        }
        public void Box(double halfWidth, double halfLength, double radius)
        {
            Line(halfLength - radius - Z); Turn(Math.PI / 2, radius);
            Line(2 * (halfWidth - radius)); Turn(Math.PI / 2, radius);
            Line(2 * (halfLength - radius)); Turn(Math.PI / 2, radius);
            Line(2 * (halfWidth - radius)); Turn(Math.PI / 2, radius);
            Line(-12 - Z);
        }
    }
    static float Distance(Point p, VehicleState v)
    {
        float x = p.X - v.X, z = p.Z - v.Z; return (float)Math.Sqrt(x * x + z * z);
    }
    static void FullRun()
    {
        // Every position in this run is integrated from throttle/steer, starting in the saved-scene bay.
        // No pose fixtures, cell writes, replenishment, geometry changes, or enlarged capacities.
        var s = new Simulation();
        var route = new Route();
        route.Line(19); route.Turn(-Math.PI / 2, 4.5); route.Turn(Math.PI / 2, 7.02);
        int workStart = route.Points.Count;
        for (int ring = 0; ring < 5; ring++)
        {
            double inset = 1.43 + ring * 1.6;
            if (ring > 0) route.Shift(1.6);
            route.Box(13 - inset, 30 - inset, Math.Max(4.6, 8.5 - inset));
                        if (ring == 0)
            {
                route.Line(8.52);
                // A slight outward bow on long straights follows boards closely while retaining nose clearance in turns.
                for (int i = workStart + 25; i < route.Points.Count; i++)
                {
                    var p = route.Points[i]; double x = p.X, z = p.Z;
                    if (Math.Abs(x) > 11.5) x += Math.Sign(x) * .08 * Math.Max(0, Math.Min(1, (21.5 - Math.Abs(z)) / 2));
                    if (Math.Abs(z) > 28.5) z += Math.Sign(z) * .08 * Math.Max(0, Math.Min(1, (4.5 - Math.Abs(x)) / 2));
                    route.Points[i] = new Point(x, z);
                }
            }
        }
        for (int lane = 0; lane < 9; lane++)
        {
            double left = -10.95 + lane * 1.625;
            route.Shift(left - route.X);
            route.Box(4.6, 20.6, 4.6);
        }
        int workEnd = route.Points.Count;
        route.Turn(Math.PI, 4.6); route.Line(4); route.Turn(Math.PI / 2, 4.6);
        route.Line(route.X - 4.6); route.Turn(-Math.PI / 2, 4.6); route.Line(14);
        int waypoint = 1, stoppedFrames = 0;
        for (int tick = 0; tick < 120000 && waypoint < route.Points.Count - 1; tick++)
        {
            while (waypoint < route.Points.Count - 1 && Distance(route.Points[waypoint], s.Vehicle) < 1.25f) waypoint++;
            var target = route.Points[waypoint];
            float distance = Distance(target, s.Vehicle);
            float desired = (float)Math.Atan2(target.X - s.Vehicle.X, target.Z - s.Vehicle.Z);
            float delta = desired - s.Vehicle.Heading;
            float steer = (float)Math.Atan2(2 * s.Config.Wheelbase * Math.Sin(delta), Math.Max(.1f, distance));
            s.Controls.Steer = Simulation.Clamp(steer / s.Config.MaxSteeringRadians, -1, 1);
            s.Controls.Throttle = .30f;
            bool working = waypoint > workStart && waypoint < workEnd;
            s.Controls.ConditionerTarget = working ? 0 : 1;
            if (working)
            {
                s.Controls.HorizontalAuger = s.Controls.VerticalAuger = true;
                s.Controls.BladeDepthMm = s.Conditioner.Lift < .1f ? .3f : 0;
                s.Controls.FloodFlow = s.Conditioner.Lift < .1f ? .23f : 0;
                s.Controls.BoardBrush = true;
            }
            else
            {
                s.Controls.BladeDepthMm = 0; s.Controls.FloodFlow = 0; s.Controls.BoardBrush = false;
                // Clear conveyors before raising; turning toward the exit takes much longer than the purge.
                bool snow = s.HorizontalSnowM3 + s.VerticalSnowM3 > .00001f;
                s.Controls.HorizontalAuger = s.Controls.VerticalAuger = snow;
                if (snow) s.Controls.ConditionerTarget = 0;
            }
            s.Step(.02f);
            stoppedFrames = tick > 100 && Math.Abs(s.Vehicle.Speed) < .001f ? stoppedFrames + 1 : 0;
            Assert(stoppedFrames < 100, "route blocked at waypoint " + waypoint + "/" + route.Points.Count + " pose=" + s.Vehicle.X.ToString("F2") + "," + s.Vehicle.Z.ToString("F2") + " heading=" + s.Vehicle.Heading.ToString("F2"));
            if (tick == 119999) throw new Exception("route did not reach bay: waypoint " + waypoint);
        }
        Stow(s);
        Assert(s.ProperExit, "correct raised threshold exit rejected: " + s.Vehicle.X + "," + s.Vehicle.Z + " " + s.LastMessage);
        s.Controls.TankLiftTarget = 1; Tick(s, 20); Shakeout(s); s.Controls.TankLiftTarget = 0; Tick(s, 6);
        Assert(s.SnowTankM3 < .005f && s.TankDumps > 0, "dump did not empty tank");
        Assert(s.TryFinish(), "run did not finish: " + s.LastMessage);
        var report = s.GetReport();
        Console.WriteLine("  driven full run: coverage=" + report.Coverage.ToString("P2") + ", quality=" + report.FinishQuality.ToString("P1") + ", score=" + report.Score.ToString("F1") + ", water=" + s.TotalFloodUsedLiters.ToString("F1") + "L, snow=" + s.DumpedSnowM3.ToString("F2") + "m3, time=" + s.Time.ToString("F0") + "s, mistakes=" + s.Mistakes);
        Assert(report.Coverage >= .95f, "full sheet coverage below 95%");
        Assert(report.Complete, "complete run not marked complete");
    }
}







