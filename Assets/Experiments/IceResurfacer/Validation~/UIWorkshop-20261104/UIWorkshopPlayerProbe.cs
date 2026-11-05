// Disposable validation-only replacement for the probe already attached to the
// packaged Mono layout player. Never imported by the production Unity project.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    public sealed class IceLayoutPlayerProbe : MonoBehaviour
    {
        IceResurfacerGame game;
        string output, current;
        bool active, finished, sliderLogged;
        readonly List<string> issues = new List<string>();
        readonly List<string> errors = new List<string>();
        float began;

        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i=0; i+1<args.Length; i++)
                if (args[i]=="--aurora-validation-output") output=Path.GetFullPath(args[i+1]);
            if (string.IsNullOrEmpty(output)) { enabled=false; yield break; }
            if (!output.Replace('\\','/').Contains("/Validation~/UIWorkshop-20261104"))
                throw new InvalidOperationException("UI probe requires its dedicated validation folder.");
            output=Path.Combine(output,"Player-"+Screen.width+"x"+Screen.height);
            Directory.CreateDirectory(output);
            Application.runInBackground=true;
            Application.logMessageReceived+=Log;
            Debug.developerConsoleEnabled=false;
            began=Time.realtimeSinceStartup;
            active=true;
            while(game==null||game.Sim==null||game.Dashboard==null||game.RouteGuide==null)
            {game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();yield return null;}
            game.enabled=false;
            game.RouteGuide.ConfigureValidationStorage(Path.Combine(output,"Routes"));
            game.RouteGuide.enabled=false;
            game.Dashboard.AuditLayout=true;
            yield return new WaitForSecondsRealtime(2);
            game.NewSession(false,false);
            yield return Capture("01-welcome");
            game.NewSession(false);
            yield return Capture("02-hud");
            game.Dashboard.Compact=false;
            yield return Capture("03-console");
            game.TogglePause();
            yield return Capture("04-pause");
            game.TogglePause();
            game.ToggleHelp();
            yield return Capture("05-help");
            game.ToggleHelp();
            game.ToggleDiagnostics();
            yield return Capture("06-diagnostics");
            game.ToggleDiagnostics();
            game.RouteGuide.ToggleRecordingPanel();
            yield return Capture("07-routes");
            game.RouteGuide.ShowRawDetails();
            yield return Capture("08-route-recording");
            game.RouteGuide.ShowTrainingPanel();
            yield return Capture("09-training-guide");
            game.RouteGuide.ToggleRecordingPanel();
            game.Finish();
            yield return Capture("10-results");
            Complete();
        }
        IEnumerator Capture(string name)
        {
            current=name;
            Debug.developerConsoleVisible=false;
            yield return null;
            Debug.developerConsoleVisible=false;
            yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            if(texture==null)throw new InvalidOperationException("No Unity framebuffer for "+name);
            File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
            foreach(var issue in game.Dashboard.LayoutIssues)issues.Add(name+": "+issue);
            if(game.RouteGuide.PanelOpen)
                foreach(var issue in game.RouteGuide.Manager.LayoutIssues)issues.Add(name+": "+issue);
        }
        void OnGUI()
        {
            if(!active||finished||game==null||game.Dashboard==null||game.enabled)return;
            Debug.developerConsoleVisible=false;
            try { game.Dashboard.Draw(game,Screen.width,Screen.height); SliderMetrics(); }
            catch(Exception e){errors.Add(current+": "+e);Complete();}
        }
        void SliderMetrics()
        {
            if(!game.Paused||sliderLogged)return;
            var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
            var slider=(GUIStyle)typeof(IceUITheme).GetField("slider",flags).GetValue(null);
            var thumb=(GUIStyle)typeof(IceUITheme).GetField("sliderThumb",flags).GetValue(null);
            if(slider==null||thumb==null)return;
            sliderLogged=true;
            var lines=new List<string>();
            void Inspect(string name,GUIStyle style)
            {
                lines.Add(name+" fixedHeight="+style.fixedHeight+" fixedWidth="+style.fixedWidth+" alignment="+style.alignment);
                lines.Add("  padding="+style.padding+" margin="+style.margin+" overflow="+style.overflow+" border="+style.border);
            }
            Inspect("Inherited slider",GUI.skin.horizontalSlider);Inspect("Inherited thumb",GUI.skin.horizontalSliderThumb);
            Inspect("Theme slider",slider);Inspect("Theme thumb",thumb);
            var control=IceDashboard.ControlRect("SpeedSlider");
            var inner=thumb.margin.Remove(slider.padding.Remove(control));
            lines.Add("Last drawn speed-slider control="+control+" native thumb y="+inner.y+" height="+thumb.fixedHeight);
            lines.Add("Track style overflow extents="+slider.overflow.Add(control));
            File.WriteAllLines(Path.Combine(output,"slider-metrics.txt"),lines);
        }
        void Update()
        {
            if(active&&!finished)Debug.developerConsoleVisible=false;
            if(active&&!finished&&Time.realtimeSinceStartup-began>65)
            {errors.Add("Timed out before all UI captures completed.");Complete();}
        }
        void Log(string message,string stack,LogType type)
        {
            if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)
                errors.Add(type+": "+message+"\n"+stack);
        }
        void Complete()
        {
            if(finished)return;finished=true;
            Application.logMessageReceived-=Log;
            var report=new List<string>{
                "Actual Unity "+Application.unityVersion+" Mono player framebuffer / "+Screen.width+"x"+Screen.height,
                "Current runtime source rebuilt against the existing player's own Managed assemblies.",
                "Validation-only configuration serialization shim preserves the old packaged scene schema; UI source is unchanged.",
                "The packaged scene/assets are from an earlier validation build. Evidence covers current UI styling/layout only.",
                "No full Editor import, current scene fidelity, new interactions, or focused-window latency verified by this probe.",
                "Measured layout issues: "+issues.Count, "Runtime errors: "+errors.Count
            };
            report.AddRange(issues);report.AddRange(errors);
            File.WriteAllLines(Path.Combine(output,"UI-results.txt"),report);
            Application.Quit(errors.Count==0&&issues.Count==0?0:1);
        }
    }
}
