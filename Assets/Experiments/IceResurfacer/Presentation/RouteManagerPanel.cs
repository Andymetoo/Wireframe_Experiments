using System;
using System.IO;
using System.Collections.Generic;
using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    public sealed class RouteManagerPanel:IDisposable
    {
        readonly IceRouteGuide owner;
        readonly RouteLibraryCatalog catalog=new RouteLibraryCatalog();
        readonly RouteRecordingPreview rawPreview=new RouteRecordingPreview();
        readonly TrainingGuideView guidePreview=new TrainingGuideView();
        RouteLibraryCatalog.Entry selected;
        Vector2 scroll;string rename="",confirmation="",status="Choose a saved route, then Use as GPS.";
        GUIStyle title,text,small,button,row,input;
        bool previewDirty=true;
        bool textEditing;
        public bool TextEditing=>textEditing||confirmation.Length>0;
        public string SelectedPath=>selected?.path;
        public int SavedCount=>catalog.entries.Count;
        public readonly List<string> LayoutIssues=new List<string>();
        public static Rect ControlRect(string name)
        {
            switch(name)
            {
                case "Close":return new Rect(1320,70,170,50);
                case "Name":return new Rect(110,188,680,48);
                case "Record":return new Rect(110,250,325,66);
                case "StopSave":return new Rect(450,250,340,66);
                case "SaveCopy":return new Rect(110,324,680,52);
                case "Load":return new Rect(110,395,525,52);
                case "Refresh":return new Rect(650,395,140,52);
                case "UseGps":return new Rect(830,585,315,64);
                case "ToggleGps":return new Rect(1160,585,320,64);
                case "Edit":return new Rect(830,665,200,52);
                case "Rename":return new Rect(1045,665,205,52);
                case "Delete":return new Rect(1265,665,215,52);
                case "Advanced":return new Rect(110,824,410,44);
                case "Folder":return new Rect(540,824,410,44);
                case "Drive":return new Rect(1070,824,410,44);
                default:throw new ArgumentException(name);
            }
        }
        public RouteManagerPanel(IceRouteGuide guide){owner=guide;}
        public void Refresh()
        {
            string path=selected?.path;
            try{catalog.Refresh(owner.Library,owner.GuideDirectory);selected=catalog.entries.Find(e=>e.path==(path??owner.LastSavedPath));previewDirty=true;}
            catch(Exception e){status="Couldn't read saved routes: "+e.Message;}
        }
        public bool Select(string path)
        {
            var entry=catalog.entries.Find(e=>e.path==path);if(entry==null)return false;
            selected=entry;previewDirty=true;confirmation="";status=entry.Valid?"Selected: "+entry.name+" — choose Use as GPS.":entry.error;return entry.Valid;
        }
        void Styles()
        {
            if(title!=null)return;
            title=new GUIStyle(GUI.skin.label){fontSize=34,fontStyle=FontStyle.Bold};title.normal.textColor=Color.white;
            text=new GUIStyle(GUI.skin.label){fontSize=27,wordWrap=true};text.normal.textColor=new Color(.91f,.95f,1);
            small=new GUIStyle(text){fontSize=23};button=new GUIStyle(GUI.skin.button){fontSize=26,wordWrap=true};
            input=new GUIStyle(GUI.skin.textField){fontSize=27};
            row=new GUIStyle(GUI.skin.button){fontSize=25,alignment=TextAnchor.MiddleLeft,wordWrap=true,padding=new RectOffset(16,12,7,7)};
        }
        void Measure(Rect rect,string content,GUIStyle style)
        {if(Event.current.type==EventType.Repaint&&style.CalcHeight(new GUIContent(content),rect.width)>rect.height+1)LayoutIssues.Add("Route Manager: "+content+" requires "+style.CalcHeight(new GUIContent(content),rect.width)+" px; has "+rect.height);}
        bool Button(string key,string label){Rect rect=ControlRect(key);Measure(rect,label,button);bool old=GUI.enabled;GUI.enabled=old&&confirmation.Length==0;bool clicked=GUI.Button(rect,label,button);GUI.enabled=old;if(clicked)GUI.FocusControl(null);return clicked;}
        public void Draw(Action advanced)
        {
            Styles();var tint=GUI.color;GUI.color=new Color(.025f,.045f,.065f,.99f);GUI.DrawTexture(new Rect(76,48,1448,840),Texture2D.whiteTexture);GUI.color=tint;
            if(Event.current.type==EventType.Repaint)LayoutIssues.Clear();
            GUI.enabled=confirmation.Length==0;
            GUI.Label(new Rect(110,67,1170,52),"ROUTE MANAGER  /  NUMPAD 8",title);
            if(Button("Close","CLOSE")){owner.ToggleRecordingPanel();return;}
            GUI.Label(new Rect(110,128,1370,45),"GPS: "+owner.ActiveRouteName+"  /  "+(owner.Visible?"SHOWN":"HIDDEN"),text);
            GUI.SetNextControlName("RouteManagerName");owner.RecordingName=GUI.TextField(ControlRect("Name"),owner.RecordingName,80,input);
            GUI.enabled=owner.CanRecord&&!owner.Recorder.Recording&&confirmation.Length==0;
            if(Button("Record","RECORD NEW ROUTE")){bool started=owner.StartRecording();status=owner.RecordingStatus;if(started)owner.ToggleRecordingPanel();}
            GUI.enabled=owner.Recorder.Recording&&confirmation.Length==0;
            if(Button("StopSave","STOP & SAVE")){owner.StopAndSaveRecording();Refresh();status=owner.RecordingStatus;}
            GUI.enabled=(owner.Recorder.Data!=null||selected?.Valid==true)&&!owner.Recorder.Recording&&confirmation.Length==0;
            if(Button("SaveCopy",owner.HasUnsavedRecording?"SAVE RECORDING":"SAVE AS NEW COPY"))
            {
                try{if(owner.HasUnsavedRecording||selected==null){bool saved=owner.SaveRecording();status=owner.RecordingStatus;if(!saved)return;}else{string path=RouteLibraryCatalog.SaveCopy(selected,owner.Library,owner.GuideDirectory);Refresh();Select(path);status="Saved a new copy. Existing routes remain unchanged.";}Refresh();}
                catch(Exception e){status="Save failed: "+e.Message;}
            }
            GUI.enabled=confirmation.Length==0;
            if(Button("Load","LOAD SAVED ROUTE")){Refresh();scroll=Vector2.zero;status="Select a route below, then Use as GPS.";}
            if(Button("Refresh","REFRESH"))Refresh();
            scroll=GUI.BeginScrollView(new Rect(110,460,680,265),scroll,new Rect(0,0,650,Mathf.Max(250,catalog.entries.Count*92+130)));
            for(int i=0;i<catalog.entries.Count;i++)
            {
                var e=catalog.entries[i];GUI.backgroundColor=e==selected?new Color(1,.25f,.75f):Color.white;
                string name=e.name.Length>39?e.name.Substring(0,37)+"…":e.name;
                string caption=(e==selected?"●  ":"")+name+"\n"+e.date+"  /  about "+e.Length.ToString("F0")+" m";Measure(new Rect(0,i*92,645,84),caption,row);
                if(GUI.Button(new Rect(0,i*92,645,84),caption,row)){GUI.FocusControl(null);Select(e.path);}
            }
            GUI.backgroundColor=Color.white;float builtin=catalog.entries.Count*92;
            if(GUI.Button(new Rect(0,builtin,645,56),"Original reference  /  built in",row)){GUI.FocusControl(null);owner.UseOriginalReference();selected=null;previewDirty=true;}
            if(GUI.Button(new Rect(0,builtin+64,645,56),"Labeled test route  /  demo",row)){GUI.FocusControl(null);owner.SelectTestFixture();selected=null;previewDirty=true;}
            GUI.EndScrollView();
            if(catalog.entries.Count==0)GUI.Label(new Rect(130,635,620,72),"No saved routes yet. Record, then Stop & Save.",small);
            DrawPreview();
            GUI.enabled=selected!=null&&selected.Valid&&!owner.Recorder.Recording&&confirmation.Length==0;
            if(Button("UseGps","USE AS GPS")){status=owner.UseSavedRouteAsGps(selected.path)?"Loaded: "+owner.ActiveRouteName+" — GPS Ready":owner.TrainingStatus;}
            GUI.enabled=confirmation.Length==0;
            if(Button("ToggleGps",(owner.Visible?"HIDE":"SHOW")+" GPS  [NUM 7]")){owner.Toggle();status=owner.TrainingStatus;}
            GUI.enabled=selected!=null&&selected.Valid&&confirmation.Length==0;
            if(Button("Edit","EDIT ROUTE"))status=owner.EditSavedRoute(selected.path);
            if(Button("Rename","RENAME")){confirmation="rename";rename=selected.name;}
            if(Button("Delete","DELETE"))confirmation="delete";
            GUI.enabled=confirmation.Length==0;
            string message=owner.Recorder.Recording?"● RECORDING  /  Numpad 9 stops and saves":owner.HasUnsavedRecording?"RECORDING STOPPED — press Save Recording above":status;
            GUI.Label(new Rect(110,746,1370,69),message,small);
            Measure(new Rect(110,746,1370,69),message,small);
            if(Button("Advanced","DIAGNOSTICS / ADVANCED"))advanced();
            if(Button("Folder","OPEN ROUTES FOLDER")){try{owner.OpenRecordingsFolder();}catch(Exception e){status="Couldn't open folder: "+e.Message;}}
            if(Button("Drive","CLOSE & DRIVE"))owner.ToggleRecordingPanel();
            if(confirmation.Length>0){GUI.enabled=true;DrawConfirmation();}
            string focus=GUI.GetNameOfFocusedControl();textEditing=focus=="RouteManagerName"||focus=="RouteManagerRename";
        }
        void DrawPreview()
        {
            Rect bounds=new Rect(830,190,650,370);
            if(previewDirty&&Event.current.type==EventType.Repaint)
            {
                if(selected?.raw!=null)rawPreview.Rebuild(selected.raw);
                else{owner.PrepareTrainingPreview();if(owner.ActiveFit!=null)guidePreview.Rebuild(null,null,owner.ActiveFit,null,GuideViewLayer.Paths,guidePreview.Revision+1);}
                previewDirty=false;
            }
            if(selected?.raw!=null)rawPreview.Draw(bounds,selected.raw,-1,false);else guidePreview.Draw(bounds,owner.ActiveFit);
        }
        void DrawConfirmation()
        {
            var old=GUI.color;GUI.color=new Color(.05f,.07f,.10f,1);GUI.DrawTexture(new Rect(280,270,1040,390),Texture2D.whiteTexture);GUI.color=old;
            if(confirmation=="rename")
            {
                GUI.Label(new Rect(320,292,960,90),"Rename this route in the library. Its recorded trajectory and file stay unchanged.",text);
                GUI.SetNextControlName("RouteManagerRename");rename=GUI.TextField(new Rect(320,390,960,52),rename,80,input);
                if(GUI.Button(new Rect(320,535,460,66),"SAVE NAME",button))
                {try{string path=selected.path;RouteLibraryCatalog.Rename(selected,rename);owner.RenameActive(path,rename);confirmation="";Refresh();Select(path);status="Route renamed. Recording unchanged.";GUI.FocusControl(null);}catch(Exception e){status=e.Message;confirmation="";}}
            }
            else
            {
                GUI.Label(new Rect(320,292,960,190),"Delete this saved file?\n"+selected.name+"\n"+Path.GetFileName(selected.path)+"\nOther saved routes and recovery drafts will remain.",text);
                if(GUI.Button(new Rect(320,535,460,66),"CONFIRM DELETE",button))
                {try{RouteLibraryCatalog.DeleteConfirmed(selected,owner.Library,owner.GuideDirectory);selected=null;confirmation="";Refresh();status="Saved file deleted. The active GPS, if selected, remains in memory.";}catch(Exception e){status=e.Message;confirmation="";}}
            }
            if(GUI.Button(new Rect(820,535,460,66),"CANCEL",button)){confirmation="";GUI.FocusControl(null);}
        }
        public void Dispose(){rawPreview.Dispose();guidePreview.Dispose();}
    }
}
