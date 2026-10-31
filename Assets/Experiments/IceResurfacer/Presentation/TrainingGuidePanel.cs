using System;
using System.IO;
using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    public sealed class TrainingGuidePanel:IDisposable
    {
        readonly IceRouteGuide guide;
        readonly TrainingGuideView view=new TrainingGuideView();
        TrainingRouteDefinition shown;
        RecordedRouteData raw;
        string[] files=Array.Empty<string>();
        Vector2 scroll;
        int selectedLeg,revision,layer;
        float refreshAt;
        GUIStyle label,small,button;
        public TrainingGuidePanel(IceRouteGuide owner){guide=owner;}
        public static Rect ControlRect(string name)
        {
            switch(name)
            {
                case "Toggle":return new Rect(910,170,275,40);
                case "Reference":return new Rect(1200,170,280,40);
                case "Fixture":return new Rect(910,222,570,39);
                case "Refresh":return new Rect(1320,274,160,32);
                case "Rejoin":return new Rect(910,611,570,40);
                default:throw new ArgumentException(name);
            }
        }
        public void RefreshFiles(){try{files=TrainingGuideStorage.List(guide.GuideDirectory);Array.Sort(files,StringComparer.Ordinal);}catch(IOException){files=Array.Empty<string>();}}
        public void Draw()
        {
            if(label==null)
            {
                label=new GUIStyle(GUI.skin.label){fontSize=18,wordWrap=true};label.normal.textColor=new Color(.9f,.95f,.97f);
                small=new GUIStyle(label){fontSize=16};button=new GUIStyle(GUI.skin.button){fontSize=17,wordWrap=true};
            }
            guide.PrepareTrainingPreview();var fit=guide.ActiveFit;
            if(shown!=guide.SelectedGuide)
            {shown=guide.SelectedGuide;raw=shown==null?null:TrainingGuideStorage.Raw(shown);selectedLeg=0;revision++;refreshAt=0;}
            if(Event.current.type==EventType.Repaint&&fit!=null&&Time.unscaledTime>=refreshAt)
            {view.Rebuild(raw,shown,fit,guide.Footprints,(GuideViewLayer)layer,++revision);refreshAt=Time.unscaledTime+.5f;}
            view.Draw(new Rect(112,134,770,594),fit);
            int nextLayer=GUI.Toolbar(new Rect(112,740,770,35),layer,new[]{"Paths","Intended","Actual cut","Actual water","Missed","Excess"},button);
            if(nextLayer!=layer){layer=nextLayer;refreshAt=0;}
            GUI.Label(new Rect(120,782,750,25),"Raw gray / fitted cyan / conditioner gold. GPS appearance:",small);
            if(GUI.Button(new Rect(120,808,145,27),"MAGENTA",button))guide.GpsColor=new Color(1,.035f,.72f,guide.GpsColor.a);
            if(GUI.Button(new Rect(278,808,145,27),"AMBER",button))guide.GpsColor=new Color(1,.5f,.03f,guide.GpsColor.a);
            GUI.Label(new Rect(437,806,150,27),"Opacity",small);var color=guide.GpsColor;color.a=GUI.HorizontalSlider(new Rect(548,813,310,20),color.a,.45f,1);guide.GpsColor=color;
            string title=shown==null?"Original generated reference":shown.name;
            if(title.Length>53)title=title.Substring(0,50)+"…";
            GUI.Label(new Rect(910,135,570,30),title,label);
            if(GUI.Button(ControlRect("Toggle"),guide.Visible?"HIDE GUIDE  [F5]":"SHOW GUIDE  [F5]",button))guide.Toggle();
            if(GUI.Button(ControlRect("Reference"),"ORIGINAL F5 REFERENCE",button)){guide.UseOriginalReference();raw=null;shown=null;refreshAt=0;}
            if(GUI.Button(ControlRect("Fixture"),"LOAD LABELED TEST GUIDE  /  NOT AN OPERATOR ROUTE",button)){guide.SelectTestFixture();refreshAt=0;}
            GUI.Label(new Rect(910,278,405,27),"CORRECTED GUIDES / Guides folder",small);
            if(GUI.Button(ControlRect("Refresh"),"REFRESH",button))RefreshFiles();
            scroll=GUI.BeginScrollView(new Rect(910,315,570,175),scroll,new Rect(0,0,543,Mathf.Max(162,files.Length*43)));
            for(int i=0;i<files.Length;i++)if(GUI.Button(new Rect(0,i*43,538,39),Path.GetFileName(files[i]),small))
            {guide.LoadTrainingGuide(files[i]);refreshAt=0;}
            GUI.EndScrollView();
            if(files.Length==0)GUI.Label(new Rect(922,336,530,92),"Create a corrected guide in the Editor:\nExperiments > Ice Resurfacer > Route Recordings > Edit fitted guide.\nLoad any future operator .route.json there.",small);
            if(fit!=null&&guide.Progress!=null)
            {
                selectedLeg=Mathf.Clamp(selectedLeg,0,fit.legs.Count-1);
                GUI.Label(new Rect(910,505,570,28),"Sequential leg "+(selectedLeg+1)+" / "+fit.legs.Count+" — explicit rejoin",label);
                selectedLeg=Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(917,549,555,22),selectedLeg,0,fit.legs.Count-1));
                var leg=fit.legs[selectedLeg];
                GUI.Label(new Rect(910,572,570,30),leg.label+"  /  "+leg.start.ToString("F1")+"–"+leg.end.ToString("F1")+" m",small);
                if(GUI.Button(ControlRect("Rejoin"),"REJOIN SELECTED LEG  /  GUIDE CURSOR ONLY",button))guide.RejoinLeg(selectedLeg);
                var progress=guide.Progress;var footprints=guide.Footprints;
                GUI.Label(new Rect(910,665,570,51),"Current leg "+(progress.Leg+1)+" / "+progress.Distance.ToString("F1")+" m\n"+progress.Status,small);
                GUI.Label(new Rect(910,725,570,77),"Intended prefix vs live footprint geometry:\nMissed cut "+(footprints.MissedBladeCells*.04f).ToString("F1")+" m² / water "+(footprints.MissedWaterCells*.04f).ToString("F1")+" m²\nExcess revisit: cut "+(footprints.ExcessBladeCells*.04f).ToString("F1")+" m² / water "+(footprints.ExcessWaterCells*.04f).ToString("F1")+" m²",small);
            }
            GUI.Label(new Rect(910,797,570,42),guide.TrainingStatus,small);
            GUI.Label(new Rect(430,840,1050,43),"GUIDES / "+guide.GuideDirectory+"   |   Advisory GPS / NUMPAD 7",small);
        }
        public void Dispose(){view.Dispose();}
    }
}
