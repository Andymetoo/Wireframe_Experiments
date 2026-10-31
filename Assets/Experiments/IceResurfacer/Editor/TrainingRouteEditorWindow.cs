using System;
using System.Collections.Generic;
using System.IO;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEngine;

namespace IceResurfacer.Editor
{
    public sealed class TrainingRouteEditorWindow:EditorWindow
    {
        sealed class History
        {
            public string name;public int selected,version;public readonly List<RouteControlPoint> points=new List<RouteControlPoint>();
        }
        readonly Stack<History> undo=new Stack<History>(),redo=new Stack<History>();
        TrainingRouteDefinition definition;
        RecordedRouteData raw;
        RouteFit fit;
        RouteFootprints footprints;
        TrainingGuideView preview;
        Vector2 scroll;
        int selected,revision,layer,drag;
        bool dirty,pending;
        double rebuildAt,draftAt;
        Rect plot;
        int problem=-1;
        bool panning;
        bool releasePointer;
        bool dragEdited;
        Event validationEvent;
        readonly List<int> visibleControls=new List<int>();
        string status="Open an operator .route.json, or the labeled validation fixture. Raw files are read-only.";
        public TrainingRouteDefinition Document => definition;
        public RouteFit Fit => fit;
        public int Selected => selected;
        public TrainingGuideView Viewport=>preview;
        public int SelectedProblem=>problem;
        public int VisibleMarkerCount=>visibleControls.Count;
        internal int GuiDrawCount {get;private set;}
        [MenuItem("Experiments/Ice Resurfacer/Training Route Editor")]
        public static void Open(){var w=GetWindow<TrainingRouteEditorWindow>("Aurora Training Route");w.minSize=new Vector2(1050,740);w.Show();}
        public static void OpenRecording(RecordedRouteData recording)
        {Open();GetWindow<TrainingRouteEditorWindow>().Create(recording);}
        void OnEnable(){preview=new TrainingGuideView();EditorApplication.update+=Tick;}
        void OnDisable(){SaveDraft();preview?.Dispose();EditorApplication.update-=Tick;}
        void Tick()
        {
            if(pending&&EditorApplication.timeSinceStartup>=rebuildAt){Rebuild();Repaint();}
            if(dirty&&EditorApplication.timeSinceStartup>=draftAt){SaveDraft();draftAt=EditorApplication.timeSinceStartup+10;}
        }
        public void Create(RecordedRouteData recording)
        {
            if(!recording.complete)throw new InvalidOperationException("Stop the recording before creating an editable guide.");
            SaveDraft();raw=recording;definition=TrainingGuideStorage.Create(recording);
            ResetInteraction();undo.Clear();redo.Clear();dirty=true;Rebuild();
            status="Faithful recorded track: raw observations, heading and actual conditioner preserved. Wheel zoom / middle drag pan. Clearance validation is separate from ideal spline certification.";
        }
        public void LoadGuide(string path)
        {
            var next=TrainingGuideStorage.Read(path);var source=TrainingGuideStorage.Raw(next);SaveDraft();
            definition=next;raw=source;ResetInteraction();undo.Clear();redo.Clear();dirty=false;Rebuild();status="Loaded derived guide. Inspect exact error regions, or restore the authoritative recorded track. Raw source remains intact.";
        }
        public string SaveCopy(string directory=null)
        {
            Rebuild();string path=TrainingGuideStorage.SaveCopy(directory??TrainingGuideStorage.DefaultDirectory,definition);
            status=(fit.Ready?"Saved ready guide: ":"Saved DRAFT guide (not ready for live use): ")+path;dirty=false;return path;
        }
        void SaveDraft()
        {
            if(!dirty||definition==null)return;
            try{TrainingGuideStorage.Draft(TrainingGuideStorage.DefaultDirectory,definition);}
            catch(Exception e){status="Editor draft was not saved: "+e.Message;}
        }
        void Push()
        {
            var h=new History{name=definition.name,selected=selected,version=definition.version};foreach(var p in definition.controls)h.points.Add(p.Copy());undo.Push(h);redo.Clear();
            if(undo.Count>64){var keep=undo.ToArray();undo.Clear();for(int i=63;i>=0;i--)undo.Push(keep[i]);}
        }
        History Current()
        {var h=new History{name=definition.name,selected=selected,version=definition.version};foreach(var p in definition.controls)h.points.Add(p.Copy());return h;}
        public void UndoEdit(bool forward=false)
        {
            if(definition==null)return;CancelDrag();
            var from=forward?redo:undo;var to=forward?undo:redo;if(from.Count==0)return;
            to.Push(Current());var h=from.Pop();definition.controls=h.points;definition.name=h.name;definition.version=h.version;selected=h.selected;NormalizeSelection();Changed(true);
        }
        public void MovePoint(int index,float x,float z)
        {RequireIndex(index);Push();selected=index;RecordedTrackReconstruction.DetachPoint(definition,index);definition.controls[index].x=x;definition.controls[index].z=z;Changed(true);}
        public void SetHandles(int index,float heading,float incoming,float outgoing)
        {RequireIndex(index);Push();selected=index;RecordedTrackReconstruction.DetachPoint(definition,index);var p=definition.controls[index];p.headingRadians=heading;p.incoming=incoming;p.outgoing=outgoing;Changed(true);}
        public void InsertPoint(){if(!NormalizeSelection()||definition.controls.Count<2)return;Push();selected=TrainingRouteEditing.Split(definition,Math.Min(selected,definition.controls.Count-2),raw);Changed(true);}
        public void RemovePoint(){if(!NormalizeSelection())return;Push();CancelDrag();if(TrainingRouteEditing.Remove(definition,selected))selected=Math.Min(selected,definition.controls.Count-1);NormalizeSelection();Changed(true);}
        public void MarkPoint(int index,RouteMarker marker,string label)
        {RequireIndex(index);Push();selected=index;definition.controls[index].marker=marker;definition.controls[index].label=label;Changed(true);}
        void RequireIndex(int index){if(definition?.controls==null||index<0||index>=definition.controls.Count||definition.controls[index]==null)throw new ArgumentOutOfRangeException(nameof(index));}
        internal bool NormalizeSelection()
        {
            if(definition?.controls==null||definition.controls.Count==0){selected=-1;CancelDrag();return false;}
            selected=Mathf.Clamp(selected,0,definition.controls.Count-1);
            if(definition.controls[selected]==null){selected=definition.controls.FindIndex(p=>p!=null);if(selected<0){CancelDrag();return false;}}
            return true;
        }
        void ResetInteraction(){CancelDrag();selected=0;problem=-1;preview.FitWholeRoute();NormalizeSelection();}
        void CancelDrag(){if(drag!=0||panning){releasePointer=true;if(Event.current!=null){GUIUtility.hotControl=0;releasePointer=false;}}drag=0;panning=false;dragEdited=false;if(preview!=null)preview.FreezeProjection=false;}
        public void RestoreRecordedTrack(){if(!NormalizeSelection())return;Push();RecordedTrackReconstruction.Restore(definition,raw);Changed(true);status="Restored observed positions, headings and actual conditioner path. Maximum deviation from the raw polyline: "+(fit?.maximumRawDeviation??0).ToString("F4")+" m. Raw file unchanged.";}
        public void FitWholeRoute(){preview.FitWholeRoute();RefreshViewport();}
        public void ZoomSelected(){if(!NormalizeSelection())return;var p=definition.controls[selected];preview.ZoomTo(p.x,p.z,4);RefreshViewport();}
        public void NavigateProblem(int direction,bool zoom=true)
        {
            if(fit==null||fit.problems.Count==0){problem=-1;status="No failing regions in the current reconstruction.";return;}
            problem=problem<0?(direction<0?fit.problems.Count-1:0):(problem+direction+fit.problems.Count)%fit.problems.Count;SelectProblem(problem,zoom);
        }
        public void SelectProblem(int index,bool zoom=true)
        {
            if(fit==null||index<0||index>=fit.problems.Count)return;problem=index;var p=fit.problems[index];selected=p.fitted.control;NormalizeSelection();
            preview.SelectedProblem=problem;
            if(zoom){var first=fit.samples[p.first];var last=fit.samples[p.last];preview.ZoomTo((first.x+last.x)*.5f,(first.z+last.z)*.5f,Mathf.Max(4,TrainingRouteDefinition.Distance(first.x,first.z,last.x,last.z)*.65f));RefreshViewport();}Repaint();
        }
        void RefreshViewport(){if(fit!=null)preview.Rebuild(raw,definition,fit,footprints,(GuideViewLayer)layer,++revision);Repaint();}
        void Changed(bool now=false){dirty=pending=true;rebuildAt=EditorApplication.timeSinceStartup+.12;if(now)Rebuild();Repaint();}
        public void Rebuild()
        {
            NormalizeSelection();if(definition==null)return;
            try
            {
                fit=RouteFitter.Fit(definition,raw);footprints=new RouteFootprints(fit);footprints.AddRaw(raw);footprints.Compare(true);
                if(problem>=fit.problems.Count)problem=-1;preview.SelectedProblem=problem;
                pending=false;revision++;preview.Rebuild(raw,definition,fit,footprints,(GuideViewLayer)layer,revision);
            }
            catch(Exception e){fit=null;footprints=null;status="Fit error: "+e.Message;pending=false;}
        }
        public byte[] PreviewPng(GuideViewLayer mode=GuideViewLayer.Paths)
        {preview.Rebuild(raw,definition,fit,footprints,mode,++revision);return preview.Texture.EncodeToPNG();}
        internal void QueueValidationEvent(Event value)
        {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))throw new InvalidOperationException("GUI event injection is limited to the isolated validation project.");
            validationEvent=new Event(value);
        }
        void OnGUI()
        {
            var injected=Event.current.type==EventType.Repaint?validationEvent:null;if(injected!=null)validationEvent=null;DrawGUI();
            // Keep GUILayout on the host's genuine Layout/Repaint event. Only
            // the shared pointer handler receives the isolated test input.
            if(injected!=null&&fit!=null&&NormalizeSelection())
            {var previous=Event.current;try{Event.current=injected;HandlePointer();}finally{Event.current=previous;}}
        }
        void DrawGUI()
        {
            GuiDrawCount++;
            if(releasePointer){GUIUtility.hotControl=0;releasePointer=false;}
            NormalizeSelection();
            EditorGUILayout.LabelField("AURORA R-54 / NON-DESTRUCTIVE TRAINING ROUTE EDITOR",EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Open raw .route.json…",GUILayout.Width(175)))
            {
                string path=EditorUtility.OpenFilePanel("Open raw route",RouteRecordingStorage.DefaultDirectory,"json");
                if(path.Length>0){Try(()=>Create(RouteRecordingStorage.Read(path)));GUIUtility.ExitGUI();}
            }
            if(GUILayout.Button("Open corrected guide…",GUILayout.Width(180)))
            {string path=EditorUtility.OpenFilePanel("Open corrected guide",TrainingGuideStorage.DefaultDirectory,"json");if(path.Length>0){Try(()=>LoadGuide(path));GUIUtility.ExitGUI();}}
            string example="Assets/Experiments/IceResurfacer/Validation~/RouteRecording/Deterministic-fixture.route.json";
            if(GUILayout.Button("Labeled test recording",GUILayout.Width(165))){Try(()=>Create(RouteRecordingStorage.Read(example)));GUIUtility.ExitGUI();}
            using(new EditorGUI.DisabledScope(definition==null))
            {
                if(GUILayout.Button("Save corrected COPY",GUILayout.Width(160)))Try(()=>SaveCopy());
                if(GUILayout.Button("Use in Play / F5",GUILayout.Width(135)))
                {
                    var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();
                    if(!EditorApplication.isPlaying||game==null||game.RouteGuide==null)status="Enter Play Mode first, or save and load the guide through F6 > Training Guides.";
                    else {Rebuild();status=game.RouteGuide.SelectTrainingGuide(definition)?"Selected; F5 toggles advisory guidance.":game.RouteGuide.TrainingStatus;}
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Fit Whole Route"))FitWholeRoute();if(GUILayout.Button("Zoom to Selected Point"))ZoomSelected();
            if(GUILayout.Button("Previous Error"))NavigateProblem(-1);if(GUILayout.Button("Next Error"))NavigateProblem(1);
            using(new EditorGUI.DisabledScope(fit==null||fit.problems.Count==0))if(GUILayout.Button("Zoom to Error"))SelectProblem(problem<0?0:problem);
            GUILayout.Label("Wheel: cursor zoom | Middle drag: pan | "+preview.Zoom.ToString("F1")+"x",EditorStyles.miniLabel);EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();EditorGUI.BeginChangeCheck();
            preview.ShowRawPath=GUILayout.Toggle(preview.ShowRawPath,new GUIContent("Raw recorded path","Original observed rear-axle trajectory."),GUILayout.Width(160));
            preview.ShowGuidePath=GUILayout.Toggle(preview.ShowGuidePath,new GUIContent("Derived guide","Hide the guide to inspect the raw track where they coincide."),GUILayout.Width(150));
            preview.ShowConditionerPath=GUILayout.Toggle(preview.ShowConditionerPath,new GUIContent("Conditioner path","Actual recorded conditioner on observed spans; modeled on Bezier spans."),GUILayout.Width(180));
            if(EditorGUI.EndChangeCheck())RefreshViewport();EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(status,MessageType.Info);
            float top=GUILayoutUtility.GetLastRect().yMax+6;
            Rect map=new Rect(8,top,Mathf.Max(350,position.width-404),Mathf.Max(240,position.height-top-94));
            if(fit!=null)plot=preview.Draw(map,fit);else{plot=map;GUI.Box(map,"No usable fit. Open a route or correct the document fields.");}
            if(definition==null){GUI.Label(map,"Load any saved operator route. Nothing edits the raw .route.json file.");return;}
            if(!NormalizeSelection()){GUI.Label(map,"No usable control points. Open a recording or another guide; the raw file is unchanged.");return;}
            if(fit!=null){DrawControls();HandlePointer();}
            GUILayout.BeginArea(new Rect(position.width-388,top,376,position.height-top-8));scroll=GUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();string name=EditorGUILayout.TextField("Guide name",definition.name);
            if(EditorGUI.EndChangeCheck()){Push();definition.name=name;dirty=true;}
            EditorGUILayout.HelpBox(raw.provenance,MessageType.None);
            if(fit!=null)
            {
                EditorGUILayout.HelpBox(fit.Ready?(fit.IdealSplineCertified?"READY — smooth spline / clearance validated.":"GPS READY — recorded-track clearance validated. Ideal smooth spline NOT certified."):"DRAFT — inspect the actual failure regions before live use.",fit.Ready?MessageType.Info:MessageType.Warning);
                GUILayout.Label(fit.samples.Count+" fitted samples / "+fit.Length.ToString("F1")+" m\nMinimum radius "+fit.minimumRadius.ToString("F3")+" m (limit "+(definition.configuration.Wheelbase/Mathf.Tan(definition.configuration.MaxSteeringRadians)).ToString("F3")+
                    ")\nMax raw-polyline deviation "+fit.maximumRawDeviation.ToString("F3")+" m",EditorStyles.wordWrappedLabel);
                foreach(string issue in fit.issues)EditorGUILayout.HelpBox(issue,fit.Ready?MessageType.Info:MessageType.Warning);
                for(int i=0;i<fit.problems.Count;i++){var issue=fit.problems[i];if(GUILayout.Button(new GUIContent((i+1)+". "+issue.kind+" / "+issue.start.ToString("F2")+"–"+issue.end.ToString("F2")+" m",issue.reason)))SelectProblem(i);}
                if(problem>=0&&problem<fit.problems.Count)DrawProblem(fit.problems[problem]);
            }
            selected=EditorGUILayout.IntSlider("Control point",selected,0,definition.controls.Count-1);
            NormalizeSelection();var p=definition.controls[selected];GUILayout.Label("Raw source sequence #"+p.sourceSequence+" (read-only)",EditorStyles.miniLabel);
            GUILayout.Label(p.recordedTrack?"Observed track: engagement/pace come from the recording. Moving this point or its handles converts only adjacent sections to editable Bezier.":"Outgoing section: editable Bezier.",EditorStyles.wordWrappedMiniLabel);
            EditorGUI.BeginChangeCheck();var positionXZ=EditorGUILayout.Vector2Field("Position X / Z",new Vector2(p.x,p.z));
            float heading=EditorGUILayout.FloatField("Body heading °",p.headingRadians*Mathf.Rad2Deg),inc=EditorGUILayout.FloatField("Incoming handle m",p.incoming),outgoing=EditorGUILayout.FloatField("Outgoing handle m",p.outgoing);
            int direction=EditorGUILayout.Popup("Outgoing direction",p.direction>0?0:1,new[]{"Forward","Reverse"});
            bool cutting,flood;float speed;
            using(new EditorGUI.DisabledScope(p.recordedTrack))
            {cutting=EditorGUILayout.Toggle("Intended blade contact",p.cutting);flood=EditorGUILayout.Toggle("Intended flood water",p.flooding);speed=EditorGUILayout.Slider("Nominal pace m/s",p.nominalSpeed,.15f,1.5f);}
            var marker=(RouteMarker)EditorGUILayout.EnumFlagsField("Transition marker",p.marker);string label=EditorGUILayout.TextField("Marker label",p.label);
            if(EditorGUI.EndChangeCheck())
            {
                Push();if(positionXZ!=new Vector2(p.x,p.z)||Mathf.Abs(heading*Mathf.Deg2Rad-p.headingRadians)>.00001f||inc!=p.incoming||outgoing!=p.outgoing||p.direction!=(direction==0?1:-1))RecordedTrackReconstruction.DetachPoint(definition,selected);
                p.x=positionXZ.x;p.z=positionXZ.y;p.headingRadians=heading*Mathf.Deg2Rad;p.incoming=Mathf.Clamp(inc,.01f,30);p.outgoing=Mathf.Clamp(outgoing,.01f,30);
                p.direction=direction==0?1:-1;p.cutting=cutting;p.flooding=flood;p.nominalSpeed=speed;p.marker=marker;p.label=label;Changed(true);
            }
            EditorGUILayout.BeginHorizontal();if(GUILayout.Button("Insert after")){Try(InsertPoint);GUIUtility.ExitGUI();}using(new EditorGUI.DisabledScope(selected==0||selected==definition.controls.Count-1))if(GUILayout.Button("Remove")){RemovePoint();GUIUtility.ExitGUI();}EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();if(GUILayout.Button("Undo")){UndoEdit();GUIUtility.ExitGUI();}if(GUILayout.Button("Redo")){UndoEdit(true);GUIUtility.ExitGUI();}EditorGUILayout.EndHorizontal();
            if(GUILayout.Button("Restore faithful recorded track")){RestoreRecordedTrack();GUIUtility.ExitGUI();}
            int next=EditorGUILayout.Popup("Preview layer",layer,new[]{"Raw + fitted paths","Intended blade + water footprint","Recorded blade footprint","Recorded water footprint","Intended areas missed by recording","Excess recorded revisits"});
            if(next!=layer){layer=next;preview.Rebuild(raw,definition,fit,footprints,(GuideViewLayer)layer,++revision);}
            if(footprints!=null)GUILayout.Label("20 cm diagnostic geometry\nPlan cut "+(footprints.PlannedBladeCells*.04f).ToString("F1")+" m² / water "+(footprints.PlannedWaterCells*.04f).ToString("F1")+" m²\nPlan vs recorded missed cut "+(footprints.MissedBladeCells*.04f).ToString("F1")+" m²\nActual ice-condition/scoring data is unchanged.",EditorStyles.wordWrappedLabel);
            GUILayout.Label("Click a visible control or an actual error location. Drag the square or handles to edit neighboring curves. Restore recorded track repairs from the source instead of accumulating positional changes. Mark pass transitions for explicit rejoin contexts.",EditorStyles.wordWrappedLabel);
            GUILayout.EndScrollView();GUILayout.EndArea();
            GUI.Label(new Rect(10,position.height-76,Mathf.Max(400,position.width-410),68),new GUIContent("Raw: gray | Guide: cyan | Conditioner: gold | Curvature: magenta | Boundary: red | Observed maneuver: amber.\nWheel zoom / middle drag pan. Shift-click guide inserts an anchor; Ctrl+Z / Ctrl+Shift+Z undo/redo.\nRaw observations remain immutable. Moving an anchor/handle edits only its adjacent derived curves.","Gray is the original vehicle observation; cyan is the derived driving guide. Gold uses the recorded conditioner trajectory on recorded sections and the modeled conditioner on edited Bezier sections. Red marks exact failed clearance samples; magenta marks excessive derived curvature. Click a problem to inspect its raw comparison and responsible contact point."),EditorStyles.wordWrappedMiniLabel);
        }
        void Try(Action action){try{action();}catch(ExitGUIException){throw;}catch(Exception e){status=e.Message;}}
        void DrawProblem(RouteProblemRegion issue)
        {
            var p=issue.fitted;var r=issue.raw;float required=definition.configuration.Wheelbase/Mathf.Tan(definition.configuration.MaxSteeringRadians);
            EditorGUILayout.HelpBox(issue.reason,issue.Blocking?MessageType.Warning:MessageType.Info);
            GUILayout.Label("Route "+issue.start.ToString("F3")+"–"+issue.end.ToString("F3")+" m | source #"+issue.sourceFirst+"–"+issue.sourceLast+" | controls "+issue.controlFirst+"–"+issue.controlLast+
                "\nRaw track match #"+p.sourceSequence+"→"+(p.sourceSequence+1)+" at "+p.sourceFraction.ToString("F3")+": "+r.vehicleX.ToString("F4")+", "+r.vehicleZ.ToString("F4")+" / "+(r.vehicleHeadingRadians*Mathf.Rad2Deg).ToString("F2")+"°"+
                "\nGuide vehicle "+p.x.ToString("F4")+", "+p.z.ToString("F4")+" / "+(p.heading*Mathf.Rad2Deg).ToString("F2")+"°"+
                "\nRaw conditioner "+r.conditionerX.ToString("F4")+", "+r.conditionerZ.ToString("F4")+" / guide "+p.conditionerX.ToString("F4")+", "+p.conditionerZ.ToString("F4")+
                "\nDeviation "+issue.maximumDeviation.ToString("F4")+" m | radius "+issue.minimumRadius.ToString("F3")+" / required "+required.ToString("F3")+" m"+
                (p.boundaryError?"\nContact point "+p.boundaryContact.PointX.ToString("F4")+", "+p.boundaryContact.PointZ.ToString("F4"):"\nNo collision here; derived curvature is the failing constraint.")+
                "\nRaw production body check: "+(issue.rawVehicleClear?"PASS":"FAIL")+"; conditioner footprint: "+(issue.rawConditionerClear?"PASS":"FAIL"),EditorStyles.wordWrappedLabel);
            EditorGUILayout.HelpBox(issue.suggestion,MessageType.None);
        }
        void UpdateVisibleControls()
        {
            visibleControls.Clear();if(!NormalizeSelection())return;var cells=new HashSet<long>();
            for(int i=0;i<definition.controls.Count;i++)
            {
                var p=definition.controls[i];if(p==null)continue;var at=preview.ToGui(plot,p.x,p.z);if(!plot.Contains(at))continue;
                int stride=preview.Zoom<1.5f?4:preview.Zoom<3?2:1;
                if(i!=selected&&p.marker==RouteMarker.None&&i%stride!=0)continue;
                float cell=preview.Zoom<1.5f?40:24;
                long key=((long)Mathf.FloorToInt(at.x/cell)<<32)^(uint)Mathf.FloorToInt(at.y/cell);
                if(i==selected||preview.Zoom>=3||cells.Add(key))visibleControls.Add(i);
            }
        }
        void DrawControls()
        {
            if(!NormalizeSelection())return;UpdateVisibleControls();GUI.BeginGroup(plot);var canvas=new Rect(0,0,plot.width,plot.height);
            foreach(int i in visibleControls)
            {
                var p=definition.controls[i];Vector2 at=preview.ToGui(canvas,p.x,p.z);
                EditorGUI.DrawRect(new Rect(at.x-3,at.y-3,6,6),i==selected?Color.white:new Color(.55f,.75f,.8f));
                if(p.marker!=RouteMarker.None&&(preview.Zoom>=3||i==selected))GUI.Label(new Rect(at.x+5,at.y-15,100,20),"#"+i+" "+p.marker,EditorStyles.miniBoldLabel);
            }
            var active=definition.controls[selected];var point=preview.ToGui(canvas,active.x,active.z);
            int previousDirection=selected>0?definition.controls[selected-1].direction:active.direction;
            var incoming=preview.ToGui(canvas,active.x-Mathf.Sin(active.headingRadians)*active.incoming*previousDirection,active.z-Mathf.Cos(active.headingRadians)*active.incoming*previousDirection);
            var outgoing=preview.ToGui(canvas,active.x+Mathf.Sin(active.headingRadians)*active.outgoing*active.direction,active.z+Mathf.Cos(active.headingRadians)*active.outgoing*active.direction);
            if(canvas.Contains(point))
            {
            GuiLine(point,incoming,new Color(1,.7f,.3f));GuiLine(point,outgoing,new Color(.4f,1,.7f));
            EditorGUI.DrawRect(new Rect(incoming.x-5,incoming.y-5,10,10),new Color(1,.7f,.3f));EditorGUI.DrawRect(new Rect(outgoing.x-5,outgoing.y-5,10,10),new Color(.4f,1,.7f));
            GUI.Label(new Rect(point.x+7,point.y+7,90,24),"CONTROL #"+selected,EditorStyles.whiteMiniLabel);
            }
            GUI.EndGroup();
        }
        void HandlePointer()
        {
            if(!NormalizeSelection())return;
            int pointer=GUIUtility.GetControlID(713521,FocusType.Passive,plot);
            var e=Event.current;if(e.type==EventType.KeyDown&&e.control&&e.keyCode==KeyCode.Z){UndoEdit(e.shift);e.Use();return;}
            if(e.type==EventType.ScrollWheel&&plot.Contains(e.mousePosition)){preview.ZoomAt(plot,e.mousePosition,e.delta.y);RefreshViewport();e.Use();return;}
            if(e.type==EventType.MouseDown&&e.button==2&&plot.Contains(e.mousePosition)){panning=true;GUIUtility.hotControl=pointer;e.Use();return;}
            if(panning&&e.type==EventType.MouseDrag){if(plot.Contains(e.mousePosition)){preview.Pan(plot,e.delta);RefreshViewport();}e.Use();return;}
            if(panning&&e.type==EventType.MouseUp){CancelDrag();e.Use();return;}
            if(e.type==EventType.MouseDown&&e.button==0&&plot.Contains(e.mousePosition))
            {
                float activeDistance=Vector2.Distance(e.mousePosition,preview.ToGui(plot,definition.controls[selected].x,definition.controls[selected].z));
                drag=PickDrag(e.mousePosition,plot,out int picked);
                if(drag!=2&&drag!=3&&activeDistance>5&&!e.shift&&fit!=null)
                {
                    float best=9;int error=-1;
                    for(int region=0;region<fit.problems.Count;region++)for(int i=fit.problems[region].first;i<=fit.problems[region].last;i++)
                    {var p=fit.samples[i];float delta=Vector2.Distance(e.mousePosition,preview.ToGui(plot,p.x,p.z));if(p.boundaryError)delta=Mathf.Min(delta,Vector2.Distance(e.mousePosition,preview.ToGui(plot,p.boundaryContact.PointX,p.boundaryContact.PointZ)));if(delta<best){best=delta;error=region;}}
                    if(error>=0){drag=0;SelectProblem(error);e.Use();return;}
                }
                if(drag==1)selected=picked;
                if(drag==0)
                {
                    if(e.shift&&fit!=null)
                    {
                        float distance=float.PositiveInfinity;int segment=0;
                        foreach(var q in fit.samples){float test=Vector2.Distance(e.mousePosition,preview.ToGui(plot,q.x,q.z));if(test<distance){distance=test;segment=q.control;}}
                        if(distance<18){selected=segment;InsertPoint();}e.Use();return;
                    }
                }
                if(drag!=0){dragEdited=false;preview.FreezeProjection=true;GUIUtility.hotControl=pointer;e.Use();}
            }
            if(e.type==EventType.MouseDrag&&drag!=0)
            {
                if(!plot.Contains(e.mousePosition)){e.Use();return;}
                if(!NormalizeSelection()){e.Use();return;}
                if(!dragEdited){Push();RecordedTrackReconstruction.DetachPoint(definition,selected);dragEdited=true;}
                var p=definition.controls[selected];var world=preview.FromGui(plot,e.mousePosition);
                if(drag==1){p.x=world.x;p.z=world.y;}
                else
                {
                    float dx=world.x-p.x,dz=world.y-p.z,length=Mathf.Clamp(Mathf.Sqrt(dx*dx+dz*dz),.01f,30);
                    int sign=drag==2?-(selected>0?definition.controls[selected-1].direction:p.direction):p.direction;
                    p.headingRadians=Mathf.Atan2(dx*sign,dz*sign);if(drag==2)p.incoming=length;else p.outgoing=length;
                }
                Changed();e.Use();
            }
            if(e.type==EventType.MouseUp&&drag!=0){bool changed=dragEdited;drag=0;dragEdited=false;GUIUtility.hotControl=0;preview.FreezeProjection=false;if(changed)Changed(true);e.Use();if(changed)GUIUtility.ExitGUI();}
        }
        int PickDrag(Vector2 mouse,Rect canvas,out int point)
        {
            point=-1;if(!NormalizeSelection())return 0;var p=definition.controls[selected];point=selected;
            if(Vector2.Distance(mouse,preview.ToGui(canvas,p.x,p.z))<5)return 1;
            float best=14;point=-1;
            UpdateVisibleControls();foreach(int i in visibleControls)
            {var q=definition.controls[i];float distance=Vector2.Distance(mouse,preview.ToGui(canvas,q.x,q.z));if(distance<best){best=distance;point=i;}}
            // Small tangents often lie within the old 11 px target of the node.
            // Give the visible node center priority; then resolve handle centers.
            if(best<5)return 1;
            int previous=selected>0?definition.controls[selected-1].direction:p.direction;
            var inc=preview.ToGui(canvas,p.x-Mathf.Sin(p.headingRadians)*p.incoming*previous,p.z-Mathf.Cos(p.headingRadians)*p.incoming*previous);
            var outgoing=preview.ToGui(canvas,p.x+Mathf.Sin(p.headingRadians)*p.outgoing*p.direction,p.z+Mathf.Cos(p.headingRadians)*p.outgoing*p.direction);
            if(Vector2.Distance(mouse,inc)<6)return 2;if(Vector2.Distance(mouse,outgoing)<6)return 3;
            return point>=0?1:0;
        }
        internal bool ControlCentersAreSelectable()
        {
            var canvas=new Rect(0,0,680,574);
            for(int i=0;i<definition.controls.Count;i++)
            {selected=i;var p=definition.controls[i];if(PickDrag(preview.ToGui(canvas,p.x,p.z),canvas,out int at)!=1||at!=i)return false;}
            selected=0;return true;
        }
        static void GuiLine(Vector2 a,Vector2 b,Color color)
        {
            var matrix=GUI.matrix;var tint=GUI.color;GUI.color=color;
            float angle=Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg;GUIUtility.RotateAroundPivot(angle,a);
            GUI.DrawTexture(new Rect(a.x,a.y,Vector2.Distance(a,b),1.5f),Texture2D.whiteTexture);GUI.matrix=matrix;GUI.color=tint;
        }
    }
}
