using System;

namespace ColdSnap
{
    /// <summary>Button gestures independent of frame rate and Unity input devices.</summary>
    public sealed class ShotGesture
    {
        public const float TapSeconds = .18f;
        public bool Charging { get; private set; }
        public bool Wrist { get; private set; }
        public float HeldSeconds { get; private set; }
        public float Charge => Math.Min(1f, HeldSeconds / (Wrist ? .72f : 1.05f));
        public bool OneTimerHeld => Charging && !consumed;
        bool lastLeft, lastRight, rightConsumed, consumed;

        public HockeyInput Update(bool left, bool right, float dt)
        {
            var result = new HockeyInput();
            if (right && !lastRight) rightConsumed = left || lastLeft;
            if (left && !lastLeft)
            {
                Charging = true; HeldSeconds = 0; consumed = false;
                // Ordering matters: pressing both together is not a wrist-shot chord.
                Wrist = right && lastRight;
                if (right) rightConsumed = true;
            }
            if (Charging && left) HeldSeconds += Math.Max(0, dt);
            if (!left && lastLeft && Charging)
            {
                result.Shoot = !consumed;
                result.ShotKind = Wrist ? HockeyShotKind.Wrist : HeldSeconds <= TapSeconds ? HockeyShotKind.Snap : HockeyShotKind.Slap;
                result.ShotPower = result.ShotKind == HockeyShotKind.Snap ? 1f : .35f + .65f * Charge;
                Charging = false; HeldSeconds = 0;
            }
            if (!right && lastRight)
            {
                result.Pass = !rightConsumed && !left && !lastLeft;
                rightConsumed = false;
            }
            result.OneTimerHeld = OneTimerHeld;
            lastLeft = left; lastRight = right;
            return result;
        }

        public void ConsumeOneTimer() { consumed = true; }
        public void Reset()
        {
            Charging = Wrist = lastLeft = lastRight = rightConsumed = consumed = false;
            HeldSeconds = 0;
        }
    }
}
