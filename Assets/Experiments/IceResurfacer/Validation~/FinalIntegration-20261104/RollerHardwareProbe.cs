using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

// Read-only serialized geometry audit. Built-in cylinder/cube surfaces are sampled directly,
// so this can measure the authored guide and tyre without loading or mutating Unity assets.
internal static class RollerHardwareProbe
{
    sealed class Part
    {
        public long id,game,parent,mesh;
        public Vector3 position,scale;
        public Quaternion rotation;
    }
    static readonly Dictionary<long,Part> parts=new Dictionary<long,Part>();
    static readonly Dictionary<long,string> blocks=new Dictionary<long,string>();
    const long Group=5722889660757518116,Bracket=5818297522479415069,Wheel=4287953743120569709;
    static long Id(string s,string field) => long.Parse(Regex.Match(s,field+@": \{fileID: (-?\d+)").Groups[1].Value);
    static Vector3 V(string s,string field)
    {
        var m=Regex.Match(s,field+@": \{x: ([^,]+), y: ([^,]+), z: ([^,}]+)");
        return new Vector3(float.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture),float.Parse(m.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture),float.Parse(m.Groups[3].Value,System.Globalization.CultureInfo.InvariantCulture));
    }
    static Matrix4x4 Matrix(Part p)
    {
        var result=Matrix4x4.CreateScale(p.scale)*Matrix4x4.CreateFromQuaternion(p.rotation)*Matrix4x4.CreateTranslation(p.position);
        return parts.TryGetValue(p.parent,out var parent)?result*Matrix(parent):result;
    }
    static bool Below(Part p,long ancestor) => p.id==ancestor||parts.TryGetValue(p.parent,out var parent)&&Below(parent,ancestor);
    static IEnumerable<Vector3> Vertices(Part p)
    {
        string game=blocks[p.game];
        foreach(Match component in Regex.Matches(game,@"component: \{fileID: (-?\d+)"))
        {
            string block=blocks[long.Parse(component.Groups[1].Value)];
            if(!block.StartsWith("MeshFilter:"))continue;
            long mesh=Id(block,"m_Mesh");
            p.mesh=mesh;
            if(mesh==10206) // Unity built-in cylinder: radius .5, half-height 1.
                for(int n=0;n<360;n++)for(int cap=-1;cap<=1;cap+=2)
                {float angle=n*MathF.PI/180;yield return new Vector3(.5f*MathF.Cos(angle),cap,.5f*MathF.Sin(angle));}
            else if(mesh==10202) // Unity built-in cube: unit sides.
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)yield return new Vector3(x*.5f,y*.5f,z*.5f);
            else throw new Exception("Unsupported hardware mesh "+mesh+" on transform "+p.id);
        }
    }
    public static void Run()
    {
        string source=File.ReadAllText("Assets/Experiments/IceResurfacer/Machine/AuroraR54.prefab");
        foreach(Match m in Regex.Matches(source,@"(?ms)^--- !u!\d+ &(-?\d+)\r?\n(.*?)(?=^--- !u!|\z)"))blocks[long.Parse(m.Groups[1].Value)]=m.Groups[2].Value;
        foreach(var b in blocks.Where(b=>b.Value.StartsWith("Transform:")))
        {
            var p=new Part{id=b.Key,game=Id(b.Value,"m_GameObject"),parent=Id(b.Value,"m_Father"),position=V(b.Value,"m_LocalPosition"),scale=V(b.Value,"m_LocalScale")};
            Vector3 q=V(b.Value,"m_LocalRotation");float w=float.Parse(Regex.Match(b.Value,@"m_LocalRotation: .*w: ([^}]+)").Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);
            p.rotation=new Quaternion(q,w);parts[p.id]=p;
        }
        foreach(bool inset in new[]{false,true})
        {
            // Proposed correction is evaluated in memory; the authored source file is never written.
            parts[Group].position.X=inset?-.827f:-1.0009999f;
            parts[Bracket].position.X=inset?.09749997f:.18449986f;
            parts[Bracket].scale.Y=inset?.097500004f:.18450001f;
            var wheel=parts[Wheel];var rest=wheel.rotation;
            Matrix4x4.Invert(Matrix(wheel),out var inverse);
            float tyreRadius=0,tyreHalf=0;
            foreach(var p in parts.Values.Where(p=>Below(p,Wheel)))foreach(var v in Vertices(p))
            {
                Vector3 q=Vector3.Transform(Vector3.Transform(v,Matrix(p)),inverse);
                tyreRadius=Math.Max(tyreRadius,new Vector2(q.Y,q.Z).Length());tyreHalf=Math.Max(tyreHalf,Math.Abs(q.X));
            }
            float clearance=float.PositiveInfinity,at=0,exact=float.PositiveInfinity,swept=float.PositiveInfinity,sliced=float.PositiveInfinity;string hit="",sweptHit="";Vector3 sweptPoint=default;int sweptPart=0;
            var assembly=new List<(Part part,Vector3 point)>();
            foreach(var p in parts.Values.Where(p=>Below(p,Group)))foreach(var v in Vertices(p))assembly.Add((p,Vector3.Transform(v,Matrix(p))));
            var tyres=parts.Values.Where(p=>Below(p,Wheel)&&p.mesh!=0).ToArray();
            var sweeps=tyres.Select(p=>
            {
                var points=Vertices(p).Select(v=>Vector3.Transform(Vector3.Transform(v,Matrix(p)),inverse)).ToArray();
                return (low:points.Min(v=>v.X),high:points.Max(v=>v.X),radius:points.Max(v=>new Vector2(v.Y,v.Z).Length()));
            }).ToArray();
            var localInverses=tyres.Select(p=>{Matrix4x4.Invert(Matrix(p)*inverse,out var inv);return inv;}).ToArray();
            // Preserve the tread's axial/radial correlation (the traction blocks are tilted sideways).
            // Each 1 mm axial strip bounds its true swept radius. Filling each strip inward is conservative.
            const int bins=340;const float binWidth=.001f,binStart=-.17f;
            var radii=new float[bins];
            for(int i=0;i<tyres.Length;i++)
            {
                var p=tyres[i];var vertices=Vertices(p).Select(v=>Vector3.Transform(Vector3.Transform(v,Matrix(p)),inverse)).ToArray();
                for(int b=0;b<bins;b++)
                {
                    float lo=binStart+b*binWidth,hi=lo+binWidth;
                    if(sweeps[i].low>hi||sweeps[i].high<lo)continue;
                    if(p.mesh==10206){radii[b]=Math.Max(radii[b],sweeps[i].radius);continue;}
                    foreach(var v in vertices)if(v.X>=lo&&v.X<=hi)radii[b]=Math.Max(radii[b],new Vector2(v.Y,v.Z).Length());
                    for(int a=0;a<vertices.Length;a++)for(int c=a+1;c<vertices.Length;c++)
                    {
                        Vector3 va=vertices[a],vc=vertices[c];if(Math.Abs(vc.X-va.X)<1e-7f)continue;
                        foreach(float boundary in new[]{lo,hi})
                        {
                            float t=(boundary-va.X)/(vc.X-va.X);if(t<0||t>1)continue;
                            Vector3 v=Vector3.Lerp(va,vc,t);radii[b]=Math.Max(radii[b],new Vector2(v.Y,v.Z).Length());
                        }
                    }
                }
            }
            for(int step=0;step<=400;step++)
            {
                float steer=-.412f+.824f*step/400;
                wheel.rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,steer)*rest;
                Matrix4x4.Invert(Matrix(wheel),out inverse);
                var tyreInverses=tyres.Select(p=>{Matrix4x4.Invert(Matrix(p),out var inv);return inv;}).ToArray();
                foreach(var v in assembly)
                {
                    Vector3 q=Vector3.Transform(v.point,inverse);
                    float d=Math.Max(new Vector2(q.Y,q.Z).Length()-tyreRadius,Math.Abs(q.X)-tyreHalf);
                    if(d<clearance){clearance=d;at=steer;}
                    float radial=new Vector2(q.Y,q.Z).Length();
                    for(int bin=0;bin<bins;bin++)
                    {
                        if(radii[bin]==0)continue;float lo=binStart+bin*binWidth,hi=lo+binWidth;
                        float gap=Math.Max(radial-radii[bin],Math.Max(lo-q.X,q.X-hi));
                        if(gap<sliced)sliced=gap;
                    }
                    for(int i=0;i<tyres.Length;i++)
                    {
                        var sweep=sweeps[i];float rotational=Math.Max(new Vector2(q.Y,q.Z).Length()-sweep.radius,Math.Max(sweep.low-q.X,q.X-sweep.high));
                        if(rotational<swept){swept=rotational;sweptPoint=q;sweptPart=i;sweptHit=Regex.Match(blocks[v.part.game],@"m_Name: (.*)").Groups[1].Value+" vs "+Regex.Match(blocks[tyres[i].game],@"m_Name: (.*)").Groups[1].Value+" at steer "+(steer*180/Math.PI).ToString("F2");}
                        Vector3 t=Vector3.Transform(v.point,tyreInverses[i]);var p=tyres[i];
                        float inner=p.mesh==10206?Math.Max((new Vector2(t.X,t.Z).Length()-.5f)*Math.Min(p.scale.X,p.scale.Z),(Math.Abs(t.Y)-1)*p.scale.Y):Math.Max((Math.Abs(t.X)-.5f)*p.scale.X,Math.Max((Math.Abs(t.Y)-.5f)*p.scale.Y,(Math.Abs(t.Z)-.5f)*p.scale.Z));
                        if(inner<exact){exact=inner;hit=Regex.Match(blocks[v.part.game],@"m_Name: (.*)").Groups[1].Value+" vs "+Regex.Match(blocks[p.game],@"m_Name: (.*)").Groups[1].Value+" at steer "+(steer*180/Math.PI).ToString("F2");}
                    }
                }
            }
            wheel.rotation=rest;
            Console.WriteLine("  "+(inset?"compact":"prior outboard")+" serialized guide hardware: tyre conservative cylinder radius "+tyreRadius.ToString("F4")+", half width "+tyreHalf.ToString("F4")+", minimum separation "+(1000*clearance).ToString("F2")+" mm at steer "+(at*180/Math.PI).ToString("F2")+" deg");
            Console.WriteLine("    Actual primitive closest surface "+(exact*1000).ToString("F2")+" mm: "+hit);
            Console.WriteLine("    Per-part rotational swept hull separation "+(swept*1000).ToString("F2")+" mm: "+sweptHit);
            float spinning=float.PositiveInfinity,spinAt=0;
            for(int degrees=0;degrees<3600;degrees++)
            {
                var p=tyres[sweptPart];Vector3 t=Vector3.Transform(Vector3.Transform(sweptPoint,Matrix4x4.CreateRotationX(degrees*MathF.PI/1800)),localInverses[sweptPart]);
                float d=p.mesh==10206?Math.Max((new Vector2(t.X,t.Z).Length()-.5f)*Math.Min(p.scale.X,p.scale.Z),(Math.Abs(t.Y)-1)*p.scale.Y):Math.Max((Math.Abs(t.X)-.5f)*p.scale.X,Math.Max((Math.Abs(t.Y)-.5f)*p.scale.Y,(Math.Abs(t.Z)-.5f)*p.scale.Z));
                if(d<spinning){spinning=d;spinAt=degrees*.1f;}
            }
            Console.WriteLine("    Actual rotating tread at limiting point "+(spinning*1000).ToString("F2")+" mm at spin "+spinAt.ToString("F1")+" deg");
            Console.WriteLine("    Conservative 1 mm axial-strip swept tyre separation "+(sliced*1000).ToString("F2")+" mm, all wheel roll angles");
            if(sliced<.001f)throw new Exception("Compact guide hardware has insufficient clearance from the actual swept tyre profile.");
        }
    }
}
