using System;
using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    public enum GuideViewLayer { Paths,IntendedFootprints,RecordedBlade,RecordedWater,Missed,ExcessOverlap }
    /// <summary>Cached full-route authoring image. Never used for the live minimap/ground guide.</summary>
    public sealed class TrainingGuideView:IDisposable
    {
        const int Width=900,Height=760;
        Texture2D texture;Color32[] pixels;
        float minX,maxX,minZ,maxZ,scale;
        public bool FreezeProjection;
        public Texture2D Texture => texture;
        public int Revision {get;private set;}=-1;
        GUIStyle text;
        public void Rebuild(RecordedRouteData raw,TrainingRouteDefinition definition,RouteFit fit,RouteFootprints footprints,GuideViewLayer layer,int revision)
        {
            if(fit==null||fit.samples.Count==0||revision==Revision)return;Revision=revision;
            if(texture==null)texture=new Texture2D(Width,Height,TextureFormat.RGBA32,false){name="Aurora fitted-guide editor preview",hideFlags=HideFlags.HideAndDontSave};
            if(!FreezeProjection||scale==0)
            {
                minX=-fit.configuration.RinkWidth*.5f-1;maxX=-minX;minZ=-fit.configuration.RinkLength*.5f-1;maxZ=-minZ;
                if(raw!=null)foreach(var p in raw.samples)Include(p.vehicleX,p.vehicleZ);
                foreach(var p in fit.samples)Include(p.x,p.z);
                if(definition!=null)foreach(var p in definition.controls){Include(p.x,p.z);Include(p.x+(float)Math.Sin(p.headingRadians)*p.outgoing*p.direction,p.z+(float)Math.Cos(p.headingRadians)*p.outgoing*p.direction);}
                scale=Mathf.Min((Width-64)/(maxX-minX),(Height-64)/(maxZ-minZ));
            }
            if(pixels==null)pixels=new Color32[Width*Height];for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(12,26,35,255);
            for(float x=(float)Math.Ceiling(minX/5)*5;x<=maxX;x+=5)Line(Point(x,minZ),Point(x,maxZ),new Color32(27,47,58,255),0);
            for(float z=(float)Math.Ceiling(minZ/5)*5;z<=maxZ;z+=5)Line(Point(minX,z),Point(maxX,z),new Color32(27,47,58,255),0);
            float hw=fit.configuration.RinkWidth*.5f,hl=fit.configuration.RinkLength*.5f,r=fit.configuration.CornerRadius;
            Vector2 last=default,first=default;
            for(int corner=0;corner<4;corner++)for(int j=0;j<=32;j++)
            {
                float a=(corner*90+j*90f/32)*Mathf.Deg2Rad;
                Vector2 p=Point((corner==0||corner==3?hw-r:-hw+r)+Mathf.Cos(a)*r,(corner<2?hl-r:-hl+r)+Mathf.Sin(a)*r);
                if(corner==0&&j==0)first=p;else Line(last,p,new Color32(101,132,147,255),1);last=p;
            }
            Line(last,first,new Color32(101,132,147,255),1);
            if(footprints!=null&&layer!=GuideViewLayer.Paths)
            for(int i=0;i<footprints.ice.Length;i++)
            {
                Color32 color=default;bool draw=false;
                switch(layer)
                {
                    case GuideViewLayer.IntendedFootprints:
                        draw=footprints.plannedWater[i]>0;color=new Color32(23,69,98,255);
                        if(footprints.plannedBlade[i]>0){draw=true;color=footprints.plannedBlade[i]>1?new Color32(47,133,99,255):new Color32(28,89,72,255);}break;
                    case GuideViewLayer.RecordedBlade:draw=footprints.actualBlade[i]>0;color=new Color32(44,113,73,255);break;
                    case GuideViewLayer.RecordedWater:draw=footprints.actualWater[i]>0;color=new Color32(34,91,135,255);break;
                    case GuideViewLayer.Missed:
                        draw=(footprints.ComparingFullPlan?footprints.plannedWater[i]:footprints.expectedWater[i])>0&&footprints.actualWater[i]==0;color=new Color32(47,105,186,255);
                        if((footprints.ComparingFullPlan?footprints.plannedBlade[i]:footprints.expectedBlade[i])>0&&footprints.actualBlade[i]==0){draw=true;color=new Color32(175,61,66,255);}break;
                    case GuideViewLayer.ExcessOverlap:
                        draw=footprints.actualWater[i]>Math.Max(2,(footprints.ComparingFullPlan?footprints.plannedWater[i]:footprints.expectedWater[i])+1);color=new Color32(43,145,195,255);
                        if(footprints.actualBlade[i]>Math.Max(2,(footprints.ComparingFullPlan?footprints.plannedBlade[i]:footprints.expectedBlade[i])+1)){draw=true;color=new Color32(210,132,45,255);}break;
                }
                if(draw)Dot(Point(footprints.X(i),footprints.Z(i)),color,Mathf.Max(1,Mathf.CeilToInt(scale*RouteFootprints.Cell*.5f)));
            }
            if(raw!=null)for(int i=1;i<raw.samples.Count;i++)
            {
                var a=raw.samples[i-1];var b=raw.samples[i];Line(Point(a.vehicleX,a.vehicleZ),Point(b.vehicleX,b.vehicleZ),new Color32(130,139,150,255),0);
                Line(Point(a.conditionerX,a.conditionerZ),Point(b.conditionerX,b.conditionerZ),new Color32(89,92,111,255),0);
            }
            for(int i=1;i<fit.samples.Count;i++)
            {
                var a=fit.samples[i-1];var b=fit.samples[i];
                Line(Point(a.conditionerX,a.conditionerZ),Point(b.conditionerX,b.conditionerZ),new Color32(236,186,91,255),0);
                Line(Point(a.x,a.z),Point(b.x,b.z),b.boundaryError||b.curvatureError?new Color32(255,88,92,255):new Color32(64,229,210,255),1);
            }
            var start=fit.samples[0];var end=fit.samples[fit.samples.Count-1];Dot(Point(start.x,start.z),new Color32(74,248,149,255),5);Dot(Point(end.x,end.z),new Color32(255,142,77,255),4);
            texture.SetPixels32(pixels);texture.Apply(false,false);
        }
        void Include(float x,float z){minX=Mathf.Min(minX,x-1);maxX=Mathf.Max(maxX,x+1);minZ=Mathf.Min(minZ,z-1);maxZ=Mathf.Max(maxZ,z+1);}
        Vector2 Point(float x,float z) => new Vector2(Width*.5f+(x-(minX+maxX)*.5f)*scale,Height*.5f+(z-(minZ+maxZ)*.5f)*scale);
        public Vector2 ToGui(Rect rect,float x,float z){var p=Point(x,z);return new Vector2(rect.x+p.x/Width*rect.width,rect.y+(1-p.y/Height)*rect.height);}
        public Vector2 FromGui(Rect rect,Vector2 p)
        {return new Vector2(((p.x-rect.x)/rect.width*Width-Width*.5f)/scale+(minX+maxX)*.5f,((1-(p.y-rect.y)/rect.height)*Height-Height*.5f)/scale+(minZ+maxZ)*.5f);}
        public Rect Draw(Rect bounds,RouteFit fit)
        {
            Rect rect=RouteRecordingPreview.Fit(bounds);
            if(texture==null){GUI.Label(bounds,"Load a recording or a saved fitted guide.");return rect;}
            GUI.DrawTexture(rect,texture,ScaleMode.StretchToFill);
            if(text==null){text=new GUIStyle(GUI.skin.label){fontSize=15,fontStyle=FontStyle.Bold};text.normal.textColor=Color.white;}
            GUI.Label(new Rect(rect.x+8,rect.y+6,250,24),"+Z / NORTH    5 m grid",text);
            if(fit!=null)
            {
                var first=fit.samples[0];var last=fit.samples[fit.samples.Count-1];var a=ToGui(rect,first.x,first.z);var b=ToGui(rect,last.x,last.z);
                GUI.Label(new Rect(a.x+6,a.y-20,90,24),"START",text);GUI.Label(new Rect(b.x+6,b.y+6,90,24),"END",text);
            }
            return rect;
        }
        void Dot(Vector2 p,Color32 color,int radius)
        {int x=Mathf.RoundToInt(p.x),y=Mathf.RoundToInt(p.y);for(int dz=-radius;dz<=radius;dz++)for(int dx=-radius;dx<=radius;dx++)if(x+dx>=0&&x+dx<Width&&y+dz>=0&&y+dz<Height)pixels[(y+dz)*Width+x+dx]=color;}
        void Line(Vector2 a,Vector2 b,Color32 color,int radius)
        {int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)));for(int i=0;i<=steps;i++)Dot(Vector2.Lerp(a,b,i/(float)steps),color,radius);}
        public void Dispose(){if(texture!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(texture);else UnityEngine.Object.DestroyImmediate(texture);}texture=null;}
    }
}
