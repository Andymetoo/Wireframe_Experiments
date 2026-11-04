using System;
using System.Collections.Generic;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEngine;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// Fits the simulated collision envelope to the authored machine as drawn (plan view, all heights:
    /// the boards and glass are a full-height wall). The chassis outline encloses every mesh vertex of the
    /// machine except the conditioner, the articulated board brush (compliant; it has its own contact) and
    /// the guide roller assembly (its own circle). It is the 5 cm station envelope, widened to cover each
    /// neighbouring station, simplified only where every vertex stays inside within 6 mm. The conditioner
    /// becomes a box about the simulated conditioner centre from its full-width parts.
    /// </summary>
    public static class IceCollisionOutlineFit
    {
        public const float Station=.05f, Tolerance=.006f;

        public sealed class Fit
        {
            public float[] Outline;
            public float ConditionerHalfWidth, ConditionerFront, ConditionerRear;
            public int Vertices, Renderers;
            public float MaxHalfWidth;
        }

        public static Fit Measure(IceWorld w,SimulatorConfig c)
        {
            Transform m=w.Machine,root=m.parent!=null?m.parent:m;
            var brush=w.BrushArm;var roller=w.GuideRoller!=null&&w.GuideRoller.parent!=null&&w.GuideRoller.parent!=m?w.GuideRoller.parent:w.GuideRoller;
            var left=new SortedDictionary<int,float>();var right=new SortedDictionary<int,float>();
            float condMinZ=float.PositiveInfinity,condMaxZ=float.NegativeInfinity,condHalf=0;
            var condPoints=new List<Vector3>();
            int renderers=0;
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                if(r.GetComponent<TextMesh>()!=null)continue;
                var f=r.GetComponent<MeshFilter>();if(f==null||f.sharedMesh==null)continue;
                if(brush!=null&&r.transform.IsChildOf(brush)||roller!=null&&r.transform.IsChildOf(roller))continue;
                bool conditioner=r.transform.IsChildOf(w.Conditioner);
                // Members that span the lift belong to neither rigid body; they lie inside both envelopes.
                if(conditioner&&IsSpan(r.transform))continue;
                renderers++;
                var mesh=f.sharedMesh;
                foreach(var v in mesh.isReadable?mesh.vertices:Corners(mesh.bounds))
                {
                    Vector3 p=m.InverseTransformPoint(r.transform.TransformPoint(v));
                    if(conditioner){condPoints.Add(w.Conditioner.InverseTransformPoint(r.transform.TransformPoint(v)));continue;}
                    int k=Mathf.FloorToInt(p.z/Station);
                    left[k]=left.TryGetValue(k,out float a)?Mathf.Min(a,p.x):p.x;
                    right[k]=right.TryGetValue(k,out float b)?Mathf.Max(b,p.x):p.x;
                }
            }
            var fit=new Fit{Renderers=renderers};
            fit.Outline=Envelope(left,right);
            fit.Vertices=fit.Outline.Length/2;
            // Conditioner: its full-width parts (within 0.15 m of its widest point) set the box length.
            foreach(var p in condPoints)condHalf=Mathf.Max(condHalf,Mathf.Abs(p.x));
            foreach(var p in condPoints)if(Mathf.Abs(p.x)>=condHalf-.15f){condMinZ=Mathf.Min(condMinZ,p.z);condMaxZ=Mathf.Max(condMaxZ,p.z);}
            // Relative to the simulated conditioner centre (where Play puts the conditioner root).
            fit.ConditionerHalfWidth=Round(condHalf);
            fit.ConditionerFront=Round(Mathf.Max(0,condMaxZ));
            fit.ConditionerRear=Round(Mathf.Max(0,-condMinZ));
            for(int i=0;i<fit.Outline.Length;i+=2)fit.MaxHalfWidth=Mathf.Max(fit.MaxHalfWidth,Mathf.Abs(fit.Outline[i]));
            return fit;
        }

        static bool IsSpan(Transform t)
        {
            var marker=t.GetComponent<IceConditionerSpan>();
            return marker!=null?marker.spanLift:t.name=="Lift linkage"||t.name.StartsWith("Hydraulic flexible hose");
        }

        static float[] Envelope(SortedDictionary<int,float> left,SortedDictionary<int,float> right)
        {
            int lo=int.MaxValue,hi=int.MinValue;
            foreach(int k in left.Keys){lo=Math.Min(lo,k);hi=Math.Max(hi,k);}
            var l=new List<Vector2>();var r=new List<Vector2>();
            for(int k=lo;k<=hi;k++)
            {
                float a=float.PositiveInfinity,b=float.NegativeInfinity;bool any=false;
                for(int j=k-1;j<=k+1;j++)
                {
                    if(left.TryGetValue(j,out float x)){a=Mathf.Min(a,x);any=true;}
                    if(right.TryGetValue(j,out float y))b=Mathf.Max(b,y);
                }
                if(!any)continue;
                float z=(k+.5f)*Station;
                l.Add(new Vector2(a,z));r.Add(new Vector2(b,z));
            }
            float zMin=lo*Station,zMax=(hi+1)*Station;
            l.Insert(0,new Vector2(l[0].x,zMin));l.Add(new Vector2(l[l.Count-1].x,zMax));
            r.Insert(0,new Vector2(r[0].x,zMin));r.Add(new Vector2(r[r.Count-1].x,zMax));
            var sl=Simplify(l,-1);var sr=Simplify(r,1);
            var outline=new List<float>();
            void Add(Vector2 p){float x=Round(p.x),z=Round(p.y);int n=outline.Count;if(n>=2&&outline[n-2]==x&&outline[n-1]==z)return;outline.Add(x);outline.Add(z);}
            foreach(var p in sl)Add(p);
            for(int i=sr.Count-1;i>=0;i--)Add(sr[i]);
            return outline.ToArray();
        }

        /// <summary>Drops points while every dropped station stays inside the chord (outward side) within the tolerance.</summary>
        static List<Vector2> Simplify(List<Vector2> points,int outward)
        {
            var keep=new List<Vector2>{points[0]};
            int i=0;
            while(i<points.Count-1)
            {
                int j=i+1;
                while(j+1<points.Count)
                {
                    Vector2 a=points[i],b=points[j+1];bool ok=true;
                    for(int k=i+1;k<=j&&ok;k++)
                    {
                        var p=points[k];
                        float t=Mathf.Approximately(b.y,a.y)?0:(p.y-a.y)/(b.y-a.y),x=a.x+(b.x-a.x)*t;
                        if(outward<0&&x>p.x+1e-5f||outward>0&&x<p.x-1e-5f||Mathf.Abs(x-p.x)>Tolerance)ok=false;
                    }
                    if(!ok)break;
                    j++;
                }
                keep.Add(points[j]);i=j;
            }
            return keep;
        }

        static float Round(float v){ return Mathf.Round(v*1000)/1000; }
        static Vector3[] Corners(Bounds b){var list=new Vector3[8];for(int i=0;i<8;i++)list[i]=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));return list;}

        /// <summary>Left/right extent of a polygon at z (NaN when z is outside it).</summary>
        public static void Extent(float[] polygon,float z,out float left,out float right)
        {
            left=float.NaN;right=float.NaN;
            for(int i=0;i<polygon.Length;i+=2)
            {
                int j=(i+2)%polygon.Length;
                float az=polygon[i+1],bz=polygon[j+1];
                if(z<Mathf.Min(az,bz)||z>Mathf.Max(az,bz))continue;
                float x=Mathf.Approximately(az,bz)?Mathf.Min(polygon[i],polygon[j]):polygon[i]+(polygon[j]-polygon[i])*(z-az)/(bz-az);
                float x2=Mathf.Approximately(az,bz)?Mathf.Max(polygon[i],polygon[j]):x;
                left=float.IsNaN(left)?Mathf.Min(x,x2):Mathf.Min(left,Mathf.Min(x,x2));
                right=float.IsNaN(right)?Mathf.Max(x,x2):Mathf.Max(right,Mathf.Max(x,x2));
            }
        }

        /// <summary>Largest left/right difference (m) between two plan outlines over their z range, and where.</summary>
        public static float Difference(float[] a,float[] b,out float atZ)
        {
            atZ=0;float worst=0,zMin=float.PositiveInfinity,zMax=float.NegativeInfinity;
            foreach(var poly in new[]{a,b})for(int i=1;i<poly.Length;i+=2){zMin=Mathf.Min(zMin,poly[i]);zMax=Mathf.Max(zMax,poly[i]);}
            for(float z=zMin+.001f;z<zMax;z+=.01f)
            {
                Extent(a,z,out float al,out float ar);Extent(b,z,out float bl,out float br);
                float d=float.IsNaN(al)!=float.IsNaN(bl)?.5f:float.IsNaN(al)?0:Mathf.Max(Mathf.Abs(al-bl),Mathf.Abs(ar-br));
                if(d>worst){worst=d;atZ=z;}
            }
            return worst;
        }
    }
}
