using System;

namespace IceResurfacer.Core
{
    public struct SnowSurfacePoint
    {
        public float X,Y,Z;
        public SnowSurfacePoint(float x,float y,float z){X=x;Y=y;Z=z;}
    }
    public struct SnowSurfacePatch
    {
        public SnowSurfacePoint A,B,C,D,Center;
        public double Volume;
    }
    /// <summary>Allocation-free, cell-local faceted reconstruction of real loose volume.</summary>
    public static class SnowSurfaceGeometry
    {
        public const float MeshThresholdMm=.55f;
        public static float Grain(int x,int z)
        {
            unchecked
            {
                uint n=(uint)(x*374761393+z*668265263);
                n=(n^(n>>13))*1274126177u;n^=n>>16;
                return (n&65535)/65535f;
            }
        }
        static float Corner(IceField field,int x,int z,float roughness)
        {
            float depth=float.MaxValue;
            for(int dz=-1;dz<=0;dz++)for(int dx=-1;dx<=0;dx++)
            {
                int col=x+dx,row=z+dz;
                if(col<0||row<0||col>=field.Columns||row>=field.Rows)return 0;
                var c=field.Cells[field.Index(col,row)];
                if(!c.IsIce)return 0;
                depth=Math.Min(depth,c.SnowMm);
            }
            return depth*.001f*(1-roughness*(.3f+.4f*Grain(x,z)));
        }
        static SnowSurfacePoint Point(IceField field,float x,float y,float z,bool boundary)
        {
            if(boundary)
            {
                float distance=field.BoardDistance(x,z,out float nx,out float nz);
                if(distance>-.0001f){x+=nx*(distance+.0001f);z+=nz*(distance+.0001f);}
            }
            return new SnowSurfacePoint(x,y,z);
        }
        public static double Area(SnowSurfacePoint a,SnowSurfacePoint b,SnowSurfacePoint c)
        {return Math.Abs((double)(b.X-a.X)*(c.Z-a.Z)-(double)(b.Z-a.Z)*(c.X-a.X))*.5;}
        public static bool Build(IceField field,int col,int row,float roughness,out SnowSurfacePatch patch)
        {
            patch=default;
            var cell=field.Cells[field.Index(col,row)];
            if(!cell.IsIce||cell.SnowMm<=MeshThresholdMm)return false;
            roughness=Simulation.Clamp(roughness,0,1);
            float size=field.CellSize,x=field.MinX+col*size,z=field.MinZ+row*size;
            bool boundary=field.BoardDistance(x+size*.5f,z+size*.5f,out _,out _)>-size;
            patch.A=Point(field,x,Corner(field,col,row,roughness),z,boundary);
            patch.B=Point(field,x,Corner(field,col,row+1,roughness),z+size,boundary);
            patch.C=Point(field,x+size,Corner(field,col+1,row+1,roughness),z+size,boundary);
            patch.D=Point(field,x+size,Corner(field,col+1,row,roughness),z,boundary);
            float u=.5f+(Grain(col,row)-.5f)*roughness*.35f;
            float v=.5f+(Grain(row,col+71)-.5f)*roughness*.35f;
            patch.Center=new SnowSurfacePoint(
                (patch.A.X*(1-u)+patch.D.X*u)*(1-v)+(patch.B.X*(1-u)+patch.C.X*u)*v,0,
                (patch.A.Z*(1-u)+patch.D.Z*u)*(1-v)+(patch.B.Z*(1-u)+patch.C.Z*u)*v);
            double ab=Area(patch.A,patch.B,patch.Center),bc=Area(patch.B,patch.C,patch.Center);
            double cd=Area(patch.C,patch.D,patch.Center),da=Area(patch.D,patch.A,patch.Center);
            double area=ab+bc+cd+da;
            if(area<1e-10)return false;
            patch.Volume=(double)size*size*cell.SnowMm*.001;
            // Exact triangle integral: crests occupy less area. No stock gain
            // and no reconstructed geometry spreading into a cleared cell.
            patch.Center.Y=(float)((3*patch.Volume-ab*(patch.A.Y+patch.B.Y)-bc*(patch.B.Y+patch.C.Y)-
                cd*(patch.C.Y+patch.D.Y)-da*(patch.D.Y+patch.A.Y))/area);
            return true;
        }
    }
}
