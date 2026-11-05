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
        public SnowSurfacePoint A,B,C,D,InnerA,InnerB,InnerC,InnerD,Center;
        public double Volume;
        public SnowSurfacePoint Point(int index)
        {
            switch(index)
            {
                case 0:return A;case 1:return B;case 2:return C;case 3:return D;
                case 4:return InnerA;case 5:return InnerB;case 6:return InnerC;default:return InnerD;
            }
        }
    }
    /// <summary>Broad, bevelled heightfield caps with exact cell-local volume.</summary>
    public static class SnowSurfaceGeometry
    {
        public const float MeshThresholdMm=.55f;
        public const int VerticesPerPatch=8,IndicesPerPatch=30;
        static readonly int[] triangles={0,1,4,1,5,4,1,2,5,2,6,5,2,3,6,3,7,6,3,0,7,0,4,7,4,5,7,7,5,6};
        public static int TriangleVertex(int index)=>triangles[index];
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
            return depth*.001f*(1-roughness*.04f);
        }
        static float Depth(IceField field,int x,int z)
        {return x>=0&&z>=0&&x<field.Columns&&z<field.Rows&&field.Cells[field.Index(x,z)].IsIce?field.Cells[field.Index(x,z)].SnowMm*.001f:0;}
        public static SnowSurfacePoint Normal(IceField field,int x,int z)
        {
            float a=Depth(field,x-1,z-1),b=Depth(field,x-1,z),c=Depth(field,x,z),d=Depth(field,x,z-1);
            float nx=(a+b-c-d)*.5f/field.CellSize,nz=(a+d-b-c)*.5f/field.CellSize;
            float inverse=1/(float)Math.Sqrt(nx*nx+nz*nz+1);
            return new SnowSurfacePoint(nx*inverse,inverse,nz*inverse);
        }
        static SnowSurfacePoint Blend(SnowSurfacePatch p,float u,float v)
        {
            return new SnowSurfacePoint((p.A.X*(1-u)+p.D.X*u)*(1-v)+(p.B.X*(1-u)+p.C.X*u)*v,1,
                (p.A.Z*(1-u)+p.D.Z*u)*(1-v)+(p.B.Z*(1-u)+p.C.Z*u)*v);
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
            // Broad tops cover about 58% or more of a cell, rather than a single
            // needle tip. Low-frequency shape variation avoids a noisy grid.
            float grain=.5f+.5f*(float)Math.Sin(col*.31f+Math.Sin(row*.27f));
            float inset=.08f+.04f*roughness*grain;
            patch.Volume=(double)size*size*cell.SnowMm*.001;
            double outer=0,inner=0;
            if(!boundary)
            {
                float width=patch.D.X-patch.A.X,depth=patch.B.Z-patch.A.Z;
                float left=x+width*inset,right=x+width*(1-inset),bottom=z+depth*inset,top=z+depth*(1-inset);
                patch.InnerA=new SnowSurfacePoint(left,0,bottom);patch.InnerB=new SnowSurfacePoint(left,0,top);
                patch.InnerC=new SnowSurfacePoint(right,0,top);patch.InnerD=new SnowSurfacePoint(right,0,bottom);
                // Exact integral for the ordinary rectangular cap, using its
                // actual float coordinates. Avoid ten triangle/point lookups
                // per occupied cell; clipped board cells use the general path.
                double ab=depth*(double)(left-x)*.5,bc=width*(double)(patch.B.Z-top)*.5;
                double cd=depth*(double)(patch.D.X-right)*.5,da=width*(double)(bottom-z)*.5;
                double ia=(top-bottom)*(double)(left-x)*.5,ib=(right-left)*(double)(patch.B.Z-top)*.5;
                double ic=(top-bottom)*(double)(patch.D.X-right)*.5,id=(right-left)*(double)(bottom-z)*.5;
                double wa=ab+da+id,wb=ab+ia+bc,wc=bc+ib+cd,wd=cd+ic+da;
                outer=wa*patch.A.Y+wb*patch.B.Y+wc*patch.C.Y+wd*patch.D.Y;
                inner=3*(double)width*depth-wa-wb-wc-wd;
            }
            else
            {
                patch.InnerA=Blend(patch,inset,inset);patch.InnerB=Blend(patch,inset,1-inset);
                patch.InnerC=Blend(patch,1-inset,1-inset);patch.InnerD=Blend(patch,1-inset,inset);
                for(int i=0;i<IndicesPerPatch;i+=3)
                {
                    int ai=triangles[i],bi=triangles[i+1],ci=triangles[i+2];
                    var a=patch.Point(ai);var b=patch.Point(bi);var c=patch.Point(ci);double area=Area(a,b,c);
                    outer+=area*((ai<4?a.Y:0)+(bi<4?b.Y:0)+(ci<4?c.Y:0));
                    inner+=area*((ai>=4?1:0)+(bi>=4?1:0)+(ci>=4?1:0));
                }
            }
            if(inner<1e-10)return false;
            float level=(float)((3*patch.Volume-outer)/inner);
            patch.InnerA.Y=patch.InnerB.Y=patch.InnerC.Y=patch.InnerD.Y=level;
            patch.Center=Blend(patch,.5f,.5f);patch.Center.Y=level;
            return true;
        }
    }
}
