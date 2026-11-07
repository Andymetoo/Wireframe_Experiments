using UnityEngine;

namespace IceResurfacer.Presentation
{
    /// <summary>What an authored arena part is to runtime systems and the physical drift report.
    /// Values are serialized; never renumber them.</summary>
    public enum IceArenaRole
    {
        None = 0,
        // Physical: SimulatorConfig / ServiceLayout are authoritative for these.
        IceSheet = 1, RinkBoardBand = 2, ServiceBay = 3, ServiceFloor = 4, ServiceSideWall = 5,
        ServiceRearWall = 6, PortalReturnWall = 7, GateJamb = 8, SnowPit = 9, PitSnow = 10, WaterStation = 12,
        // Bound by runtime systems; free to edit unless noted in the rig Inspector.
        ArenaLight = 11, SupplyValveHandle = 13, NozzleHolderHighlight = 14, StationLevels = 15
    }

    /// <summary>
    /// Marks an authored arena part that runtime systems bind or the drift report checks.
    /// Parts are found by role, not by serialized cross-references, so a section can be
    /// regenerated, moved in the hierarchy or edited without breaking the arena.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IceArenaPart : MonoBehaviour
    {
        public IceArenaRole role;
        [Tooltip("Root of a separately regenerable arena section (its own prefab).")]
        public bool sectionRoot;
        // Generated local pose, for "restore generated pose" on physical parts.
        [SerializeField, HideInInspector] Vector3 bakedPosition;
        [SerializeField, HideInInspector] Quaternion bakedRotation = Quaternion.identity;
        [SerializeField, HideInInspector] Vector3 bakedScale = Vector3.one;
        // Section roots only: the calibration their shapes were generated from.
        [SerializeField, HideInInspector] string bakedConfigJson;

        public string BakedConfigJson { get { return bakedConfigJson; } }
        public bool IsPhysical { get { return IceArenaRig.IsPhysical(role); } }
        public bool AtBakedPose
        {
            get
            {
                return Vector3.Distance(transform.localPosition, bakedPosition) < .0005f &&
                    Quaternion.Angle(transform.localRotation, bakedRotation) < .01f &&
                    Vector3.Distance(transform.localScale, bakedScale) < .0001f;
            }
        }
        public void RestoreBakedPose()
        {
            transform.localPosition = bakedPosition; transform.localRotation = bakedRotation; transform.localScale = bakedScale;
        }

#if UNITY_EDITOR
        /// <summary>Editor bake only.</summary>
        public void RecordBake(string configJson)
        {
            bakedPosition = transform.localPosition; bakedRotation = transform.localRotation; bakedScale = transform.localScale;
            if (sectionRoot) bakedConfigJson = configJson;
        }
#endif
    }
}
