using System;
using System.Collections.Generic;
using System.Numerics;

namespace Meridian
{
    public enum GroundTargetKind { Truck, AntiAircraft }

    /// <summary>Solid convoy vehicles; Position is collision centre, not the terrain contact point.</summary>
    public sealed class GroundTarget
    {
        public int Id, Team = 1;
        public string Name = "";
        public GroundTargetKind Kind;
        public Vector3 Position, PreviousPosition, Velocity;
        public Vector3 AimDirection = Vector3.UnitZ;
        public Vector3 Extents = new Vector3(3f, 2f, 6f);
        public float Heading, Health = 39f, MaxHealth = 39f, Radius = 3f;
        public bool Alive = true, IsObjective = true;
        public int ShotsFired, Ammunition = 440;
        internal float FireDelay, BurstTime;
        public bool IsAntiAircraft { get { return Kind == GroundTargetKind.AntiAircraft; } }

        public void Step(float dt, TerrainField terrain)
        {
            PreviousPosition = Position;
            if (!Alive) return;
            Position += Velocity * dt;
            Position.Y = terrain.Height(Position.X, Position.Z) + Extents.Y;
        }
    }

    /// <summary>Finite, physical small-calibre AAA. No radius damage or instantaneous hit rolls.</summary>
    public sealed class GroundDefense
    {
        private readonly Random random;
        public GroundDefense(int seed) { random = new Random(seed); }

        public void Step(float dt, IList<GroundTarget> targets, IList<AircraftState> aircraft, List<Projectile> rounds)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                GroundTarget gun = targets[i];
                if (!gun.Alive || !gun.IsAntiAircraft || gun.Ammunition <= 0) continue;
                gun.FireDelay = Math.Max(0f, gun.FireDelay - dt);
                gun.BurstTime += dt;
                if (gun.FireDelay > 0f || gun.BurstTime % 5.2f > 2.2f) continue;
                AircraftState target = null;
                float best = 1150f * 1150f;
                for (int a = 0; a < aircraft.Count; a++)
                {
                    AircraftState plane = aircraft[a];
                    if (!plane.Alive || plane.Team == gun.Team || plane.Position.Y < gun.Position.Y + 15f) continue;
                    float d = Vector3.DistanceSquared(plane.Position, gun.Position);
                    if (d < best) { best = d; target = plane; }
                }
                if (target == null) continue;
                float travel = (float)Math.Sqrt(best) / 540f;
                Vector3 origin = gun.Position + Vector3.UnitY * (gun.Extents.Y + .8f);
                Vector3 predicted = target.Position + target.Velocity * (travel * .82f) + Vector3.UnitY * (4.905f * travel * travel);
                predicted += new Vector3(Jitter(), Jitter(), Jitter()) * (9f + travel * 8f);
                Vector3 aim = Vector3.Normalize(predicted - origin);
                gun.AimDirection = aim;
                rounds.Add(new Projectile { Position = origin, PreviousPosition = origin, Velocity = aim * 540f,
                    Team = gun.Team, OwnerId = gun.Id, Damage = 7f, Lifetime = 2.6f });
                gun.Heading = (float)Math.Atan2(aim.X, aim.Z);
                gun.FireDelay = .22f; gun.ShotsFired++; gun.Ammunition--;
            }
        }
        private float Jitter() { return (float)random.NextDouble() * 2f - 1f; }
    }
}
