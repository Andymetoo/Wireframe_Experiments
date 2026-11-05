using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    /// <summary>
    /// An authored tank sight gauge. The indicator's authored pose and size mean "full"; the water
    /// service shrinks it toward its own base as the tank empties. Keep the indicator out of runtime
    /// batching (IceKeepUnbatched) so it can move.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IceWaterLevelGauge : MonoBehaviour
    {
        public WaterFillPort tank = WaterFillPort.Flood;
        [Tooltip("Scaled along its local Y from its lowest point. Authored size = full tank.")]
        public Transform indicator;

        bool captured;
        Vector3 fullScale, fullPosition;
        float baseY;

        public bool IsComplete { get { return (tank == WaterFillPort.Flood || tank == WaterFillPort.Wash) && indicator != null && indicator.IsChildOf(transform); } }

        /// <summary>Shows a tank fraction (0..1). The first call records the authored full pose.</summary>
        public void Show(float fraction)
        {
            if (indicator == null) return;
            if (!captured)
            {
                fullScale = indicator.localScale; fullPosition = indicator.localPosition;
                var filter = indicator.GetComponent<MeshFilter>();
                baseY = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds.min.y : -.5f;
                captured = true;
            }
            fraction = Mathf.Clamp01(fraction);
            // Keep the indicator's base fixed in its parent while its height follows the tank.
            Vector3 bottom = fullPosition + indicator.localRotation * new Vector3(0, baseY * fullScale.y, 0);
            var scale = new Vector3(fullScale.x, Mathf.Max(fullScale.y * .008f, fullScale.y * fraction), fullScale.z);
            indicator.localScale = scale;
            indicator.localPosition = bottom - indicator.localRotation * new Vector3(0, baseY * scale.y, 0);
        }
    }
}
