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
            public string name;public int selected;public readonly List<RouteControlPoint> points=new List<RouteControlPoint>();
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
        string status="Open an operator .route.json, or the labeled validation fixture. Raw files are read-only.";
        public TrainingRouteDefinition Document => definition;
        public RouteFit Fit => fit;
        public int Selected => selected;
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
            TrainingRouteEditing.ConstrainTangents(definition,80,true);selected=0;undo.Clear();redo.Clear();dirty=true;Rebuild();
            status="Derived guide created. Raw gray / fitted cyan / modeled conditioner gold. Red sections must be repaired before live use.";
        }
        public void LoadGuide(string path)
        {
            var next=TrainingGuideStorage.Read(path);var source=TrainingGuideStorage.Raw(next);SaveDraft();
            definition=next;raw=source;selected=0;undo.Clear();redo.Clear();dirty=false;Rebuild();status="Loaded corrected guide; embedded raw source remains intact.";
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
            var h=new History{name=definition.name,selected=selected};foreach(var p in definition.controls)h.points.Add(p.Copy());undo.Push(h);redo.Clear();
            if(undo.Count>64){var keep=undo.ToArray();undo.Clear();for(int i=63;i>=0;i--)undo.Push(keep[i]);}
        }
        History Current()
        {var h=new History{name=definition.name,selected=selected};foreach(var p in definition.controls)h.points.Add(p.Copy());return h;}
        public void UndoEdit(bool forward=false)
        {
            var from=forward?redo:undo;var to=forward?undo:redo;if(from.Count==0)return;
            to.Push(Current());var h=from.Pop();definition.controls=h.points;definition.name=h.name;selected=h.selected;Changed(true);
        }
        public void MovePoint(int index,float x,float z)
        {Push();selected=index;definition.controls[index].x=x;definition.controls[index].z=z;Changed(true);}
        public void SetHandles(int index,float heading,float incoming,float outgoing)
        {Push();selected=index;var p=definition.controls[index];p.headingRadians=heading;p.incoming=incoming;p.outgoing=outgoing;Changed(true);}
        public void InsertPoint(){Push();selected=TrainingRouteEditing.Split(definition,Math.Min(selected,definition.controls.Count-2));Changed(true);}
        public void RemovePoint(){Push();if(TrainingRouteEditing.Remove(definition,selected))selected=Math.Min(selected,definition.controls.Count-1);Changed(true);}
        public void MarkPoint(int index,RouteMarker marker,string label)
        {Push();selected=index;definition.controls[index].marker=marker;definition.controls[index].label=label;Changed(true);}
        void Changed(bool now=false){dirty=pending=true;rebuildAt=EditorApplication.timeSinceStartup+.12;if(now)Rebuild();Repaint();}
        public void Rebuild()
        {
            if(definition==null)return;
            try
            {
                fit=RouteFitter.Fit(definition,raw);footprints=new RouteFootprints(fit);footprints.AddRaw(raw);footprints.Compare(true);
                pending=false;revision++;preview.Rebuild(raw,definition,fit,footprints,(GuideViewLayer)layer,revision);
            }
            catch(Exception e){status="Fit error: "+e.Message;pending=false;}
        }
        public byte[] PreviewPng(GuideViewLayer mode=GuideViewLayer.Paths)
        {preview.Rebuild(raw,definition,fit,footprints,mode,++revision);return preview.Texture.EncodeToPNG();}
        void OnGUI()
        {
            EditorGUILayout.LabelField("AURORA R-54 / NON-DESTRUCTIVE TRAINING ROUTE EDITOR",EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Open raw .route.json…",GUILayout.Width(175)))
            {
                string path=EditorUtility.OpenFilePanel("Open raw route",RouteRecordingStorage.DefaultDirectory,"json");
                if(path.Length>0)Try(()=>Create(RouteRecordingStorage.Read(path)));
            }
            if(GUILayout.Button("Open corrected guide…",GUILayout.Width(180)))
            {string path=EditorUtility.OpenFilePanel("Open corrected guide",TrainingGuideStorage.DefaultDirectory,"json");if(path.Length>0)Try(()=>LoadGuide(path));}
            string example="Assets/Experiments/IceResurfacer/Validation~/RouteRecording/Deterministic-fixture.route.json";
            if(GUILayout.Button("Labeled test recording",GUILayout.Width(165)))Try(()=>Create(RouteRecordingStorage.Read(example)));
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
            EditorGUILayout.HelpBox(status,MessageType.Info);
            float top=GUILayoutUtility.GetLastRect().yMax+6;
            Rect map=new Rect(8,top,Mathf.Max(350,position.width-364),Mathf.Max(240,position.height-top-68));plot=preview.Draw(map,fit);
            if(definition==null){GUI.Label(map,"Load any saved operator route. Nothing edits the raw .route.json file.");return;}
            DrawControls();HandlePointer();
            GUILayout.BeginArea(new Rect(position.width-348,top,336,position.height-top-8));scroll=GUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();string name=EditorGUILayout.TextField("Guide name",definition.name);
            if(EditorGUI.EndChangeCheck()){Push();definition.name=name;dirty=true;}
            EditorGUILayout.HelpBox(raw.provenance,MessageType.None);
            if(fit!=null)
            {
                EditorGUILayout.HelpBox(fit.Ready?"READY — turning radius and body/conditioner clearance checks pass.":"DRAFT — red sections are not available for live guidance.",fit.Ready?MessageType.Info:MessageType.Warning);
                GUILayout.Label(fit.samples.Count+" fitted samples / "+fit.Length.ToString("F1")+" m\nMinimum radius "+fit.minimumRadius.ToString("F3")+" m (limit "+(definition.configuration.Wheelbase/Mathf.Tan(definition.configuration.MaxSteeringRadians)).ToString("F3")+
                    ")\nMax raw-sample deviation "+fit.maximumRawDeviation.ToString("F3")+" m",EditorStyles.wordWrappedLabel);
                foreach(string issue in fit.issues)EditorGUILayout.HelpBox(issue,MessageType.Warning);
            }
            selected=EditorGUILayout.IntSlider("Control point",selected,0,definition.controls.Count-1);
            var p=definition.controls[selected];GUILayout.Label("Raw source sequence #"+p.sourceSequence+" (read-only)",EditorStyles.miniLabel);
            EditorGUI.BeginChangeCheck();var positionXZ=EditorGUILayout.Vector2Field("Position X / Z",new Vector2(p.x,p.z));
            float heading=EditorGUILayout.FloatField("Body heading °",p.headingRadians*Mathf.Rad2Deg),inc=EditorGUILayout.FloatField("Incoming handle m",p.incoming),outgoing=EditorGUILayout.FloatField("Outgoing handle m",p.outgoing);
            int direction=EditorGUILayout.Popup("Outgoing direction",p.direction>0?0:1,new[]{"Forward","Reverse"});
            bool cutting=EditorGUILayout.Toggle("Intended blade contact",p.cutting),flood=EditorGUILayout.Toggle("Intended flood water",p.flooding);
            float speed=EditorGUILayout.Slider("Nominal pace m/s",p.nominalSpeed,.15f,1.5f);
            var marker=(RouteMarker)EditorGUILayout.EnumFlagsField("Transition marker",p.marker);string label=EditorGUILayout.TextField("Marker label",p.label);
            if(EditorGUI.EndChangeCheck())
            {
                Push();p.x=positionXZ.x;p.z=positionXZ.y;p.headingRadians=heading*Mathf.Deg2Rad;p.incoming=Mathf.Clamp(inc,.01f,30);p.outgoing=Mathf.Clamp(outgoing,.01f,30);
                p.direction=direction==0?1:-1;p.cutting=cutting;p.flooding=flood;p.nominalSpeed=speed;p.marker=marker;p.label=label;Changed(true);
            }
            EditorGUILayout.BeginHorizontal();if(GUILayout.Button("Insert after"))InsertPoint();using(new EditorGUI.DisabledScope(selected==0||selected==definition.controls.Count-1))if(GUILayout.Button("Remove"))RemovePoint();EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();if(GUILayout.Button("Undo"))UndoEdit();if(GUILayout.Button("Redo"))UndoEdit(true);EditorGUILayout.EndHorizontal();
            if(GUILayout.Button("Constrain fit within 12 cm corridor"))
            {Push();TrainingRouteEditing.ConstrainTangents(definition,80,true);Changed(true);status="Adjusted derived handles/headings and controls by at most 12 cm. Check red issues and coverage; raw samples are unchanged.";}
            int next=EditorGUILayout.Popup("Preview layer",layer,new[]{"Raw + fitted paths","Intended blade + water footprint","Recorded blade footprint","Recorded water footprint","Intended areas missed by recording","Excess recorded revisits"});
            if(next!=layer){layer=next;preview.Rebuild(raw,definition,fit,footprints,(GuideViewLayer)layer,++revision);}
            if(footprints!=null)GUILayout.Label("20 cm diagnostic geometry\nPlan cut "+(footprints.PlannedBladeCells*.04f).ToString("F1")+" m² / water "+(footprints.PlannedWaterCells*.04f).ToString("F1")+" m²\nPlan vs recorded missed cut "+(footprints.MissedBladeCells*.04f).ToString("F1")+" m²\nActual ice-condition/scoring data is unchanged.",EditorStyles.wordWrappedLabel);
            GUILayout.Label("Click a control, drag its square or tangent circles. Shift-click a curve to insert a point. Gray = raw; cyan = fitted vehicle; gold = modeled conditioner; red = constraint failure. Mark significant pass transitions to create explicit rejoin contexts.",EditorStyles.wordWrappedLabel);
            GUILayout.EndScrollView();GUILayout.EndArea();
            GUI.Label(new Rect(10,position.height-48,Mathf.Max(400,position.width-370),42),"Raw samples remain immutable. Widths: blade "+definition.configuration.ConditionerWidth.ToString("F3")+" m; flood "+definition.configuration.WaterDistributionWidth.ToString("F2")+" m + physical fringe.\nCtrl+Z / Ctrl+Shift+Z undo/redo. Saved guides and drafts live in "+TrainingGuideStorage.DefaultDirectory,EditorStyles.miniLabel);
        }
        void Try(Action action){try{action();}catch(Exception e){status=e.Message;}}
        void DrawControls()
        {
            if(definition==null)return;
            for(int i=0;i<definition.controls.Count;i++)
            {
                var p=definition.controls[i];Vector2 at=preview.ToGui(plot,p.x,p.z);
                EditorGUI.DrawRect(new Rect(at.x-3,at.y-3,6,6),i==selected?Color.white:new Color(.55f,.75f,.8f));
                if(p.marker!=RouteMarker.None)GUI.Label(new Rect(at.x+5,at.y-15,65,20),"#"+i+" "+p.marker,EditorStyles.miniBoldLabel);
            }
            var active=definition.controls[selected];var point=preview.ToGui(plot,active.x,active.z);
            int previousDirection=selected>0?definition.controls[selected-1].direction:active.direction;
            var incoming=preview.ToGui(plot,active.x-Mathf.Sin(active.headingRadians)*active.incoming*previousDirection,active.z-Mathf.Cos(active.headingRadians)*active.incoming*previousDirection);
            var outgoing=preview.ToGui(plot,active.x+Mathf.Sin(active.headingRadians)*active.outgoing*active.direction,active.z+Mathf.Cos(active.headingRadians)*active.outgoing*active.direction);
            GuiLine(point,incoming,new Color(1,.7f,.3f));GuiLine(point,outgoing,new Color(.4f,1,.7f));
            EditorGUI.DrawRect(new Rect(incoming.x-5,incoming.y-5,10,10),new Color(1,.7f,.3f));EditorGUI.DrawRect(new Rect(outgoing.x-5,outgoing.y-5,10,10),new Color(.4f,1,.7f));
            GUI.Label(new Rect(point.x+7,point.y+7,90,24),"CONTROL #"+selected,EditorStyles.whiteMiniLabel);
        }
        void HandlePointer()
        {
            int pointer=GUIUtility.GetControlID(713521,FocusType.Passive,plot);
            var e=Event.current;if(e.type==EventType.KeyDown&&e.control&&e.keyCode==KeyCode.Z){UndoEdit(e.shift);e.Use();return;}
            if(e.type==EventType.MouseDown&&e.button==0&&plot.Contains(e.mousePosition))
            {
                drag=PickDrag(e.mousePosition,plot,out int picked);if(drag==1)selected=picked;
                if(drag==0)
                {
                    if(e.shift&&fit!=null)
                    {
                        float distance=float.PositiveInfinity;int segment=0;
                        foreach(var q in fit.samples){float test=Vector2.Distance(e.mousePosition,preview.ToGui(plot,q.x,q.z));if(test<distance){distance=test;segment=q.control;}}
                        if(distance<18){selected=segment;InsertPoint();}e.Use();return;
                    }
                }
                if(drag!=0){Push();preview.FreezeProjection=true;GUIUtility.hotControl=pointer;e.Use();}
            }
            if(e.type==EventType.MouseDrag&&drag!=0)
            {
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
            if(e.type==EventType.MouseUp&&drag!=0){drag=0;GUIUtility.hotControl=0;preview.FreezeProjection=false;Changed(true);e.Use();}
        }
        int PickDrag(Vector2 mouse,Rect canvas,out int point)
        {
            var p=definition.controls[selected];point=selected;
            if(Vector2.Distance(mouse,preview.ToGui(canvas,p.x,p.z))<5)return 1;
            float best=14;point=-1;
            for(int i=0;i<definition.controls.Count;i++)
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
