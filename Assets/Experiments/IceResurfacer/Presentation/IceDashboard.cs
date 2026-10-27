using System;
using System.Collections.Generic;
using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    /// <summary>Immediate-mode operator instruments. Only writes operator commands; never edits ice or scoring.</summary>
    public sealed class IceDashboard : IDisposable
    {
        public const float LayoutWidth = 1600f, LayoutHeight = 900f;
        public bool AuditLayout { get; set; }
        public readonly List<string> LayoutIssues = new List<string>();
        static readonly Color Navy = new Color(.035f, .068f, .09f, .96f);
        static readonly Color Modal = new Color(.035f, .068f, .09f, 1);
        static readonly Color Inset = new Color(.025f, .044f, .058f, 1);
        static readonly Color Slate = new Color(.13f, .20f, .24f, 1);
        static readonly Color Muted = new Color(.72f, .80f, .83f, 1);
        static readonly Color White = new Color(.91f, .95f, .95f, 1);
        static readonly Color Mint = new Color(.44f, .90f, .76f, 1);
        static readonly Color Amber = new Color(1f, .70f, .32f, 1);
        static readonly Color Red = new Color(1f, .39f, .31f, 1);
        static readonly float[] Separators = { 286, 528, 752, 1023, 1207 };
        GUIStyle text, small, heading, title, number, button;
        Texture2D disc, ring;
        float dialLastAngle;
        string hoverHint;

        void Initialize()
        {
            if (text != null) return;
            text = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true, richText = false, padding = new RectOffset(0, 0, 0, 0) };
            text.normal.textColor = White;
            small = new GUIStyle(text) { fontSize = 16 };
            heading = new GUIStyle(text) { fontSize = 17, fontStyle = FontStyle.Bold };
            title = new GUIStyle(text) { fontSize = 31, fontStyle = FontStyle.Bold };
            number = new GUIStyle(text) { fontSize = 34, fontStyle = FontStyle.Bold };
            button = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter, fontSize = 17, fontStyle = FontStyle.Bold, wordWrap = false, padding = new RectOffset(8, 8, 3, 3) };
            button.normal.background = null;
            button.hover.background = null;
            button.active.background = null;
            button.focused.background = null;
            button.hover.textColor = White;
            button.active.textColor = White;
            disc = Circle(false); ring = Circle(true);
        }

        static Texture2D Circle(bool hollow)
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = hollow ? "IceResurfacer / instrument ring" : "IceResurfacer / instrument disc", hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(48, 48));
                float alpha = Mathf.Clamp01(47 - distance);
                if (hollow) alpha *= Mathf.Clamp01(distance - 38);
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }
            texture.SetPixels(pixels); texture.Apply(false, true); return texture;
        }

        public static Vector2 CanvasToViewport(Vector2 point, float width, float height)
        {
            float scale = Mathf.Min(width / LayoutWidth, height / LayoutHeight);
            return new Vector2((width - LayoutWidth * scale) * .5f, (height - LayoutHeight * scale) * .5f) + point * scale;
        }

        /// <summary>Actual clickable bounds in the reference canvas, also used by explicit validation.</summary>
        public static Rect ControlRect(string name)
        {
            switch (name)
            {
                case "Conditioner": return new Rect(120, 721, 46, 116);
                case "HorizontalAuger": return new Rect(304, 721, 208, 38);
                case "VerticalAuger": return new Rect(304, 766, 208, 38);
                case "BoardBrush": return new Rect(304, 811, 208, 38);
                case "Blade": return new Rect(592, 725, 98, 98);
                case "WashValve": return new Rect(770, 726, 94, 94);
                case "WashPump": return new Rect(877, 734, 128, 64);
                case "Flood": return new Rect(1041, 721, 46, 116);
                case "TankLift": return new Rect(1217, 721, 46, 116);
                case "Service": return new Rect(1323, 721, 157, 38);
                case "Finish": return new Rect(1323, 766, 157, 38);
                case "NewSession": return new Rect(1323, 811, 157, 38);
                case "SnowBreaker": return new Rect(880, 637, 250, 34);
                case "Guide": return new Rect(1184, 637, 140, 34);
                case "Pause": return new Rect(1338, 637, 142, 34);
                default: throw new ArgumentException("Unknown Aurora instrument: " + name);
            }
        }

        public void Draw(IceResurfacerGame game) { Draw(game, Screen.width, Screen.height); }

        public void Draw(IceResurfacerGame game, float width, float height)
        {
            Initialize();
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            bool oldEnabled = GUI.enabled;
            int oldDepth = GUI.depth;
            try
            {
                float scale = Mathf.Min(width / LayoutWidth, height / LayoutHeight);
                GUI.matrix = Matrix4x4.TRS(new Vector3((width - LayoutWidth * scale) * .5f, (height - LayoutHeight * scale) * .5f), Quaternion.identity, new Vector3(scale, scale, 1));
                GUI.color = Color.white; GUI.depth = -10; GUI.enabled = true;
                hoverHint = "";
                LayoutIssues.Clear();
                Header(game);
                Survey(game);
                Rack(game);
                if (game.Started && !game.Sim.Finished && !game.HelpVisible && !game.Paused) Guidance(game);
                if (game.Sim.Finished && !game.HelpVisible) Results(game);
                else if (!game.Started && !game.HelpVisible) Welcome(game);
                else if (game.Paused && !game.HelpVisible) Pause(game);
                if (game.HelpVisible) Help(game);
                if (!string.IsNullOrEmpty(hoverHint) && !game.HelpVisible && game.Started && !game.Paused && !game.Sim.Finished)
                {
                    Box(new Rect(100, 580, 1400, 40), Navy);
                    Label(new Rect(118, 588, 1364, 25), hoverHint, small, White);
                }
            }
            finally
            {
                GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.enabled = oldEnabled; GUI.depth = oldDepth;
            }
        }

        void Header(IceResurfacerGame game)
        {
            var s = game.Sim;
            Box(new Rect(28, 24, 1274, 92), Navy);
            Box(new Rect(28, 24, 5, 92), Amber);
            Label(new Rect(48, 36, 310, 36), "AURORA  /  R-54", title, White);
            Label(new Rect(49, 79, 310, 24), "NORTHLINE  /  ICE CENTRE", small, Muted);
            Label(new Rect(386, 37, 130, 46), (Mathf.Abs(s.Vehicle.Speed) * 3.6f).ToString("0.0"), number, Mint);
            Label(new Rect(389, 82, 150, 24), s.Vehicle.Speed < -.02f ? "km/h   REVERSE" : "km/h   FORWARD", small, Muted);
            Meter(new Rect(542, 43, 149, 49), "MACHINE LOAD", s.MachineLoad, Mathf.RoundToInt(s.MachineLoad * 100) + "%", s.MachineLoad > .85f ? Amber : Mint);
            Meter(new Rect(719, 43, 155, 49), "FLOOD WATER", s.FloodWaterLiters / s.Config.FloodCapacityLiters, s.FloodWaterLiters.ToString("0") + " L", Mint);
            Meter(new Rect(901, 43, 155, 49), "WASH WATER", s.WashWaterLiters / s.Config.WashCapacityLiters, s.WashWaterLiters.ToString("0") + " L", Mint);
            float snow = s.SnowTankM3 / s.Config.SnowCapacityM3;
            Meter(new Rect(1083, 43, 193, 49), "SNOW TANK", snow, (snow * 100).ToString("0") + "%  /  " + s.SnowTankM3.ToString("0.00") + " m3", snow > .85f ? Amber : Mint);
        }

        void Survey(IceResurfacerGame game)
        {
            var s = game.Sim; var r = game.Report;
            Box(new Rect(1330, 24, 242, 523), Navy);
            Label(new Rect(1350, 40, 200, 23), "SURFACE SURVEY", heading, White);
            Rect map = new Rect(1381, 78, 140, 323.08f);
            Box(map, Inset);
            if (game.Surface != null && game.Surface.MapTexture != null)
                GUI.DrawTexture(map, game.Surface.MapTexture, ScaleMode.StretchToFill, true);
            Vector2 p = MapPoint(map, s.Vehicle.X, s.Vehicle.Z, s.Config);
            p.x = Mathf.Clamp(p.x, map.x + 3, map.xMax - 3);
            p.y = Mathf.Clamp(p.y, map.y + 8, map.yMax - 8);
            Vector2 forward = new Vector2(Mathf.Sin(s.Vehicle.Heading), -Mathf.Cos(s.Vehicle.Heading));
            Line(p - forward * 5, p + forward * 7, 3, White);
            Line(p + forward * 7, p + new Vector2(-forward.y, forward.x) * 5, 2, White);
            Line(p + forward * 7, p - new Vector2(-forward.y, forward.x) * 5, 2, White);
            Vector2 tail = MapPoint(map, s.Conditioner.X, s.Conditioner.Z, s.Config);
            if (map.Contains(tail))
            {
                Vector2 right = new Vector2(Mathf.Cos(s.Conditioner.Heading), Mathf.Sin(s.Conditioner.Heading));
                Line(tail - right * 5.3f, tail + right * 5.3f, 3, Amber);
            }
            Label(new Rect(1350, 410, 204, 24), "SOUTH GATE / SERVICE", small, Muted);
            Label(new Rect(1350, 440, 204, 23), "COVERAGE     " + (r.Coverage * 100).ToString("0.0") + "%", heading, Mint);
            Label(new Rect(1350, 466, 204, 22), "FINISH            " + (r.FinishQuality * 100).ToString("0") + "%", small, White);
            Box(new Rect(1350, 503, 9, 9), Mint); Label(new Rect(1365, 498, 66, 19), "Finished", small, Muted);
            Box(new Rect(1438, 503, 9, 9), Amber); Label(new Rect(1453, 498, 100, 19), "Too wet", small, Muted);
        }

        static Vector2 MapPoint(Rect rect, float x, float z, SimulatorConfig config)
        { return new Vector2(rect.x + (x / config.RinkWidth + .5f) * rect.width, rect.y + (.5f - z / config.RinkLength) * rect.height); }

        void Guidance(IceResurfacerGame game)
        {
            var s = game.Sim;
            Box(new Rect(28, 135, 492, 155), Navy);
            string mode = s.Practice ? "PRACTICE  /  UNSCORED" : "SCORED RUN";
            Label(new Rect(46, 148, 450, 25), mode + "     " + Duration(s.Time), heading, Mint);
            Label(new Rect(46, 183, 452, 92), NextAction(s), text, White);
            if (s.MachineLoad > .85f || s.EscapingSnowRate > .001f || s.PumpDamage > .05f)
            {
                Box(new Rect(28, 302, 492, 96), new Color(.26f, .10f, .065f, .97f));
                Label(new Rect(46, 315, 455, 72), s.EscapingSnowRate > .001f ? "SNOW ESCAPING  /  Retract blade. Clear both conveyors and check tank capacity." : s.PumpDamage > .05f ? "PUMP WEAR  /  Prime wash before pickup. Service in the bay to repair." : "HEAVY LOAD  /  Reduce cutting depth or ground speed.", small, Amber);
            }
            if (!string.IsNullOrEmpty(game.Notice))
            {
                Box(new Rect(28, 410, 492, 104), Navy);
                Label(new Rect(46, 421, 422, 82), game.Notice, small, Amber);
                if (Button(new Rect(478, 416, 31, 30), "x", Slate)) game.Notice = "";
            }
        }

        static string NextAction(Simulation s)
        {
            var c = s.Controls;
            if (s.TankLift > .8f && s.SnowTankM3 > .005f) return "UNLOADING  /  Hold position. Lower the snow tank after it empties.";
            if (s.TankLift > .02f || c.TankLiftTarget > .02f) return "STOW TANK  /  Return the tank lever fully down before moving or finishing.";
            if (s.InServiceBay && !s.HasEnteredRink) return "ENTER THE RINK  /  W drives through the south gate. Keep the conditioner raised until fully on ice.";
            if (s.InServiceBay && s.SnowTankM3 > .005f && !s.InDumpZone) return "APPROACH THE DUMP PIT  /  Drive toward the far end, centred and facing the pit. Stop at the yellow line.";
            if (s.InDumpZone && s.SnowTankM3 > .005f && Mathf.Abs(s.Vehicle.Speed) > .035f) return "STOP TO DUMP  /  Hold CTRL until stationary, then stow equipment and raise the snow-tank lever.";
            if (s.InDumpZone && s.SnowTankM3 > .005f) return "EMPTY THE TANK  /  With all equipment stowed, raise the tank. Wait until empty, then lower it fully.";
            if (s.InServiceBay && s.EquipmentSafe) return "READY TO RETURN  /  Finish this run, or refill water and continue working on missed strips.";
            if (s.InServiceBay) return "STOW EQUIPMENT  /  Blade out, augers and water off, brush stowed and conditioner fully raised.";
            if (s.HasEnteredRink && s.Vehicle.Z < -s.Config.RinkLength * .5f + 8 && Mathf.Abs(s.Vehicle.X) < s.Config.GateHalfWidth + 1 && Mathf.Cos(s.Vehicle.Heading) < -.5f && c.BladeDepthMm < .02f && c.FloodFlow < .02f)
                return "EXIT SEQUENCE  /  Stop on ice and clear conveyors. Shut augers and water, stow brush, then lift fully before the threshold.";
            if (c.ConditionerTarget > .05f && s.Conditioner.Lift > .25f) return "LOWER CONDITIONER  /  Blade out. Creep forward, lower fully, then engage both augers.";
            if (!c.HorizontalAuger || !c.VerticalAuger) return "CONVEYORS  /  Engage both augers before cutting. Let them clear before the exit sequence.";
            if (s.ConveyorJammed) return "AUGER JAM  /  Stop, blade out, BOTH augers OFF. Empty the tank; J resets after safe inspection.";
            if (s.SnowTankM3 > s.Config.SnowCapacityM3 * .85f) return "TANK NEAR CAPACITY  /  Retract blade, clear conveyors and return to the service bay.";
            if (c.BladeDepthMm < .02f) return "SET THE CUT  /  Turn the blade wheel clockwise. Start near 0.35 mm; use a lighter cut in creases.";
            if (c.WashValve > .02f && !c.WashPump && s.WashPrimeSeconds < 10) return "PRIMING WASH  /  " + Mathf.CeilToInt(10 - s.WashPrimeSeconds) + " s remaining. Continue forward before starting the pickup pump.";
            if (c.WashValve > .02f && !c.WashPump) return "WASH PRIMED  /  Enable the pickup pump to recover wash water through the filter.";
            if (c.FloodFlow < .02f) return "APPLY FINISH WATER  /  Open the flood lever while moving. Reduce flow through overlaps and turns.";
            return "WORK THE SHEET  /  Overlap the wet edge slightly. Grey strips on the survey remain unfinished. F1 opens the exit guide.";
        }

        void Rack(IceResurfacerGame game)
        {
            var s = game.Sim; var c = s.Controls;
            Box(new Rect(100, 625, 1400, 255), Navy);
            Box(new Rect(100, 625, 1400, 3), Slate);
            Label(new Rect(120, 643, 700, 25), "OPERATOR STATION     /     MANUAL EQUIPMENT CONTROL", heading, White);
            GUI.enabled = game.Started && !game.Paused && !s.Finished && !game.Looking && !game.HelpVisible;
            if (Button(ControlRect("SnowBreaker"), "SNOW BREAKER  [6]", Slate)) game.OperateSnowBreaker();
            GUI.enabled = true;
            if (Button(ControlRect("Guide"), "GUIDE  F1", Slate)) game.ToggleHelp();
            GUI.enabled = game.Started && !s.Finished;
            if (Button(ControlRect("Pause"), game.Paused ? "RESUME" : "PAUSE  ESC", Slate)) game.TogglePause();
            GUI.enabled = game.Started && !game.Paused && !s.Finished && !game.Looking && !game.HelpVisible;
            for (int i = 0; i < 5; i++)
            {
                float x = Separators[i];
                Box(new Rect(x, 690, 1, 174), Slate);
            }
            Label(new Rect(120, 690, 150, 25), "CONDITIONER", heading, Muted);
            c.ConditionerTarget = Lever(501, new Rect(120, 721, 150, 116), c.ConditionerTarget, "RAISE", "LOWER", "Drag conditioner lever. Raise only with blade out, conveyors and water off.");
            Label(new Rect(120, 852, 150, 24), "HEIGHT " + (s.Conditioner.Lift * 100).ToString("0") + "%", small, Mint);

            Label(new Rect(304, 690, 208, 25), "SNOW CONVEYORS", heading, Muted);
            c.HorizontalAuger = Toggle(ControlRect("HorizontalAuger"), "HORIZONTAL  [2]", c.HorizontalAuger, "Horizontal auger moves shaved snow toward the vertical auger.");
            c.VerticalAuger = Toggle(ControlRect("VerticalAuger"), "VERTICAL  [3]", c.VerticalAuger, "Vertical auger lifts snow into the tank. Stop before raising conditioner or tank.");
            c.BoardBrush = Toggle(ControlRect("BoardBrush"), "BOARD BRUSH  [4]", c.BoardBrush, "Brush extends LEFT. Use only in actual contact with the perimeter kickplate.");
            Label(new Rect(304, 852, 208, 24), s.ConveyorJammed?"JAM / STOP AUGERS":"BUFFER " + ((s.HorizontalSnowM3 + s.VerticalSnowM3) * 1000).ToString("0.0") + " L", small, s.ConveyorJammed?Amber:Muted);

            Label(new Rect(546, 690, 190, 25), "BLADE HANDWHEEL", heading, Muted);
            c.BladeDepthMm = Dial(502, ControlRect("Blade"), c.BladeDepthMm, s.Config.MaxBladeDepthMm, "3.5 turns: clockwise deeper, counterclockwise retract. Screw setting and effective bite differ. Q / E also adjust.", s.Config.BladeWheelTurns*360);
            CenterLabel(new Rect(546, 826, 190, 25), c.BladeDepthMm < .005f ? "BLADE OUT" : c.BladeDepthMm.ToString("0.00") + " mm SET", heading, c.BladeDepthMm > .45f ? Amber : Mint);
            CenterLabel(new Rect(546, 852, 190, 24), "BITE " + s.ActualBladeDepthMm.ToString("0.000") + " mm", small, Muted);

            Label(new Rect(770, 690, 235, 25), "WASH CIRCUIT", heading, Muted);
            c.WashValve = Dial(503, ControlRect("WashValve"), c.WashValve, 1, "Rotate wash valve clockwise to open. Wait 10 seconds before enabling pickup pump.");
            c.WashPump = Toggle(ControlRect("WashPump"), "PICKUP\nPUMP [5]", c.WashPump, "Pickup pump recirculates wash water. A dry pump suffers wear.");
            Label(new Rect(770, 826, 235, 25), "VALVE " + (c.WashValve * 100).ToString("0") + "%", heading, Mint);
            Label(new Rect(770, 852, 235, 24), s.WashPrimeSeconds >= 10 ? "PRIMED / READY" : "PRIME " + s.WashPrimeSeconds.ToString("0.0") + " / 10 s", small, s.WashPrimeSeconds >= 10 ? Mint : Muted);

            Label(new Rect(1041, 690, 150, 25), "FLOOD WATER", heading, Muted);
            c.FloodFlow = Lever(504, new Rect(1041, 721, 150, 116), c.FloodFlow, "OPEN", "OFF", "Drag flood lever for continuous flow. Close before stopping to avoid a puddle.");
            Label(new Rect(1041, 852, 150, 24), (c.FloodFlow * s.Config.FloodMaxLitersPerSecond * 60).ToString("0") + " L/min", small, Mint);

            Label(new Rect(1225, 690, 255, 25), "SNOW TANK / SERVICE", heading, Muted);
            c.TankLiftTarget = Lever(505, new Rect(1217, 721, 105, 116), c.TankLiftTarget, "DUMP", "STOW", "At the far-end pit: face the pit, centre the machine and stop at the yellow line. Stow equipment before dumping.");
            if (GUI.enabled && ControlRect("Service").Contains(Event.current.mousePosition)) hoverHint = "Refill water and repair pickup-pump wear while safely parked in the service bay.";
            if (Button(ControlRect("Service"), "REFILL", Slate)) game.Service();
            if (Button(ControlRect("Finish"), "FINISH RUN", new Color(.14f, .37f, .31f))) game.Finish();
            if (Button(ControlRect("NewSession"), "NEW SESSION", Slate)) game.NewSession(false, false);
            Label(new Rect(1225, 852, 255, 24), s.InDumpZone ? "DUMP PIT ALIGNED" : s.InServiceBay ? "PIT AT FAR END OF BAY" : "DUMP OFF ICE ONLY", small, s.InDumpZone ? Mint : Muted);
            GUI.enabled = true;
            Box(new Rect(100, 880, 1400, 20), Navy);
            Label(new Rect(120, 881, 1380, 19), game.Looking ? "HEAD LOOK  /  Release RMB to keep this view and use instruments. R recenters. Hold SPACE to rise." : "W/S drive   A/D steer   SHIFT precision   CTRL brake   SPACE rise   RMB look   R recenter   Mouse instruments", small, Muted);
        }

        float Lever(int hint, Rect rect, float value, string top, string bottom, string help)
        {
            int id = GUIUtility.GetControlID(hint, FocusType.Passive);
            Rect track = new Rect(rect.x + 14, rect.y + 9, 12, rect.height - 18);
            Rect hit = new Rect(rect.x, rect.y, 46, rect.height);
            Event e = Event.current;
            if (GUI.enabled && hit.Contains(e.mousePosition)) hoverHint = help;
            if (GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && hit.Contains(e.mousePosition))
            { GUIUtility.hotControl = id; value = Mathf.Clamp01(1 - (e.mousePosition.y - track.y) / track.height); e.Use(); }
            if (GUIUtility.hotControl == id)
            {
                if (GUI.enabled && e.type == EventType.MouseDrag) { value = Mathf.Clamp01(1 - (e.mousePosition.y - track.y) / track.height); e.Use(); }
                if (e.type == EventType.MouseUp || !GUI.enabled) { GUIUtility.hotControl = 0; if (e.type == EventType.MouseUp) e.Use(); }
            }
            Box(track, Inset);
            float pos = track.y + (1 - value) * track.height;
            Box(new Rect(track.x + 4, pos, 4, track.yMax - pos), new Color(.52f, .39f, .20f));
            for (int i = 0; i <= 4; i++) Box(new Rect(track.xMax + 7, track.y + track.height * i / 4, 6, 1), Slate);
            Box(new Rect(track.x - 10, pos - 7, 32, 15), GUI.enabled ? Amber : Muted);
            Box(new Rect(track.x - 5, pos - 1, 22, 2), Inset);
            Label(new Rect(rect.x + 45, rect.y + 1, rect.width - 45, 24), top, small, Muted);
            Label(new Rect(rect.x + 45, rect.yMax - 24, rect.width - 45, 24), bottom, small, Muted);
            Label(new Rect(rect.x + 45, rect.y + 46, rect.width - 45, 24), (value * 100).ToString("0") + "%", small, White);
            return value;
        }

        float Dial(int hint, Rect rect, float value, float maximum, string help,float travelDegrees=300)
        {
            int id = GUIUtility.GetControlID(hint, FocusType.Passive);
            Event e = Event.current;
            Vector2 center = rect.center;
            float angle = Mathf.Atan2(e.mousePosition.y - center.y, e.mousePosition.x - center.x) * Mathf.Rad2Deg;
            if (GUI.enabled && rect.Contains(e.mousePosition))
            {
                hoverHint = help;
                if (e.type == EventType.ScrollWheel) { value = Mathf.Clamp(value - e.delta.y * maximum * 7.5f/travelDegrees, 0, maximum); e.Use(); }
                if (e.type == EventType.MouseDown && e.button == 0) { GUIUtility.hotControl = id; dialLastAngle = angle; e.Use(); }
            }
            if (GUIUtility.hotControl == id)
            {
                if (GUI.enabled && e.type == EventType.MouseDrag)
                {
                    if (Vector2.Distance(e.mousePosition, center) > 12)
                        value = Mathf.Clamp(value + Mathf.DeltaAngle(dialLastAngle, angle) / travelDegrees * maximum, 0, maximum);
                    dialLastAngle = angle; e.Use();
                }
                if (e.type == EventType.MouseUp || !GUI.enabled) { GUIUtility.hotControl = 0; if (e.type == EventType.MouseUp) e.Use(); }
            }
            Texture(rect, disc, Inset);
            Texture(rect, ring, GUI.enabled ? Amber : Muted);
            float rotation = value / maximum * travelDegrees - 120;
            for (int i = 0; i < 3; i++)
            {
                float a = (rotation + i * 120) * Mathf.Deg2Rad;
                Line(center, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rect.width * .40f, 5, GUI.enabled ? Amber : Muted);
            }
            Texture(new Rect(center.x - 12, center.y - 12, 24, 24), disc, Slate);
            float pointer = rotation * Mathf.Deg2Rad;
            Vector2 tip = center + new Vector2(Mathf.Cos(pointer), Mathf.Sin(pointer)) * rect.width * .38f;
            Texture(new Rect(tip.x - 5, tip.y - 5, 10, 10), disc, White);
            return value;
        }

        bool Toggle(Rect rect, string label, bool value, string help)
        {
            if (GUI.enabled && rect.Contains(Event.current.mousePosition)) hoverHint = help;
            Box(rect, value ? new Color(.12f, .31f, .27f) : Inset);
            Box(new Rect(rect.x + 7, rect.y + 10, 5, rect.height - 20), value ? Mint : Slate);
            var previousWrap = button.wordWrap;
            button.wordWrap = true;
            Rect contentRect = new Rect(rect.x + 13, rect.y, rect.width - 15, rect.height);
            if (AuditLayout && Event.current.type == EventType.Repaint)
            {
                float required = button.CalcHeight(new GUIContent(label), contentRect.width);
                if (required > contentRect.height + 1f)
                    LayoutIssues.Add(label + " switch requires " + required.ToString("0.0") + " px, has " + contentRect.height.ToString("0.0") + " px");
            }
            bool clicked = GUI.Button(contentRect, label, button);
            button.wordWrap = previousWrap;
            return clicked ? !value : value;
        }

        void Welcome(IceResurfacerGame game)
        {
            Box(new Rect(360, 189, 835, 365), Modal);
            Box(new Rect(360, 189, 6, 365), Amber);
            Label(new Rect(396, 216, 730, 25), "NORTHLINE  /  BETWEEN SESSIONS", heading, Mint);
            Label(new Rect(394, 256, 740, 47), "A clean sheet is in your hands.", title, White);
            Label(new Rect(396, 319, 740, 71), "Drive a full resurfacing pass from the operator's seat. Manage the blade, snow conveyors and water while watching the actual finish behind your conditioner.", text, Muted);
            Label(new Rect(396, 405, 740, 34), "WASD drive  /  RMB look  /  R recenter  /  SPACE rise  /  MOUSE instruments", heading, White);
            if (Button(new Rect(397, 465, 225, 50), "START SCORED RUN", new Color(.18f, .40f, .33f))) game.NewSession(false);
            if (Button(new Rect(640, 465, 225, 50), "UNSCORED PRACTICE", Slate)) game.NewSession(true);
            if (Button(new Rect(884, 465, 290, 50), "READ THE OPERATOR GUIDE", Slate)) game.ToggleHelp();
        }

        void Pause(IceResurfacerGame game)
        {
            Box(new Rect(430, 235, 700, 281), Modal);
            Label(new Rect(468, 264, 624, 44), "Operator break", title, White);
            Label(new Rect(470, 322, 618, 45), "The machine and ice simulation are paused.", text, Muted);
            if (Button(new Rect(470, 391, 180, 49), "RESUME", new Color(.18f, .40f, .33f))) game.TogglePause();
            if (Button(new Rect(670, 391, 190, 49), "OPERATOR GUIDE", Slate)) game.ToggleHelp();
            if (Button(new Rect(870, 391, 220, 49), "NEW SESSION", Slate)) game.NewSession(false, false);
            Label(new Rect(470, 468, 620, 20), "ESC returns to the seat.", small, Muted);
        }

        void Help(IceResurfacerGame game)
        {
            Box(new Rect(180, 125, 1240, 490), Modal);
            Box(new Rect(180, 125, 1240, 3), Amber);
            Label(new Rect(214, 150, 950, 44), "Operator's quick guide", title, White);
            if (Button(new Rect(1254, 151, 132, 36), "CLOSE  F1", Slate)) game.ToggleHelp();
            Label(new Rect(216, 218, 550, 25), "01  PREPARE & RESURFACE", heading, Mint);
            Label(new Rect(216, 255, 550, 280), "Enter with conditioner raised and blade out. Once fully on ice, creep forward, lower the conditioner and engage both augers.\n\nSet wheel near 0.35 mm (0.056 mm bite). Open wash water, wait 10 seconds, then start the pickup pump. Apply flood water while moving.\n\nOverlap the visible wet edge slightly. Use the left brush near boards. Reduce cut and flood in goal areas and on repeated overlaps. Jam: stop BOTH augers before J reset.", text, White);
            Box(new Rect(795, 220, 1, 314), Slate);
            Label(new Rect(826, 218, 550, 25), "02  RETURN & EMPTY", heading, Mint);
            Label(new Rect(826, 255, 550, 280), "Retract blade and close flood flow. Stop on ice and let conveyors clear. Stop augers and wash pump; close wash valve and stow brush.\n\nFully lift before the south threshold. Drive to the far snow pit, centred and facing it. Stop at the yellow line, empty the tank, then lower it fully.\n\nFinish Run measures actual coverage and finish. Refill restores water and repairs pickup-pump wear for another pass.", text, White);
            Label(new Rect(216, 558, 1170, 24), "1 lift   2 horizontal auger   3 vertical auger   4 brush   5 pump   6 snow breaker   Q/E blade   J safe jam reset", small, Muted);
            Label(new Rect(216, 585, 1170, 24), "SHIFT precision   CTRL brake   Hold SPACE rise   RMB look, release keeps view   R recenter   F1 guide   ESC pause", small, Muted);
        }

        void Results(IceResurfacerGame game)
        {
            var r = game.Report; var s = game.Sim;
            Box(new Rect(365, 162, 835, 442), Modal);
            Label(new Rect(399, 186, 755, 24), r.Practice ? "PRACTICE REVIEW  /  NO SCORE RECORDED" : r.Complete ? "FULL RUN COMPLETE" : "RUN RETURNED  /  PARTIAL SHEET", heading, Mint);
            Label(new Rect(397, 223, 530, 45), r.Practice ? "Your surface, reviewed." : r.Score.ToString("0.0") + "  /  100", title, White);
            Label(new Rect(970, 232, 190, 24), Duration(r.ElapsedSeconds), heading, Muted);
            ReportLine(399, 294, "COVERAGE", (r.Coverage * 100).ToString("0.0") + "%", r.Practice ? "" : (r.Coverage * 45).ToString("0.0") + " / 45");
            ReportLine(399, 329, "FINISH QUALITY", (r.FinishQuality * 100).ToString("0.0") + "%", r.Practice ? "" : (r.FinishQuality * 35).ToString("0.0") + " / 35");
            ReportLine(399, 364, "MISSED STRIPS", (r.MissedFraction * 100).ToString("0.0") + "%", "");
            ReportLine(790, 294, "OVERWATERED", (r.OverwaterFraction * 100).ToString("0.0") + "%", r.Practice ? "" : ((1 - r.OverwaterFraction) * 10).ToString("0.0") + " / 10");
            bool emptied = s.TotalSnowCollectedM3 < .005f || (s.DumpedSnowM3 >= s.TotalSnowCollectedM3 * .98f && s.SnowTankM3 < .005f);
            ReportLine(790, 329, "TANK HANDLING", r.TankDumps + " dumps", r.Practice ? "" : (emptied ? "5" : "0") + " / 5");
            ReportLine(790, 364, "EXIT PROCEDURE", r.ProperExit ? "Correct" : "Incomplete", r.Practice ? "" : (r.ProperExit ? "5" : "0") + " / 5");
            Label(new Rect(400, 415, 750, 26), r.Mistakes + " operator mistakes" + (r.Practice ? "  /  practice feedback only" : "  /  -" + (r.Mistakes * 1.2f).ToString("0.0") + " points"), text, r.Mistakes > 0 ? Amber : Mint);
            Label(new Rect(400, 454, 748, 42), r.Complete ? "The sheet is covered. Inspect the survey for remaining finish differences." : "Grey areas were not fully processed. A complete sheet requires at least 95% actual surface coverage.", small, Muted);
            if (Button(new Rect(401, 524, 229, 46), "NEW SCORED RUN", new Color(.18f, .40f, .33f))) game.NewSession(false);
            if (Button(new Rect(651, 524, 229, 46), "NEW PRACTICE", Slate)) game.NewSession(true);
            if (Button(new Rect(902, 524, 261, 46), "OPERATOR GUIDE", Slate)) game.ToggleHelp();
        }

        void ReportLine(float x, float y, string label, string value, string points)
        {
            Label(new Rect(x, y, 182, 24), label, small, Muted);
            Label(new Rect(x + 184, y - 1, 110, 24), value, heading, White);
            Label(new Rect(x + 302, y, 82, 24), points, small, Mint);
        }
        static string Duration(float seconds) { return ((int)seconds / 60).ToString("00") + ":" + ((int)seconds % 60).ToString("00"); }
        void Meter(Rect rect, string caption, float value, string reading, Color color)
        {
            Label(new Rect(rect.x, rect.y, rect.width, 19), caption, small, Muted);
            Label(new Rect(rect.x, rect.y + 20, rect.width, 21), reading, heading, color);
            Box(new Rect(rect.x, rect.y + 47, rect.width, 4), Slate);
            Box(new Rect(rect.x, rect.y + 47, rect.width * Mathf.Clamp01(value), 4), color);
        }
        bool Button(Rect rect, string caption, Color color)
        {
            if (AuditLayout && Event.current.type == EventType.Repaint)
            {
                Vector2 required = button.CalcSize(new GUIContent(caption));
                if (required.x > rect.width + 1f || required.y > rect.height + 1f)
                    LayoutIssues.Add(caption + " button requires " + required + ", has " + rect.size);
            }
            bool hover = GUI.enabled && rect.Contains(Event.current.mousePosition);
            Box(rect, hover ? Color.Lerp(color, White, .13f) : color);
            return GUI.Button(rect, caption, button);
        }
        void Label(Rect rect, string caption, GUIStyle style, Color color)
        {
            if (AuditLayout && Event.current.type == EventType.Repaint)
            {
                float required = style.CalcHeight(new GUIContent(caption), rect.width);
                if (required > rect.height + 1f)
                    LayoutIssues.Add(caption + " requires " + required.ToString("0.0") + " px, has " + rect.height.ToString("0.0") + " px");
            }
            Color previous = style.normal.textColor; style.normal.textColor = color;
            GUI.Label(rect, caption, style); style.normal.textColor = previous;
        }
        void CenterLabel(Rect rect, string caption, GUIStyle style, Color color)
        {
            var previous = style.alignment; style.alignment = TextAnchor.UpperCenter;
            Label(rect, caption, style, color); style.alignment = previous;
        }
        static void Box(Rect rect, Color color) { Texture(rect, Texture2D.whiteTexture, color); }
        static void Texture(Rect rect, Texture texture, Color color)
        {
            Color previous = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, texture); GUI.color = previous;
        }
        static void Line(Vector2 a, Vector2 b, float width, Color color)
        {
            Matrix4x4 previous = GUI.matrix;
            float degrees = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            GUI.matrix = previous * Matrix4x4.TRS(new Vector3(a.x, a.y, 0), Quaternion.Euler(0, 0, degrees), Vector3.one);
            Box(new Rect(0, -width * .5f, Vector2.Distance(a, b), width), color);
            GUI.matrix = previous;
        }
        public void Dispose()
        {
            if (disc != null) UnityEngine.Object.Destroy(disc);
            if (ring != null) UnityEngine.Object.Destroy(ring);
        }
    }
}
