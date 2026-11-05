using System;
using System.Collections.Generic;
using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    /// <summary>
    /// Dasher boards, top rail and protective glass as closed solids, shared by the procedural
    /// arena and the authored-arena migration so both are built from one definition.
    /// Inner faces lie exactly on the simulated board line (the collision authority); every band
    /// only extends outward. Heights are visual: collision never reads them.
    /// Board 1.22 m (48 in, inside the NHL 40-48 in range). Glass 2.44 m above the boards around
    /// both ends and every corner, 1.83 m along the sides, with one sloped transition panel
    /// between two existing supports on each side straight.
    /// </summary>
    public static class IceRinkBoards
    {
        public const float KickBottom=.015f, KickTop=.17f, BoardTop=1.13f, RailTop=1.22f;
        public const float KickThickness=.19f, BoardThickness=.17f, RailThickness=.21f, GlassThickness=.012f;
        public const float SideGlassAboveBoards=1.83f, EndGlassAboveBoards=2.44f;
        public const float SideGlassTop=RailTop+SideGlassAboveBoards, EndGlassTop=RailTop+EndGlassAboveBoards;
        public const int SupportEvery=5;
        // Gate post, measured from the gate axis: clear of the 2.7 m opening, the width of the jamb.
        public const float PostInnerClearance=.03f, PostWidth=.18f, PostChamfer=.025f;

        public struct Point { public Vector3 Position, Normal; }

        /// <summary>The simulated rounded board line (same points as the original builder).</summary>
        public static List<Point> Boundary(SimulatorConfig c)
        {
            float r=c.CornerRadius,hx=c.RinkWidth*.5f-r,hz=c.RinkLength*.5f-r;
            Vector2[] centres={new Vector2(hx,hz),new Vector2(hx,-hz),new Vector2(-hx,-hz),new Vector2(-hx,hz)};
            var points=new List<Point>();
            for(int corner=0;corner<4;corner++)
            {
                var centre=centres[corner];
                for(int i=0;i<=24;i++)
                {
                    float a=(corner*90+i*90f/24)*Mathf.Deg2Rad;
                    var n=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));
                    points.Add(new Point{Position=new Vector3(centre.x,0,centre.y)+n*r,Normal=n});
                }
                var end=points[points.Count-1].Position;
                var next=centres[(corner+1)%4];
                float nextA=(corner+1)*90*Mathf.Deg2Rad;
                var start=new Vector3(next.x+Mathf.Sin(nextA)*r,0,next.y+Mathf.Cos(nextA)*r);
                int segments=Mathf.CeilToInt(Vector3.Distance(end,start)/.7f);
                var straightNormal=points[points.Count-1].Normal;
                for(int j=1;j<segments;j++)points.Add(new Point{Position=Vector3.Lerp(end,start,j/(float)segments),Normal=straightNormal});
            }
            return points;
        }

        /// <summary>Glass supports: every fifth board point outside the service portal (as before).</summary>
        public static List<int> SupportIndices(List<Point> boundary,ServiceLayout layout)
        {
            var list=new List<int>();
            for(int i=0;i<boundary.Count;i+=SupportEvery)
                if(!layout.IsPortal(boundary[i].Position.x,boundary[i].Position.z))list.Add(i);
            return list;
        }

        /// <summary>Glass height profile: tall around the ends and corners, a sloped panel between
        /// the two supports nearest each corner on every side straight, side height between.</summary>
        public sealed class GlassProfile
        {
            readonly float hz,halfWidth;
            // [east/west][north/south]: tall support z, side-height support z.
            readonly float[,] tallZ=new float[2,2],sideZ=new float[2,2];
            public GlassProfile(SimulatorConfig c,List<Point> boundary,ServiceLayout layout)
            {
                hz=c.RinkLength*.5f-c.CornerRadius;halfWidth=c.RinkWidth*.5f;
                for(int s=0;s<2;s++)
                {
                    float x=s==0?halfWidth:-halfWidth;
                    var zs=new List<float>();
                    foreach(int i in SupportIndices(boundary,layout))
                    {
                        var p=boundary[i].Position;
                        if(Mathf.Abs(p.x-x)<.001f&&Mathf.Abs(p.z)<=hz+.001f)zs.Add(p.z);
                    }
                    zs.Sort();
                    if(zs.Count<4)throw new InvalidOperationException("Too few glass supports on a side straight to place the height transition.");
                    tallZ[s,0]=zs[zs.Count-1];sideZ[s,0]=zs[zs.Count-2];
                    tallZ[s,1]=zs[0];sideZ[s,1]=zs[1];
                }
            }
            public float Top(Vector3 p)
            {
                if(Mathf.Abs(p.z)>hz+.001f||Mathf.Abs(p.x)<halfWidth-.5f)return EndGlassTop;
                int s=p.x>0?0:1,end=p.z>0?0:1;
                float tall=tallZ[s,end],side=sideZ[s,end];
                float t=Mathf.InverseLerp(side,tall,p.z);
                return Mathf.Lerp(SideGlassTop,EndGlassTop,Mathf.Clamp01(t));
            }
        }

        // ---- Bands ----------------------------------------------------------------------------

        /// <summary>The board line from one side of the service gate around to the other, clipped
        /// exactly at |side| = clipSide (on the true arc, so inner points stay on the board line).
        /// Returned in loop order, from the southwest gate end to the northeast gate end.</summary>
        public static List<Point> ClippedChain(List<Point> boundary,SimulatorConfig c,ServiceLayout layout,float clipSide,float radius)
        {
            int n=boundary.Count,gate=0;float best=float.MaxValue;
            for(int i=0;i<n;i++)
            {
                layout.ToLocal(boundary[i].Position.x,boundary[i].Position.z,out float side,out float outward);
                float score=Mathf.Abs(side)+Mathf.Abs(outward);
                if(score<best){best=score;gate=i;}
            }
            bool Cut(int i)
            {
                layout.ToLocal(boundary[i].Position.x,boundary[i].Position.z,out float side,out _);
                return Mathf.Abs(side)<clipSide+.01f;
            }
            int first=gate;while(Cut(first%n))first++;
            int last=gate+n;while(Cut(last%n))last--;
            var chain=new List<Point>{ArcPoint(c,layout,-clipSide,radius)};
            for(int i=first;i<=last;i++)
            {
                var p=boundary[i%n];
                if(radius!=c.CornerRadius)p.Position+=p.Normal*(radius-c.CornerRadius);
                chain.Add(p);
            }
            chain.Add(ArcPoint(c,layout,clipSide,radius));
            return chain;
        }

        /// <summary>Point on the gate corner's arc (radius from its centre) at a given gate side.</summary>
        public static Point ArcPoint(SimulatorConfig c,ServiceLayout layout,float side,float radius)
        {
            layout.ToWorld(0,0,out float gx,out float gz);
            Vector3 sideAxis=new Vector3(ServiceLayout.Axis,0,ServiceLayout.Axis),outAxis=new Vector3(ServiceLayout.Axis,0,-ServiceLayout.Axis);
            Vector3 centre=new Vector3(gx,0,gz)-outAxis*c.CornerRadius;
            float phi=Mathf.Asin(Mathf.Clamp(side/radius,-1,1));
            Vector3 n=sideAxis*Mathf.Sin(phi)+outAxis*Mathf.Cos(phi);
            return new Point{Position=centre+n*radius,Normal=n};
        }

        /// <summary>A closed band: ice-facing face on the board line (plus inset), outer face, top,
        /// optional bottom and both end caps. heights/bases give a varying top/base (null: y1/y0).</summary>
        public static Mesh Band(string name,SimulatorConfig c,ServiceLayout layout,List<Point> boundary,float y0,float y1,float thickness,float clipSide,
            Func<Vector3,float> heights=null,bool top=true,bool bottom=false,float inset=0,Func<Vector3,float> bases=null)
        {
            var inner=ClippedChain(boundary,c,layout,clipSide,c.CornerRadius+inset);
            var outer=ClippedChain(boundary,c,layout,clipSide,c.CornerRadius+inset+thickness);
            var b=new Builder();
            int count=inner.Count;
            var tops=new float[count];var lows=new float[count];float run=0;var u=new float[count];
            for(int i=0;i<count;i++)
            {
                tops[i]=heights!=null?heights(inner[i].Position):y1;
                lows[i]=bases!=null?bases(inner[i].Position):y0;
                if(i>0)run+=Vector3.Distance(inner[i].Position,inner[i-1].Position);u[i]=run;
            }
            Vector3 Up(Vector3 p,float y){p.y=y;return p;}
            for(int i=0;i+1<count;i++)
            {
                Vector3 ai=inner[i].Position,bi=inner[i+1].Position,ao=outer[i].Position,bo=outer[i+1].Position;
                // Smooth ice face and back face: shared, per-vertex board normals.
                b.Quad(Up(ai,lows[i]),Up(ai,tops[i]),Up(bi,tops[i+1]),Up(bi,lows[i+1]),-inner[i].Normal,-inner[i].Normal,-inner[i+1].Normal,-inner[i+1].Normal,u[i],u[i+1],lows[i],tops[i],tops[i+1]);
                b.Quad(Up(ao,lows[i]),Up(bo,lows[i+1]),Up(bo,tops[i+1]),Up(ao,tops[i]),outer[i].Normal,outer[i+1].Normal,outer[i+1].Normal,outer[i].Normal,u[i],u[i+1],lows[i],tops[i],tops[i+1]);
                if(top)b.Flat(Up(ai,tops[i]),Up(ao,tops[i]),Up(bo,tops[i+1]),Up(bi,tops[i+1]),TopNormal(ai,bi,ao,tops[i],tops[i+1]));
                if(bottom)b.Flat(Up(ai,lows[i]),Up(bi,lows[i+1]),Up(bo,lows[i+1]),Up(ao,lows[i]),-TopNormal(ai,bi,ao,lows[i],lows[i+1]));
            }
            // Closed end caps in the clip planes at both gate ends.
            Vector3 startNormal=(inner[0].Position-inner[1].Position);startNormal.y=0;
            Vector3 endNormal=(inner[count-1].Position-inner[count-2].Position);endNormal.y=0;
            int e=count-1;
            b.Flat(Up(inner[0].Position,lows[0]),Up(inner[0].Position,tops[0]),Up(outer[0].Position,tops[0]),Up(outer[0].Position,lows[0]),startNormal.normalized);
            b.Flat(Up(inner[e].Position,lows[e]),Up(inner[e].Position,tops[e]),Up(outer[e].Position,tops[e]),Up(outer[e].Position,lows[e]),endNormal.normalized);
            return b.Mesh(name);
        }

        static Vector3 TopNormal(Vector3 a,Vector3 b,Vector3 outerA,float ya,float yb)
        {
            Vector3 along=new Vector3(b.x-a.x,yb-ya,b.z-a.z),across=outerA-a;across.y=0;
            Vector3 n=Vector3.Cross(along,across).normalized;
            return n.y<0?-n:n;
        }

        // ---- Gate posts ----------------------------------------------------------------------

        public sealed class GatePost
        {
            public string Side;
            /// <summary>Plan outline (arena x,z), counter-clockwise from the ice/opening corner.</summary>
            public Vector2[] Plan;
            public Vector3 GuardBottom, GuardTop, StanchionCentre, ReturnStart, ReturnEnd;
            public float GlassTop;
        }

        /// <summary>The solid board post at each end of the service entrance: ice face on the board
        /// line, opening face 3 cm outside the 2.7 m clear opening, back face at the gate jamb.</summary>
        public static GatePost[] GatePosts(SimulatorConfig c,ServiceLayout layout,GlassProfile glass,float uprightFront)
        {
            var posts=new GatePost[2];
            for(int k=0;k<2;k++)
            {
                float sign=k==0?-1:1,inner=layout.GateHalfWidth+PostInnerClearance,outerSide=inner+PostWidth;
                Vector3 W(float side,float outward){layout.ToWorld(sign*side,outward,out float x,out float z);return new Vector3(x,0,z);}
                Vector3 a=ArcPoint(c,layout,sign*inner,c.CornerRadius).Position,bp=ArcPoint(c,layout,sign*outerSide,c.CornerRadius).Position;
                layout.ToLocal(a.x,a.z,out _,out float aOut);
                // Chamfer the exposed ice/opening corner.
                Vector3 alongFace=(bp-a).normalized;
                Vector3 a1=a+alongFace*PostChamfer,a0=W(inner,aOut+PostChamfer);
                Vector3 cBack=W(outerSide,-.002f),dBack=W(inner,-.002f);
                var plan=new[]{a0,a1,bp,cBack,dBack};
                // Keep the outline counter-clockwise seen from above (Builder.Prism winds from it).
                float area=0;for(int i=0;i<plan.Length;i++){var p=plan[i];var q=plan[(i+1)%plan.Length];area+=p.x*q.z-q.x*p.z;}
                if(area<0)Array.Reverse(plan);
                var post=new GatePost{Side=k==0?"southwest":"northeast",Plan=Array.ConvertAll(plan,p=>new Vector2(p.x,p.z))};
                // Rubber edge guard on the chamfer, set 12 mm into the post (stays outside the opening).
                Vector3 chamferMid=(a0+a1)*.5f,inward=(W(inner+.1f,aOut+.1f)-chamferMid).normalized;
                Vector3 guard=chamferMid+inward*.012f;
                post.GuardBottom=guard+Vector3.up*(KickTop+.01f);post.GuardTop=guard+Vector3.up*(BoardTop-.005f);
                var mid=ArcPoint(c,layout,sign*(inner+PostWidth*.5f),c.CornerRadius);
                post.StanchionCentre=mid.Position+mid.Normal*(GlassThickness*.5f);
                post.GlassTop=glass.Top(mid.Position);
                post.ReturnStart=mid.Position+mid.Normal*.03f;
                post.ReturnEnd=W(inner+PostWidth*.5f,uprightFront);
                posts[k]=post;
            }
            return posts;
        }

        /// <summary>Glass clip at the post centre line; boards clip at the post's far face.</summary>
        public static float BoardClip(ServiceLayout layout){ return layout.GateHalfWidth+PostInnerClearance+PostWidth; }
        public static float GlassClip(ServiceLayout layout){ return layout.GateHalfWidth+PostInnerClearance+PostWidth*.5f; }

        // ---- Mesh helpers -------------------------------------------------------------------

        public static Mesh Prism(string name,Vector2[] plan,float y0,float y1)
        {
            var b=new Builder();
            int n=plan.Length;
            for(int i=0;i<n;i++)
            {
                Vector2 p=plan[i],q=plan[(i+1)%n];
                Vector3 normal=new Vector3(q.y-p.y,0,-(q.x-p.x)).normalized;
                b.Flat(new Vector3(p.x,y0,p.y),new Vector3(p.x,y1,p.y),new Vector3(q.x,y1,q.y),new Vector3(q.x,y0,q.y),normal);
            }
            for(int i=1;i+1<n;i++)
            {
                b.Triangle(new Vector3(plan[0].x,y1,plan[0].y),new Vector3(plan[i].x,y1,plan[i].y),new Vector3(plan[i+1].x,y1,plan[i+1].y),Vector3.up);
                b.Triangle(new Vector3(plan[0].x,y0,plan[0].y),new Vector3(plan[i].x,y0,plan[i].y),new Vector3(plan[i+1].x,y0,plan[i+1].y),Vector3.down);
            }
            return b.Mesh(name);
        }

        /// <summary>Flat-panel quad with thickness (glass return), between two plan points.</summary>
        public static Mesh Panel(string name,Vector3 a,Vector3 b,float y0,float y1,float thickness)
        {
            Vector3 along=(b-a);along.y=0;Vector3 n=new Vector3(-along.z,0,along.x).normalized*(thickness*.5f);
            var p=new[]{new Vector2(a.x-n.x,a.z-n.z),new Vector2(b.x-n.x,b.z-n.z),new Vector2(b.x+n.x,b.z+n.z),new Vector2(a.x+n.x,a.z+n.z)};
            float area=0;for(int i=0;i<4;i++){var u=p[i];var v=p[(i+1)%4];area+=u.x*v.y-v.x*u.y;}
            if(area<0)Array.Reverse(p);
            return Prism(name,p,y0,y1);
        }

        /// <summary>Triangle lists with explicit normals; every face is wound to face its normal.</summary>
        sealed class Builder
        {
            readonly List<Vector3> v=new List<Vector3>(),n=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>();
            readonly List<int> t=new List<int>();
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector3 normal)
            {
                if(Vector3.Dot(Vector3.Cross(b-a,c-a),normal)<0){var s=b;b=c;c=s;}
                int i=v.Count;v.Add(a);v.Add(b);v.Add(c);n.Add(normal);n.Add(normal);n.Add(normal);
                uv.Add(new Vector2(a.x+a.z,a.y));uv.Add(new Vector2(b.x+b.z,b.y));uv.Add(new Vector2(c.x+c.z,c.y));
                t.Add(i);t.Add(i+1);t.Add(i+2);
            }
            public void Flat(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal){ Quad(a,b,c,d,normal,normal,normal,normal,0,0,0,0,0,true); }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 na,Vector3 nb,Vector3 nc,Vector3 nd,float u0,float u1,float y0,float ya,float yb,bool flat=false)
            {
                Vector3 normal=(na+nb+nc+nd).normalized;
                bool flip=Vector3.Dot(Vector3.Cross(b-a,c-a),normal)<0;
                int i=v.Count;
                v.Add(a);v.Add(b);v.Add(c);v.Add(d);n.Add(na);n.Add(nb);n.Add(nc);n.Add(nd);
                if(flat){uv.Add(new Vector2(a.x+a.z,a.y));uv.Add(new Vector2(b.x+b.z,b.y));uv.Add(new Vector2(c.x+c.z,c.y));uv.Add(new Vector2(d.x+d.z,d.y));}
                else{uv.Add(new Vector2(u0,y0));uv.Add(new Vector2(u0,ya));uv.Add(new Vector2(u1,yb));uv.Add(new Vector2(u1,y0));}
                if(!flip){t.Add(i);t.Add(i+1);t.Add(i+2);t.Add(i);t.Add(i+2);t.Add(i+3);}
                else{t.Add(i);t.Add(i+2);t.Add(i+1);t.Add(i);t.Add(i+3);t.Add(i+2);}
            }
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh{name=name};
                if(v.Count>65000)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);mesh.RecalculateBounds();
                return mesh;
            }
            public void CopyTo(Mesh mesh)
            {
                mesh.Clear();mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);mesh.RecalculateBounds();
            }
        }

        /// <summary>Replaces a mesh's data in place (keeps the asset object, its GUID and file ID).</summary>
        public static void CopyInto(Mesh source,Mesh target)
        {
            string name=target.name;
            target.Clear();
            target.indexFormat=source.indexFormat;
            target.SetVertices(source.vertices);target.SetNormals(source.normals);target.SetUVs(0,source.uv);
            target.SetTriangles(source.triangles,0);target.RecalculateBounds();
            target.name=name;
        }
    }
}
