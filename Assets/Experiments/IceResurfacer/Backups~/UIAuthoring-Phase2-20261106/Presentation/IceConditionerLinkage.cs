using System.Collections.Generic;
using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    /// <summary>
    /// The one conditioner pose used by Play (IceResurfacerGame.UpdateMachine) and the Edit Mode lift
    /// preview: the root at the simulated hitch and lift, chassis-to-conditioner members spanning their
    /// pins, and the lift cylinders on their clevises. Controls mounted under the conditioner simply ride
    /// with it; their handles are animated relative to their own rest pose.
    /// </summary>
    public static class IceConditionerMotion
    {
        public const float LiftTravel = .34f;

        /// <summary>Conditioner root in machine space for a calibration and lift (0 lowered .. 1 raised).</summary>
        public static Vector3 LocalPosition(SimulatorConfig c,float lift)
        {
            return new Vector3(0,lift*LiftTravel,-(c.HitchBehindAxle+c.DrawbarLength));
        }

        public static void Pose(IceWorld w,Vector3 position,Quaternion rotation)
        {
            w.Conditioner.SetPositionAndRotation(position,rotation);
            IceConditionerLinkage.Pose(w);
            IcePhysicalControls.PoseCylinders(w);
        }

        /// <summary>Machine-relative pose (the Edit Mode preview path).</summary>
        public static void PoseLocal(IceWorld w,SimulatorConfig c,float lift)
        {
            Pose(w,w.Machine.TransformPoint(LocalPosition(c,lift)),w.Machine.rotation);
        }
    }

    public static class IceConditionerLinkage
    {
        public sealed class Span
        {
            public Transform Part;
            public Vector3 ChassisEnd;      // machine space
            public Vector3 ConditionerEnd;  // conditioner space
            public bool UpFromChassis;      // the member's +Y points from the chassis end
        }

        static bool Candidate(Transform t,out IceConditionerSpan marker)
        {
            marker=t.GetComponent<IceConditionerSpan>();
            if(marker!=null)return marker.spanLift;
            return t.name=="Lift linkage"||t.name.StartsWith("Hydraulic flexible hose");
        }

        /// <summary>
        /// Records each spanning member's two ends at the current (authored) pose. Call before anything
        /// moves the conditioner, and before runtime batching. Only built-in style cylinders qualify.
        /// </summary>
        public static List<Span> Capture(IceWorld w)
        {
            var spans=new List<Span>();
            if(w.Conditioner==null||w.Machine==null)return spans;
            foreach(var t in w.Conditioner.GetComponentsInChildren<Transform>(true))
            {
                if(t==w.Conditioner||!Candidate(t,out var marker))continue;
                Vector3 half=t.up*t.lossyScale.y,a=t.position-half,b=t.position+half;
                Vector3 la=w.Machine.InverseTransformPoint(a),lb=w.Machine.InverseTransformPoint(b);
                bool upper=marker==null||marker.chassisEnd==IceConditionerSpan.ChassisEnd.Upper;
                // a is the -Y end, b the +Y end.
                bool chassisIsB=upper?lb.y>=la.y:lb.y<la.y;
                spans.Add(new Span{Part=t,ChassisEnd=chassisIsB?lb:la,ConditionerEnd=w.Conditioner.InverseTransformPoint(chassisIsB?a:b),UpFromChassis=!chassisIsB});
            }
            return spans;
        }

        public static void Pose(IceWorld w)
        {
            if(w.ConditionerSpans==null)return;
            foreach(var s in w.ConditionerSpans)
            {
                if(s.Part==null)continue;
                Vector3 chassis=w.Machine.TransformPoint(s.ChassisEnd),conditioner=w.Conditioner.TransformPoint(s.ConditionerEnd);
                Vector3 axis=s.UpFromChassis?conditioner-chassis:chassis-conditioner;
                float length=axis.magnitude;
                if(length<.001f)continue;
                s.Part.SetPositionAndRotation((chassis+conditioner)*.5f,Quaternion.FromToRotation(Vector3.up,axis/length));
                float parentScale=s.Part.parent!=null?s.Part.parent.lossyScale.y:1;
                var scale=s.Part.localScale;scale.y=length*.5f/parentScale;s.Part.localScale=scale;
            }
        }
    }
}
