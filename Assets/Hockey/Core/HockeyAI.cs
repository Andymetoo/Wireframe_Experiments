using System;
using System.Numerics;

namespace ColdSnap
{
    /// <summary>Role-based hockey decisions sampled at a human-sized reaction interval.</summary>
    public sealed class HockeyAI
    {
        private readonly Random random;
        private readonly int id;
        private float decisionTime;
        private float actionTime;
        private HockeyInput current;
        private Vector2 target;
        private Vector2 aim;
        private bool goalie;
        private float requestedSpeed;
        private static readonly Vector2[] NetCorners = {
            new Vector2(-2.8f, 24f), new Vector2(2.8f, 24f),
            new Vector2(-2.8f, 27.8f), new Vector2(2.8f, 27.8f) };
        public HockeyAI(int seed, int skaterId) { random = new Random(seed); id = skaterId; }
        public void Reset() { decisionTime = 0f; actionTime = .55f; current = default(HockeyInput); }

        public HockeyInput Think(HockeySession match, SkaterState skater, float dt)
        {
            actionTime -= dt;
            decisionTime -= dt;
            current.Shoot = false; current.Pass = false; current.Check = false;
            if (decisionTime <= 0f)
            {
                // Keepers need a readable human reaction delay; their saves still depend on physical pad reach.
                decisionTime = skater.Goalie ? .23f + Rand() * .12f : .16f + Rand() * .1f;
                Decide(match, skater);
            }
            Vector2 navigation = goalie ? target : RouteAroundNet(skater.Position, target);
            Vector2 displacement = navigation - skater.Position;
            float distance = displacement.Length();
            Vector2 wanted = distance > .05f ? displacement / distance * Math.Min(requestedSpeed, distance * (goalie ? 3f : 2.1f)) : Vector2.Zero;
            current.Move = Limit((wanted - skater.Velocity) * .6f);
            current.Brake = distance < (goalie ? .16f : .65f) && skater.Velocity.Length() > 1f;
            current.Aim = aim.LengthSquared() > .01f ? Vector2.Normalize(aim) : skater.Forward;
            current.Sprint = !goalie && distance > 7f;
            return current;
        }

        private void Decide(HockeySession match, SkaterState skater)
        {
            goalie = skater.Goalie;
            requestedSpeed = goalie ? 4.7f : 7.8f;
            float attack = skater.Team == 0 ? 1f : -1f;
            Vector2 theirGoal = new Vector2(0f, 25f * attack);
            Vector2 ownGoal = -theirGoal;
            PuckState puck = match.Puck;
            if (goalie) { Goalkeeper(match, skater, attack); return; }
            SkaterState carrier = puck.ControllerId >= 0 ? match.Skaters[puck.ControllerId] : null;
            if (carrier == skater)
            {
                Carry(match, skater, theirGoal, attack);
                return;
            }
            aim = puck.Position - skater.Position;
            int pursuer = Nearest(match, skater.Team, puck.Position, carrier == null ? -1 : carrier.Id);
            if (carrier == null)
            {
                if (pursuer == id)
                {
                    float time = Math.Min(.5f, Vector2.Distance(skater.Position, puck.Position) / 10f);
                    target = RecoveryPoint(puck.Position + puck.Velocity * time);
                    skater.Intent = "Recover loose puck";
                }
                else
                {
                    bool back = id % 3 == 2;
                    float lane = id % 3 == 1 ? -1f : 1f;
                    target = Bound(new Vector2(lane * (back ? 5.5f : 7.5f), puck.Position.Y + attack * (back ? -7f : 4f)));
                    skater.Intent = back ? "Protect middle" : "Offer outlet";
                }
            }
            else if (carrier.Team == skater.Team)
            {
                bool back = id % 3 == 2;
                float lane = id % 3 == 1 ? -1f : 1f;
                if (Math.Abs(carrier.Position.X) > 3f) lane = -Math.Sign(carrier.Position.X);
                float y = carrier.Position.Y + attack * (back ? -6f : 6f);
                y = Clamp(y, -20.8f, 20.8f);
                target = new Vector2(lane * (back ? 4.7f : 7.2f), y);
                // The high support stays available for a return pass, the wing attacks the far post.
                if (!back && attack * carrier.Position.Y > 13f)
                    target = new Vector2(lane * 3.7f, attack * 19.5f);
                skater.Intent = back ? "High support" : "Far-side passing lane";
            }
            else
            {
                if (pursuer == id)
                {
                    target = Bound(carrier.Position + carrier.Velocity * .23f);
                    skater.Intent = "Pressure carrier";
                    if (Vector2.Distance(skater.Position, carrier.Position) < 1.6f && actionTime <= 0f)
                    { current.Check = true; actionTime = 1f + Rand() * .5f; }
                }
                else
                {
                    SkaterState threat = MostDangerousReceiver(match, skater, carrier);
                    Vector2 protect = threat == null ? new Vector2(id % 3 == 1 ? -4f : 4f, -attack * 14f) : threat.Position;
                    Vector2 towardNet = ownGoal - protect;
                    if (towardNet.LengthSquared() > .1f) towardNet = Vector2.Normalize(towardNet);
                    target = Bound(protect + towardNet * 2f);
                    if (attack * puck.Position.Y < -12f)
                        target = Vector2.Lerp(target, new Vector2(id % 3 == 1 ? -3f : 3f, -attack * 21f), .4f);
                    skater.Intent = "Cover receiver / slot";
                }
            }
        }

