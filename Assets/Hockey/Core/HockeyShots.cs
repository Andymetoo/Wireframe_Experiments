using System;
using System.Numerics;

namespace ColdSnap
{
    /// <summary>Deterministic shot profiles and a gravity-compensated, imperfect aim solution.</summary>
    public static class HockeyShots
    {
        public const float Gravity = 9.81f;

        public static float Speed(HockeyShotKind kind, float power)
        {
            power = HockeyMath.Clamp(power, 0, 1);
            switch (kind)
            {
                case HockeyShotKind.Slap: return 27 + power * 9;
                case HockeyShotKind.Wrist: return 22 + power * 6;
                case HockeyShotKind.OneTimer: return 29 + power * 7;
                default: return 24 + power * 2;
            }
        }

        public static float Spread(HockeyShotKind kind)
        {
            switch (kind)
            {
                case HockeyShotKind.Wrist: return .0055f;
                case HockeyShotKind.Slap: return .021f;
                case HockeyShotKind.OneTimer: return .027f;
                default: return .012f;
            }
        }

        public static void Launch(SkaterState skater, PuckState puck, HockeyInput input, out Vector2 horizontal, out float vertical)
        {
            Vector2 toTarget = input.ShotTarget - puck.Position;
            float distance = Math.Max(.4f, toTarget.Length());
            Vector2 direction = HockeyMath.Unit(toTarget, HockeyMath.Unit(input.Aim, skater.Forward));
            float spread = (Spread(input.ShotKind) + skater.Speed * .0005f) * (1 + Math.Min(1.5f, distance / 40) * .45f);
            uint random = unchecked((uint)puck.ShotSequence * 747796405u + (uint)(skater.Id + 1) * 2891336453u + 277803737u);
            float lateralError = Noise(ref random) * spread;
            float heightError = Noise(ref random) * spread * distance * .4f;
            float sin = (float)Math.Sin(lateralError), cos = (float)Math.Cos(lateralError);
            Vector2 imperfect = new Vector2(direction.X * cos + direction.Y * sin, direction.Y * cos - direction.X * sin);
            horizontal = imperfect * Speed(input.ShotKind, input.ShotPower) + skater.Velocity * .18f;
            float time = distance / Math.Max(8, Vector2.Dot(horizontal, direction));
            float targetHeight = HockeyMath.Clamp(input.TargetHeight + heightError, RinkPhysics.PuckHalfHeight, 8);
            vertical = (targetHeight - puck.Height + .5f * Gravity * time * time) / time;
            // A close, impossible high target stays an imperfect physical shot; it is
            // never snapped into the net or given unbounded vertical energy.
            vertical = HockeyMath.Clamp(vertical, -5, input.ShotKind == HockeyShotKind.Snap ? 13 : 17);
        }

        private static float Noise(ref uint state)
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (state & 0x00ffffffu) / 8388607.5f - 1;
        }
    }
}
