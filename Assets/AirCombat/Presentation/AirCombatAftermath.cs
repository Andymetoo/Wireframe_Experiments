using System.Collections.Generic;
using UnityEngine;

namespace Meridian
{
    public sealed partial class AirRenderer
    {
        // These snapshots belong solely to presentation. The frozen mission is never advanced or edited.
        sealed class WreckVisual
        {
            public Vector3 Position, Velocity;
            public Quaternion Rotation;
            public float Started;
            public int Id;
            public AircraftVisual Visual;
        }

        readonly Dictionary<int, WreckVisual> wrecks = new Dictionary<int, WreckVisual>();
        AirMission outcomeMission;
        public bool OutcomeActive { get; private set; }
        public float OutcomeElapsed { get; private set; }
        public int VisibleWreckCount
        {
            get
            {
                int count = 0;
                foreach (var wreck in wrecks.Values)
                    if (wreck.Visual.Root != null && wreck.Visual.Root.gameObject.activeSelf) count++;
                return count;
            }
        }

        /// <summary>Latch a finalized result. Calling twice for the same result never restarts its clock.</summary>
        public void BeginOutcome(AirMission mission)
        {
            if (mission == null || mission.Outcome == MissionOutcome.Flying || outcomeMission == mission) return;
            outcomeMission = mission; OutcomeElapsed = 0f; OutcomeActive = true;
            foreach (var plane in mission.Aircraft)
            {
                AircraftVisual visual;
                if (!plane.Alive && aircraft.TryGetValue(plane.Id, out visual)) ObserveWreck(plane, visual, mission);
            }
        }

        /// <summary>Absolute, pause-aware UI time supplied by the session. Repeated Sync calls do not advance it.</summary>
        public void SetOutcomeElapsed(float seconds)
        {
            if (!OutcomeActive || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            OutcomeElapsed = Mathf.Clamp(Mathf.Max(OutcomeElapsed, seconds), 0f, 8f);
        }

        public void ResetOutcome()
        {
            OutcomeActive = false; OutcomeElapsed = 0f; outcomeMission = null; wrecks.Clear();
        }

        public bool TryGetWreckPosition(int aircraftId, out Vector3 position)
        {
            WreckVisual wreck;
            if (wrecks.TryGetValue(aircraftId, out wreck) && wreck.Visual.Root != null && wreck.Visual.Root.gameObject.activeSelf)
            { position = wreck.Visual.Root.position; return true; }
            position = Vector3.zero; return false;
        }

        WreckVisual ObserveWreck(AircraftState plane, AircraftVisual visual, AirMission mission)
        {
            WreckVisual wreck;
            if (!wrecks.TryGetValue(plane.Id, out wreck))
            {
                wreck = new WreckVisual { Id = plane.Id, Position = World(plane.Position), Velocity = World(plane.Velocity),
                    Rotation = World(plane.Rotation), Started = mission.Time, Visual = visual };
                wrecks.Add(plane.Id, wreck);
            }
            return wreck;
        }

        void SyncAircraftWreck(AircraftState plane, AircraftVisual visual, AirMission mission)
        {
            if (plane.Alive) { visual.Root.gameObject.SetActive(true); return; }
            WreckVisual wreck = ObserveWreck(plane, visual, mission);
            float age = Mathf.Max(0f, mission.Time + OutcomeElapsed - wreck.Started);
            visual.Root.gameObject.SetActive(age < 6f);
            if (age >= 6f) return;
            visual.Root.position = WreckPosition(wreck, age, mission.Terrain);
            float side = (wreck.Id & 1) == 0 ? 1f : -1f;
            visual.Root.rotation = wreck.Rotation * Quaternion.Euler(age * 17f, age * side * 8f, age * side * 39f);
        }

        static Vector3 WreckPosition(WreckVisual wreck, float age, TerrainField terrain)
        {
            // Retain impact momentum, then lose speed and lift. The added descent is visual wreck motion,
            // not an alternative live flight model. Sampled terrain prevents wrecks falling through hills.
            float coast = (1f - Mathf.Exp(-age * .25f)) / .25f;
            Vector3 position = wreck.Position + wreck.Velocity * coast + Vector3.down * (7f * age + 4.905f * age * age);
            position.y = Mathf.Max(position.y, terrain.Height(position.x, position.z) + 1.4f);
            return position;
        }

        void SyncWreckSmoke(AirMission mission, ref int cursor)
        {
            foreach (var wreck in wrecks.Values)
            {
                float age = Mathf.Max(0f, mission.Time + OutcomeElapsed - wreck.Started);
                if (age >= 6f) continue;
                // Fixed timestamps produce a continuous trail without allocating particles or remembering frame count.
                for (int puff = 0; puff < 16 && cursor < smokePool.Count; puff++)
                {
                    float emitted = Mathf.Floor(age / .16f) * .16f - puff * .16f;
                    if (emitted < 0f) break;
                    float drift = age - emitted;
                    Transform smoke = smokePool[cursor++]; smoke.gameObject.SetActive(true);
                    smoke.position = WreckPosition(wreck, emitted, mission.Terrain) + new Vector3(drift * 2, 1 + drift * 6, 0);
                    float size = 1.5f + drift * 4;
                    smoke.localScale = new Vector3(size, size * .8f, size);
                    smoke.rotation = Quaternion.Euler(wreck.Id * 31 + puff * 47, puff * 79, puff * 23);
                }
            }
        }

        void SyncOutcomeCamera(AirMission mission)
        {
            if (!OutcomeActive || mission.Player.Alive) return;
            WreckVisual wreck;
            if (!wrecks.TryGetValue(mission.Player.Id, out wreck)) return;
            Vector3 position = wreck.Visual.Root.position;
            Vector3 desired = position + wreck.Rotation * new Vector3(24f, 13f, -48f);
            desired.y = Mathf.Max(desired.y, mission.Terrain.Height(desired.x, desired.z) + 5f);
            viewCamera.transform.SetPositionAndRotation(desired, Quaternion.LookRotation(position + Vector3.up * 2f - desired, Vector3.up));
            cockpit.gameObject.SetActive(false);
        }
    }
}
