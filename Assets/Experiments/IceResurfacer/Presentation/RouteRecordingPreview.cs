using System;
using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    /// <summary>Shared overhead authoring view. Draws raw segments, never resamples or smooths them.</summary>
    public sealed class RouteRecordingPreview:IDisposable
    {
        const int Width=900,Height=760,Margin=32;
        static readonly Color32 Background=new Color32(12,26,35,255),Grid=new Color32(27,47,58,255);
        static readonly Color32[] Palette={new Color32(64,219,190,255),new Color32(255,184,78,255),new Color32(135,161,255,255),new Color32(246,117,170,255),new Color32(181,228,108,255)};
        Texture2D texture;
        GUIStyle annotation;
        Color32[] pixels;
        RecordedRouteData current;
        int count=-1;
        double routeDistance=-1;
        float minX,minZ,maxX,maxZ,scale;
        public Texture2D Texture => texture;
        public int SampleCount => Mathf.Max(0,count);
        public bool ShowConditioner {get;set;}=true;
        bool previousConditioner;

        public void Rebuild(RecordedRouteData data)
        {
            if(data==null||data.samples==null||data.samples.Count==0)return;
            if(current==data&&count==data.samples.Count&&routeDistance==data.distanceMeters&&previousConditioner==ShowConditioner)return;
            current=data;count=data.samples.Count;routeDistance=data.distanceMeters;previousConditioner=ShowConditioner;
            minX=-data.configuration.RinkWidth*.5f-1;maxX=-minX;
            minZ=-data.configuration.RinkLength*.5f-1;maxZ=-minZ;
            foreach(var p in data.samples)
            {
                minX=Mathf.Min(minX,p.vehicleX-1,p.conditionerX-1);maxX=Mathf.Max(maxX,p.vehicleX+1,p.conditionerX+1);
                minZ=Mathf.Min(minZ,p.vehicleZ-1,p.conditionerZ-1);maxZ=Mathf.Max(maxZ,p.vehicleZ+1,p.conditionerZ+1);
            }
            scale=Mathf.Min((Width-Margin*2)/(maxX-minX),(Height-Margin*2)/(maxZ-minZ));
            if(texture==null)texture=new Texture2D(Width,Height,TextureFormat.RGBA32,false){name="Aurora raw route authoring preview",hideFlags=HideFlags.HideAndDontSave};
            if(pixels==null)pixels=new Color32[Width*Height];for(int i=0;i<pixels.Length;i++)pixels[i]=Background;
            for(float x=(float)Math.Ceiling(minX/5)*5;x<=maxX;x+=5)Line(Point(x,minZ),Point(x,maxZ),Grid,0);
            for(float z=(float)Math.Ceiling(minZ/5)*5;z<=maxZ;z+=5)Line(Point(minX,z),Point(maxX,z),Grid,0);
            float hw=data.configuration.RinkWidth*.5f,hl=data.configuration.RinkLength*.5f,r=data.configuration.CornerRadius;
            Vector2 last=default,first=default;
            for(int corner=0;corner<4;corner++)
            for(int step=0;step<=24;step++)
            {
                float a=(corner*90+step*90f/24)*Mathf.Deg2Rad;
                float x=(corner==0||corner==3?hw-r:-hw+r)+Mathf.Cos(a)*r;
                float z=(corner<2?hl-r:-hl+r)+Mathf.Sin(a)*r;
                Vector2 next=Point(x,z);if(corner==0&&step==0)first=next;else Line(last,next,new Color32(101,132,147,255),1);last=next;
            }
            Line(last,first,new Color32(101,132,147,255),1);
            if(ShowConditioner)for(int i=1;i<count;i++)
            {var a=data.samples[i-1];var b=data.samples[i];Line(Point(a.conditionerX,a.conditionerZ),Point(b.conditionerX,b.conditionerZ),new Color32(116,145,160,255),0);}
            for(int i=1;i<count;i++)
            {
                var a=data.samples[i-1];var b=data.samples[i];
                Color32 color=Palette[((int)(b.distanceMeters/40))%Palette.Length];
                if(b.direction<0)color=new Color32(255,131,93,255);
                Line(Point(a.vehicleX,a.vehicleZ),Point(b.vehicleX,b.vehicleZ),color,1);
            }
            var start=data.samples[0];var end=data.samples[count-1];
            Dot(Point(start.vehicleX,start.vehicleZ),new Color32(89,255,169,255),6);
            Dot(Point(end.vehicleX,end.vehicleZ),new Color32(255,105,91,255),5);
            texture.SetPixels32(pixels);texture.Apply(false,false);
        }
        Vector2 Point(float x,float z)
        {return new Vector2(Width*.5f+(x-(minX+maxX)*.5f)*scale,Height*.5f+(z-(minZ+maxZ)*.5f)*scale);}
        public Vector2 ToGui(Rect rect,float x,float z)
        {var p=Point(x,z);return new Vector2(rect.x+p.x/Width*rect.width,rect.y+(1-p.y/Height)*rect.height);}
        public Rect Draw(Rect bounds,RecordedRouteData data,int selected=-1,bool rebuild=true)
        {
            if(rebuild)Rebuild(data);
            if(annotation==null){annotation=new GUIStyle(GUI.skin.label){fontSize=16,fontStyle=FontStyle.Bold};annotation.normal.textColor=Color.white;}
            Rect rect=Fit(bounds);
            if(texture==null){GUI.Label(bounds,"Record a driven route or load a saved file to preview it.");return rect;}
            GUI.DrawTexture(rect,texture,ScaleMode.StretchToFill);
            GUI.Label(new Rect(rect.x+8,rect.y+7,250,26),"+Z / NORTH     5 m grid",annotation);
            if(data!=null&&data.samples.Count>0)
            {
                var first=data.samples[0];var end=data.samples[data.samples.Count-1];
                LabelPoint(rect,first,"START",new Color(.4f,1,.7f),-22);
                LabelPoint(rect,end,"END",new Color(1,.55f,.45f),8);
                if(selected>=0&&selected<data.samples.Count)
                {
                    var p=data.samples[selected];var at=ToGui(rect,p.vehicleX,p.vehicleZ);
                    Color old=GUI.color;GUI.color=Color.white;
                    GUI.DrawTexture(new Rect(at.x-5,at.y-5,10,10),Texture2D.whiteTexture);GUI.color=old;
                    GUI.Label(new Rect(at.x+9,at.y-12,100,24),"#"+p.sequence,annotation);
                    var cp=ToGui(rect,p.conditionerX,p.conditionerZ);
                    GUI.color=new Color(.6f,.8f,1);GUI.DrawTexture(new Rect(cp.x-3,cp.y-3,6,6),Texture2D.whiteTexture);GUI.color=old;
                }
            }
            return rect;
        }
        public static Rect Fit(Rect bounds)
        {
            float ratio=Width/(float)Height,w=Mathf.Min(bounds.width,bounds.height*ratio),h=w/ratio;
            return new Rect(bounds.center.x-w*.5f,bounds.center.y-h*.5f,w,h);
        }
        void LabelPoint(Rect rect,RecordedRouteSample p,string label,Color color,float offset)
        {
            var at=ToGui(rect,p.vehicleX,p.vehicleZ);Color old=GUI.color;GUI.color=color;
            GUI.Label(new Rect(Mathf.Clamp(at.x+9,rect.x,rect.xMax-85),Mathf.Clamp(at.y+offset,rect.y+30,rect.yMax-24),85,24),label,annotation);GUI.color=old;
        }
        void Line(Vector2 a,Vector2 b,Color32 color,int radius)
        {
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)));
            for(int i=0;i<=steps;i++)Dot(Vector2.Lerp(a,b,i/(float)steps),color,radius);
        }
        void Dot(Vector2 point,Color32 color,int radius)
        {
            int x=Mathf.RoundToInt(point.x),y=Mathf.RoundToInt(point.y);
            for(int dy=-radius;dy<=radius;dy++)for(int dx=-radius;dx<=radius;dx++)
                if(x+dx>=0&&x+dx<Width&&y+dy>=0&&y+dy<Height)pixels[(y+dy)*Width+x+dx]=color;
        }
        public void Dispose()
        {if(texture!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(texture);else UnityEngine.Object.DestroyImmediate(texture);}texture=null;}
    }
}
