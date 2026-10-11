using System;
using System.Collections.Generic;
using System.Numerics;

namespace ColdSnap
{
    /// <summary>Ice-plane puck physics. Every stick touch changes velocity, never position.</summary>
    public static class PuckPhysics
    {
        public const float StickReach = 1.75f, GuideAcceleration = 105;
        public const float PassSpeed = 16.5f, MaxShotSpeed = 36;

        public static bool CanReach(SkaterState s, PuckState puck)
        {
            Vector2 d = puck.Position - s.Position;
            float d2 = d.LengthSquared();
            return !s.Goalie && s.Stun <= 0 && d2 < StickReach * StickReach &&
                (d2 < .36f || Vector2.Dot(HockeyMath.Unit(d, s.Forward), s.Forward) > -.15f) &&
                StickPathClear(s.Position, puck.Position);
        }

        private static bool StickPathClear(Vector2 from, Vector2 to)
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                float y = RinkPhysics.GoalLine * sign, back = (RinkPhysics.GoalLine + RinkPhysics.GoalDepth) * sign;
                float x = RinkPhysics.GoalHalfWidth;
                if (Crosses(from, to, new Vector2(-x, y), new Vector2(-x, back)) ||
                    Crosses(from, to, new Vector2(x, y), new Vector2(x, back)) ||
                    Crosses(from, to, new Vector2(-x, back), new Vector2(x, back))) return false;
                Vector2 path = to - from;
                float lengthSquared = path.LengthSquared();
                if (lengthSquared < .00001f) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 post = new Vector2(x * side, y);
                    float t = HockeyMath.Clamp(Vector2.Dot(post - from, path) / lengthSquared, 0, 1);
                    if (Vector2.DistanceSquared(post, from + path * t) < .125f * .125f) return false;
                }
            }
            return true;
        }

        private static bool Crosses(Vector2 from, Vector2 to, Vector2 a, Vector2 b)
        {
            Vector2 path = to - from, segment = b - a, offset = a - from;
            float denominator = path.X * segment.Y - path.Y * segment.X;
            if (Math.Abs(denominator) < .000001f) return false;
            float t = (offset.X * segment.Y - offset.Y * segment.X) / denominator;
            float u = (offset.X * path.Y - offset.Y * path.X) / denominator;
            return t > 0 && t < 1 && u >= 0 && u <= 1;
        }

        public static int Guide(PuckState puck, IList<SkaterState> skaters, float dt)
        {
            int oldController = puck.ControllerId;
            puck.ControllerId = -1;
            if (puck.NoControlTime > 0) { puck.ControlTime = 0; return -1; }
            SkaterState best = null;
            float bestCost = float.MaxValue;
            for (int i = 0; i < skaters.Count; i++)
            {
                SkaterState s = skaters[i];
                if (!CanReach(s, puck) || s.StickCooldown > 0 || (puck.Velocity - s.Velocity).LengthSquared() > 24 * 24) continue;
                float cost = Vector2.DistanceSquared(puck.Position, s.Position + s.Forward * 1.03f);
                if (s.Id == oldController) cost -= .18f;
                if (cost < bestCost) { bestCost = cost; best = s; }
            }
            if (best == null) { puck.ControlTime = 0; return -1; }
            Vector2 right = new Vector2(best.Forward.Y, -best.Forward.X);
            Vector2 target = best.Position + best.Forward * 1.07f + right * .14f;
            Vector2 force = (target - puck.Position) * 42 + (best.Velocity - puck.Velocity) * 13;
            puck.Velocity += HockeyMath.Limited(force, GuideAcceleration) * Math.Min(dt, .05f);
            // "Control" is a proximity observation, not ownership or a transport constraint.
            if ((puck.Velocity - best.Velocity).LengthSquared() < 11 * 11)
            {
                puck.ControllerId = best.Id;
                puck.ControlTime = oldController == best.Id ? puck.ControlTime + dt : 0;
                puck.LastTouchId = best.Id; puck.LastTouchTeam = best.Team;
            }
            return puck.ControllerId;
        }

        public static bool Strike(SkaterState s, PuckState puck, Vector2 direction, float power, bool isPass)
        {
            if (s.StickCooldown > 0 || !CanReach(s, puck)) return false;
            Vector2 aim = HockeyMath.Unit(direction, s.Forward);
            float speed = isPass ? PassSpeed * (.75f + .25f * HockeyMath.Clamp(power, 0, 1)) : 19 + 17 * HockeyMath.Clamp(power, 0, 1);
            // The stick supplies a finite impulse; inherited skater momentum remains visible.
            Vector2 desired = aim * speed + s.Velocity * .36f;
            puck.Velocity += HockeyMath.Limited(desired - puck.Velocity, isPass ? 24 : 45);
            puck.LastTouchId = s.Id; puck.LastTouchTeam = s.Team;
            puck.ControllerId = -1; puck.ControlTime = 0;
            puck.NoControlTime = isPass ? .10f : .13f;
            s.StickCooldown = isPass ? .27f : .42f;
            s.Cooldown = Math.Max(s.Cooldown, isPass ? .22f : .36f);
            return true;
        }

        /// <returns>Scoring team (0 toward +Y, 1 toward -Y), or -1.</returns>
        public static int Step(PuckState puck, IList<SkaterState> skaters, float dt)
        {
            if (dt <= 0) return -1;
            dt = Math.Min(dt, .05f);
            puck.NoControlTime = Math.Max(0, puck.NoControlTime - dt);
            // A 9cm maximum step protects the 15cm posts even on hard point shots.
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(dt * 180, puck.Velocity.Length() * dt / .09f)));
            float h = dt / steps;
            for (int step = 0; step < steps; step++)
            {
                Vector2 previous = puck.Position;
                float speed = puck.Velocity.Length();
                if (speed > 0)
                {
                    float nextSpeed = Math.Max(0, speed - (.20f + speed * .012f) * h);
                    puck.Velocity *= nextSpeed / speed;
                }
                puck.Position += puck.Velocity * h;
                RinkPhysics.Constrain(ref puck.Position, ref puck.Velocity, RinkPhysics.PuckRadius, .81f);
                ResolveNets(puck);
                for (int i = 0; i < skaters.Count; i++)
                {
                    Vector2 beforeContact = puck.Position;
                    ResolveBody(puck, skaters[i]);
                    ResolveNetContactMotion(puck, beforeContact);
                }
                // Pad/skate separation can push an already constrained puck back through
                // a board. Static rink containment must be the final contact constraint.
                RinkPhysics.Constrain(ref puck.Position, ref puck.Velocity, RinkPhysics.PuckRadius, .81f);
                for (int team = 0; team < 2; team++)
                {
                    float sign = team == 0 ? 1 : -1;
                    float oldY = previous.Y * sign, newY = puck.Position.Y * sign;
                    // Entire puck must pass the goal line; posts exclude their own radius.
                    float line = RinkPhysics.GoalLine + RinkPhysics.PuckRadius;
                    if (oldY < line && newY >= line)
                    {
                        float t = (line - oldY) / Math.Max(.000001f, newY - oldY);
                        float x = previous.X + (puck.Position.X - previous.X) * t;
                        if (Math.Abs(x) < RinkPhysics.GoalHalfWidth - RinkPhysics.PostRadius - RinkPhysics.PuckRadius)
                            return team;
                    }
                }
            }
            if (!HockeyMath.Finite(puck.Position) || !HockeyMath.Finite(puck.Velocity))
                throw new InvalidOperationException("Non-finite puck state");
            return -1;
        }

        private static void ResolveNets(PuckState p)
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                float y = RinkPhysics.GoalLine * sign;
                float back = (RinkPhysics.GoalLine + RinkPhysics.GoalDepth) * sign;
                float x = RinkPhysics.GoalHalfWidth;
                RinkPhysics.Segment(ref p.Position, ref p.Velocity, new Vector2(-x, y), new Vector2(-x, y), RinkPhysics.PuckRadius + RinkPhysics.PostRadius, .7f);
                RinkPhysics.Segment(ref p.Position, ref p.Velocity, new Vector2(x, y), new Vector2(x, y), RinkPhysics.PuckRadius + RinkPhysics.PostRadius, .7f);
                RinkPhysics.Segment(ref p.Position, ref p.Velocity, new Vector2(-x, y), new Vector2(-x, back), RinkPhysics.PuckRadius + .025f, .33f);
                RinkPhysics.Segment(ref p.Position, ref p.Velocity, new Vector2(x, y), new Vector2(x, back), RinkPhysics.PuckRadius + .025f, .33f);
                RinkPhysics.Segment(ref p.Position, ref p.Velocity, new Vector2(-x, back), new Vector2(x, back), RinkPhysics.PuckRadius + .025f, .24f);
            }
        }

        // Player separation is a correction that can be larger than the ordinary 9cm
        // integration step. Sweep that correction against solid netting and posts so
        // its endpoint cannot emerge on the opposite side of a thin obstacle.
        private static void ResolveNetContactMotion(PuckState puck, Vector2 start)
        {
            Vector2 end = puck.Position;
            if (Vector2.DistanceSquared(start, end) < .00000001f) return;
            for (int iteration = 0; iteration < 3; iteration++)
            {
                float fraction = 2, bounce = .33f;
                Vector2 normal = Vector2.Zero;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    float y = RinkPhysics.GoalLine * sign;
                    float back = (RinkPhysics.GoalLine + RinkPhysics.GoalDepth) * sign;
                    float x = RinkPhysics.GoalHalfWidth;
                    ContactCapsule(start, end, new Vector2(-x, y), new Vector2(-x, y), RinkPhysics.PuckRadius + RinkPhysics.PostRadius, .7f, ref fraction, ref normal, ref bounce);
                    ContactCapsule(start, end, new Vector2(x, y), new Vector2(x, y), RinkPhysics.PuckRadius + RinkPhysics.PostRadius, .7f, ref fraction, ref normal, ref bounce);
                    ContactCapsule(start, end, new Vector2(-x, y), new Vector2(-x, back), RinkPhysics.PuckRadius + .025f, .33f, ref fraction, ref normal, ref bounce);
                    ContactCapsule(start, end, new Vector2(x, y), new Vector2(x, back), RinkPhysics.PuckRadius + .025f, .33f, ref fraction, ref normal, ref bounce);
                    ContactCapsule(start, end, new Vector2(-x, back), new Vector2(x, back), RinkPhysics.PuckRadius + .025f, .24f, ref fraction, ref normal, ref bounce);
                }
                if (fraction > 1) break;
                Vector2 motion = end - start;
                Vector2 contact = start + motion * fraction + normal * .0001f;
                Vector2 remainder = motion * (1 - fraction);
                remainder -= normal * Math.Min(0, Vector2.Dot(remainder, normal));
                start = contact;
                end = contact + remainder;
                float into = Vector2.Dot(puck.Velocity, normal);
                if (into < 0) puck.Velocity -= normal * into * (1 + bounce);
            }
            puck.Position = end;
        }

        private static void ContactCapsule(Vector2 start, Vector2 end, Vector2 a, Vector2 b, float radius, float restitution,
            ref float fraction, ref Vector2 normal, ref float bounce)
        {
            Vector2 axis = b - a;
            float length = axis.Length();
            ContactCircle(start, end, a, radius, restitution, ref fraction, ref normal, ref bounce);
            if (length < .00001f) return;
            ContactCircle(start, end, b, radius, restitution, ref fraction, ref normal, ref bounce);
            axis /= length;
            Vector2 perpendicular = new Vector2(-axis.Y, axis.X);
            float before = Vector2.Dot(start - a, perpendicular);
            float after = Vector2.Dot(end - a, perpendicular);
            float side = before >= 0 ? 1 : -1;
            if (Math.Abs(before) < radius - .0002f || (after - before) * side >= -.0000001f) return;
            float t = (side * radius - before) / (after - before);
            if (t < -.0001f || t > 1 || t >= fraction) return;
            t = Math.Max(0, t);
            float along = Vector2.Dot(start + (end - start) * t - a, axis);
            if (along < 0 || along > length) return;
            fraction = t; normal = perpendicular * side; bounce = restitution;
        }

        private static void ContactCircle(Vector2 start, Vector2 end, Vector2 center, float radius, float restitution,
            ref float fraction, ref Vector2 normal, ref float bounce)
        {
            Vector2 motion = end - start, offset = start - center;
            float a = motion.LengthSquared();
            if (a < .00000001f) return;
            float b = Vector2.Dot(offset, motion);
            if (b >= 0) return;
            float c = offset.LengthSquared() - radius * radius;
            float discriminant = b * b - a * c;
            if (discriminant < 0) return;
            float t = c <= .000001f ? 0 : (-b - (float)Math.Sqrt(discriminant)) / a;
            if (t < 0 || t > 1 || t >= fraction) return;
            fraction = t;
            normal = HockeyMath.Unit(start + motion * t - center, -HockeyMath.Unit(motion, Vector2.UnitY));
            bounce = restitution;
        }

        private static void ResolveBody(PuckState puck, SkaterState s)
        {
            // Goalies have a horizontal pad capsule. Skaters have a small skate/body disc.
            Vector2 center = s.Position;
            float radius = s.Goalie ? .37f : .31f;
            if (s.Goalie)
            {
                Vector2 right = new Vector2(s.Forward.Y, -s.Forward.X);
                float t = HockeyMath.Clamp(Vector2.Dot(puck.Position - center, right), -.52f, .52f);
                center += right * t;
            }
            Vector2 delta = puck.Position - center;
            float minDist = radius + RinkPhysics.PuckRadius;
            if (delta.LengthSquared() >= minDist * minDist) return;
            Vector2 normal = HockeyMath.Unit(delta, HockeyMath.Unit(puck.Velocity - s.Velocity, s.Forward) * -1);
            puck.Position = center + normal * minDist;
            Vector2 relative = puck.Velocity - s.Velocity;
            float toward = Vector2.Dot(relative, normal);
            if (toward < 0)
            {
                // Pads give a softer, recoverable rebound; ordinary skates can deflect hard.
                puck.Velocity -= normal * toward * (s.Goalie ? 1.33f : 1.62f);
                if (s.Goalie) puck.Velocity *= .86f;
                if (toward < -.6f)
                {
                    puck.LastCollisionId = s.Id; puck.CollisionSerial++;
                    puck.LastTouchId = s.Id; puck.LastTouchTeam = s.Team;
                    puck.ControllerId = -1; puck.NoControlTime = s.Goalie ? .23f : .07f;
                }
            }
        }
    }
}
