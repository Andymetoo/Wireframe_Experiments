using System;
using System.Collections.Generic;
using System.Numerics;

namespace ColdSnap
{
    /// <summary>Sliding and airborne puck physics. Stick touches change velocity, never position.</summary>
    public static class PuckPhysics
    {
        public const float StickReach = 1.75f, GuideAcceleration = 105;
        public const float PassSpeed = 16.5f, MaxShotSpeed = 36;
        public const float GuideHeight = .28f, StrikeHeight = .55f;

        public static bool CanReach(SkaterState s, PuckState puck)
        { return Reach(s, puck, GuideHeight); }

        public static bool CanStrike(SkaterState s, PuckState puck)
        { return Reach(s, puck, StrikeHeight); }

        private static bool Reach(SkaterState s, PuckState puck, float heightLimit)
        {
            Vector2 d = puck.Position - s.Position;
            float d2 = d.LengthSquared();
            return !s.Goalie && s.Stun <= 0 && !puck.OutOfPlay && puck.Height <= heightLimit && d2 < StickReach * StickReach &&
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
            if (s.StickCooldown > 0 || !CanStrike(s, puck)) return false;
            Vector2 aim = HockeyMath.Unit(direction, s.Forward);
            float speed = isPass ? PassSpeed * (.75f + .25f * HockeyMath.Clamp(power, 0, 1)) : 19 + 17 * HockeyMath.Clamp(power, 0, 1);
            // The stick supplies a finite impulse; inherited skater momentum remains visible.
            Vector2 desired = aim * speed + s.Velocity * .36f;
            puck.Velocity += HockeyMath.Limited(desired - puck.Velocity, isPass ? 24 : 45);
            puck.VerticalVelocity = puck.Height > RinkPhysics.PuckHalfHeight + .01f ? -2 : 0;
            RegisterStrike(s, puck, isPass, HockeyShotKind.Snap);
            return true;
        }

        public static bool Strike(SkaterState s, PuckState puck, HockeyInput input)
        {
            if (input.Pass || !input.HasShotTarget)
                return Strike(s, puck, input.Aim, input.ShotPower, input.Pass);
            if (s.StickCooldown > 0 || !CanStrike(s, puck)) return false;
            Vector2 velocity; float vertical;
            HockeyShots.Launch(s, puck, input, out velocity, out vertical);
            puck.Velocity += HockeyMath.Limited(velocity - puck.Velocity, input.ShotKind == HockeyShotKind.OneTimer ? 80 : 65);
            puck.VerticalVelocity += HockeyMath.Clamp(vertical - puck.VerticalVelocity, -24, 24);
            RegisterStrike(s, puck, false, input.ShotKind);
            s.StickCooldown = input.ShotKind == HockeyShotKind.Slap ? .62f : input.ShotKind == HockeyShotKind.Wrist ? .38f : .28f;
            return true;
        }

        private static void RegisterStrike(SkaterState s, PuckState puck, bool isPass, HockeyShotKind kind)
        {
            puck.LastTouchId = s.Id; puck.LastTouchTeam = s.Team;
            puck.ControllerId = -1; puck.ControlTime = 0;
            puck.NoControlTime = isPass ? .10f : .13f;
            puck.LastStrikeWasPass = isPass; puck.LastPasserId = isPass ? s.Id : -1;
            puck.LastPassTeam = isPass ? s.Team : -1; puck.PassAge = 0;
            puck.LastShotKind = kind; puck.ShotSequence = unchecked(puck.ShotSequence + 1);
            s.StickCooldown = isPass ? .27f : .42f;
            s.Cooldown = Math.Max(s.Cooldown, isPass ? .22f : .36f);
        }

