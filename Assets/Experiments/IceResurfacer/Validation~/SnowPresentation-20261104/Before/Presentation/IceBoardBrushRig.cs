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
    /// Mechanical values (pivot X/Z, arm length, radius, working and stowed angles) are a draft
    /// until "Apply Brush Geometry to Simulation" copies them into the scene calibration. Play
    /// always poses the arm from the calibration, so the visible disc stays on the simulated
    /// contact. Cosmetic values (heights, link and roller offsets) never affect contact.
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

        public Transform Arm { get { return transform; } }

        public bool IsComplete { get { return machine!=null&&head!=null&&link!=null&&roller!=null&&head.IsChildOf(transform); } }

        public float HeadHeight(float deployment) { return Mathf.Lerp(retractedHeadHeight,deployedHeadHeight,deployment); }

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
            local.y=HeadHeight(deployment);
            head.position=machine.TransformPoint(local);
            if(Application.isPlaying)Spin(spinDegrees);
            Vector3 from=transform.TransformPoint(linkPivotOffset),to=head.position+machine.rotation*linkHeadOffset;
            link.position=(from+to)*.5f;
            link.rotation=Quaternion.FromToRotation(Vector3.up,to-from);
            link.localScale=new Vector3(linkThickness,(to-from).magnitude*.5f,linkThickness);
            roller.position=head.position+machine.rotation*rollerOffset;
            roller.rotation=machine.rotation;
        }

        // Bristles spin about the head's own up axis on top of the authored tilt; the motor
        // and guard plate are counter-rotated so they hold their authored pose.
        void Spin(float spinDegrees)
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
            head.localRotation=headRest*Quaternion.Euler(0,spin,0);
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
