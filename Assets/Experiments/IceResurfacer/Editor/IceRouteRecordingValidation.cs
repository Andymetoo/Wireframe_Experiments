using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace IceResurfacer.Editor
{
    public static class IceRouteRecordingValidation
    {
        const string Root="Assets/Experiments/IceResurfacer/Validation~/RouteRecording/";
        static readonly List<string> results=new List<string>();
        static void Check(bool value,string text)
        {if(!value)throw new InvalidOperationException("Route recorder: "+text);results.Add("PASS "+text);}
        static void Reject(Action action,string name)
        {bool rejected=false;try{action();}catch(FormatException){rejected=true;}Check(rejected,name);}
        internal static void Run(IceResurfacerGame game,Keyboard keyboard,Mouse mouse)
        {
            Directory.CreateDirectory(Root);results.Clear();bool enabled=game.enabled;game.enabled=false;
            var guide=game.RouteGuide;
            var input=typeof(IceRouteGuide).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
            void KeyInput(params Key[] keys)
            {InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();input.Invoke(guide,null);}
            try
            {
                guide.ConfigureValidationStorage(Path.GetFullPath(Root+"Editor-Library"));
                game.NewSession(true);guide.Refresh();KeyInput();KeyInput(Key.F6);
                Check(guide.PanelOpen,"actual F6 opens the recorder within the existing route component");KeyInput();KeyInput(Key.F6);KeyInput();
                guide.RecordingName="Unity input validation";KeyInput(Key.F7);KeyInput();
                Check(guide.Recorder.Recording&&guide.Recorder.Data.samples.Count==1,"actual F7 starts a raw route at the current rear axle");
                var sim=game.Sim;var data=guide.Recorder.Data;
                Check(!ReferenceEquals(data.configuration,sim.Config)&&data.configuration.Wheelbase==sim.Config.Wheelbase,"configuration snapshot is detached from live calibration");
                for(int i=0;i<500;i++){sim.Step(.02f);guide.ObserveSimulationStep(sim);guide.ObserveSimulationStep(sim);}
                Check(data.samples.Count==1&&Math.Abs(data.durationSeconds-10)<.00001,"ten stationary seconds and duplicate callbacks add no redundant samples");
                sim.Controls.Throttle=-.45f;
                for(int i=0;i<300;i++){sim.Step(.02f);guide.ObserveSimulationStep(sim);}
                KeyInput(Key.F7);KeyInput();
                Check(!guide.Recorder.Recording&&data.complete&&data.samples.Count>10&&data.distanceMeters>1,"actual F7 stops after real controlled reverse departure");
                var last=data.samples[data.samples.Count-1];
                Check(last.vehicleX==sim.Vehicle.X&&last.vehicleZ==sim.Vehicle.Z&&last.conditionerX==sim.Conditioner.X&&last.conditionerHeadingRadians==sim.Conditioner.Heading,
                    "stop retains the exact final vehicle and conditioner observation below the sampling threshold");
                bool saved=guide.SaveRecording();Check(saved&&File.Exists(guide.LastSavedPath),"save named JSON copy through route component: "+guide.RecordingStatus);
                string firstPath=guide.LastSavedPath,firstBytes=File.ReadAllText(firstPath);
                Check(guide.SaveRecording()&&guide.LastSavedPath!=firstPath&&File.ReadAllText(firstPath)==firstBytes,"repeated save creates another copy without overwriting the raw original");
                var loaded=RouteRecordingStorage.Read(firstPath);
                Check(loaded.samples.Count==data.samples.Count,"save/load preserves sample count");
                double maxDoubleError=0;
                foreach(var field in typeof(RecordedRouteSample).GetFields())
                for(int i=0;i<data.samples.Count;i++)
                {
                    object a=field.GetValue(data.samples[i]),b=field.GetValue(loaded.samples[i]);
                    if(field.FieldType==typeof(double))maxDoubleError=Math.Max(maxDoubleError,Math.Abs((double)a-(double)b));
                    else if(field.FieldType==typeof(float))
                    {if(BitConverter.ToInt32(BitConverter.GetBytes((float)a),0)!=BitConverter.ToInt32(BitConverter.GetBytes((float)b),0))throw new Exception("Float changed: "+field.Name+" / "+i);}
                    else if(!Equals(a,b))throw new Exception("Sample field changed: "+field.Name+" / "+i);
                }
                Check(maxDoubleError<1e-10,"float poses and states round-trip exactly; parsed double time/distance max error="+maxDoubleError.ToString("E3"));
                Check(RouteRecordingStorage.Encode(loaded)==firstBytes,"copying an unchanged loaded recording preserves its entire original raw JSON payload verbatim");
                var projection=new ResurfacingRoute(loaded);Check(projection.Points.Count==loaded.samples.Count,"loaded route reuses existing ResurfacingRoute path representation");
                Check(guide.LoadRecording(firstPath)&&guide.RecordedPreview.Points.Count==loaded.samples.Count,"load displays recorded path without replacing the F5 reference");
                float px=sim.Vehicle.X,pz=sim.Vehicle.Z,water=sim.FloodWaterLiters;var controls=sim.Controls.Throttle;
                guide.Toggle();guide.Toggle();Check(sim.Vehicle.X==px&&sim.Vehicle.Z==pz&&sim.FloodWaterLiters==water&&sim.Controls.Throttle==controls,"F5 and recording preview remain read-only");
                string corrupt=Root+"truncated-test.json";File.WriteAllText(corrupt,firstBytes.Substring(0,firstBytes.Length/2));
                var previous=guide.Recorder.Data;Check(!guide.LoadRecording(corrupt)&&guide.Recorder.Data==previous,"truncated file is rejected while the current route stays intact");
                Reject(()=>RouteRecordingStorage.Decode("{}"),"missing schema and checksum are rejected");
                var escaped=RouteRecordingStorage.Decode(firstBytes);escaped.name="Operator \"route\": {curves} / Łódź \\ take";
                string escapedJson=RouteRecordingStorage.Encode(escaped);
                Check(RouteRecordingStorage.Decode(escapedJson).name==escaped.name,"quoted names, braces, slashes and Unicode survive payload integrity parsing");
                Reject(()=>RouteRecordingStorage.Decode(firstBytes.Replace("\"version\": 1","\"version\": 999")),"unsupported future envelope version is rejected explicitly");
                Reject(()=>RouteRecordingStorage.Decode(firstBytes.Replace("AuroraR54.RecordedRoute","Unknown.Route")),"unknown file format is rejected");
                Reject(()=>RouteRecordingStorage.Decode(firstBytes.Replace("Unity input validation","Changed without checksum")),"accidental payload edits fail the integrity check");
                var bad=RouteRecordingStorage.Decode(firstBytes);var point=bad.samples[1];point.sequence=0;bad.samples[1]=point;
                Reject(()=>RouteRecordingStorage.Encode(bad),"duplicate/out-of-order sample sequence is rejected");
                bad=RouteRecordingStorage.Decode(firstBytes);point=bad.samples[1];point.vehicleX=float.NaN;bad.samples[1]=point;
                Reject(()=>RouteRecordingStorage.Encode(bad),"non-finite pose is rejected before writing");
                bad=RouteRecordingStorage.Decode(firstBytes);point=bad.samples[1];point.seconds=-1;bad.samples[1]=point;
                Reject(()=>RouteRecordingStorage.Encode(bad),"invalid timestamp is rejected before writing");
                string draft=guide.Library.SaveDraft(loaded);string oldDraft=File.ReadAllText(draft);
                var updated=RouteRecordingStorage.Decode(firstBytes);updated.name="Updated recovery metadata";
                guide.Library.SaveDraft(updated);
                Check(File.ReadAllText(draft+".bak")==oldDraft&&RouteRecordingStorage.Read(draft).name==updated.name,"atomic recovery replacement retains a valid previous backup");
                string currentDraft=File.ReadAllText(draft);bool writeFailed=false;
                using(var locked=new FileStream(draft,FileMode.Open,FileAccess.Read,FileShare.None))
                {try{guide.Library.SaveDraft(loaded);}catch(IOException){writeFailed=true;}}
                Check(writeFailed&&File.ReadAllText(draft)==currentDraft,"failed/locked draft replacement preserves the previously published file");
                Check(Directory.GetFiles(Path.GetDirectoryName(draft),"*.tmp").Length==0,"failed save cleans its own temporary file");
                // A separate take is automatically stopped at a new-session boundary.
                game.NewSession(true);guide.Refresh();guide.RecordingName="Session boundary validation";guide.StartRecording();
                var boundary=guide.Recorder.Data;game.NewSession(true);guide.Refresh();
                Check(!guide.Recorder.Recording&&boundary.complete&&File.Exists(Path.Combine(guide.Library.DirectoryPath,"Drafts",boundary.id+".route.json")),"new session stops and persists the preceding take instead of joining trajectories");
                var fixture=RecordedRouteFixture.Create();fixture.Validate();
                string fixturePath=Root+"Deterministic-fixture.route.json";RouteRecordingStorage.Export(fixturePath,fixture,File.Exists(fixturePath));
                File.WriteAllText(Root+"Fixture-file-sha256.txt",FileHash(fixturePath));
                Check(guide.LoadRecording(fixturePath),"deterministic driven fixture loads in the same operator preview");
                string originalRaw=JsonUtility.ToJson(fixture);
                using(var preview=new RouteRecordingPreview())
                {preview.Rebuild(fixture);File.WriteAllBytes(Root+"Raw-route-overhead.png",preview.Texture.EncodeToPNG());}
                Check(JsonUtility.ToJson(fixture)==originalRaw,"preview does not smooth, deduplicate, reorder or mutate raw observations");
                results.Add("Fixture: "+fixture.samples.Count+" observations / "+fixture.distanceMeters.ToString("F3")+" m / "+fixture.durationSeconds.ToString("F2")+" s. Test driver only, NOT the operator's route.");
                results.Add("Paired fixture verifies identical vehicle and conditioner pose every fixed step, material totals and every final ice cell.");
                // UI should also stop a take at normal Play Mode shutdown; this draft
                // is checked by a fresh Editor process after this process exits.
                game.NewSession(true);guide.Refresh();guide.RecordingName="Shutdown recovery validation";guide.StartRecording();
                for(int i=0;i<10;i++){game.Sim.Step(.02f);guide.ObserveSimulationStep(game.Sim);}
                File.WriteAllText(Root+"Shutdown-draft-path.txt",Path.Combine(guide.Library.DirectoryPath,"Drafts",guide.Recorder.Data.id+".route.json"));
            }
            finally
            {
                if(guide.PanelOpen)guide.ToggleRecordingPanel();
                game.enabled=enabled;File.WriteAllLines(Root+"Unity-recorder-results.txt",results);
            }
        }
        static string FileHash(string path)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        public static void VerifyPersisted()
        {
            try
            {
                if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))throw new InvalidOperationException("Use the isolated validation project.");
                results.Clear();string path=Root+"Deterministic-fixture.route.json";
                var data=RouteRecordingStorage.Read(path);var recorder=new RouteRecorder();recorder.Load(data);
                Check(!EditorApplication.isPlaying&&data.samples.Count>1000&&data.provenance.Contains("NOT the operator"),"fresh Editor process reloads the same inspectable file after Play Mode ended");
                Check(FileHash(path)==File.ReadAllText(Root+"Fixture-file-sha256.txt"),"saved original bytes survive Play Mode shutdown unchanged");
                var route=new ResurfacingRoute(data);
                for(int i=0;i<data.samples.Count;i++)
                    if(data.samples[i].sequence!=i||route.Points[i].X!=data.samples[i].vehicleX||route.Points[i].Z!=data.samples[i].vehicleZ)throw new Exception("Reload reordered samples.");
                Check(true,"all raw point ordering and positions survive fresh-process load");
                string draftPath=File.ReadAllText(Root+"Shutdown-draft-path.txt");var draft=RouteRecordingStorage.Read(draftPath);
                Check(draft.complete&&draft.stopReason.Contains("closed")||draft.complete&&draft.stopReason.Contains("ended"),"normal Play Mode/application shutdown flushes and completes the unsaved recovery draft");
                File.WriteAllLines(Root+"Persistence-reload-results.txt",results);EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText(Root+"Persistence-reload-results.txt","FAIL "+e);EditorApplication.Exit(1);}
        }
    }
    [InitializeOnLoad]
    static class IceRouteRecordingLifecycle
    {
        static IceRouteRecordingLifecycle()
        {
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingPlayMode)Flush("Play Mode ended");};
            AssemblyReloadEvents.beforeAssemblyReload+=()=>Flush("Scripts reloaded");
        }
        static void Flush(string reason)
        {if(EditorApplication.isPlaying)foreach(var guide in UnityEngine.Object.FindObjectsByType<IceRouteGuide>())guide.FlushRecordingForShutdown(reason);}
    }
}
