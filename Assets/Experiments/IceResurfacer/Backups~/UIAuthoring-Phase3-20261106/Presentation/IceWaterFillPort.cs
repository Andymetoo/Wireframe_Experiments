using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    /// <summary>
    /// An authored machine water fill port. The water service couples the hose at hoseSocket and
    /// spills overflow at overflowSocket, wherever you put them; the decorative visual never affects
    /// either. Move or rotate the whole port, reshape or replace its visual, or move a socket on its own.
    /// hoseSocket's +X (red) axis is the coupled nozzle's direction, pointing away from the machine.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IceWaterFillPort : MonoBehaviour
    {
        public WaterFillPort port = WaterFillPort.Flood;
        [Tooltip("Hose coupling point and click/E target. The coupled nozzle lies along this transform's +X (red) axis.")]
        public Transform hoseSocket;
        [Tooltip("Where overflow water leaves the tank when this port is overfilled (falls straight down from here).")]
        public Transform overflowSocket;
        [Tooltip("Decorative model only (optional). Replace it with your own mesh freely.")]
        public Transform visual;

        public bool IsComplete { get { return (port == WaterFillPort.Flood || port == WaterFillPort.Wash) && hoseSocket != null && overflowSocket != null; } }
        public string Label { get { return port == WaterFillPort.Flood ? "FLOOD WATER" : "WASH WATER"; } }
    }
}
