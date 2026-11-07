using System;
using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    /// <summary>
    /// Authoring for the board-brush assembly. Lives on "Board brush articulated arm" and owns
    /// the whole articulation: the arm pivot and angle, brush head, link and contact roller are
    /// all posed from these values plus BoardBrushArticulation (the formula the simulation uses).
    ///
    /// Existing geometry stays a calibration draft. Explicitly saved and enabled endpoints
    /// instead derive the runtime mechanism from the authored rig, leaving the scene's other
    /// calibration intact. The visible disc always follows the simulated spring/contact point.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IceBoardBrushRig : MonoBehaviour
    {
        [Serializable]
        public struct Mechanism
        {
            public float PivotX, PivotZ, ArmLength, Radius, DeployedDegrees, RetractedDegrees;
            public static Mechanism From(SimulatorConfig c)
            {
                return new Mechanism { PivotX=c.BrushPivotX, PivotZ=c.BrushPivotZ, ArmLength=c.BrushArmLength,
                    Radius=c.BrushRadius, DeployedDegrees=c.BrushDeployedDegrees, RetractedDegrees=c.BrushRetractedDegrees };
            }
            public bool Matches(Mechanism other,float tolerance=.0005f)
            {
                return Mathf.Abs(PivotX-other.PivotX)<=tolerance&&Mathf.Abs(PivotZ-other.PivotZ)<=tolerance&&
                    Mathf.Abs(ArmLength-other.ArmLength)<=tolerance&&Mathf.Abs(Radius-other.Radius)<=tolerance&&
                    Mathf.Abs(DeployedDegrees-other.DeployedDegrees)<=.01f&&Mathf.Abs(RetractedDegrees-other.RetractedDegrees)<=.01f;
            }
        }

        [Tooltip("Simulation contact geometry (draft). Apply it to the scene calibration to make it physical.")]
        public Mechanism mechanism = new Mechanism { PivotX=-.90f, PivotZ=1.16f, ArmLength=.38f, Radius=.25f, DeployedDegrees=0, RetractedDegrees=85 };

        [Serializable]
        public struct Endpoint
        {
            public Vector3 position;
            public Quaternion rotation;
        }
        [Header("Authored endpoints")]
        [Tooltip("Use the two saved endpoints as the physical brush geometry in Play. Existing scene calibration remains active until explicitly enabled.")]
        public bool useAuthoredEndpoints;
        [HideInInspector] public bool endpointsInitialized, stowedEndpointSaved, workingEndpointSaved;
        [HideInInspector] public Endpoint stowedEndpoint, workingEndpoint;

        [Header("Board contact spring")]
        [Tooltip("Maximum inward movement of the brush carriage, in metres. Geometry beyond this travel is reported as a mechanical limit.")]
        [Range(0,.3f)] public float complianceTravel=.12f;
        [Tooltip("Spring return speed (rad/s). Higher values restore the deployed rest pose faster.")]
        [Range(1,40)] public float springReturn=12f;
        [Tooltip("Damping ratio: 1 gives a smooth critically damped return; higher is slower.")]
        [Range(.5f,3)] public float springDamping=1f;

        [Header("Cosmetic (never affects contact)")]
        [Tooltip("Height of the arm pivot above the ice (m, machine space).")]
        public float pivotHeight = .38f;
        [Tooltip("Height of the brush head origin when fully deployed / fully stowed (m).")]
        public float deployedHeadHeight = .15f, retractedHeadHeight = .30f;
        [Tooltip("Link end on the arm, in the arm's frame.")]
        public Vector3 linkPivotOffset = Vector3.zero;
        [Tooltip("Link end on the brush head, machine-oriented offset from the head origin.")]
        public Vector3 linkHeadOffset = new Vector3(0,.05f,0);
        public float linkThickness = .065f;
        [Tooltip("Kickplate contact-roller carriage, machine-oriented offset from the head origin.")]
        public Vector3 rollerOffset = new Vector3(0,.20f,0);

        [Header("Parts")]
        public Transform machine;
        public Transform head, link, roller;
        [Tooltip("Head children that stay still while the bristles spin (motor, guard plate).")]
        public Transform[] nonSpinning = new Transform[0];

        // Runtime-only state: authored head tilt and non-spinning rest poses, and the spin.
        [NonSerialized] bool restCaptured;
        [NonSerialized] Quaternion headRest;
        [NonSerialized] Vector3[] stillPositions;
        [NonSerialized] Quaternion[] stillRotations;
        [NonSerialized] float spin;
        [NonSerialized] bool endpointPreviewRotationCaptured;
        [NonSerialized] Quaternion endpointPreviewRestRotation;

        public Transform Arm { get { return transform; } }

        public bool IsComplete { get { return machine!=null&&head!=null&&link!=null&&roller!=null&&head.IsChildOf(transform); } }

        public float HeadHeight(float deployment) { return Mathf.Lerp(retractedHeadHeight,deployedHeadHeight,deployment); }

        /// <summary>Explicitly start from the current mechanism, never from obsolete defaults.</summary>
        public void InitializeEndpoints()
        {
            if(endpointsInitialized||!IsComplete)return;
            Quaternion headInArm=Quaternion.Inverse(transform.rotation)*head.rotation;
            stowedEndpoint=new Endpoint { position=ContactLocal(mechanism,0)+Vector3.up*retractedHeadHeight,
                rotation=Quaternion.Euler(0,-mechanism.RetractedDegrees,0)*headInArm };
            workingEndpoint=new Endpoint { position=ContactLocal(mechanism,1)+Vector3.up*deployedHeadHeight,
                rotation=Quaternion.Euler(0,-mechanism.DeployedDegrees,0)*headInArm };
            endpointsInitialized=true;
        }

        public bool TryEndpointMechanism(out Mechanism result,out string error)
        {
            result=mechanism;error=null;
            if(!endpointsInitialized){error="Initialize endpoints from the current rig first.";return false;}
            bool stowed=BoardBrushArticulation.Endpoint(result.PivotX,result.PivotZ,result.ArmLength,stowedEndpoint.position.x,stowedEndpoint.position.z,out float stow,out float stowError);
            bool working=BoardBrushArticulation.Endpoint(result.PivotX,result.PivotZ,result.ArmLength,workingEndpoint.position.x,workingEndpoint.position.z,out float work,out float workError);
            if(!stowed||!working)
            {
                error="Endpoint reach differs from the arm length: STOWED "+(stowError*1000).ToString("F1")+" mm; WORKING "+(workError*1000).ToString("F1")+
                    " mm. Adjust the pivot/arm length or project the endpoints onto the arm circle.";return false;
            }
            if(stow<0||stow>100||work< -45||work>stow)
            { error="Arm angles must satisfy -45 <= WORKING <= STOWED and 0 <= STOWED <= 100 degrees. Move an endpoint around the pivot.";return false; }
            result.DeployedDegrees=work;result.RetractedDegrees=stow;
            return true;
        }

        public bool ApplyAuthoredConfiguration(SimulatorConfig runtimeConfig,out string error)
        {
            error=null;
            runtimeConfig.BrushComplianceTravel=complianceTravel;
            runtimeConfig.BrushSpringReturn=springReturn;
            runtimeConfig.BrushSpringDamping=springDamping;
            if(!useAuthoredEndpoints)return true;
            if(!stowedEndpointSaved||!workingEndpointSaved){error="Save both STOWED and WORKING endpoints before enabling them in Play.";return false;}
            if(!TryEndpointMechanism(out Mechanism m,out error))return false;
            runtimeConfig.BrushPivotX=m.PivotX;runtimeConfig.BrushPivotZ=m.PivotZ;runtimeConfig.BrushArmLength=m.ArmLength;
            runtimeConfig.BrushRadius=m.Radius;runtimeConfig.BrushDeployedDegrees=m.DeployedDegrees;runtimeConfig.BrushRetractedDegrees=m.RetractedDegrees;
            return true;
        }

        public void PoseEndpointPreview(float deployment)
        {
            if(!endpointsInitialized||!IsComplete)return;
            if(!endpointPreviewRotationCaptured)
            {
                endpointPreviewRestRotation=head.localRotation;
                endpointPreviewRotationCaptured=true;
            }
            var m=mechanism;
            if(TryEndpointMechanism(out Mechanism derived,out _))m=derived;
            Vector3 position=deployment<=0?stowedEndpoint.position:deployment>=1?workingEndpoint.position:ContactLocal(m,deployment)+Vector3.up*Mathf.Lerp(stowedEndpoint.position.y,workingEndpoint.position.y,deployment);
            Pose(m,deployment,machine.TransformPoint(position),0);
            head.position=machine.TransformPoint(position);
            head.rotation=machine.rotation*Quaternion.Slerp(stowedEndpoint.rotation,workingEndpoint.rotation,deployment);
            PoseLinkAndRoller();
        }

        /// <summary>Discard temporary endpoint tilt when returning to the existing rig.
        /// Endpoint pose data stays saved; the legacy authored head rotation stays intact.</summary>
        public void RestoreEndpointPreviewRotation()
        {
            if(!endpointPreviewRotationCaptured)return;
            if(head!=null)head.localRotation=endpointPreviewRestRotation;
            endpointPreviewRotationCaptured=false;
        }

        /// <summary>Brush disc centre in machine space for a deployment, before board compression.</summary>
        public static Vector3 ContactLocal(Mechanism m,float deployment)
        {
            BoardBrushArticulation.Contact(m.PivotX,m.PivotZ,m.ArmLength,
                BoardBrushArticulation.ArmRadians(m.DeployedDegrees,m.RetractedDegrees,deployment),out float x,out float z);
            return new Vector3(x,0,z);
        }

        /// <summary>Edit Mode pose: the head sits exactly on the articulated contact point.</summary>
        public void PosePreview(Mechanism m,float deployment)
        {
            Pose(m,deployment,machine.TransformPoint(ContactLocal(m,deployment)),0);
        }

        /// <summary>
        /// Poses arm, head, link and roller. contactWorld is the disc centre (the simulation's
        /// BrushWorldX/Z at runtime, including board compression); only its X/Z are used.
        /// spinDegrees is added to the bristle rotation (0 in Edit Mode).
        /// </summary>
        public void Pose(Mechanism m,float deployment,Vector3 contactWorld,float spinDegrees)
        {
            if(!IsComplete)return;
            float degrees=BoardBrushArticulation.ArmDegrees(m.DeployedDegrees,m.RetractedDegrees,deployment);
            transform.SetPositionAndRotation(machine.TransformPoint(m.PivotX,pivotHeight,m.PivotZ),machine.rotation*Quaternion.Euler(0,-degrees,0));
            Vector3 local=machine.InverseTransformPoint(contactWorld);
            bool endpoints=useAuthoredEndpoints&&stowedEndpointSaved&&workingEndpointSaved&&TryEndpointMechanism(out _,out _);
            local.y=endpoints?Mathf.Lerp(stowedEndpoint.position.y,workingEndpoint.position.y,deployment):HeadHeight(deployment);
            head.position=machine.TransformPoint(local);
            if(endpoints)
            {
                head.rotation=machine.rotation*Quaternion.Slerp(stowedEndpoint.rotation,workingEndpoint.rotation,deployment);
                if(Application.isPlaying)Spin(spinDegrees,true);
            }
            else if(Application.isPlaying)Spin(spinDegrees,false);
            PoseLinkAndRoller();
        }

        void PoseLinkAndRoller()
        {
            Vector3 from=transform.TransformPoint(linkPivotOffset),to=head.position+machine.rotation*linkHeadOffset;
            link.position=(from+to)*.5f;
            link.rotation=Quaternion.FromToRotation(Vector3.up,to-from);
            link.localScale=new Vector3(linkThickness,(to-from).magnitude*.5f,linkThickness);
            roller.position=head.position+machine.rotation*rollerOffset;
            roller.rotation=machine.rotation;
        }

        // Bristles spin about the head's own up axis on top of the authored tilt; the motor
        // and guard plate are counter-rotated so they hold their authored pose.
        void Spin(float spinDegrees,bool endpointRotation)
        {
            if(!restCaptured)
            {
                headRest=head.localRotation;
                stillPositions=new Vector3[nonSpinning.Length];stillRotations=new Quaternion[nonSpinning.Length];
                for(int i=0;i<nonSpinning.Length;i++)
                    if(nonSpinning[i]!=null){stillPositions[i]=nonSpinning[i].localPosition;stillRotations[i]=nonSpinning[i].localRotation;}
                restCaptured=true;
            }
            spin=Mathf.Repeat(spin+spinDegrees,360);
            head.localRotation=(endpointRotation?head.localRotation:headRest)*Quaternion.Euler(0,spin,0);
            var unspin=Quaternion.Euler(0,-spin,0);
            for(int i=0;i<nonSpinning.Length;i++)
            {
                if(nonSpinning[i]==null)continue;
                nonSpinning[i].localPosition=unspin*stillPositions[i];
                nonSpinning[i].localRotation=unspin*stillRotations[i];
            }
        }
    }
}
