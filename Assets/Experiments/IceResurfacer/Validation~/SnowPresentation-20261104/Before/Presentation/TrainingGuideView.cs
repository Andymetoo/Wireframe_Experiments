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
        float centerX,centerZ,zoom=1;
        bool customViewport;
        public float Zoom=>zoom;
        public bool ShowRawPath=true,ShowGuidePath=true,ShowConditionerPath=true;
        public int SelectedProblem=-1;
        public bool FreezeProjection;
        public Texture2D Texture => texture;
        public int Revision {get;private set;}=-1;
        GUIStyle text;
        public void Rebuild(RecordedRouteData raw,TrainingRouteDefinition definition,RouteFit fit,RouteFootprints footprints,GuideViewLayer layer,int revision)
        {
            if(fit==null||fit.samples.Count==0||revision==Revision)return;Revision=revision;
            if(texture==null)texture=new Texture2D(Width,Height,TextureFormat.RGBA32,false){name="Aurora fitted-guide editor preview",hideFlags=HideFlags.HideAndDontSave};
            if((!FreezeProjection&&!customViewport)||scale==0)
            {
                minX=-fit.configuration.RinkWidth*.5f-1;maxX=-minX;minZ=-fit.configuration.RinkLength*.5f-1;maxZ=-minZ;
                if(raw!=null)foreach(var p in raw.samples)Include(p.vehicleX,p.vehicleZ);
                foreach(var p in fit.samples)Include(p.x,p.z);
                if(definition!=null)foreach(var p in definition.controls){Include(p.x,p.z);Include(p.x+(float)Math.Sin(p.headingRadians)*p.outgoing*p.direction,p.z+(float)Math.Cos(p.headingRadians)*p.outgoing*p.direction);}
                scale=Mathf.Min((Width-64)/(maxX-minX),(Height-64)/(maxZ-minZ));
                centerX=(minX+maxX)*.5f;centerZ=(minZ+maxZ)*.5f;
            }
            if(pixels==null)pixels=new Color32[Width*Height];for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(12,26,35,255);
            for(float x=(float)Math.Ceiling(minX/5)*5;x<=maxX;x+=5)Line(Point(x,minZ),Point(x,maxZ),new Color32(27,47,58,255),0);
            for(float z=(float)Math.Ceiling(minZ/5)*5;z<=maxZ;z+=5)Line(Point(minX,z),Point(maxX,z),new Color32(27,47,58,255),0);
            var layout=new ServiceLayout(fit.configuration);
            float hw=fit.configuration.RinkWidth*.5f,hl=fit.configuration.RinkLength*.5f,r=fit.configuration.CornerRadius;
            Vector2 last=default,first=default;
            bool previousPortal=false;
            const int outlineSteps=512;
            for(int corner=0;corner<4;corner++)for(int j=0;j<=outlineSteps;j++)
            {
                float a=(corner*90+j*90f/outlineSteps)*Mathf.Deg2Rad;
                float wx=(corner==0||corner==3?hw-r:-hw+r)+Mathf.Cos(a)*r,wz=(corner<2?hl-r:-hl+r)+Mathf.Sin(a)*r;
                Vector2 p=Point(wx,wz);bool portal=layout.IsPortal(wx,wz);
                if(corner==0&&j==0)first=p;else if(!portal&&!previousPortal)Line(last,p,new Color32(101,132,147,255),1);last=p;previousPortal=portal;
            }
            Line(last,first,new Color32(101,132,147,255),1);
            float[] sides={-layout.GateHalfWidth,layout.GateHalfWidth,layout.GateHalfWidth,layout.RoomHalfWidth,layout.RoomHalfWidth,-layout.RoomHalfWidth,-layout.RoomHalfWidth,-layout.GateHalfWidth,-layout.GateHalfWidth};
            float[] outs={layout.NeckStart,layout.NeckStart,layout.RoomStart,layout.RoomStart,layout.RoomLength,layout.RoomLength,layout.RoomStart,layout.RoomStart,layout.NeckStart};
            for(int i=1;i<sides.Length;i++){layout.ToWorld(sides[i-1],outs[i-1],out float ax,out float az);layout.ToWorld(sides[i],outs[i],out float bx,out float bz);Line(Point(ax,az),Point(bx,bz),new Color32(101,132,147,255),1);}
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
                if(draw)Dot(Point(footprints.X(i),footprints.Z(i)),color,Mathf.Max(1,Mathf.CeilToInt(scale*zoom*RouteFootprints.Cell*.5f)));
            }
            if(raw!=null)for(int i=1;i<raw.samples.Count;i++)
            {
                var a=raw.samples[i-1];var b=raw.samples[i];if(ShowRawPath)Line(Point(a.vehicleX,a.vehicleZ),Point(b.vehicleX,b.vehicleZ),new Color32(130,139,150,255),0);
                if(ShowConditionerPath)Line(Point(a.conditionerX,a.conditionerZ),Point(b.conditionerX,b.conditionerZ),new Color32(89,92,111,255),0);
            }
            for(int i=1;i<fit.samples.Count;i++)
            {
                var a=fit.samples[i-1];var b=fit.samples[i];
                if(ShowConditionerPath)Line(Point(a.conditionerX,a.conditionerZ),Point(b.conditionerX,b.conditionerZ),new Color32(236,186,91,255),0);
                if(ShowGuidePath)Line(Point(a.x,a.z),Point(b.x,b.z),b.boundaryError?new Color32(255,88,92,255):b.curvatureError?new Color32(235,104,242,255):b.recordedRadiusWarning?new Color32(255,190,80,255):new Color32(64,229,210,255),1);
            }
            var start=fit.samples[0];var end=fit.samples[fit.samples.Count-1];Dot(Point(start.x,start.z),new Color32(74,248,149,255),5);Dot(Point(end.x,end.z),new Color32(255,142,77,255),4);
            if(SelectedProblem>=0&&SelectedProblem<fit.problems.Count)
            {
                var issue=fit.problems[SelectedProblem];var p=issue.fitted;var a=issue.raw;
                BodyEnvelope(a.vehicleX,a.vehicleZ,a.vehicleHeadingRadians,fit.configuration,new Color32(175,185,195,255));
                BodyEnvelope(p.x,p.z,p.heading,fit.configuration,p.boundaryError?new Color32(255,88,92,255):new Color32(235,104,242,255));
                if(p.boundaryError){var contact=Point(p.boundaryContact.PointX,p.boundaryContact.PointZ);Dot(contact,new Color32(255,88,92,255),5);Line(Point(p.x,p.z),contact,new Color32(255,88,92,255),0);}
            }
            texture.SetPixels32(pixels);texture.Apply(false,false);
        }
        void BodyEnvelope(float x,float z,float heading,SimulatorConfig c,Color32 color)
        {
            float sn=Mathf.Sin(heading),cs=Mathf.Cos(heading);var corners=new Vector2[4];
            for(int i=0;i<4;i++){float side=(i==0||i==3?-1:1)*c.VehicleHalfWidth,fore=i<2?-c.BodyRear:c.BodyFront;corners[i]=Point(x+cs*side+sn*fore,z-sn*side+cs*fore);}
            for(int i=0;i<4;i++)Line(corners[i],corners[(i+1)%4],color,1);
        }
        void Include(float x,float z){minX=Mathf.Min(minX,x-1);maxX=Mathf.Max(maxX,x+1);minZ=Mathf.Min(minZ,z-1);maxZ=Mathf.Max(maxZ,z+1);}
        Vector2 Point(float x,float z) => new Vector2(Width*.5f+(x-centerX)*scale*zoom,Height*.5f+(z-centerZ)*scale*zoom);
        public Vector2 ToGui(Rect rect,float x,float z){var p=Point(x,z);return new Vector2(rect.x+p.x/Width*rect.width,rect.y+(1-p.y/Height)*rect.height);}
        public Vector2 FromGui(Rect rect,Vector2 p)
        {return new Vector2(((p.x-rect.x)/rect.width*Width-Width*.5f)/(scale*zoom)+centerX,((1-(p.y-rect.y)/rect.height)*Height-Height*.5f)/(scale*zoom)+centerZ);}
        public void FitWholeRoute(){zoom=1;customViewport=FreezeProjection=false;Revision=-1;}
        public void ZoomAt(Rect rect,Vector2 cursor,float wheel)
        {
            if(scale<=0||!rect.Contains(cursor))return;var before=FromGui(rect,cursor);
            zoom=Mathf.Clamp(zoom*Mathf.Pow(1.18f,-wheel),1,80);var after=FromGui(rect,cursor);
            centerX+=before.x-after.x;centerZ+=before.y-after.y;customViewport=true;Revision=-1;
        }
        public void Pan(Rect rect,Vector2 pixels)
        {if(scale<=0)return;centerX-=pixels.x*Width/rect.width/(scale*zoom);centerZ+=pixels.y*Height/rect.height/(scale*zoom);customViewport=true;Revision=-1;}
        public void ZoomTo(float x,float z,float halfExtent=4)
        {if(scale<=0)return;zoom=Mathf.Clamp(Mathf.Min(Width-64,Height-64)/(2*Mathf.Max(.5f,halfExtent)*scale),1,80);centerX=x;centerZ=z;customViewport=true;Revision=-1;}
        public Rect Draw(Rect bounds,RouteFit fit)
        {
            Rect rect=RouteRecordingPreview.Fit(bounds);
            if(texture==null){GUI.Label(bounds,"Load a recording or a saved fitted guide.");return rect;}
            GUI.DrawTexture(rect,texture,ScaleMode.StretchToFill);
            if(text==null){text=new GUIStyle(GUI.skin.label){fontSize=15,fontStyle=FontStyle.Bold};text.normal.textColor=Color.white;}
            GUI.Label(new Rect(rect.x+8,rect.y+6,250,24),"+Z / NORTH    5 m grid",text);
            if(fit!=null&&fit.samples.Count>0)
            {
                GUI.BeginGroup(rect);var canvas=new Rect(0,0,rect.width,rect.height);
                var first=fit.samples[0];var last=fit.samples[fit.samples.Count-1];var a=ToGui(canvas,first.x,first.z);var b=ToGui(canvas,last.x,last.z);
                GUI.Label(new Rect(a.x+6,a.y-20,90,24),"START",text);GUI.Label(new Rect(b.x+6,b.y+6,90,24),"END",text);
                GUI.EndGroup();
            }
            return rect;
        }
        void Dot(Vector2 p,Color32 color,int radius)
        {int x=Mathf.RoundToInt(p.x),y=Mathf.RoundToInt(p.y);for(int dz=Mathf.Max(-radius,-y);dz<=Mathf.Min(radius,Height-1-y);dz++)for(int dx=Mathf.Max(-radius,-x);dx<=Mathf.Min(radius,Width-1-x);dx++)pixels[(y+dz)*Width+x+dx]=color;}
        void Line(Vector2 a,Vector2 b,Color32 color,int radius)
        {
            Vector2 delta=b-a;float lo=0,hi=1;
            if(!Clip(-delta.x,a.x,ref lo,ref hi)||!Clip(delta.x,Width-1-a.x,ref lo,ref hi)||!Clip(-delta.y,a.y,ref lo,ref hi)||!Clip(delta.y,Height-1-a.y,ref lo,ref hi))return;
            b=a+delta*hi;a+=delta*lo;int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)));for(int i=0;i<=steps;i++)Dot(Vector2.Lerp(a,b,i/(float)steps),color,radius);
        }
        static bool Clip(float p,float q,ref float lo,ref float hi)
        {if(Mathf.Abs(p)<.000001f)return q>=0;float r=q/p;if(p<0){if(r>hi)return false;lo=Mathf.Max(lo,r);}else{if(r<lo)return false;hi=Mathf.Min(hi,r);}return true;}
        public void Dispose(){if(texture!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(texture);else UnityEngine.Object.DestroyImmediate(texture);}texture=null;}
    }
}
