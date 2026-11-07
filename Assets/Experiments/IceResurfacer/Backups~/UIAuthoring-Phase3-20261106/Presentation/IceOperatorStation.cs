using UnityEngine;

namespace IceResurfacer.Presentation
{
    /// <summary>Authored viewpoints and three independent cockpit control assemblies.</summary>
    [DisallowMultipleComponent]
    public sealed class IceOperatorStation : MonoBehaviour
    {
        [Header("Editable camera anchors")]
        [Tooltip("Normal seated camera origin and forward direction. Move this Transform in Prefab Mode; the seat does not move.")]
        public Transform driverEyeAnchor;
        [Tooltip("Exact E view position and rotation. The camera approaches this pose without automatic yaw or shoulder lean.")]
        public Transform controlsViewAnchor;
        [Min(.02f), Tooltip("Seconds to settle into the authored E view.")]
        public float controlsEnterSeconds=.24f;
        [Min(.02f), Tooltip("Seconds to restore the exact view saved before a held inspection.")]
        public float controlsReturnSeconds=.24f;

        [Header("Moving control parts (move their whole parent assemblies to rearrange)")]
        public Transform boardBrushSwitch;
        public Transform hornButton;
        public Transform washPumpSwitch;
        [Range(5,45)] public float switchOnDegrees=24;
        [Range(.001f,.02f)] public float hornTravel=.006f;

        public Transform Control(int index)
        {return index==9?boardBrushSwitch:index==10?hornButton:index==11?washPumpSwitch:null;}

        void OnValidate()
        {
            controlsEnterSeconds=Mathf.Max(.02f,controlsEnterSeconds);
            controlsReturnSeconds=Mathf.Max(.02f,controlsReturnSeconds);
        }

        void OnDrawGizmosSelected()
        {
            DrawAnchor(driverEyeAnchor,new Color(.25f,1,.65f));
            DrawAnchor(controlsViewAnchor,new Color(1,.75f,.2f));
        }
        static void DrawAnchor(Transform anchor,Color color)
        {
            if(anchor==null)return;
            Gizmos.color=color;Gizmos.DrawWireSphere(anchor.position,.025f);
            Gizmos.DrawLine(anchor.position,anchor.position+anchor.forward*.3f);
            Gizmos.DrawLine(anchor.position+anchor.forward*.3f,anchor.position+anchor.forward*.24f+anchor.right*.04f);
            Gizmos.DrawLine(anchor.position+anchor.forward*.3f,anchor.position+anchor.forward*.24f-anchor.right*.04f);
        }
    }
}
