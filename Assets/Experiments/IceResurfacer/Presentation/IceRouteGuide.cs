using IceResurfacer.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace IceResurfacer.Presentation
{
    /// <summary>Existing F5 advisory route plus observational route authoring. Neither writes physics or scoring.</summary>
    public sealed class IceRouteGuide:MonoBehaviour
    {
        IceResurfacerGame game;
        Simulation session;
        LineRenderer line;
        Material material;
        readonly Vector3[] upcoming=new Vector3[120];
        readonly RouteRecorder recorder=new RouteRecorder();
        RouteRecordingPanel panel;
        float nextCheckpoint;
        Simulation recordingSession;
        bool validationStorage;
        public RouteRecorder Recorder => recorder;
        public RouteRecordingStorage Library {get;private set;}
        public ResurfacingRoute RecordedPreview {get;private set;}
        public int RenderedRecordingSamples => panel==null?0:panel.PreviewSampleCount;
        public bool PanelOpen {get;private set;}
        public string RecordingName="Operator route";
        public string RecordingStatus {get;private set;}="Start in Free Simulation / Practice, then close this panel and drive normally.";
        public string LastSavedPath {get;private set;}="";
        public ResurfacingRoute Route {get;private set;}
        public bool Visible {get;private set;}
        public bool Available {get;private set;}=true;
        public int NextPoint {get;private set;}
        public string Phase => Route==null?"":NextPoint<Route.WorkStart?"ENTRY / REVERSE THEN TURN":NextPoint<Route.WorkEnd?"OVERLAPPING PASSES":"RETURN TO SERVICE";
        public void Initialize(IceResurfacerGame owner)
        {
            game=owner;
            Library=new RouteRecordingStorage(RouteRecordingStorage.DefaultDirectory);panel=new RouteRecordingPanel(this);
            material=new Material(Resources.Load<Shader>("CollisionOverlay")){name="Aurora optional route guide"};
            material.SetColor("_BaseColor",new Color(.30f,.90f,.75f,.40f));
            var node=new GameObject("F5 advisory upcoming route");node.transform.SetParent(transform,false);
            line=node.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;
            line.startWidth=.075f;line.endWidth=.025f;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;
            Refresh();
        }
        public void Toggle()
        {
            Refresh();
            if(!Available){game.Notice="The reference route is calibrated for Northline's standard rink and machinery geometry. Free driving remains available.";return;}
            Visible=!Visible;Refresh();
        }
        void Update()
        {
            if(game==null)return;
            if(Keyboard.current!=null&&Keyboard.current.f5Key.wasPressedThisFrame)Toggle();
            if(Keyboard.current!=null&&Keyboard.current.f6Key.wasPressedThisFrame)ToggleRecordingPanel();
            if(Keyboard.current!=null&&Keyboard.current.f7Key.wasPressedThisFrame)
            {if(recorder.Recording)StopRecording();else StartRecording();}
            Refresh();
            if(recorder.Recording&&Time.unscaledTime>=nextCheckpoint)
            {Checkpoint();nextCheckpoint=Time.unscaledTime+10;}
        }
        public void ToggleRecordingPanel()
        {
            PanelOpen=!PanelOpen;game.ReleaseControls();game.ReleaseLook();
            if(PanelOpen){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;panel.RefreshFiles();}
        }
        public bool CanRecord => game!=null&&game.Sim!=null&&game.Started&&!game.Sim.GuidedTraining&&!game.Sim.Finished&&!game.Paused&&!game.HelpVisible;
        public bool StartRecording()
        {
            if(recorder.Recording)return false;
            if(!CanRecord){RecordingStatus="Start or resume Free Simulation / Practice before recording. Guided Training is excluded.";return false;}
            try
            {
                // Protect the previous take before replacing the in-memory view.
                if(recorder.Data!=null)Library.SaveDraft(recorder.Data);
                recorder.Start(game.Sim,RecordingName,validationStorage?"Automated Unity validation; NOT the operator's actual route":"Manual operator driving");recordingSession=game.Sim;RecordedPreview=null;
                nextCheckpoint=Time.unscaledTime+10;Library.SaveDraft(recorder.Data);
                RecordingStatus="RECORDING — close panel and drive. F7 stops. Draft checkpoints every 10 seconds.";
                game.Notice=RecordingStatus;return true;
            }
            catch(System.Exception e){RecordingStatus=(recorder.Recording?"RECORDING IN MEMORY; draft unavailable: ":"Recording/storage: ")+e.Message;return recorder.Recording;}
        }
        public bool StopRecording(string reason="Stopped by operator")
        {
            if(!recorder.Stop(reason))return false;
            recordingSession=null;RecordedPreview=new ResurfacingRoute(recorder.Data);
            RecordingStatus="Stopped: "+recorder.Data.samples.Count+" raw samples / "+recorder.Data.distanceMeters.ToString("F2")+" m. Save a named copy or load its draft.";
            Checkpoint();panel.RefreshFiles();game.Notice=RecordingStatus;return true;
        }
        public void ObserveSimulationStep(Simulation sim)
        {
            if(!recorder.Recording)return;
            if(sim!=recordingSession){StopRecording("Session changed; original trajectory retained");return;}
            recorder.Observe(sim,.02);
            if(!recorder.Recording)
            {RecordedPreview=new ResurfacingRoute(recorder.Data);Checkpoint();RecordingStatus=recorder.Data.stopReason;}
        }
        bool Checkpoint()
        {
            if(recorder.Data==null)return true;
            try {Library.SaveDraft(recorder.Data);return true;}
            catch(System.Exception e){RecordingStatus="Draft not written: "+e.Message+" Data remains in memory; stop and save before leaving.";Debug.LogWarning(RecordingStatus);return false;}
        }
        public bool SaveRecording()
        {
            try
            {
                if(recorder.Data==null)throw new System.InvalidOperationException("Record or load a route first.");
                LastSavedPath=Library.SaveCopy(recorder.Data);RecordingStatus="Saved "+System.IO.Path.GetFileName(LastSavedPath);
                panel.RefreshFiles();game.Notice=RecordingStatus;return true;
            }
            catch(System.Exception e){RecordingStatus="Save failed: "+e.Message;return false;}
        }
        public bool LoadRecording(string path)
        {
            if(recorder.Recording){RecordingStatus="Stop recording before loading another route.";return false;}
            try
            {
                var data=RouteRecordingStorage.Read(path); // Validate before replacing the current view.
                if(recorder.Data!=null)Library.SaveDraft(recorder.Data);
                var preview=new ResurfacingRoute(data);recorder.Load(data);RecordedPreview=preview;
                LastSavedPath=path;RecordingStatus="Loaded "+data.name+(data.complete?"":" (recovered partial recording)")+". Raw points preserved.";
                return true;
            }
            catch(System.Exception e){RecordingStatus="Load failed: "+e.Message;return false;}
        }
        public void DrawRecordingPanel() {if(PanelOpen)panel.Draw();}
        public void ConfigureValidationStorage(string directory)
        {
            if(!directory.Replace('\\','/').Contains("/Validation~/"))throw new System.InvalidOperationException("Validation routes must stay in Validation~/.");
            if(recorder.Recording)throw new System.InvalidOperationException("Stop before changing validation storage.");
            Library=new RouteRecordingStorage(directory);validationStorage=true;panel.RefreshFiles();
        }
        public void Refresh()
        {
            if(game==null||game.Sim==null||line==null)return;
            if(session!=game.Sim)
            {
                if(recorder.Recording)StopRecording("New session; original trajectory retained");
                session=game.Sim;var c=session.Config;
                Available=Mathf.Abs(c.RinkWidth-26)<.001f&&Mathf.Abs(c.RinkLength-60)<.001f&&Mathf.Abs(c.CornerRadius-8.5f)<.001f;
                Route=null;NextPoint=1;
                if(Available)
                {
                    try {Route=new ResurfacingRoute(c);}
                    catch(System.InvalidOperationException){Available=false;}
                }
                if(!Available)Visible=false;
            }
            if(!Available){line.enabled=false;return;}
            line.enabled=Visible&&game.Started&&!session.OnFoot&&!session.Finished;
            // Advance sequentially: crossing a later pass cannot skip the route.
            for(int i=0;game.Started&&i<20&&NextPoint<Route.Points.Count-1;i++)
            {
                var p=Route.Points[NextPoint];float dx=p.X-session.Vehicle.X,dz=p.Z-session.Vehicle.Z;
                if(dx*dx+dz*dz>2.25f)break;NextPoint++;
            }
            if(!line.enabled)return;
            int count=Mathf.Min(upcoming.Length,Route.Points.Count-NextPoint);
            line.positionCount=count;
            for(int i=0;i<count;i++)
            {var p=Route.Points[NextPoint+i];upcoming[i]=new Vector3(p.X,.025f,p.Z);line.SetPosition(i,upcoming[i]);}
        }
        public void FlushRecordingForShutdown(string reason)
        {if(recorder.Recording)recorder.Stop(reason);Checkpoint();}
        void OnApplicationPause(bool paused){if(paused)Checkpoint();}
        void OnApplicationQuit(){FlushRecordingForShutdown("Application closed");}
        void OnDestroy()
        {
            if(recorder.Recording)recorder.Stop("Play Mode ended or scene unloaded");
            Checkpoint();panel?.Dispose();if(material!=null)Destroy(material);
        }
    }
}
