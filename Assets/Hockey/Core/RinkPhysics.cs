using System;
using System.Collections.Generic;
using System.Numerics;

namespace ColdSnap
{
    /// <summary>Analytic rounded boards and goal frames shared by simulation and geometry.</summary>
    public static class RinkPhysics
    {
        public const float HalfWidth = 14, HalfLength = 29, CornerRadius = 6;
        public const float GoalLine = 25, GoalHalfWidth = 1.8f, GoalDepth = 1.8f;
        public const float SkaterRadius = .4f, PuckRadius = .125f, PostRadius = .075f;

        public static bool Inside(Vector2 p, float margin = 0)
        {
            float x = Math.Abs(p.X), y = Math.Abs(p.Y);
            if (x > HalfWidth - margin || y > HalfLength - margin) return false;
            Vector2 q = new Vector2(Math.Max(0, x - (HalfWidth - CornerRadius)), Math.Max(0, y - (HalfLength - CornerRadius)));
            return q.LengthSquared() <= (CornerRadius - margin) * (CornerRadius - margin);
        }

        public static void Constrain(ref Vector2 p, ref Vector2 velocity, float radius, float bounce)
        {
            float sx = p.X < 0 ? -1 : 1, sy = p.Y < 0 ? -1 : 1;
            float x = Math.Abs(p.X), y = Math.Abs(p.Y);
            Vector2 n = Vector2.Zero;
            Vector2 q = new Vector2(Math.Max(0, x - (HalfWidth - CornerRadius)), Math.Max(0, y - (HalfLength - CornerRadius)));
            float allowed = CornerRadius - radius;
            if (x > HalfWidth - CornerRadius && y > HalfLength - CornerRadius && q.LengthSquared() > allowed * allowed)
            {
                n = HockeyMath.Unit(new Vector2(q.X * sx, q.Y * sy), Vector2.UnitX);
                p = new Vector2((HalfWidth - CornerRadius) * sx, (HalfLength - CornerRadius) * sy) + n * allowed;
            }
            else if (x > HalfWidth - radius)
            {
                n = new Vector2(sx, 0); p.X = sx * (HalfWidth - radius);
            }
            else if (y > HalfLength - radius)
            {
                n = new Vector2(0, sy); p.Y = sy * (HalfLength - radius);
            }
            if (n != Vector2.Zero)
            {
                float outSpeed = Vector2.Dot(velocity, n);
                if (outSpeed > 0) velocity -= n * outSpeed * (1 + bounce);
            }
        }

        public static void ResolveSkater(SkaterState s)
        {
            Constrain(ref s.Position, ref s.Velocity, SkaterRadius, .08f);
            // Side and rear netting are solid, but the front is open.
            for (int side = -1; side <= 1; side += 2)
            {
                float y = GoalLine * side, back = (GoalLine + GoalDepth) * side;
                Segment(ref s.Position, ref s.Velocity, new Vector2(-GoalHalfWidth, y), new Vector2(-GoalHalfWidth, back), SkaterRadius + .06f, .08f);
                Segment(ref s.Position, ref s.Velocity, new Vector2(GoalHalfWidth, y), new Vector2(GoalHalfWidth, back), SkaterRadius + .06f, .08f);
                Segment(ref s.Position, ref s.Velocity, new Vector2(-GoalHalfWidth, back), new Vector2(GoalHalfWidth, back), SkaterRadius + .06f, .08f);
            }
        }

        public static void ResolvePlayers(IList<SkaterState> skaters)
        {
            for (int iteration = 0; iteration < 2; iteration++)
            for (int i = 0; i < skaters.Count; i++)
            for (int j = i + 1; j < skaters.Count; j++)
            {
                SkaterState a = skaters[i], b = skaters[j];
                Vector2 delta = b.Position - a.Position;
                float radius = (a.Goalie || b.Goalie) ? .94f : SkaterRadius * 2;
                float d2 = delta.LengthSquared();
                if (d2 >= radius * radius) continue;
                float d = (float)Math.Sqrt(d2);
                Vector2 n = d > .0001f ? delta / d : new Vector2(i % 2 == 0 ? 1 : -1, 0);
                float overlap = radius - d;
                // Contact position correction is separate from velocity, never attraction.
                a.Position -= n * overlap * .5f; b.Position += n * overlap * .5f;
                float approach = Vector2.Dot(b.Velocity - a.Velocity, n);
                if (approach < 0)
                {
                    Vector2 impulse = n * approach * .54f;
                    a.Velocity += impulse; b.Velocity -= impulse;
                }
            }
            for (int i = 0; i < skaters.Count; i++) ResolveSkater(skaters[i]);
        }

        internal static bool Segment(ref Vector2 p, ref Vector2 velocity, Vector2 a, Vector2 b, float radius, float bounce)
        {
            Vector2 ab = b - a;
            float t = HockeyMath.Clamp(Vector2.Dot(p - a, ab) / Math.Max(.00001f, ab.LengthSquared()), 0, 1);
            Vector2 closest = a + ab * t, delta = p - closest;
            if (delta.LengthSquared() >= radius * radius) return false;
            Vector2 n = HockeyMath.Unit(delta, HockeyMath.Unit(-velocity, Vector2.UnitY));
            p = closest + n * radius;
            float into = Vector2.Dot(velocity, n);
            if (into < 0) velocity -= n * into * (1 + bounce);
            return true;
        }
    }
}
