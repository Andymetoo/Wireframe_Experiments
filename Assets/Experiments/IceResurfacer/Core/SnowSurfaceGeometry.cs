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
    /// <summary>Continuous, sloped heightfield patches with exact cell-local volume.</summary>
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
        internal static float CornerHeight(IceField field,int x,int z)
        {
            float minimum=float.MaxValue;
            for(int dz=-1;dz<=0;dz++)for(int dx=-1;dx<=0;dx++)
            {
                int col=x+dx,row=z+dz;
                if(col<0||row<0||col>=field.Columns||row>=field.Rows)return 0;
                var c=field.Cells[field.Index(col,row)];
                // No surface is stretched over a cleared/sub-threshold cell.
                if(!c.IsIce||c.SnowMm<=MeshThresholdMm)return 0;
                minimum=Math.Min(minimum,c.SnowMm);
            }
            // Reconstruct one shared node from cell averages. The symmetric
            // -1,5,5,-1 / 8 stencil reproduces a constant/linear/quadratic
            // layer without the old minimum-depth valleys between every cell.
            // Limit sharp stock changes so a shallow neighbor can still own a
            // nonnegative, volume-conserving patch. No physical stock is moved.
            float value=0;
            for(int dz=-2;dz<=1;dz++)for(int dx=-2;dx<=1;dx++)
                value+=Depth(field,x+dx,z+dz)*(dx==-2||dx==1?-1:5)*(dz==-2||dz==1?-1:5);
            return Simulation.Clamp(value/64,0,minimum*.002f);
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
            return new SnowSurfacePoint((p.A.X*(1-u)+p.D.X*u)*(1-v)+(p.B.X*(1-u)+p.C.X*u)*v,
                (p.A.Y*(1-u)+p.D.Y*u)*(1-v)+(p.B.Y*(1-u)+p.C.Y*u)*v,
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
            var cell=field.Cells[field.Index(col,row)];
            if(!cell.IsIce||cell.SnowMm<=MeshThresholdMm){patch=default;return false;}
            return Build(field,col,row,roughness,CornerHeight(field,col,row),CornerHeight(field,col,row+1),
                CornerHeight(field,col+1,row+1),CornerHeight(field,col+1,row),out patch);
        }
        internal static bool Build(IceField field,int col,int row,float roughness,float a,float b,float c,float d,out SnowSurfacePatch patch)
        {
            patch=default;
            var cell=field.Cells[field.Index(col,row)];
            if(!cell.IsIce||cell.SnowMm<=MeshThresholdMm)return false;
            roughness=Simulation.Clamp(roughness,0,1);
            float size=field.CellSize,x=field.MinX+col*size,z=field.MinZ+row*size;
            bool boundary=field.BoardDistance(x+size*.5f,z+size*.5f,out _,out _)>-size;
            patch.A=Point(field,x,a,z,boundary);
            patch.B=Point(field,x,b,z+size,boundary);
            patch.C=Point(field,x+size,c,z+size,boundary);
            patch.D=Point(field,x+size,d,z,boundary);
            // Inner vertices follow the reconstructed slope; they are not a
            // separate flat cap. The broad support also bounds isolated cells.
            float grain=.5f+.5f*(float)Math.Sin(col*.31f+Math.Sin(row*.27f));
            float inset=.08f+.04f*roughness*grain;
            patch.Volume=(double)size*size*cell.SnowMm*.001;
            patch.InnerA=Blend(patch,inset,inset);patch.InnerB=Blend(patch,inset,1-inset);
            patch.InnerC=Blend(patch,1-inset,1-inset);patch.InnerD=Blend(patch,1-inset,inset);
            double outer=0,waInner=0,wbInner=0,wcInner=0,wdInner=0;
            if(!boundary)
            {
                float width=patch.D.X-patch.A.X,depth=patch.B.Z-patch.A.Z;
                float left=x+width*inset,right=x+width*(1-inset),bottom=z+depth*inset,top=z+depth*(1-inset);
                patch.InnerA.X=patch.InnerB.X=left;patch.InnerC.X=patch.InnerD.X=right;
                patch.InnerA.Z=patch.InnerD.Z=bottom;patch.InnerB.Z=patch.InnerC.Z=top;
                // Exact integral for the ordinary rectangular cap, using its
                // actual float coordinates. Avoid ten triangle/point lookups
                // per occupied cell; clipped board cells use the general path.
                double ab=depth*(double)(left-x)*.5,bc=width*(double)(patch.B.Z-top)*.5;
                double cd=depth*(double)(patch.D.X-right)*.5,da=width*(double)(bottom-z)*.5;
                double ia=(top-bottom)*(double)(left-x)*.5,ib=(right-left)*(double)(patch.B.Z-top)*.5;
                double ic=(top-bottom)*(double)(patch.D.X-right)*.5,id=(right-left)*(double)(bottom-z)*.5;
                double wa=ab+da+id,wb=ab+ia+bc,wc=bc+ib+cd,wd=cd+ic+da;
                outer=wa*patch.A.Y+wb*patch.B.Y+wc*patch.C.Y+wd*patch.D.Y;
                double topArea=(right-left)*(double)(top-bottom);
                waInner=ab+ia+id+topArea*.5;wbInner=ia+bc+ib+topArea;
                wcInner=ib+cd+ic+topArea*.5;wdInner=ic+da+id+topArea;
            }
            else
            {
                for(int i=0;i<IndicesPerPatch;i+=3)
                {
                    int ai=triangles[i],bi=triangles[i+1],ci=triangles[i+2];
                    var pa=patch.Point(ai);var pb=patch.Point(bi);var pc=patch.Point(ci);double area=Area(pa,pb,pc);
                    outer+=area*((ai<4?pa.Y:0)+(bi<4?pb.Y:0)+(ci<4?pc.Y:0));
                    waInner+=area*((ai==4?1:0)+(bi==4?1:0)+(ci==4?1:0));
                    wbInner+=area*((ai==5?1:0)+(bi==5?1:0)+(ci==5?1:0));
                    wcInner+=area*((ai==6?1:0)+(bi==6?1:0)+(ci==6?1:0));
                    wdInner+=area*((ai==7?1:0)+(bi==7?1:0)+(ci==7?1:0));
                }
            }
            double inner=waInner+wbInner+wcInner+wdInner;
            if(inner<1e-10)return false;
            double target=(3*patch.Volume-outer)/inner;
            double basis=(waInner*patch.InnerA.Y+wbInner*patch.InnerB.Y+wcInner*patch.InnerC.Y+wdInner*patch.InnerD.Y)/inner;
            float minimum=Math.Min(Math.Min(patch.InnerA.Y,patch.InnerB.Y),Math.Min(patch.InnerC.Y,patch.InnerD.Y));
            double slope=minimum+target-basis>=0?1:Math.Max(0,target/(basis-minimum));
            patch.Center=Blend(patch,.5f,.5f);
            patch.InnerA.Y=(float)(target+(patch.InnerA.Y-basis)*slope);
            patch.InnerB.Y=(float)(target+(patch.InnerB.Y-basis)*slope);
            patch.InnerC.Y=(float)(target+(patch.InnerC.Y-basis)*slope);
            patch.InnerD.Y=(float)(target+(patch.InnerD.Y-basis)*slope);
            patch.Center.Y=(float)(target+(patch.Center.Y-basis)*slope);
            return true;
        }
    }
}
