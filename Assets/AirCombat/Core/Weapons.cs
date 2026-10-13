using System;
using System.Collections.Generic;
using System.Numerics;

namespace Meridian
{
    public enum CombatEventKind { GunHit, Destroyed, GroundHit }

    public sealed class CombatEvent
    {
        public Vector3 Position;
        public CombatEventKind Kind;
        public float Age;
        public float Lifetime;
        // Aircraft-only damage metadata; ground impacts retain the default values.
        public int AircraftId = -1, OwnerId = -1;
        public float AircraftDamage;
        public bool DestroyedAircraft;
        public CombatEvent(Vector3 position, CombatEventKind kind)
        { Position = position; Kind = kind; Lifetime = kind == CombatEventKind.Destroyed ? 5f : 1.1f; }
    }

    public sealed class Projectile
    {
        public Vector3 Position, PreviousPosition, Velocity;
        public int Team, OwnerId;
        public float Age, Lifetime = 1.85f;
        public float Damage = Weapons.RoundDamage;
    }

    /// <summary>Physical rounds: inherited velocity, gravity, dispersion, and swept moving-target collision.</summary>
    public sealed class Weapons
    {
        public const float MuzzleSpeed = 760f;
        public const float ShotInterval = .075f;
        public const float RoundDamage = 13f;
        private readonly Random random;
        private readonly Dictionary<int, float> cooldown = new Dictionary<int, float>();
        public int PlayerRounds { get; private set; }
        public int PlayerHits { get; private set; }
        public int TotalRounds { get; private set; }
        public int PlayerAircraftHits { get; private set; }
        public int PlayerAircraftKills { get; private set; }
        public float PlayerAircraftDamage { get; private set; }
        public float LastPlayerAircraftDamage { get; private set; }
        public int LastPlayerAircraftTargetId { get; private set; } = -1;

        public Weapons(int seed) { random = new Random(seed); }

        public void Fire(AircraftState aircraft, bool trigger, float dt, List<Projectile> rounds)
        {
            float delay;
            cooldown.TryGetValue(aircraft.Id, out delay);
            delay -= dt;
            if (!trigger || !aircraft.Alive || aircraft.Ammo <= 0 || aircraft.Role == AircraftRole.Courier || aircraft.Role == AircraftRole.Bomber)
            { cooldown[aircraft.Id] = Math.Max(0f, delay); return; }
            // The cooldown is carried across ticks, so rate of fire is independent of simulation rate.
            while (delay <= 0f && aircraft.Ammo > 0)
            {
                float spread = (aircraft.IsPlayer ? .0026f : .0045f) * aircraft.SpreadMultiplier;
                float x = ((float)random.NextDouble() * 2f - 1f) * spread;
                float y = ((float)random.NextDouble() * 2f - 1f) * spread;
                Vector3 direction = Vector3.Normalize(aircraft.Forward + aircraft.Right * x + aircraft.Up * y);
                float side = (aircraft.Ammo & 1) == 0 ? -1.4f : 1.4f;
                Vector3 origin = aircraft.Position + aircraft.Forward * 4f + aircraft.Right * side;
                rounds.Add(new Projectile { Position = origin, PreviousPosition = origin,
                    Velocity = aircraft.Velocity + direction * (MuzzleSpeed * aircraft.MuzzleSpeedMultiplier),
                    Damage = RoundDamage * aircraft.DamageMultiplier, Team = aircraft.Team, OwnerId = aircraft.Id });
                aircraft.Ammo--; TotalRounds++;
                if (aircraft.IsPlayer) PlayerRounds++;
                delay += ShotInterval / Math.Max(.25f, Math.Min(3f, aircraft.FireRateMultiplier));
            }
            cooldown[aircraft.Id] = delay;
        }

        public void Step(float dt, List<Projectile> rounds, IList<AircraftState> aircraft,
            Dictionary<int, Vector3> previousPositions, TerrainField terrain, List<CombatEvent> events)
        { Step(dt, rounds, aircraft, previousPositions, terrain, events, null); }