        private void Carry(HockeySession match, SkaterState skater, Vector2 goal, float attack)
        {
            PuckState puck = match.Puck;
            Vector2 toGoal = goal - puck.Position;
            float distance = toGoal.Length();
            SkaterState keeper = match.Skaters[skater.Team == 0 ? 7 : 6];
            float corner = keeper.Position.X >= 0f ? -1.18f : 1.18f;
            Vector2 shotTarget = goal + new Vector2(corner + (Rand() - .5f) * .52f, 0f);
            aim = shotTarget - skater.Position;
            float pressure = NearestOpponentDistance(match, skater);
            SkaterState receiver = FindReceiver(match, skater, attack);
            bool called = skater.Team == 0 && match.PassCallTime > 0f && receiver == match.Player;
            float lane = LaneClearance(match, skater, receiver);
            if (actionTime <= 0f && receiver != null && lane > 1f && (called || pressure < 3.6f || (distance > 17f && Rand() < .2f)))
            {
                float travel = Vector2.Distance(skater.Position, receiver.Position) / 15f;
                aim = receiver.Position + receiver.Velocity * Math.Min(.65f, travel * .75f) - skater.Position;
                current.Pass = true; current.ShotPower = .55f;
                actionTime = .8f + Rand() * .6f;
                skater.Intent = called ? "Answer pass call" : "Move puck to support";
            }
            else if (actionTime <= 0f && distance < 20.5f && distance > 2f && Math.Abs(puck.Position.X) < 10f && attack * puck.Position.Y < 24f)
            {
                current.Shoot = true; current.ShotPower = .65f + Rand() * .35f;
                actionTime = .8f + Rand() * .6f;
                skater.Intent = "Shoot far corner";
            }
            else skater.Intent = "Carry into shooting lane";
            // Move off the boards and toward the slot. Avoid skating through a keeper or net.
            float side = skater.Position.X >= 0f ? 1f : -1f;
            target = new Vector2(side * 2.2f, attack * 18.5f);
            if (pressure < 2.6f) target.X = -side * 5.5f;
            if (attack * skater.Position.Y > 23f) target = new Vector2(side * 6f, attack * 18f);
            requestedSpeed = 6.6f;
        }

