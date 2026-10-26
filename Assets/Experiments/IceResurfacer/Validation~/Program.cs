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
        Test("complete route, return, unload and finish", FullRun);
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
    static void Boundaries()
    {
        var s = new Simulation(); Place(s, 0, 25); s.Controls.Throttle = 1; Tick(s, 10);
        Assert(s.Vehicle.Z <= 27.5f && s.Mistakes > 0, "machine passed end boards");
        Assert(!s.Navigable(8, -31) && s.Navigable(0, -31), "gate opening mismatch");
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
                Assert(cut == wet, "cut/water edges disagree at width " + width);
                if (cut) Assert(Math.Abs(s.Ice.CellX(col) - offset) <= width * .5f + .0001f,
                    "processed a cell centre beyond the blade edge");
            }
            float span = cutColumns * s.Ice.CellSize;
            Near(span, width, s.Ice.CellSize + .001f, "physical width was not propagated to processing");
            Assert(cutColumns == wetColumns, "wet strip width differs from cutting width");
            if (width == 1.956f) { smallest = Math.Min(smallest, span); largest = Math.Max(largest, span); }
        }
        var reference = new SimulatorConfig();
        Near(reference.ConditionerWidth, 77f * .0254f, .001f, "default blade no longer matches 77-inch reference");
        Near(reference.VehicleHalfWidth * 2, 84f * .0254f, .004f, "vehicle width no longer matches 84-inch reference");
        Console.WriteLine("  physical blade=1.956m, rasterized straight cut/wet span=" + smallest.ToString("F2") + "–" + largest.ToString("F2") + "m at 0.20m cells");
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
                    Assert(Math.Abs(dx * cos - dz * sin) <= s.Config.ConditionerWidth * .5f + .0001f,
                        "turning treatment escaped the actual lateral blade extent");
                    Assert(Math.Abs(dx * sin + dz * cos) <= s.Config.ConditionerLength * .5f + .0001f,
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
        s.Controls.VerticalAuger = true; Tick(s, 3);
        Near(s.SnowTankM3, c.SnowCapacityM3, .00001f, "capacity-limited tank wrong");
        Assert(load > .7f && s.EscapingSnowRate > 0, "overload/spill not observable");
        Assert(s.SnowTankM3 <= c.SnowCapacityM3, "tank overflow stored beyond capacity");
    }
    static void SnowBreaker()
    {
        var s = new Simulation(); Place(s, 0, -12); Prepare(s);
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
        int i = s.Ice.IndexAt(0, -1.1f); Assert(s.Ice.Cells[i].OverwaterMm > 1 && s.Mistakes > 0, "pooling has no consequence");
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
        var s = new Simulation(); Assert(!s.TryFinish(), "can finish at initial spawn");
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
            double inset = 1.48 + ring * 1.6;
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
                s.Controls.FloodFlow = s.Conditioner.Lift < .1f ? .24f : 0;
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
        s.Controls.TankLiftTarget = 1; Tick(s, 20); s.Controls.TankLiftTarget = 0; Tick(s, 6);
        Assert(s.SnowTankM3 < .005f && s.TankDumps > 0, "dump did not empty tank");
        Assert(s.TryFinish(), "run did not finish: " + s.LastMessage);
        var report = s.GetReport();
        Console.WriteLine("  driven full run: coverage=" + report.Coverage.ToString("P2") + ", quality=" + report.FinishQuality.ToString("P1") + ", score=" + report.Score.ToString("F1") + ", water=" + s.TotalFloodUsedLiters.ToString("F1") + "L, snow=" + s.DumpedSnowM3.ToString("F2") + "m3, time=" + s.Time.ToString("F0") + "s, mistakes=" + s.Mistakes);
        Assert(report.Coverage >= .95f, "full sheet coverage below 95%");
        Assert(report.Complete, "complete run not marked complete");
    }
}







