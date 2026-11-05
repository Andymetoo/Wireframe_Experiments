using UnityEngine;

namespace IceResurfacer.Presentation
{
    /// <summary>
    /// A straight member (built-in cylinder, length along its local Y) that joins the chassis to the
    /// conditioner: one end stays pinned to the chassis part it touches at the authored pose, the other
    /// rides on the conditioner, and the member spans the two at every lift. Authored parts named
    /// "Lift linkage" or "Hydraulic flexible hose" under the conditioner behave like this without the
    /// component; add it to any other hose or rod to opt in, or to choose which end is the chassis end.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IceConditionerSpan : MonoBehaviour
    {
        public enum ChassisEnd { Upper, Lower }
        [Tooltip("Which end of the member is pinned to the chassis (the other rides on the conditioner).")]
        public ChassisEnd chassisEnd = ChassisEnd.Upper;
        [Tooltip("Clear to keep this member rigid on the conditioner.")]
        public bool spanLift = true;
    }
}
