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
        public CombatEvent(Vector3 position, CombatEventKind kind)
        { Position = position; Kind = kind; Lifetime = kind == CombatEventKind.Destroyed ? 5f : 1.1f; }
    }

    public sealed class Projectile
    {
        public Vector3 Position, PreviousPosition, Velocity;
        public int Team, OwnerId;
        public float Age, Lifetime = 1.85f;
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

        public Weapons(int seed) { random = new Random(seed); }

        public void Fire(AircraftState aircraft, bool trigger, float dt, List<Projectile> rounds)
        {
            float delay;
            cooldown.TryGetValue(aircraft.Id, out delay);
            delay -= dt;
            if (!trigger || !aircraft.Alive || aircraft.Ammo <= 0 || aircraft.Role == AircraftRole.Courier)
            { cooldown[aircraft.Id] = Math.Max(0f, delay); return; }
            // The cooldown is carried across ticks, so rate of fire is independent of simulation rate.
            while (delay <= 0f && aircraft.Ammo > 0)
            {
                float spread = aircraft.IsPlayer ? .0026f : .0045f;
                float x = ((float)random.NextDouble() * 2f - 1f) * spread;
                float y = ((float)random.NextDouble() * 2f - 1f) * spread;
                Vector3 direction = Vector3.Normalize(aircraft.Forward + aircraft.Right * x + aircraft.Up * y);
                float side = (aircraft.Ammo & 1) == 0 ? -1.4f : 1.4f;
                Vector3 origin = aircraft.Position + aircraft.Forward * 4f + aircraft.Right * side;
                rounds.Add(new Projectile { Position = origin, PreviousPosition = origin,
                    Velocity = aircraft.Velocity + direction * MuzzleSpeed, Team = aircraft.Team, OwnerId = aircraft.Id });
                aircraft.Ammo--; TotalRounds++;
                if (aircraft.IsPlayer) PlayerRounds++;
                delay += ShotInterval;
            }
            cooldown[aircraft.Id] = delay;
        }

        public void Step(float dt, List<Projectile> rounds, IList<AircraftState> aircraft,
            Dictionary<int, Vector3> previousPositions, TerrainField terrain, List<CombatEvent> events)
        {
            for (int i = rounds.Count - 1; i >= 0; i--)
            {
                Projectile round = rounds[i];
                round.PreviousPosition = round.Position;
                round.Velocity += new Vector3(0f, -9.81f * dt, 0f);
                round.Position += round.Velocity * dt;
                round.Age += dt;
                AircraftState hit = null;
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
                if (hit != null)
                {
                    hit.Health = Math.Max(0f, hit.Health - RoundDamage);
                    Vector3 impact = Vector3.Lerp(round.PreviousPosition, round.Position, firstFraction);
                    events.Add(new CombatEvent(impact, CombatEventKind.GunHit));
                    if (round.Team == 0) PlayerHits++;
                    if (hit.Health <= 0f) hit.Alive = false;
                    rounds.RemoveAt(i);
                }
                else if (round.Position.Y <= terrain.Height(round.Position.X, round.Position.Z))
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
            float size = target.Role == AircraftRole.Courier ? 1.75f : 1f;
            float body, wing;
            bool bodyHit = SegmentEllipsoid(a, b, new Vector3(1.5f, 1.6f, 5.5f) * size, out body);
            bool wingHit = SegmentEllipsoid(a, b, new Vector3(6.1f, .65f, 2.0f) * size, out wing);
            fraction = bodyHit && wingHit ? Math.Min(body, wing) : bodyHit ? body : wing;
            return bodyHit || wingHit;
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
            float aa = Vector3.Dot(relativeVelocity, relativeVelocity) - MuzzleSpeed * MuzzleSpeed;
            float bb = 2f * Vector3.Dot(offset, relativeVelocity);
            float cc = Vector3.Dot(offset, offset);
            float disc = bb * bb - 4f * aa * cc;
            float t = offset.Length() / MuzzleSpeed;
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
