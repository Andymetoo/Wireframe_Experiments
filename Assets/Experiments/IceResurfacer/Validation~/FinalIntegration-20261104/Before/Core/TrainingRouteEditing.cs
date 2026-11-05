using System;

namespace IceResurfacer.Core
{
    public static class TrainingRouteEditing
    {
        public static int Split(TrainingRouteDefinition d,int segment,RecordedRouteData raw=null)
        {
            if(segment<0||segment>=d.controls.Count-1)throw new ArgumentOutOfRangeException(nameof(segment));
            var a=d.controls[segment];var b=d.controls[segment+1];float sign=a.direction;
            if(a.recordedTrack)
            {
                if(raw==null)throw new InvalidOperationException("Recorded-track subdivision requires the original source.");
                int sequence=(a.sourceSequence+b.sourceSequence)/2;
                if(sequence==a.sourceSequence)throw new InvalidOperationException("No source observation between these anchors; move a point to edit this section as Bezier.");
                var sample=raw.samples[sequence];var point=a.Copy();point.sourceSequence=sequence;point.x=sample.vehicleX;point.z=sample.vehicleZ;point.headingRadians=sample.vehicleHeadingRadians;
                point.direction=RecordedTrackReconstruction.Direction(raw,sequence);point.marker=RouteMarker.None;point.label="";
                d.controls.Insert(segment+1,point);return segment+1;
            }
            float p1x=a.x+(float)Math.Sin(a.headingRadians)*a.outgoing*sign,p1z=a.z+(float)Math.Cos(a.headingRadians)*a.outgoing*sign;
            float p2x=b.x-(float)Math.Sin(b.headingRadians)*b.incoming*sign,p2z=b.z-(float)Math.Cos(b.headingRadians)*b.incoming*sign;
            float q0x=(a.x+p1x)*.5f,q0z=(a.z+p1z)*.5f,q1x=(p1x+p2x)*.5f,q1z=(p1z+p2z)*.5f,q2x=(p2x+b.x)*.5f,q2z=(p2z+b.z)*.5f;
            float r0x=(q0x+q1x)*.5f,r0z=(q0z+q1z)*.5f,r1x=(q1x+q2x)*.5f,r1z=(q1z+q2z)*.5f;
            var p=a.Copy();p.x=(r0x+r1x)*.5f;p.z=(r0z+r1z)*.5f;
            p.headingRadians=TrainingRouteDefinition.Angle((float)Math.Atan2(r1x-r0x,r1z-r0z)+(sign<0?(float)Math.PI:0));
            p.incoming=TrainingRouteDefinition.Distance(p.x,p.z,r0x,r0z);p.outgoing=TrainingRouteDefinition.Distance(p.x,p.z,r1x,r1z);
            p.sourceSequence=(a.sourceSequence+b.sourceSequence)/2;p.marker=RouteMarker.None;p.label="";
            a.outgoing*=.5f;b.incoming*=.5f;d.controls.Insert(segment+1,p);return segment+1;
        }
        public static bool Remove(TrainingRouteDefinition d,int point)
        {if(point<=0||point>=d.controls.Count-1||d.controls.Count<=2)return false;d.controls[point-1].recordedTrack&=d.controls[point].recordedTrack;d.controls.RemoveAt(point);return true;}
        public static void ConstrainTangents(TrainingRouteDefinition d,int iterations=60,bool allowPositionAdjustment=false)
        {
            // Legacy Bezier utility, retained for authored/test curves. Its position
            // budget is per invocation, not an immutable source corridor. Recording
            // imports/corrections use RecordedTrackReconstruction.Restore instead.
            // Full body/conditioner checks still decide readiness after this utility.
            float limit=(float)Math.Tan(d.configuration.MaxSteeringRadians)/d.configuration.Wheelbase;
            var headings=new float[d.controls.Count];for(int i=0;i<headings.Length;i++)headings[i]=d.controls[i].headingRadians;
            var positionsX=new float[headings.Length];var positionsZ=new float[headings.Length];for(int i=0;i<headings.Length;i++){positionsX[i]=d.controls[i].x;positionsZ[i]=d.controls[i].z;}
            for(int pass=0;pass<iterations;pass++)
            {
                bool changed=false;
                for(int i=0;i<d.controls.Count;i++)
                {
                    var p=d.controls[i];float score=Local(d,i,limit,headings[i]);
                    float bestHeading=p.headingRadians,bestIn=p.incoming,bestOut=p.outgoing;
                    float bestX=p.x,bestZ=p.z;float fine=pass<20?1:pass<40?.25f:.06f;
                    for(int option=0;option<(allowPositionAdjustment&&i>0&&i<d.controls.Count-1?10:6);option++)
                    {
                        float h=p.headingRadians,inc=p.incoming,outgoing=p.outgoing,x=p.x,z=p.z;
                        if(option<2)p.headingRadians+=(option==0?-.0015f:.0015f)*fine;
                        else if(option<4)p.incoming*=1+(option==2?-.03f:.03f)*fine;
                        else if(option<6)p.outgoing*=1+(option==4?-.03f:.03f)*fine;
                        else if(option<8)p.x+=(option==6?-.005f:.005f)*fine;
                        else p.z+=(option==8?-.005f:.005f)*fine;
                        float displacement=TrainingRouteDefinition.Distance(p.x,p.z,positionsX[i],positionsZ[i]);
                        float test=displacement>.12f?float.PositiveInfinity:Local(d,i,limit,headings[i]);
                        if(test<score-.0000001f){score=test;bestHeading=p.headingRadians;bestIn=p.incoming;bestOut=p.outgoing;bestX=p.x;bestZ=p.z;changed=true;}
                        p.headingRadians=h;p.incoming=inc;p.outgoing=outgoing;p.x=x;p.z=z;
                    }
                    p.headingRadians=bestHeading;p.incoming=bestIn;p.outgoing=bestOut;p.x=bestX;p.z=bestZ;
                }
                if(!changed)break;
            }
        }
        static float Local(TrainingRouteDefinition d,int point,float limit,float originalHeading)
        {
            var p=d.controls[point];if(p.incoming<.01f||p.outgoing<.01f||p.incoming>30||p.outgoing>30)return float.PositiveInfinity;
            float angle=TrainingRouteDefinition.Angle(p.headingRadians-originalHeading);
            if(Math.Abs(angle)>.07f)return float.PositiveInfinity;
            float score=angle*angle*.1f;
            for(int k=Math.Max(0,point-1);k<=Math.Min(d.controls.Count-2,point);k++)
            {
                float prior=0;
                for(int i=0;i<=32;i++)
                {
                    float curve=RouteFitter.Evaluate(d.controls[k],d.controls[k+1],i/32.0).curvature/limit;
                    float excess=Math.Max(0,Math.Abs(curve)-.995f);
                    score+=excess*excess*10000;
                    if(i>0)score+=(curve-prior)*(curve-prior)*.01f;prior=curve;
                }
            }
            return score;
        }
    }
}