        /// <returns>Scoring team (0 toward +Y, 1 toward -Y), or -1.</returns>
        public static int Step(PuckState puck, IList<SkaterState> skaters, float dt)
        {
            if (dt <= 0 || !HockeyMath.Finite(dt) || puck.OutOfPlay) return -1;
            dt = Math.Min(dt, .05f);
            puck.NoControlTime = Math.Max(0, puck.NoControlTime - dt);
            puck.PassAge += dt;
            // A 9cm maximum step protects the 15cm posts even on hard point shots.
            float spatialSpeed = (float)Math.Sqrt(puck.Velocity.LengthSquared() + puck.VerticalVelocity * puck.VerticalVelocity);
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(dt * 180, spatialSpeed * dt / .07f)));
            float h = dt / steps;
            for (int step = 0; step < steps; step++)
            {
                Vector2 previous = puck.Position;
                float previousHeight = Math.Max(RinkPhysics.PuckHalfHeight, puck.Height);
                puck.Height = previousHeight;
                bool grounded = puck.Height <= RinkPhysics.PuckHalfHeight + .0001f && puck.VerticalVelocity <= 0;
                float speed = puck.Velocity.Length();
                if (speed > 0)
                {
                    float nextSpeed = grounded ? Math.Max(0, speed - (.20f + speed * .012f) * h) : speed * (1 - .002f * h);
                    puck.Velocity *= nextSpeed / speed;
                }
                puck.Position += puck.Velocity * h;
                if (!grounded)
                {
                    puck.Height += puck.VerticalVelocity * h - .5f * HockeyShots.Gravity * h * h;
                    puck.VerticalVelocity -= HockeyShots.Gravity * h;
                }
                else puck.VerticalVelocity = 0;
                if (puck.Height < RinkPhysics.PuckHalfHeight)
                {
                    puck.Height = RinkPhysics.PuckHalfHeight;
                    if (puck.VerticalVelocity < -1.2f)
                    {
                        puck.VerticalVelocity = -puck.VerticalVelocity * .24f;
                        puck.Velocity *= .90f;
                    }
                    else puck.VerticalVelocity = 0;
                }
                ResolveBoards(puck);
                if (puck.OutOfPlay) return -1;
                ResolveRaisedGoal(puck, previous, previousHeight);
                ResolveNets(puck);
                for (int i = 0; i < skaters.Count; i++)
                {
                    Vector2 beforeContact = puck.Position;
                    ResolveBody(puck, skaters[i]);
                    ResolveNetContactMotion(puck, beforeContact);
                }
                // Pad/skate separation can push an already constrained puck back through
                // a board. Static rink containment must be the final contact constraint.
                ResolveBoards(puck);
                ResolveRaisedGoal(puck, previous, previousHeight);
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
                        float height = previousHeight + (puck.Height - previousHeight) * t;
                        if (Math.Abs(x) < RinkPhysics.GoalHalfWidth - RinkPhysics.PostRadius - RinkPhysics.PuckRadius &&
                            height + RinkPhysics.PuckHalfHeight <= RinkPhysics.GoalHeight - RinkPhysics.PostRadius)
                            return team;
                    }
                }
            }
            if (!HockeyMath.Finite(puck.Position) || !HockeyMath.Finite(puck.Velocity) || !HockeyMath.Finite(puck.Height) || !HockeyMath.Finite(puck.VerticalVelocity))
                throw new InvalidOperationException("Non-finite puck state");
            return -1;
        }

        private static void ResolveNets(PuckState p)
        {
            if (p.Height - RinkPhysics.PuckHalfHeight > RinkPhysics.GoalHeight) return;
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

        private static void ResolveBoards(PuckState puck)
        {
            if (puck.Height - RinkPhysics.PuckHalfHeight <= RinkPhysics.GlassHeight)
                RinkPhysics.Constrain(ref puck.Position, ref puck.Velocity, RinkPhysics.PuckRadius,
                    puck.Height - RinkPhysics.PuckHalfHeight <= RinkPhysics.BoardHeight ? .81f : .62f);
            else if (!RinkPhysics.Inside(puck.Position, RinkPhysics.PuckRadius)) puck.OutOfPlay = true;
        }

        private static void ResolveRaisedGoal(PuckState p, Vector2 previous, float previousHeight)
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                float line = RinkPhysics.GoalLine * sign;
                // Crossbar is a real horizontal cylinder. The puck's broad radius and
                // thin vertical profile form an elliptical contact region around it.
                if (Math.Abs(p.Position.X) <= RinkPhysics.GoalHalfWidth)
                {
                    float longitudinalRadius = RinkPhysics.PuckRadius + RinkPhysics.PostRadius;
                    float verticalRadius = RinkPhysics.PuckHalfHeight + RinkPhysics.PostRadius;
                    Vector2 q = new Vector2((p.Position.Y - line) / longitudinalRadius, (p.Height - RinkPhysics.GoalHeight) / verticalRadius);
                    if (q.LengthSquared() < 1)
                    {
                        Vector2 direction = HockeyMath.Unit(q, new Vector2(previous.Y < line ? -1 : 1, 0));
                        p.Position.Y = line + direction.X * longitudinalRadius;
                        p.Height = RinkPhysics.GoalHeight + direction.Y * verticalRadius;
                        Vector2 normal = HockeyMath.Unit(new Vector2(direction.X / longitudinalRadius, direction.Y / verticalRadius), Vector2.UnitY);
                        Vector2 velocity = new Vector2(p.Velocity.Y, p.VerticalVelocity);
                        float into = Vector2.Dot(velocity, normal);
                        if (into < 0) velocity -= normal * into * 1.68f;
                        p.Velocity.Y = velocity.X; p.VerticalVelocity = velocity.Y;
                    }
                }
                float depth = p.Position.Y * sign - RinkPhysics.GoalLine;
                if (Math.Abs(p.Position.X) > RinkPhysics.GoalHalfWidth || depth < 0 || depth > RinkPhysics.GoalDepth) continue;
                float roofTop = RinkPhysics.GoalHeight + RinkPhysics.PuckHalfHeight;
                float roofBottom = RinkPhysics.GoalHeight - RinkPhysics.PuckHalfHeight;
                if (previousHeight >= roofTop && p.Height < roofTop && p.VerticalVelocity < 0)
                { p.Height = roofTop; p.VerticalVelocity *= -.22f; }
                else if (previousHeight <= roofBottom && p.Height > roofBottom && p.VerticalVelocity > 0)
                { p.Height = roofBottom; p.VerticalVelocity *= -.22f; }
            }
        }

        // Player separation is a correction that can be larger than the ordinary 9cm
        // integration step. Sweep that correction against solid netting and posts so
        // its endpoint cannot emerge on the opposite side of a thin obstacle.
        private static void ResolveNetContactMotion(PuckState puck, Vector2 start)
        {
            if (puck.Height - RinkPhysics.PuckHalfHeight > RinkPhysics.GoalHeight) return;
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
            if (s.Goalie) ResolveHand(puck, s);
            if (puck.Height - RinkPhysics.PuckHalfHeight > (s.Goalie ? 1.72f : 1.85f)) return;
            // Goalies have a horizontal pad capsule. Skaters have a small skate/body disc.
            Vector2 center = s.Position;
            float radius = s.Goalie ? (puck.Height <= .65f ? .37f : .27f) : puck.Height > 1.4f ? .21f : .31f;
            if (s.Goalie)
            {
                Vector2 right = new Vector2(s.Forward.Y, -s.Forward.X);
                float width = puck.Height <= .65f ? .52f : .14f;
                float t = HockeyMath.Clamp(Vector2.Dot(puck.Position - center, right), -width, width);
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
                if (s.Goalie)
                {
                    puck.Velocity *= .86f;
                    if (puck.Height > .3f) puck.VerticalVelocity = Math.Min(-.6f, puck.VerticalVelocity * .35f);
                }
                if (toward < -.6f)
                    RecordContact(puck, s);
            }
        }

        private static void ResolveHand(PuckState puck, SkaterState keeper)
        {
            Vector2 right = new Vector2(keeper.Forward.Y, -keeper.Forward.X);
            Vector2 hand = keeper.Position + right * HockeyMath.Clamp(keeper.GoalieHandReach, -.85f, .85f) + keeper.Forward * .17f;
            float handHeight = HockeyMath.Clamp(keeper.GoalieHandHeight, .6f, 1.9f);
            Vector2 horizontal = puck.Position - hand;
            float vertical = puck.Height - handHeight;
            const float horizontalRadius = .37f, verticalRadius = .32f;
            Vector3 q = new Vector3(horizontal.X / horizontalRadius, vertical / verticalRadius, horizontal.Y / horizontalRadius);
            if (q.LengthSquared() >= 1) return;
            Vector3 direction = q.LengthSquared() > .000001f ? Vector3.Normalize(q) : new Vector3(keeper.Forward.X, 0, keeper.Forward.Y);
            puck.Position = hand + new Vector2(direction.X, direction.Z) * horizontalRadius;
            puck.Height = Math.Max(RinkPhysics.PuckHalfHeight, handHeight + direction.Y * verticalRadius);
            Vector3 normal = Vector3.Normalize(new Vector3(direction.X / horizontalRadius, direction.Y / verticalRadius, direction.Z / horizontalRadius));
            Vector3 velocity = new Vector3(puck.Velocity.X - keeper.Velocity.X, puck.VerticalVelocity, puck.Velocity.Y - keeper.Velocity.Y);
            float into = Vector3.Dot(velocity, normal);
            if (into >= 0) return;
            velocity -= normal * into * 1.28f;
            velocity *= .8f;
            puck.Velocity = new Vector2(velocity.X + keeper.Velocity.X, velocity.Z + keeper.Velocity.Y);
            puck.VerticalVelocity = Math.Min(velocity.Y, -.45f);
            if (into < -.6f) RecordContact(puck, keeper);
        }

        private static void RecordContact(PuckState puck, SkaterState s)
        {
            puck.LastCollisionId = s.Id; puck.CollisionSerial++;
            puck.LastTouchId = s.Id; puck.LastTouchTeam = s.Team;
            puck.ControllerId = -1; puck.NoControlTime = s.Goalie ? .23f : .07f;
        }
    }
}