        private void Goalkeeper(HockeySession match, SkaterState skater, float attack)
        {
            Vector2 ownGoal = new Vector2(0f, -attack * 25f);
            Vector2 toPuck = match.Puck.Position - ownGoal;
            float longitudinal = Math.Max(1.5f, toPuck.Y * attack);
            float depth = longitudinal < 12f ? 1.15f : 1.6f;
            float x = Clamp(toPuck.X * depth / longitudinal, -1.62f, 1.62f);
            // React to the predicted line only after the normal delayed decision tick.
            Vector2 v = match.Puck.Velocity;
            if (v.Y * attack < -5f)
            {
                float t = (ownGoal.Y + attack * depth - match.Puck.Position.Y) / v.Y;
                if (t > 0f && t < .9f)
                    x = Clamp(match.Puck.Position.X + v.X * t, -2.1f, 2.1f);
            }
            target = new Vector2(x, ownGoal.Y + attack * depth);
            aim = toPuck;
            skater.Intent = Math.Abs(v.Y) > 8f ? "Square to shot" : "Track puck angle";
        }

        private SkaterState FindReceiver(HockeySession match, SkaterState skater, float attack)
        {
            SkaterState result = null;
            float best = -1000f;
            for (int i = 0; i < 6; i++)
            {
                SkaterState candidate = match.Skaters[i];
                if (candidate.Team != skater.Team || candidate == skater) continue;
                float d = Vector2.Distance(candidate.Position, skater.Position);
                if (d < 3f || d > 24f) continue;
                float score = (candidate.Position.Y - skater.Position.Y) * attack * .6f - d * .12f
                    + Math.Min(6f, NearestOpponentDistance(match, candidate));
                if (candidate == match.Player && match.PassCallTime > 0f) score += 30f;
                if (score > best) { best = score; result = candidate; }
            }
            return result;
        }

        private static float LaneClearance(HockeySession match, SkaterState from, SkaterState receiver)
        {
            if (receiver == null) return 0f;
            Vector2 delta = receiver.Position - from.Position;
            float distanceSquared = delta.LengthSquared();
            float nearest = 100f;
            for (int i = 0; i < 6; i++)
            {
                SkaterState other = match.Skaters[i];
                if (other.Team == from.Team) continue;
                float t = Clamp(Vector2.Dot(other.Position - from.Position, delta) / distanceSquared, 0f, 1f);
                nearest = Math.Min(nearest, Vector2.Distance(other.Position, from.Position + delta * t));
            }
            return nearest;
        }

        private static SkaterState MostDangerousReceiver(HockeySession match, SkaterState defender, SkaterState carrier)
        {
            SkaterState best = null;
            float distance = float.MaxValue;
            Vector2 ownGoal = new Vector2(0f, defender.Team == 0 ? -25f : 25f);
            for (int i = 0; i < 6; i++)
            {
                SkaterState candidate = match.Skaters[i];
                if (candidate.Team == defender.Team || candidate == carrier) continue;
                float d = Vector2.Distance(candidate.Position, ownGoal) + Vector2.Distance(candidate.Position, defender.Position) * .35f;
                // Two covering players prefer different lanes rather than marking one receiver.
                if ((defender.Id % 3 == 1) != (candidate.Position.X < 0f)) d += 6f;
                if (d < distance) { distance = d; best = candidate; }
            }
            return best;
        }

        private static int Nearest(HockeySession match, int team, Vector2 point, int exclude)
        {
            int best = -1;
            float distance = float.MaxValue;
            for (int i = 0; i < 6; i++)
            {
                SkaterState skater = match.Skaters[i];
                if (skater.Team != team || skater.Id == exclude) continue;
                float d = Vector2.Distance(skater.Position, point) + (skater.Stun > 0f ? 3f : 0f);
                if (d < distance) { distance = d; best = skater.Id; }
            }
            return best;
        }

