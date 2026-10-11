using System;

namespace ColdSnap
{
    public static class ShotGestureValidation
    {
        public static void Run(Action<bool, string> check)
        {
            var gesture = new ShotGesture();
            check(!gesture.Update(true, false, .016f).Shoot, "click waits only for release, not an invented wind-up");
            var tap = gesture.Update(false, false, .016f);
            check(tap.Shoot && tap.ShotKind == HockeyShotKind.Snap && tap.ShotPower == 1, "short click immediately emits one full snap on release");
            check(!gesture.Update(false, false, .5f).Shoot, "released snap cannot fire twice");
            gesture.Update(true, false, .6f);
            check(gesture.Charging && gesture.OneTimerHeld, "held LMB charges and enables contact one-timers");
            var slap = gesture.Update(false, false, .01f);
            check(slap.Shoot && slap.ShotKind == HockeyShotKind.Slap && slap.ShotPower > .65f, "long LMB release emits charged slapshot");
            check(!gesture.Update(false, true, .1f).Pass, "RMB press defers pass so a wrist chord can be selected");
            check(gesture.Update(false, false, .1f).Pass, "RMB release sends a normal pass");
            gesture.Update(false, true, .1f);
            gesture.Update(true, true, .6f);
            check(gesture.Wrist, "RMB before LMB selects wrist shot");
            check(!gesture.Update(true, false, .1f).Pass, "releasing chord RMB early does not accidentally pass");
            var wrist = gesture.Update(false, false, .01f);
            check(wrist.Shoot && wrist.ShotKind == HockeyShotKind.Wrist, "wrist choice survives release of modifier");
            gesture.Update(true, false, .4f);
            gesture.Update(true, true, .3f);
            var reverse = gesture.Update(false, false, .01f);
            check(reverse.ShotKind == HockeyShotKind.Slap && !reverse.Pass, "LMB before RMB stays slapshot without stray pass");
            gesture.Update(true, false, .3f); gesture.ConsumeOneTimer();
            check(!gesture.OneTimerHeld && !gesture.Update(false, false, .01f).Shoot, "successful automatic one-timer consumes held shot and release");
            gesture.Update(false, true, .1f); gesture.Update(true, true, .3f); gesture.Reset();
            var reset = gesture.Update(false, false, .1f);
            check(!reset.Shoot && !reset.Pass && !gesture.Charging, "pause or restart cancels all pending button gestures");
        }
    }
}
