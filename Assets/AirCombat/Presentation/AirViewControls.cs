using UnityEngine;

namespace Meridian
{
    /// <summary>Bounded head movement only; never writes aircraft attitude or flight controls.</summary>
    public sealed class AirViewControls
    {
        public const float MaxYaw = 165f, MinPitch = -80f, MaxPitch = 70f;
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public bool Returning { get; private set; }
        bool previousFreeLook, previousRearView;
        float rearSide = 1f;

        public void Reset()
        {
            Yaw = Pitch = 0;
            Returning = previousFreeLook = previousRearView = false;
            rearSide = 1f;
        }

        public void Step(AirControlIntent intent, float dt, bool active = true)
        {
            if (!active) { Reset(); return; }
            if (float.IsNaN(dt) || float.IsInfinity(dt)) return;
            dt = Mathf.Clamp(dt, 0, .1f);
            if (intent.CenterLook && !intent.RearViewHeld)
            {
                // Preserve the established V / middle mouse / stick-click command.
                // Only release-to-forward gestures use the eased transition.
                Yaw = Pitch = 0;
                Returning = false;
                previousFreeLook = intent.FreeLookHeld;
                previousRearView = false;
                return;
            }
            bool released = intent.LookReleased || (previousFreeLook && !intent.FreeLookHeld)
                || (previousRearView && !intent.RearViewHeld);
            if (intent.RearViewHeld)
            {
                if (!previousRearView) rearSide = Yaw < -1f ? -1f : 1f;
                Returning = false;
                // Prefer the shoulder already being looked over. Stop short of an
                // impossible 180-degree head rotation while exposing the rear hemisphere.
                Yaw = Mathf.MoveTowards(Yaw, rearSide * MaxYaw, 1100f * dt);
                Pitch = Mathf.MoveTowards(Pitch, -5f, 650f * dt);
            }
            else
            {
                if (released) Returning = true;
                if (Returning)
                {
                    // Exponential decay is frame-rate independent. From full rear view
                    // this reaches less than one degree in a quarter-second.
                    float remainder = Mathf.Exp(-22f * dt);
                    Yaw *= remainder; Pitch *= remainder;
                    if (Mathf.Abs(Yaw) < .1f && Mathf.Abs(Pitch) < .1f)
                    { Yaw = Pitch = 0; Returning = false; }
                }
                else
                {
                    Vector2 delta = intent.LookRate * dt;
                    if (intent.FreeLookHeld) delta += intent.LookDelta;
                    Yaw = Mathf.Clamp(Yaw + delta.x, -MaxYaw, MaxYaw);
                    Pitch = Mathf.Clamp(Pitch + delta.y, MinPitch, MaxPitch);
                }
            }
            previousFreeLook = intent.FreeLookHeld;
            previousRearView = intent.RearViewHeld;
        }
    }
}
