using System;
using System.Numerics;

namespace ColdSnap
{
    public static class Skating
    {
        public const float CruiseSpeed = 8.3f, SprintSpeed = 11.2f;

        public static void Step(SkaterState s, HockeyInput input, float dt)
        {
            if (dt <= 0) return;
            dt = Math.Min(dt, .05f);
            s.Cooldown = Math.Max(0, s.Cooldown - dt);
            s.StickCooldown = Math.Max(0, s.StickCooldown - dt);
            s.CheckCooldown = Math.Max(0, s.CheckCooldown - dt);
            s.Stun = Math.Max(0, s.Stun - dt);
            Vector2 aim = HockeyMath.Unit(input.Aim, s.Forward);
            float speed = s.Speed;
            float turnRate = (s.Goalie ? 5.5f : 3.8f) / (1 + speed * .055f);
            s.Facing += HockeyMath.Clamp(HockeyMath.Wrap(HockeyMath.Angle(aim) - s.Facing), -turnRate * dt, turnRate * dt);
            s.Facing = HockeyMath.Wrap(s.Facing);
            Vector2 move = s.Stun > 0 ? Vector2.Zero : HockeyMath.Limited(input.Move, 1);
            float intent = move.Length();
            float maxSpeed = s.Goalie ? 5 : input.Sprint ? SprintSpeed : CruiseSpeed;
            float forward = intent > .001f ? Vector2.Dot(move / intent, s.Forward) : 1;
            if (!s.Goalie) maxSpeed *= forward < -.2f ? .7f : .87f + .13f * Math.Max(0, forward);
            float alongMove = intent > .001f ? Vector2.Dot(s.Velocity, move / intent) : 0;
            float drive = Math.Max(0, 1 - Math.Max(0, alongMove) * Math.Max(0, alongMove) / (maxSpeed * maxSpeed));
            Vector2 acceleration = move * ((s.Goalie ? 14 : 8.8f) * drive);
            if (speed > .001f)
            {
                // Coasting loses very little speed. Edges bite while actively steering.
                acceleration -= s.Velocity / speed * (.065f + speed * .015f);
                if (intent > .01f)
                {
                    Vector2 axis = move / intent;
                    Vector2 lateral = s.Velocity - axis * Vector2.Dot(s.Velocity, axis);
                    acceleration -= HockeyMath.Limited(lateral * (s.Goalie ? 2.3f : 1.25f), s.Goalie ? 12 : 5.6f) * intent;
                }
                if (input.Brake) acceleration -= s.Velocity / speed * Math.Min(speed / dt, 5.6f + speed * .8f);
            }
            Vector2 next = s.Velocity + acceleration * dt;
            if (input.Brake && intent < .01f && Vector2.Dot(next, s.Velocity) < 0) next = Vector2.Zero;
            s.Velocity = HockeyMath.Limited(next, 18);
            if (s.Velocity.LengthSquared() < .0004f && intent < .01f) s.Velocity = Vector2.Zero;
            s.Position += s.Velocity * dt;
            RinkPhysics.ResolveSkater(s);
            if (!HockeyMath.Finite(s.Position) || !HockeyMath.Finite(s.Velocity))
                throw new InvalidOperationException("Non-finite skater state for " + s.Id);
        }
    }
}
