using System.Collections.Generic;
using System.Numerics;

namespace Meridian
{
    public enum SquadronOrder { FormUp, Engage, CoverMe, Regroup }

    /// <summary>Mission intent, independent of keyboards, rendering and the aerodynamic controls.
    /// Sessions keep one context per pilot and update it in place.</summary>
    public sealed class PilotOrders
    {
        public SquadronOrder Order = SquadronOrder.Engage;
        public AircraftState Leader;
        public int DesignatedTargetId = -1;
        public Vector3 FormationOffset, ObjectiveAnchor, Destination;
        public IList<AircraftState> ProtectedAssets;
        public IList<GroundTarget> GroundTargets;
        public bool FollowRoute, CanStrafe, PreferBombers;
        public float LeashRadius = 7000;
        public int Revision;
        public string FlightName = "";
    }
}
