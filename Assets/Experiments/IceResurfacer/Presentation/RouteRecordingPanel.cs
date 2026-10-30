using System;
using System.IO;
using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    public sealed class RouteRecordingPanel:IDisposable
    {
        readonly IceRouteGuide guide;
        readonly RouteRecordingPreview preview=new RouteRecordingPreview();
        readonly TrainingGuidePanel training;
        public bool TrainingTab {get;set;}
        GUIStyle label,small,heading,button;
        string[] files=Array.Empty<string>();
        Vector2 scroll;
        int selected=-1;
        float nextPreview;
        RecordedRouteData previewData;
        public int PreviewSampleCount => preview.SampleCount;
        public RouteRecordingPanel(IceRouteGuide owner){guide=owner;training=new TrainingGuidePanel(owner);}
        public static Rect ControlRect(string name)
        {
            switch(name)
            {
                case "Close":return new Rect(1354,82,132,36);
                case "Name":return new Rect(910,169,570,36);
                case "Start":return new Rect(910,218,275,42);
                case "Stop":return new Rect(1200,218,280,42);
                case "Save":return new Rect(910,272,275,42);
                case "Drive":return new Rect(1200,272,280,42);
                case "Refresh":return new Rect(1320,399,160,34);
                default:throw new ArgumentException(name);
            }
        }
        public void RefreshFiles()
        {try{files=guide.Library.ListFiles();training.RefreshFiles();}catch(IOException){files=Array.Empty<string>();}catch(UnauthorizedAccessException){files=Array.Empty<string>();}}
        void Styles()
        {
            if(label!=null)return;
            label=new GUIStyle(GUI.skin.label){fontSize=18,wordWrap=true};label.normal.textColor=new Color(.90f,.95f,.97f);
            small=new GUIStyle(label){fontSize=16};heading=new GUIStyle(label){fontSize=25,fontStyle=FontStyle.Bold};
            button=new GUIStyle(GUI.skin.button){fontSize=17,wordWrap=true};
        }
        bool Button(string control,string text) => GUI.Button(ControlRect(control),text,button);
        public void Draw()
        {
            Styles();Color old=GUI.color;GUI.color=new Color(.018f,.035f,.047f,1);
            GUI.DrawTexture(new Rect(90,60,1420,830),Texture2D.whiteTexture);GUI.color=old;
            GUI.Label(new Rect(112,80,780,36),TrainingTab?"TRAINING GUIDE / AUTHORING PREVIEW":"ROUTE RECORDING / RAW TRAJECTORY",heading);
            if(GUI.Button(new Rect(910,82,200,36),"RAW RECORDINGS",button))TrainingTab=false;
            if(GUI.Button(new Rect(1118,82,224,36),"TRAINING GUIDES",button)){TrainingTab=true;training.RefreshFiles();}
            if(Button("Close","CLOSE  F6"))guide.ToggleRecordingPanel();
            if(TrainingTab){training.Draw();return;}
            var data=guide.Recorder.Data;
            // Repaint the route at two Hz while driving; inspection never rebuilds raw data.
            bool finalChanged=!guide.Recorder.Recording&&data!=null&&preview.SampleCount!=data.samples.Count;
            if(Event.current.type==EventType.Repaint&&(previewData!=data||finalChanged||Time.unscaledTime>=nextPreview))
            {preview.Rebuild(data);previewData=data;nextPreview=Time.unscaledTime+.5f;}
            // Draw() has a cache; keep texture generation out of Layout/input events.
            preview.Draw(new Rect(112,134,770,622),data,selected,false);
            GUI.Label(new Rect(120,763,750,29),"Color changes every 40 m in sequence. Orange = reverse. Gray = actual conditioner.",small);
            if(data!=null&&data.samples.Count>0)
            {
                selected=Mathf.Clamp(selected<0?data.samples.Count-1:selected,0,data.samples.Count-1);
                selected=Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(128,804,726,20),selected,0,data.samples.Count-1));
            }
            GUI.Label(new Rect(910,138,570,28),"Name for the next recording",label);
            GUI.SetNextControlName("RouteRecordingName");guide.RecordingName=GUI.TextField(ControlRect("Name"),guide.RecordingName,80,button);
            GUI.enabled=guide.CanRecord&&!guide.Recorder.Recording;
            if(Button("Start","START NEW  [F7]")){if(guide.StartRecording())selected=-1;}
            GUI.enabled=guide.Recorder.Recording;
            if(Button("Stop","STOP RECORDING  [F7]"))guide.StopRecording();
            GUI.enabled=data!=null&&!guide.Recorder.Recording&&data.complete;
            if(Button("Save","SAVE NAMED COPY"))guide.SaveRecording();
            GUI.enabled=true;if(Button("Drive","CLOSE AND DRIVE"))guide.ToggleRecordingPanel();
            Color saved=GUI.color;GUI.color=guide.Recorder.Recording?new Color(.5f,1,.75f):new Color(1,.82f,.55f);
            GUI.Label(new Rect(910,327,570,62),guide.RecordingStatus,label);GUI.color=saved;
            GUI.Label(new Rect(910,401,400,30),"SAVED ROUTES / RECOVERY DRAFTS",small);
            if(Button("Refresh","REFRESH"))RefreshFiles();
            scroll=GUI.BeginScrollView(new Rect(910,441,570,166),scroll,new Rect(0,0,543,Mathf.Max(152,files.Length*44)));
            GUI.enabled=!guide.Recorder.Recording;
            for(int i=0;i<files.Length;i++)
            {
                string path=files[i];bool draft=Path.GetFileName(Path.GetDirectoryName(path))=="Drafts";
                string fileName=Path.GetFileName(path);
                if(fileName.Length>59)fileName=fileName.Substring(0,32)+"…"+fileName.Substring(fileName.Length-22);
                if(GUI.Button(new Rect(0,i*44,538,40),(draft?"DRAFT / ":"LOAD / ")+fileName,small))
                {if(guide.LoadRecording(path))selected=-1;}
            }
            GUI.enabled=true;GUI.EndScrollView();
            if(files.Length==0)GUI.Label(new Rect(928,464,530,62),"No saved recordings yet. Stop, preview, then Save Named Copy.",small);
            if(data!=null)
            {
                string title=data.name.Length>48?data.name.Substring(0,45)+"…":data.name;
                GUI.Label(new Rect(910,615,570,27),title,label);
                GUI.Label(new Rect(910,646,570,25),data.samples.Count+" points  /  "+data.distanceMeters.ToString("F2")+" m  /  "+data.durationSeconds.ToString("F2")+" s",small);
                GUI.Label(new Rect(910,678,570,41),data.provenance+(data.complete?"":" / PARTIAL RECOVERY"),small);
                if(selected>=0&&selected<data.samples.Count)
                {
                    var p=data.samples[selected];
                    GUI.Label(new Rect(910,723,570,82),"#"+p.sequence+"  t="+p.seconds.ToString("F2")+" s  d="+p.distanceMeters.ToString("F3")+" m  "+(p.direction<0?"REVERSE":p.direction>0?"FORWARD":"STOPPED")+
                        "\nVehicle "+p.vehicleX.ToString("F3")+", "+p.vehicleZ.ToString("F3")+"  heading "+(p.vehicleHeadingRadians*Mathf.Rad2Deg).ToString("F1")+"°"+
                        "\nConditioner "+p.conditionerX.ToString("F3")+", "+p.conditionerZ.ToString("F3")+"  heading "+(p.conditionerHeadingRadians*Mathf.Rad2Deg).ToString("F1")+"°  lift "+p.conditionerLift.ToString("F2"),small);
                }
            }
            GUI.Label(new Rect(910,809,570,26),"Park first; machine runs. F5 reference unchanged.",small);
            GUI.Label(new Rect(112,840,1368,43),"FILES  /  "+guide.Library.DirectoryPath,small);
        }
        public void Dispose(){preview.Dispose();training.Dispose();}
    }
}
