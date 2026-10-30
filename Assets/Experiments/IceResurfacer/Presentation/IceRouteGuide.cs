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
        readonly GuideSample[] mapUpcoming=new GuideSample[120];
        RouteFit referenceFit,selectedFit;
        TrainingRouteDefinition selectedGuide;
        float nextComparison;
        public RouteProgress Progress {get;private set;}
        public RouteFootprints Footprints {get;private set;}
        public TrainingRouteDefinition SelectedGuide => selectedGuide;
        public RouteFit ActiveFit => Route==null?null:Route.Guide;
        public int DisplayPointCount {get;private set;}
        public int MapPointCount {get;private set;}
        public double LookEnd {get;private set;}
        public const float LookAheadMeters=28,HiddenNearMeters=4;
        public string TrainingStatus {get;private set;}="Original generated F5 reference selected";
        public string GuideDirectory => System.IO.Path.Combine(Library.DirectoryPath,"Guides");
        public GuideSample MapPointAt(int i) => mapUpcoming[i];
        public bool IsTestGuide => selectedGuide!=null&&ActiveFit!=null&&(ActiveFit.provenance.IndexOf("NOT the operator",System.StringComparison.OrdinalIgnoreCase)>=0||ActiveFit.provenance.IndexOf("validation",System.StringComparison.OrdinalIgnoreCase)>=0);
        public bool TrainingPanelOpen => PanelOpen&&panel.TrainingTab;
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
        public string Phase => Progress!=null&&ActiveFit!=null?"LEG "+(Progress.Leg+1)+" / "+ActiveFit.legs[Progress.Leg].label:Route==null?"":NextPoint<Route.WorkStart?"ENTRY / REVERSE THEN TURN":NextPoint<Route.WorkEnd?"OVERLAPPING PASSES":"RETURN TO SERVICE";
        public void Initialize(IceResurfacerGame owner)
        {
            game=owner;
            Library=new RouteRecordingStorage(RouteRecordingStorage.DefaultDirectory);panel=new RouteRecordingPanel(this);
            material=new Material(Resources.Load<Shader>("TrainingRoute")){name="Aurora depth-tested optional route guide"};
            material.SetColor("_BaseColor",new Color(.30f,.90f,.75f,.62f));
            var node=new GameObject("F5 advisory upcoming route");node.transform.SetParent(transform,false);
            line=node.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;
            line.startWidth=.075f;line.endWidth=.025f;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.1f,0),new GradientAlphaKey(.9f,.25f),new GradientAlphaKey(.65f,.8f),new GradientAlphaKey(0,1)});line.colorGradient=gradient;
            Refresh();
        }
        public void Toggle()
        {
            Refresh();
            if(!Available){game.Notice="The reference route is calibrated for Northline's standard rink and machinery geometry. Free driving remains available.";return;}
            Visible=!Visible;
            if(Visible){PrepareNavigation();Progress?.Synchronize(game.Sim.Vehicle.X,game.Sim.Vehicle.Z);}
            Refresh();
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
        public void ShowTrainingPanel(){if(!PanelOpen)ToggleRecordingPanel();panel.TrainingTab=true;}
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
            if(Visible&&Progress!=null&&session==sim)
            {
                Progress.Observe(sim.Vehicle.X,sim.Vehicle.Z,sim.Vehicle.Heading,sim.Vehicle.Speed);
                Footprints.ObserveSimulation(sim);Footprints.AdvanceExpected(Progress.Distance);
            }
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
        public bool LoadTrainingGuide(string path)
        {
            try{return SelectTrainingGuide(TrainingGuideStorage.Read(path));}
            catch(System.Exception e){TrainingStatus="Guide load failed: "+e.Message;return false;}
        }
        public bool SelectTrainingGuide(TrainingRouteDefinition definition)
        {
            try
            {
                definition=TrainingGuideStorage.Decode(TrainingGuideStorage.Encode(definition)); // Detach live guidance from subsequent Editor edits.
                var raw=TrainingGuideStorage.Raw(definition);var fit=RouteFitter.Fit(definition,raw);
                if(!fit.Ready){TrainingStatus="Draft guide: repair turning/boundary issues in the Editor before live use.";return false;}
                if(!fit.Compatible(game.Sim.Config)){TrainingStatus="Guide machine/rink calibration differs; revalidate in the Editor.";return false;}
                selectedGuide=definition;selectedFit=fit;Route=new ResurfacingRoute(fit);Progress=null;Footprints=null;Available=true;
                PrepareNavigation();TrainingStatus="Selected "+definition.name+". F5 toggles guidance; driving remains free.";return true;
            }
            catch(System.Exception e){TrainingStatus="Guide rejected: "+e.Message;return false;}
        }
        public void UseOriginalReference()
        {
            selectedGuide=null;selectedFit=null;Route=new ResurfacingRoute(game.Sim.Config);Progress=null;Footprints=null;
            Available=true;if(Visible)PrepareNavigation();TrainingStatus="Original generated F5 reference selected";Refresh();
        }
        public void RejoinLeg(int leg)
        {PrepareNavigation();Progress.RejoinLeg(leg);Footprints.RebaseExpected(Progress.Distance);TrainingStatus="Explicit rejoin: leg "+(Progress.Leg+1)+". Only the guide cursor changed.";}
        public void PrepareTrainingPreview(){Refresh();if(Available)PrepareNavigation();}
        public bool SelectTestFixture()
        {
            var text=Resources.Load<TextAsset>("TrainingTestGuide");
            if(text==null){TrainingStatus="Labeled test guide is unavailable; save/load your own guide in the Editor.";return false;}
            try{return SelectTrainingGuide(TrainingGuideStorage.Decode(text.text));}catch(System.Exception e){TrainingStatus=e.Message;return false;}
        }
        void PrepareNavigation()
        {
            if(Route==null||Progress!=null)return;
            if(selectedFit!=null)Route=new ResurfacingRoute(selectedFit);
            else
            {
                if(referenceFit==null||!referenceFit.Compatible(game.Sim.Config))referenceFit=RouteFitter.Reference(Route,TrainingRouteDefinition.CopyConfiguration(game.Sim.Config));
                Route=new ResurfacingRoute(referenceFit);
            }
            Progress=new RouteProgress(Route.Guide);Footprints=new RouteFootprints(Route.Guide);
        }
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
                Route=null;Progress=null;Footprints=null;NextPoint=1;
                if(selectedFit!=null&&selectedFit.Compatible(c)){Route=new ResurfacingRoute(selectedFit);Available=true;}
                else if(selectedFit!=null){selectedFit=null;selectedGuide=null;TrainingStatus="Selected guide calibration changed; original reference restored where available.";}
                if(Available&&Route==null)
                {
                    try {Route=new ResurfacingRoute(c);}
                    catch(System.InvalidOperationException){Available=false;}
                }
                if(!Available)Visible=false;
            }
            if(!Available){line.enabled=false;return;}
            line.enabled=Visible&&game.Started&&!session.OnFoot&&!session.Finished;
            if(!Visible){DisplayPointCount=MapPointCount=0;return;}
            PrepareNavigation();var fit=ActiveFit;NextPoint=fit.IndexAt(Progress.Distance);
            int lastLeg=Mathf.Min(fit.legs.Count-1,Progress.Leg+1);
            if(fit.legs[lastLeg].direction!=fit.legs[Progress.Leg].direction)lastLeg=Progress.Leg;
            LookEnd=System.Math.Min(fit.Length,System.Math.Min(Progress.Distance+LookAheadMeters,fit.legs[lastLeg].end));
            MapPointCount=Mathf.Min(mapUpcoming.Length,Mathf.Max(2,Mathf.CeilToInt((float)(LookEnd-Progress.Distance)/.4f)+1));
            for(int i=0;i<MapPointCount;i++)mapUpcoming[i]=fit.At(Progress.Distance+(LookEnd-Progress.Distance)*i/(MapPointCount-1));
            double start=System.Math.Min(LookEnd,Progress.Distance+HiddenNearMeters);
            DisplayPointCount=LookEnd-start<.1?0:Mathf.Min(upcoming.Length,Mathf.CeilToInt((float)(LookEnd-start)/.35f)+1);
            line.positionCount=DisplayPointCount;
            for(int i=0;i<DisplayPointCount;i++)
            {var p=fit.At(start+(LookEnd-start)*i/(DisplayPointCount-1));upcoming[i]=new Vector3(p.x,.045f,p.z);line.SetPosition(i,upcoming[i]);}
            if(Time.unscaledTime>=nextComparison){Footprints.Compare();nextComparison=Time.unscaledTime+.5f;}
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