        public void Step(float dt, List<Projectile> rounds, IList<AircraftState> aircraft,
            Dictionary<int, Vector3> previousPositions, TerrainField terrain, List<CombatEvent> events, IList<GroundTarget> groundTargets)
        {
            for (int i = rounds.Count - 1; i >= 0; i--)
            {
                Projectile round = rounds[i];
                round.PreviousPosition = round.Position;
                round.Velocity += new Vector3(0f, -9.81f * dt, 0f);
                round.Position += round.Velocity * dt;
                round.Age += dt;
                AircraftState hit = null;
                GroundTarget groundHit = null;
                float firstFraction = float.MaxValue;
                for (int a = 0; a < aircraft.Count; a++)
                {
                    AircraftState candidate = aircraft[a];
                    if (!candidate.Alive || candidate.Id == round.OwnerId || candidate.Team == round.Team) continue;
                    Vector3 previous;
                    if (!previousPositions.TryGetValue(candidate.Id, out previous)) previous = candidate.Position;
                    float fraction;
                    if (SweptAircraftHit(round.PreviousPosition, round.Position, previous, candidate, out fraction) && fraction < firstFraction)
                    { hit = candidate; firstFraction = fraction; }
                }
                if (groundTargets != null)
                    for (int g = 0; g < groundTargets.Count; g++)
                    {
                        GroundTarget candidate = groundTargets[g];
                        if (!candidate.Alive || candidate.Team == round.Team) continue;
                        float fraction;
                        if (SweptGroundHit(round.PreviousPosition, round.Position, candidate, out fraction) && fraction < firstFraction)
                        { groundHit = candidate; hit = null; firstFraction = fraction; }
                    }
                // Hills intercept the round before targets on the other side of terrain.
                bool intoTerrain = round.Position.Y <= terrain.Height(round.Position.X, round.Position.Z);
                if (intoTerrain)
                {
                    float a = round.PreviousPosition.Y - terrain.Height(round.PreviousPosition.X, round.PreviousPosition.Z);
                    float b = round.Position.Y - terrain.Height(round.Position.X, round.Position.Z);
                    float terrainFraction = a <= 0f ? 0f : a / Math.Max(.0001f, a - b);
                    if (terrainFraction < firstFraction) { hit = null; groundHit = null; }
                }
                if (hit != null)
                {
                    float previousHealth = hit.Health;
                    hit.Health = Math.Max(0f, hit.Health - round.Damage * hit.DamageTakenMultiplier);
                    float actualDamage = Math.Max(0f, previousHealth - hit.Health);
                    bool destroyed = previousHealth > 0f && hit.Health <= 0f;
                    Vector3 impact = Vector3.Lerp(round.PreviousPosition, round.Position, firstFraction);
                    events.Add(new CombatEvent(impact, CombatEventKind.GunHit) { AircraftId = hit.Id,
                        OwnerId = round.OwnerId, AircraftDamage = actualDamage, DestroyedAircraft = destroyed });
                    if (round.OwnerId == 0) PlayerHits++;
                    if (round.OwnerId == 0 && actualDamage > 0f)
                    {
                        PlayerAircraftHits++;
                        PlayerAircraftDamage += actualDamage;
                        LastPlayerAircraftDamage = actualDamage;
                        LastPlayerAircraftTargetId = hit.Id;
                        if (destroyed) PlayerAircraftKills++;
                    }
                    if (hit.Health <= 0f) { hit.Alive = false; hit.DestructionCause = "Gunfire"; }
                    rounds.RemoveAt(i);
                }
                else if (groundHit != null)
                {
                    groundHit.Health = Math.Max(0f, groundHit.Health - round.Damage);
                    if (round.OwnerId == 0) PlayerHits++;
                    events.Add(new CombatEvent(Vector3.Lerp(round.PreviousPosition, round.Position, firstFraction), CombatEventKind.GunHit));
                    if (groundHit.Health <= 0f) groundHit.Alive = false;
                    rounds.RemoveAt(i);
                }
                else if (intoTerrain)
                { events.Add(new CombatEvent(round.Position, CombatEventKind.GroundHit)); rounds.RemoveAt(i); }
                else if (round.Age >= round.Lifetime) rounds.RemoveAt(i);
            }
        }