        private static float NearestOpponentDistance(HockeySession match, SkaterState skater)
        {
            float distance = 100f;
            for (int i = 0; i < 6; i++) if (match.Skaters[i].Team != skater.Team)
                distance = Math.Min(distance, Vector2.Distance(skater.Position, match.Skaters[i].Position));
            return distance;
        }
        private float Rand() { return (float)random.NextDouble(); }
        private static float Clamp(float value, float min, float max) { return Math.Max(min, Math.Min(max, value)); }
        private static Vector2 Limit(Vector2 value) { return value.LengthSquared() > 1f ? Vector2.Normalize(value) : value; }
        private static Vector2 Bound(Vector2 point) { return new Vector2(Clamp(point.X, -11.5f, 11.5f), Clamp(point.Y, -26.5f, 26.5f)); }

        private static Vector2 RecoveryPoint(Vector2 point)
        {
            // Support lanes stay away from the boards, but a retriever must reach the entire playable ice.
            Vector2 unusedVelocity = Vector2.Zero;
            RinkPhysics.Constrain(ref point, ref unusedVelocity, RinkPhysics.SkaterRadius + .12f, 0f);
            return point;
        }

        private static Vector2 RouteAroundNet(Vector2 start, Vector2 destination)
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector2 from = new Vector2(start.X, start.Y * sign);
                Vector2 to = new Vector2(destination.X, destination.Y * sign);
                // A puck beside netting is approached from the accessible side, within normal stick reach.
                if (InsideNetBox(to))
                {
                    if (to.Y > RinkPhysics.GoalLine + RinkPhysics.GoalDepth) to.Y = 27.6f;
                    else if (Math.Abs(to.X) > RinkPhysics.GoalHalfWidth) to.X = Math.Sign(to.X) * 2.65f;
                    else to.Y = 24.2f;
                }
                if (InsideNetBox(from))
                {
                    Vector2 exit = from;
                    if (from.Y > RinkPhysics.GoalLine + RinkPhysics.GoalDepth) exit.Y = 27.7f;
                    else if (Math.Abs(from.X) > RinkPhysics.GoalHalfWidth) exit.X = Math.Sign(from.X) * 2.8f;
                    else exit.Y = 24f;
                    return new Vector2(exit.X, exit.Y * sign);
                }
                if (!CrossesNetBox(from, to))
                {
                    destination = new Vector2(to.X, to.Y * sign);
                    continue;
                }
                float bestLength = float.MaxValue;
                Vector2 waypoint = from;
                for (int a = 0; a < NetCorners.Length; a++)
                {
                    Vector2 first = NetCorners[a];
                    if (Vector2.DistanceSquared(from, first) < .12f * .12f) continue;
                    if (CrossesNetBox(from, first)) continue;
                    for (int b = 0; b < NetCorners.Length; b++)
                    {
                        Vector2 last = NetCorners[b];
                        if (CrossesNetBox(first, last) || CrossesNetBox(last, to)) continue;
                        float length = Vector2.Distance(from, first) + Vector2.Distance(first, last) + Vector2.Distance(last, to);
                        if (length < bestLength) { bestLength = length; waypoint = first; }
                    }
                }
                return new Vector2(waypoint.X, waypoint.Y * sign);
            }
            return destination;
        }

        private static bool InsideNetBox(Vector2 p)
        { return Math.Abs(p.X) < 2.5f && p.Y > 24.35f && p.Y < 27.45f; }

        private static bool CrossesNetBox(Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            float enter = 0f, leave = 1f;
            return Slab(from.X, delta.X, -2.5f, 2.5f, ref enter, ref leave)
                && Slab(from.Y, delta.Y, 24.35f, 27.45f, ref enter, ref leave);
        }

        private static bool Slab(float origin, float direction, float min, float max, ref float enter, ref float leave)
        {
            if (Math.Abs(direction) < .00001f) return origin >= min && origin <= max;
            float first = (min - origin) / direction, second = (max - origin) / direction;
            if (first > second) { float swap = first; first = second; second = swap; }
            enter = Math.Max(enter, first); leave = Math.Min(leave, second);
            return enter <= leave;
        }
    }
}
