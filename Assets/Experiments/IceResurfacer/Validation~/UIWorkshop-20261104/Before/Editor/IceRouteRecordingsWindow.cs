using System;
using System.IO;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEngine;

namespace IceResurfacer.Editor
{
    /// <summary>Uses the same recording files and preview as the existing route component.</summary>
    public sealed class IceRouteRecordingsWindow:EditorWindow
    {
        RouteRecordingPreview preview;
        RecordedRouteData data;
        string[] paths=Array.Empty<string>(),labels=Array.Empty<string>();
        int fileIndex,pointIndex;
        string status="Load a recording. The deterministic fixture is validation data, not an operator's route.";
        Vector2 details;
        bool live;
        [MenuItem("Experiments/Ice Resurfacer/Route Recordings")]
        public static void Open()
        {var window=GetWindow<IceRouteRecordingsWindow>("Aurora Route Recordings");window.minSize=new Vector2(850,650);window.Show();}
        void OnEnable(){preview=new RouteRecordingPreview();Refresh();}
        void OnDisable(){preview?.Dispose();}
        void OnInspectorUpdate(){if(live)Repaint();}
        void Refresh()
        {
            try
            {
                paths=new RouteRecordingStorage(RouteRecordingStorage.DefaultDirectory).ListFiles();
                labels=new string[paths.Length];for(int i=0;i<labels.Length;i++)labels[i]=(Path.GetFileName(Path.GetDirectoryName(paths[i]))=="Drafts"?"DRAFT / ":"")+Path.GetFileName(paths[i]);
                fileIndex=Mathf.Clamp(fileIndex,0,Mathf.Max(0,paths.Length-1));
            }
            catch(Exception e){status=e.Message;}
        }
        public void Inspect(RecordedRouteData recording) {recording.Validate();data=recording;live=false;pointIndex=0;Repaint();}
        void Load(string path)
        {try{Inspect(RouteRecordingStorage.Read(path));status="Loaded "+path;}catch(Exception e){status="Load failed; previous preview retained. "+e.Message;}}
        void OnGUI()
        {
            EditorGUILayout.LabelField("AURORA R-54 / RAW ROUTE RECORDINGS",EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Refresh files",GUILayout.Width(115)))Refresh();
            if(paths.Length>0)fileIndex=EditorGUILayout.Popup(fileIndex,labels);
            using(new EditorGUI.DisabledScope(paths.Length==0))if(GUILayout.Button("Load selected",GUILayout.Width(120)))Load(paths[fileIndex]);
            if(GUILayout.Button("Open JSON…",GUILayout.Width(120)))
            {string path=EditorUtility.OpenFilePanel("Open an Aurora raw route",RouteRecordingStorage.DefaultDirectory,"json");if(!string.IsNullOrEmpty(path))Load(path);}
            EditorGUILayout.EndHorizontal();
            using(new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                live=EditorGUILayout.ToggleLeft("Inspect the live F6 recording while playing",live&&EditorApplication.isPlaying);
            if(live)
            {var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();if(game!=null&&game.RouteGuide!=null)data=game.RouteGuide.Recorder.Data;}
            EditorGUILayout.SelectableLabel(RouteRecordingStorage.DefaultDirectory,EditorStyles.miniLabel,GUILayout.Height(18));
            EditorGUILayout.HelpBox(status,MessageType.Info);
            float top=GUILayoutUtility.GetLastRect().yMax+8;
            Rect map=new Rect(8,top,Mathf.Max(360,position.width-350),Mathf.Max(200,position.height-top-40));
            if(data!=null&&data.samples.Count>0)
            {
                pointIndex=Mathf.Clamp(pointIndex,0,data.samples.Count-1);
                preview.Draw(map,data,pointIndex);
                GUILayout.BeginArea(new Rect(position.width-330,top,318,position.height-top-12));
                details=GUILayout.BeginScrollView(details);
                GUILayout.Label(data.name,EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(data.provenance+(data.complete?"":" / PARTIAL RECOVERY"),MessageType.None);
                GUILayout.Label(data.samples.Count+" raw observations\n"+data.distanceMeters.ToString("F3")+" m / "+data.durationSeconds.ToString("F2")+" s");
                pointIndex=EditorGUILayout.IntSlider("Sequence",pointIndex,0,data.samples.Count-1);
                var p=data.samples[pointIndex];
                GUILayout.Label("t="+p.seconds.ToString("F3")+" s / d="+p.distanceMeters.ToString("F3")+" m\n"+(p.direction<0?"REVERSE":p.direction>0?"FORWARD":"STOPPED")+" / "+p.speedMetersPerSecond.ToString("F3")+" m/s");
                GUILayout.Label("Vehicle (rear axle)\nX "+p.vehicleX.ToString("F4")+" / Z "+p.vehicleZ.ToString("F4")+"\nHeading "+(p.vehicleHeadingRadians*Mathf.Rad2Deg).ToString("F3")+"°\n\nActual conditioner\nX "+p.conditionerX.ToString("F4")+" / Z "+p.conditionerZ.ToString("F4")+"\nHeading "+(p.conditionerHeadingRadians*Mathf.Rad2Deg).ToString("F3")+"° / Lift "+p.conditionerLift.ToString("F3"));
                GUILayout.Label("\nBlade "+p.bladeDepthMm.ToString("F3")+" mm\nFlood "+p.floodValve.ToString("F3")+" / Wash "+p.washValve.ToString("F3")+"\nAugers H/V "+p.horizontalAuger+" / "+p.verticalAuger+"\nPump "+p.washPump+" / Brush "+p.boardBrush);
                preview.ShowConditioner=EditorGUILayout.ToggleLeft("Show actual conditioner",preview.ShowConditioner);
                GUILayout.Label("Colors advance every 40 m in sequence. Orange = reverse. Start green; end red. No smoothing or path editing.",EditorStyles.wordWrappedLabel);
                if(GUILayout.Button("EDIT FITTED GUIDE / preserve raw"))
                {try{TrainingRouteEditorWindow.OpenRecording(data);}catch(Exception e){status=e.Message;}}
                if(GUILayout.Button("EXPORT TO LOCATION… (opens file picker)"))
                {
                    string path=EditorUtility.SaveFilePanel("Save raw route JSON",RouteRecordingStorage.DefaultDirectory,data.name,"route.json");
                    if(!string.IsNullOrEmpty(path))
                    {
                        try
                        {
                            if(!data.complete)throw new InvalidOperationException("Stop the recording first.");
                            // A selected existing file is confirmed by the native Save dialog.
                            RouteRecordingStorage.Export(path,data,File.Exists(path));status="Saved "+path;
                        }
                        catch(Exception e){status=e.Message;}
                    }
                }
                GUILayout.EndScrollView();GUILayout.EndArea();
            }
            else GUI.Label(map,"No recording selected. In Play Mode use F6, Start New, close the panel and drive; F7 stops.");
            GUI.Label(new Rect(10,position.height-28,Mathf.Max(500,position.width-350),24),"Overhead raw trajectory / +Z up / 5 m grid / inspect points in sequence");
        }
    }
}