        public static bool SweptAircraftHit(Vector3 start, Vector3 end, Vector3 previousTarget,
            AircraftState target, out float fraction)
        {
            Quaternion inverse = Quaternion.Conjugate(target.Rotation);
            Vector3 a = Vector3.Transform(start - previousTarget, inverse);
            Vector3 b = Vector3.Transform(end - target.Position, inverse);
            // Two overlapping ellipsoids approximate solid fuselage and broad, thin wings.
            float size = target.Role == AircraftRole.Courier || target.Role == AircraftRole.Bomber ? 1.75f : 1f;
            float body, wing;
            bool bodyHit = SegmentEllipsoid(a, b, new Vector3(1.5f, 1.6f, 5.5f) * size, out body);
            bool wingHit = SegmentEllipsoid(a, b, new Vector3(6.1f, .65f, 2.0f) * size, out wing);
            fraction = bodyHit && wingHit ? Math.Min(body, wing) : bodyHit ? body : wing;
            return bodyHit || wingHit;
        }

        public static bool SweptGroundHit(Vector3 start, Vector3 end, GroundTarget target, out float fraction)
        {
            Quaternion inverse = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -target.Heading);
            Vector3 a = Vector3.Transform(start - target.PreviousPosition, inverse);
            Vector3 b = Vector3.Transform(end - target.Position, inverse);
            return SegmentEllipsoid(a, b, target.Extents, out fraction);
        }

        private static bool SegmentEllipsoid(Vector3 start, Vector3 end, Vector3 radii, out float fraction)
        {
            Vector3 a = start / radii, d = (end - start) / radii;
            float c = Vector3.Dot(a, a) - 1f;
            if (c <= 0f) { fraction = 0f; return true; }
            float aa = Vector3.Dot(d, d), bb = 2f * Vector3.Dot(a, d);
            float disc = bb * bb - 4f * aa * c;
            if (aa < .0000001f || disc < 0f) { fraction = 0f; return false; }
            fraction = (-bb - (float)Math.Sqrt(disc)) / (2f * aa);
            return fraction >= 0f && fraction <= 1f;
        }

        /// <summary>Intercept point for HUD/AI; this is guidance, never an automatic hit.</summary>
        public static Vector3 ComputeLead(AircraftState shooter, AircraftState target)
        {
            Vector3 offset = target.Position - shooter.Position;
            Vector3 relativeVelocity = target.Velocity - shooter.Velocity;
            float muzzleSpeed = MuzzleSpeed * Math.Max(.25f, shooter.MuzzleSpeedMultiplier);
            float aa = Vector3.Dot(relativeVelocity, relativeVelocity) - muzzleSpeed * muzzleSpeed;
            float bb = 2f * Vector3.Dot(offset, relativeVelocity);
            float cc = Vector3.Dot(offset, offset);
            float disc = bb * bb - 4f * aa * cc;
            float t = offset.Length() / muzzleSpeed;
            if (disc >= 0f && Math.Abs(aa) > .0001f)
            {
                float root = (float)Math.Sqrt(disc);
                float t1 = (-bb - root) / (2f * aa), t2 = (-bb + root) / (2f * aa);
                if (t1 > 0f) t = t1;
                if (t2 > 0f && t2 < t) t = t2;
            }
            t = Math.Min(2.5f, Math.Max(0f, t));
            return target.Position + relativeVelocity * t + Vector3.UnitY * (4.905f * t * t);
        }
    }
}
