using System;
using System.Numerics;

namespace ColdSnap
{
    /// <summary>World-space acceleration intent and stick/look aim. Action flags are pulses.</summary>
    public struct HockeyInput
    {
        public Vector2 Move, Aim;
        public bool Brake, Sprint, Shoot, Pass, Check, Call;
        public float ShotPower;
    }

    public sealed class SkaterState
    {
        public int Id, Team;
        public bool Goalie;
        public Vector2 Position, Velocity;
        // Zero faces +Y; positive yaw faces +X. Rendering maps XY to Unity XZ.
        public float Facing, Cooldown, Stun, StickCooldown, CheckCooldown;
        public string Role = "", Intent = "";
        public Vector2 Forward { get { return new Vector2((float)Math.Sin(Facing), (float)Math.Cos(Facing)); } }
        public float Speed { get { return Velocity.Length(); } }
    }

    public sealed class PuckState
    {
        public Vector2 Position, Velocity;
        public int LastTouchTeam = -1, LastTouchId = -1, ControllerId = -1;
        public int LastCollisionId = -1, CollisionSerial;
        public float NoControlTime, ControlTime;
    }

    public static class HockeyMath
    {
        public static float Clamp(float x, float min, float max) { return Math.Max(min, Math.Min(max, x)); }
        public static Vector2 Unit(Vector2 v, Vector2 fallback)
        {
            float l = v.LengthSquared();
            return l > .00001f && Finite(l) ? v / (float)Math.Sqrt(l) : fallback;
        }
        public static Vector2 Limited(Vector2 v, float max)
        {
            float l = v.LengthSquared();
            return l > max * max ? Unit(v, Vector2.Zero) * max : v;
        }
        public static float Wrap(float a)
        {
            while (a > Math.PI) a -= (float)Math.PI * 2;
            while (a < -Math.PI) a += (float)Math.PI * 2;
            return a;
        }
        public static float Angle(Vector2 d) { return (float)Math.Atan2(d.X, d.Y); }
        public static bool Finite(float x) { return !float.IsNaN(x) && !float.IsInfinity(x); }
        public static bool Finite(Vector2 v) { return Finite(v.X) && Finite(v.Y); }
    }
}
