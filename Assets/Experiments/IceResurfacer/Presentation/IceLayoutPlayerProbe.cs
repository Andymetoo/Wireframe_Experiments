#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    /// <summary>
    /// Explicit development-player validation only. The normal launch scene never contains
    /// this component; the editor build tool attaches it to a disposable scene clone.
    /// </summary>
    public sealed class IceLayoutPlayerProbe : MonoBehaviour
    {
        readonly List<string> failures = new List<string>();
        readonly List<string> layoutIssues = new List<string>();
        readonly List<string> passes = new List<string>();
        readonly List<string> inputTrace = new List<string>();
        IceResurfacerGame game;
        string outputDirectory, currentResolution = "initialization";
        float startedAt;
        int sentEvents, consumedEvents;
        int requestedWidth = 1366, requestedHeight = 768;
        EventType expectedInput;
        IntPtr playerWindow;
        bool active, complete;

        IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "--aurora-validation-output") outputDirectory = Path.GetFullPath(args[i + 1]);
                if (args[i] == "--aurora-validation-width") requestedWidth = int.Parse(args[i + 1]);
                if (args[i] == "--aurora-validation-height") requestedHeight = int.Parse(args[i + 1]);
            }
            // Attaching the component without the explicit command does not run a test or write files.
            if (string.IsNullOrEmpty(outputDirectory)) { enabled = false; yield break; }
            var directory = new DirectoryInfo(outputDirectory);
            if (directory.Name != "Validation~" || directory.Parent == null || directory.Parent.Name != "IceResurfacer")
            { Debug.LogError("Aurora validation output must be the experiment's Validation~ directory."); Application.Quit(1); yield break; }
            Directory.CreateDirectory(outputDirectory);
            Application.runInBackground = true;
            // Native offscreen D3D presentation diagnostics can reopen the developer
            // console between capture yields. Keep that overlay out of this opt-in
            // HUD fixture; all managed errors still fail below, and the launcher
            // records native presentation diagnostics separately in the report.
            Debug.developerConsoleEnabled=false;
            Application.logMessageReceived += OnLog;
            startedAt = Time.realtimeSinceStartup;
            active = true;
            yield return RunSafely(Run());
        }

        IEnumerator RunSafely(IEnumerator root)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            while (stack.Count > 0 && !complete)
            {
                object yielded = null;
                bool moved = false;
                Exception error = null;
                try { moved = stack.Peek().MoveNext(); if (moved) yielded = stack.Peek().Current; }
                catch (Exception e) { error = e; }
                if (error != null) { failures.Add(error.ToString()); Finish(); yield break; }
                if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (yielded is IEnumerator nested) stack.Push(nested);
                else yield return yielded;
            }
        }

        IEnumerator Run()
        {
            while (game == null || game.Sim == null || game.Dashboard == null)
            { game = UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>(); yield return null; }
            game.enabled = false;
            game.NewSession(false);
            game.Dashboard.AuditLayout = true;
            // The launcher selects the client resolution before native startup.
            // One resolution per process avoids runtime swapchain changes.
            yield return new WaitForSecondsRealtime(2);
            playerWindow = FindOwnPlayerWindow();
            if (playerWindow == IntPtr.Zero) throw new InvalidOperationException("No process-owned Unity player window for native input validation.");
            yield return ValidateResolution(requestedWidth, requestedHeight);
            Finish();
        }

        IEnumerator ValidateResolution(int width, int height)
        {
            currentResolution = width + "x" + height;
            float until = Time.realtimeSinceStartup + 8;
            while ((Screen.width != width || Screen.height != height) && Time.realtimeSinceStartup < until) yield return null;
            Check(Screen.width == width && Screen.height == height,
                "Requested client resolution (actual " + Screen.width + "x" + Screen.height + ")");
            // Give the native player window and its repaint loop time to settle.
            for (int i = 0; i < 8; i++) yield return null;
            game.NewSession(false);
            game.Dashboard.AuditLayout = true;
            yield return Capture("HUD-" + currentResolution + ".png", "normal HUD");
            yield return Click("MachineLoad");
            Check(game.DiagnosticsVisible,"Machine Load meter opens its demand explanation");
            yield return Capture("HUD-"+currentResolution+"-diagnostics.png","load explanation and diagnostics");
            yield return Click("MachineLoad");
            Check(!game.DiagnosticsVisible,"Machine Load explanation closes without changing machinery");

            var controls = game.Sim.Controls;
            yield return HoldHydraulic("Conditioner",false);
            yield return Click("HorizontalAuger");
            Check(controls.HorizontalAuger, "Horizontal auger switch");
            yield return Click("VerticalAuger");
            Check(controls.VerticalAuger, "Vertical auger switch");
            yield return Click("BoardBrush");
            Check(controls.BoardBrush, "Board brush switch");

            float bladeBefore = controls.BladeDepthMm;
            yield return RotateDial("Blade");
            Check(Mathf.Abs(controls.BladeDepthMm-bladeBefore-.8f/14)<.01f, "Precise quarter-turn blade handwheel drag");
            controls.BladeDepthMm=0;
            yield return ValidateBladeTurns();
            float washBefore = controls.WashValve;
            yield return RotateWater("WashValve",.38f);
            Check(Mathf.Abs(controls.WashValve-.38f)<.035f, "Analog quarter-turn wash handle");
            yield return Click("WashPump");
            Check(controls.WashPump, "Pickup pump switch");
            yield return RotateWater("Flood",.43f);
            Check(Mathf.Abs(controls.FloodFlow - .43f) < .035f, "Independent analog quarter-turn flood handle");
            yield return HoldHydraulic("TankLift",true);

            // The manual actuator has its own real interlock. This UI fixture supplies a
            // lowered conditioner and running vertical auger; it never changes ice cells.
            game.Sim.Conditioner.Lift = 0;
            controls.ConditionerTarget = 0;
            controls.TankLiftTarget = 0;
            controls.VerticalAuger = true;
            int strokesBefore = game.Sim.SnowBreakerStrokes;
            yield return Click("SnowBreaker");
            Check(game.Sim.SnowBreakerStrokes == strokesBefore + 1, "Manual snow-breaker button and interlock");

            yield return Click("Guide");
            Check(game.HelpVisible, "Guide button opens");
            yield return Capture("HUD-" + currentResolution + "-guide.png", "operator guide");
            yield return Click("Guide");
            Check(!game.HelpVisible, "Guide button closes");
            yield return Click("Pause");
            Check(game.Paused, "Pause button");
            yield return Capture("HUD-" + currentResolution + "-pause.png", "pause panel");
            yield return Click("Pause");
            Check(!game.Paused, "Resume button");

            // Return to the default service-bay fixture for service/finish controls.
            game.NewSession(false);
            controls = game.Sim.Controls;
            controls.FloodFlow = .3f;
            game.Sim.Step(.2f);
            controls.FloodFlow = 0;
            game.Notice = "";
            float serviceHelpWater=game.Sim.FloodWaterLiters;
            yield return Click("Service");
            Check(game.Sim.FloodWaterLiters==serviceHelpWater && game.Notice.Contains("hose"),
                "Service help explains physical hose without silently topping up tanks");
            game.Notice = "";
            yield return Click("Finish");
            Check(game.Sim.Finished, "Free Simulation may finish a partial session without a progression guard");
            game.NewSession(false);
            yield return Click("NewSession");
            Check(!game.Started, "New-session button");
            yield return Capture("HUD-" + currentResolution + "-welcome.png", "welcome panel");
            yield return ClickAt(new Vector2(950, 480));
            Check(game.Started && game.Sim.Practice, "Unscored-practice launch button");
            game.NewSession(false, false);
            yield return ClickAt(new Vector2(510, 490));
            Check(game.Started && !game.Sim.Practice, "Scored-run launch button");

            // A legal partial run exercises the results UI; the separate deterministic
            // full-sheet validation covers score and coverage. Back on and drive out
            // with raised, dry equipment, then brake before calling the normal finish path.
            game.NewSession(false);
            controls = game.Sim.Controls;
            controls.Throttle = -.6f;
            for (int i = 0; i < 1000; i++) game.Sim.Step(.02f);
            controls.Throttle = .6f;
            for (int i = 0; i < 1400 && !game.Sim.InServiceBay; i++) game.Sim.Step(.02f);
            for (int i = 0; i < 180; i++) game.Sim.Step(.02f);
            controls.Throttle = 0; controls.Brake = true;
            for (int i = 0; i < 250; i++) game.Sim.Step(.02f);
            game.Finish();
            Check(game.Sim.Finished && game.Sim.ProperExit, "Legal partial run reaches results through normal finish path");
            yield return Capture("HUD-" + currentResolution + "-results.png", "results panel");
            game.NewSession(false);
            game.Dashboard.Compact=true;
            yield return Capture("HUD-"+currentResolution+"-compact.png","compact operator view");
            game.Dashboard.Compact=false;
            game.StartServicePractice();
            game.Sim.TryDismount();
            typeof(IceWaterService).GetMethod("BeginWalk",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(game.WaterService,null);
            game.WaterService.HandleInput(true,0);
            game.WaterService.UpdateViewAndEffects(true,.25f);
            yield return Capture("HUD-"+currentResolution+"-service.png","service panel");
        }

        IEnumerator Click(string key) { yield return ClickAt(IceDashboard.ControlRect(key).center); }

        IEnumerator HoldHydraulic(string key,bool raise)
        {
            Rect rect=IceDashboard.ControlRect(key);
            Vector2 point=new Vector2(rect.center.x,raise?rect.y+12:rect.yMax-12);
            var s=game.Sim;var c=s.Controls;
            yield return Send(EventType.MouseDown,point);
            float demand=key=="Conditioner"?c.ConditionerHydraulic:c.TankHydraulic;
            Check(raise?demand>.99f:demand<-.99f,key+" held direction");
            for(int i=0;i<25;i++)s.Step(.02f);
            float attained=key=="Conditioner"?s.Conditioner.Lift:s.TankLift;
            yield return Send(EventType.MouseUp,point);
            demand=key=="Conditioner"?c.ConditionerHydraulic:c.TankHydraulic;
            Check(demand==0,key+" springs to neutral on native mouse release");
            for(int i=0;i<25;i++)s.Step(.02f);
            Check(Mathf.Abs(attained-(key=="Conditioner"?s.Conditioner.Lift:s.TankLift))<.0001f,key+" holds attained position after release");
        }
        IEnumerator RotateWater(string key,float fraction)
        {
            Rect rect=IceDashboard.ControlRect(key);float r=rect.width*.35f;
            Vector2 begin=rect.center+Vector2.left*r;
            float angle=(180-fraction*90)*Mathf.Deg2Rad;
            Vector2 end=rect.center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*r;
            yield return Send(EventType.MouseDown,begin);
            yield return Send(EventType.MouseDrag,end);
            yield return Send(EventType.MouseUp,end);
        }

        IEnumerator ValidateBladeTurns()
        {
            Rect rect=IceDashboard.ControlRect("Blade");float radius=rect.width*.35f;
            yield return Send(EventType.MouseDown,rect.center+Vector2.right*radius);
            for(int quarter=1;quarter<=15;quarter++)
            {
                float angle=quarter*Mathf.PI*.5f;
                yield return Send(EventType.MouseDrag,rect.center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius);
                if(quarter==12)Check(game.Sim.Controls.BladeDepthMm>.67f&&game.Sim.Controls.BladeDepthMm<.70f,"three full turns retain remaining blade adjustment");
                if(quarter==14)Check(Mathf.Abs(game.Sim.Controls.BladeDepthMm-.8f)<.005f,"3.5 full turns reach deepest blade stop");
                if(quarter==15)Check(Mathf.Abs(game.Sim.Controls.BladeDepthMm-.8f)<.005f,"blade mechanical end stop rejects extra rotation");
            }
            Vector2 reversed=rect.center+Vector2.left*radius;
            yield return Send(EventType.MouseDrag,reversed);
            Check(Mathf.Abs(game.Sim.Controls.BladeDepthMm-(.8f-.8f/14))<.01f,"blade reverses continuously from end stop");
            yield return Send(EventType.MouseUp,reversed);
        }

        IEnumerator ClickAt(Vector2 canvasPoint)
        {
            yield return Send(EventType.MouseDown, canvasPoint);
            yield return Send(EventType.MouseUp, canvasPoint);
            yield return null;
        }

        IEnumerator DragLever(string key, float value)
        {
            Rect rect = IceDashboard.ControlRect(key);
            float y = rect.y + 9 + (1 - value) * (rect.height - 18);
            Vector2 begin = new Vector2(rect.x + 20, rect.yMax - 14);
            Vector2 end = new Vector2(rect.x + 20, y);
            yield return Send(EventType.MouseDown, begin);
            yield return Send(EventType.MouseDrag, end);
            yield return Send(EventType.MouseUp, end);
        }

        IEnumerator RotateDial(string key)
        {
            Rect rect = IceDashboard.ControlRect(key);
            yield return Send(EventType.MouseDown, rect.center + Vector2.right * rect.width * .35f);
            yield return Send(EventType.MouseDrag, rect.center + Vector2.up * rect.height * .35f);
            yield return Send(EventType.MouseUp, rect.center + Vector2.up * rect.height * .35f);
        }

        IEnumerator Scroll(string key, float amount)
        { yield return Send(EventType.ScrollWheel, IceDashboard.ControlRect(key).center, new Vector2(0, amount)); }

        IEnumerator Send(EventType type, Vector2 canvasPoint, Vector2 delta = default)
        {
            Vector2 clientPoint = IceDashboard.CanvasToViewport(canvasPoint, Screen.width, Screen.height);
            int target = ++sentEvents;
            expectedInput = type;
            PostMouse(type, clientPoint, delta);
            float deadline = Time.realtimeSinceStartup + 5;
            while (consumedEvents < target && !complete && Time.realtimeSinceStartup < deadline) yield return null;
            if (consumedEvents < target) throw new TimeoutException("Native player IMGUI did not consume " + type);
            // A clean normal repaint follows native window input before the next assertion.
            yield return null;
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        delegate bool WindowCallback(IntPtr window, IntPtr parameter);
        [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
        [DllImport("user32.dll", SetLastError = true)] static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
        [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int X, Y; }
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window, ref NativePoint point);
#endif

        static IntPtr FindOwnPlayerWindow()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            IntPtr found = IntPtr.Zero;
            using (var process = System.Diagnostics.Process.GetCurrentProcess())
            {
                EnumWindows((window, parameter) =>
                {
                    GetWindowThreadProcessId(window, out uint owner);
                    if (owner != process.Id) return true;
                    var name = new StringBuilder(256); GetClassName(window, name, 256);
                    if (name.ToString() != "UnityWndClass") return true;
                    found = window; return false;
                }, IntPtr.Zero);
            }
            return found;
#else
            return IntPtr.Zero;
#endif
        }

        void PostMouse(EventType type, Vector2 point, Vector2 delta)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            // Only this opt-in development player's window receives messages. This
            // does not move the OS pointer, activate a window or target another process.
            GetWindowThreadProcessId(playerWindow, out uint owner);
            using (var process = System.Diagnostics.Process.GetCurrentProcess())
                if (owner != process.Id) throw new InvalidOperationException("Validation input window ownership changed.");
            int x = Mathf.RoundToInt(point.x), y = Mathf.RoundToInt(point.y);
            uint message; int buttons = 0;
            if (type == EventType.MouseDown) { message = 0x0201; buttons = 1; }
            else if (type == EventType.MouseUp) message = 0x0202;
            else if (type == EventType.MouseDrag) { message = 0x0200; buttons = 1; }
            else if (type == EventType.ScrollWheel)
            {
                message = 0x020A; buttons = Mathf.RoundToInt(-delta.y * 40) << 16;
                var screenPoint = new NativePoint { X = x, Y = y };
                if (!ClientToScreen(playerWindow, ref screenPoint)) throw new InvalidOperationException("Cannot map validation wheel input.");
                x = screenPoint.X; y = screenPoint.Y;
            }
            else throw new ArgumentOutOfRangeException(nameof(type));
            IntPtr position = new IntPtr((x & 0xFFFF) | ((y & 0xFFFF) << 16));
            if (!PostMessage(playerWindow, message, new IntPtr(buttons), position))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Cannot post input to the validation player's own window.");
            inputTrace.Add("POST " + type + " client=" + point);
#else
            throw new PlatformNotSupportedException("Native window input probe requires the explicit Windows development player.");
#endif
        }

        IEnumerator Capture(string name, string context)
        {
            // Suppress only the development-build overlay; errors remain in the report.
            Debug.developerConsoleVisible = false;
            yield return null;
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            if (texture == null) throw new InvalidOperationException("No rendered framebuffer for " + name);
            File.WriteAllBytes(Path.Combine(outputDirectory, name), texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
            foreach (string issue in game.Dashboard.LayoutIssues)
            {
                string entry = currentResolution + " / " + context + ": " + issue;
                if (!layoutIssues.Contains(entry)) layoutIssues.Add(entry);
            }
            File.WriteAllText(Path.Combine(outputDirectory, "HUD-layout-issues-" + currentResolution + ".txt"),
                layoutIssues.Count == 0 ? "PASS: no measured text clipping in captured HUD states.\n" : string.Join("\n", layoutIssues) + "\n");
        }

        void OnGUI()
        {
            if (!active || complete || game == null || game.Dashboard == null) return;
            try
            {
                EventType nativeType = Event.current.type;
                if ((Event.current.isMouse || nativeType == EventType.ScrollWheel) && inputTrace.Count < 160)
                    inputTrace.Add("IMGUI " + nativeType + " position=" + Event.current.mousePosition);
                game.Dashboard.Draw(game, Screen.width, Screen.height);
                if (sentEvents > consumedEvents && nativeType == expectedInput) consumedEvents = sentEvents;
            }
            catch (Exception e) { failures.Add(e.ToString()); Finish(); }
        }

        void Update()
        {
            // The explicit HUD probe disables Game.Update, so sample its real
            // player frame cadence here instead of freezing the startup sample.
            if(active&&!complete&&game!=null&&game.Performance!=null)
            {game.Performance.BeginFrame(Time.unscaledDeltaTime);game.Performance.EndWork();}
            if (active && !complete && Time.realtimeSinceStartup - startedAt > 120)
            { failures.Add("Development-player probe exceeded its 120-second timeout."); Finish(); }
        }

        void Check(bool passed, string message)
        {
            string entry = currentResolution + " / " + message;
            if (passed) passes.Add("PASS " + entry);
            else failures.Add("FAIL " + entry);
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (!complete && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                failures.Add("Runtime " + type + ": " + message + "\n" + stack);
        }

        void Finish()
        {
            if (complete) return;
            complete = true;
            Application.logMessageReceived -= OnLog;
            if (layoutIssues.Count > 0) failures.Add("Measured HUD clipping: " + layoutIssues.Count + " entries; see HUD-layout-issues-" + currentResolution + ".txt.");
            var report = new StringBuilder();
            report.AppendLine("Aurora native development-player HUD validation / Unity " + Application.unityVersion);
            report.AppendLine("Actual player framebuffer captures include the world and live IMGUI instruments.");
            report.AppendLine("The opt-in probe disables the Development Console overlay; native presentation diagnostics remain in the player log and are counted by the launcher.");
            report.AppendLine("Native mouse messages target only this player's own window; hydraulic checks advance fixed simulation ticks while held and after release.");
            report.AppendLine("Handwheel and valve dragging are tested. Native wheel messages use the OS pointer, so optional scroll shortcuts are not automated here.");
            report.AppendLine("The results capture follows an actual raised, dry entry and safe return through the normal finish path.");
            foreach (string pass in passes) report.AppendLine(pass);
            foreach (string failure in failures) report.AppendLine(failure);
            if (failures.Count > 0) foreach (string input in inputTrace) report.AppendLine(input);
            report.AppendLine(failures.Count == 0 ? "PASS: all player HUD checks." : "FAIL: " + failures.Count + " check(s).");
            File.WriteAllText(Path.Combine(outputDirectory, "HUD-player-results-" + currentResolution + ".txt"), report.ToString());
            Debug.Log(report.ToString());
            Application.Quit(failures.Count == 0 ? 0 : 1);
        }
    }
}
#endif
