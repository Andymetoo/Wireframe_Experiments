using System;
using System.Collections;
using Meridian;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using NVector3 = System.Numerics.Vector3;

namespace VectorCabinet
{
    public sealed partial class CabinetSmoke
    {
        bool DescriptionsFit(AirCombatGame air)
        {
            bool fits=air.DescriptionsMeasured>0&&air.DescriptionOverflowCount==0;
            if(!fits)Note("DESCRIPTION LAYOUT: measured="+air.DescriptionsMeasured+" overflow="+air.DescriptionOverflowCount+" text="+air.DescriptionOverflowText);
            return fits;
        }
        static void FinishDiagnosticAftermath(AirCombatGame air)
        {
            if (air.Mission.Outcome == MissionOutcome.Flying) return;
            air.AdvanceAftermathForDiagnostics(0); air.SkipAftermath(); air.View.Sync(air.Mission, 0);
        }

        IEnumerator ExercisePolish(AirCombatGame air)
        {
            if (air == null) throw new InvalidOperationException("Polish smoke requires the existing flight scene");
            air.DiagnosticControl = true;
            yield return AirInputValidation.Run(Check);
            previousKeyboard = Keyboard.current; previousMouse = Mouse.current;
            testKeyboard = InputSystem.AddDevice<Keyboard>("Polish diagnostic keyboard");
            testMouse = InputSystem.AddDevice<Mouse>("Polish diagnostic mouse");
            testKeyboard.MakeCurrent(); testMouse.MakeCurrent();
            try
            {
                foreach (MissionKind kind in Enum.GetValues(typeof(MissionKind)))
                {
                    air.StartSortie(81, kind);
                    Check(air.ShowLead, "Polish: lead guidance defaults on for " + kind + " without aiming or firing the guns");
                }
                air.StartRogueRun(81); air.LaunchRogueRoute(0);
                Check(air.ShowLead, "Polish: roguelite launch also defaults lead guidance on");
                yield return SquadronKey(Key.G);
                Check(!air.ShowLead, "Polish: G still lets the pilot disable lead guidance");
                yield return SquadronKey(Key.G);
                Check(air.ShowLead, "Polish: G restores optional lead guidance");
                yield return ExercisePolishView(air, true);
                air.StartSortie(81, MissionKind.Duel);
                yield return ExercisePolishView(air, false);
                yield return ExercisePolishAbilities(air);
                yield return ExercisePolishHitsAndOutcome(air);
                yield return ExercisePolishNavigation(air);
                yield return ExerciseReadableFlightLabels(air);
                air.ShowStandardMissions();
                yield return Capture("61-polish-readable-standard-briefing");
                Check(DescriptionsFit(air), "Polish: enlarged standard briefing and separate keyboard/controller descriptions fit their layout bounds");
                CheckScene(GameCollection.AirScene);
            }
            finally { CleanupKeyboard(); CleanupMouse(); air.DiagnosticControl = true; }
        }

        IEnumerator ExercisePolishView(AirCombatGame air, bool rogue)
        {
            air.DiagnosticControl = true; air.View.SetCameraMode(0);
            var attitude = air.Mission.Player.Rotation; var position = air.Mission.Player.Position;
            int rounds = air.Mission.RoundsFired;
            InputSystem.QueueStateEvent(testMouse, new MouseState().WithButton(MouseButton.Right).WithButton(MouseButton.Left));
            InputSystem.QueueDeltaStateEvent(testMouse.delta, new Vector2(2100, 1400));
            yield return null; yield return null;
            Check(air.ViewYaw > 150 && air.ViewYaw <= 165.01f && air.ViewPitch >= -80.01f && air.ViewPitch < -65,
                "Polish " + (rogue ? "roguelite" : "standard") + ": RMB reaches the rear hemisphere with bounded human head movement");
            Check(air.Mission.Player.Rotation == attitude && air.Mission.Player.Position == position && air.Mission.RoundsFired == rounds,
                "Polish: diagnostic free-look changes camera orientation without changing authoritative aircraft attitude or firing");
            if (!rogue) yield return Capture("50-polish-cockpit-rear-look");
            InputSystem.QueueStateEvent(testMouse, new MouseState());
            yield return new WaitForSecondsRealtime(.35f);
            Check(Mathf.Abs(air.ViewYaw) < 1 && Mathf.Abs(air.ViewPitch) < 1,
                "Polish: releasing RMB automatically recenters within a comfortable fraction of a second");
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.X));
            yield return new WaitForSecondsRealtime(.25f);
            Check(Mathf.Abs(air.ViewYaw) > 155 && Mathf.Abs(air.ViewYaw) <= 165.01f,
                "Polish: held X provides a quick six-o'clock view without changing aircraft heading");
            InputSystem.QueueStateEvent(testMouse, new MouseState().WithButton(MouseButton.Right));
            InputSystem.QueueDeltaStateEvent(testMouse.delta, new Vector2(-1000, -1000));
            yield return null; yield return null;
            Check(Mathf.Abs(air.ViewYaw) > 155 && Mathf.Abs(air.ViewPitch + 5) < 1,
                "Polish: rear-view hold takes precedence over simultaneous free-look deltas");
            InputSystem.QueueStateEvent(testMouse, new MouseState());
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(.35f);
            Check(Mathf.Abs(air.ViewYaw) < 1 && Mathf.Abs(air.ViewPitch) < 1 && air.Mission.Player.Rotation == attitude,
                "Polish: releasing rear view returns forward without rotating the aircraft");
            InputSystem.QueueStateEvent(testMouse, new MouseState().WithButton(MouseButton.Right));
            InputSystem.QueueDeltaStateEvent(testMouse.delta, new Vector2(900, 250));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.V));
            yield return null; yield return null;
            Check(air.ViewYaw == 0 && air.ViewPitch == 0,
                "Polish: V preserves immediate center while release gestures use the eased return");
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            InputSystem.QueueStateEvent(testMouse, new MouseState());
            yield return null; yield return null;
        }

        IEnumerator ExercisePolishAbilities(AirCombatGame air)
        {
            air.StartRogueRun(81); air.LaunchRogueRoute(0);
            RogueValidation.CompleteFixture(air.Mission); air.Campaign.FinishSortie();
            FinishDiagnosticAftermath(air);
            Note("ABILITY UI FIXTURE: the first encounter is explicitly completed to acquire a second real equipment reward; this is not combat evidence.");
            yield return Capture("51-polish-readable-equipment-cards");
            Check(DescriptionsFit(air), "Polish: generated reward-card descriptions fit their enlarged-font layout");
            yield return AuditEquipmentDescriptions(air);
            int ability = -1;
            for (int i = 0; i < air.Campaign.Rewards.Count; i++)
                if (air.Campaign.Rewards[i].Kind == RogueRewardKind.Ability) { ability = i; break; }
            Check(ability >= 0 && air.ChooseRogueReward(ability), "Polish: an actual generated ability reward can fill the second tactical slot");
            yield return Capture("51a-polish-readable-planning");
            Check(DescriptionsFit(air), "Polish: hangar, route, workshop and ability descriptions fit their enlarged-font layout");
            air.LaunchRogueRoute(0);
            // Separate input timing from enemy combat: this explicit setup keeps opponents out of contact.
            foreach (var enemy in air.Mission.Aircraft)
                if (enemy.Team != air.Mission.Player.Team) enemy.Position += NVector3.UnitZ * 15000;
            int first = air.Campaign.Charges[0], second = air.Campaign.Charges[1];
            yield return SquadronKey(Key.T); yield return SquadronKey(Key.Y);
            Check(air.Campaign.Charges[0] == first - 1 && air.Campaign.ActiveSeconds[0] > 0,
                "Polish: keyboard T activates slot one through normal Update");
            Check(air.Campaign.Charges[1] == second - 1 && air.Campaign.ActiveSeconds[1] > 0,
                "Polish: keyboard Y activates the acquired second ability through normal Update");
            yield return Capture("52-polish-two-active-abilities");
            yield return SquadronKey(Key.Digit5); yield return SquadronKey(Key.Digit6);
            Check(air.Campaign.Charges[0] == first - 1 && air.Campaign.Charges[1] == second - 1 && !string.IsNullOrEmpty(air.Campaign.LastMessage),
                "Polish: repeated active ability shortcuts preserve charges and give concise unavailable feedback");
            for (int tick = 0; tick < 21 * 60 && air.Campaign.Phase == RoguePhase.Flying; tick++)
            {
                air.StepForDiagnostics(new FlightInput { Throttle = .78f }, 1f / 60f);
                if (tick % 60 == 0) yield return null;
            }
            yield return SquadronKey(Key.Digit5); yield return SquadronKey(Key.Digit6);
            Check(air.Campaign.Phase == RoguePhase.Flying && air.Campaign.Charges[0] == first - 2
                && air.Campaign.Charges[1] == second - 2 && air.Campaign.ActiveSeconds[0] > 0 && air.Campaign.ActiveSeconds[1] > 0,
                "Polish: legacy 5/6 shortcuts still activate the same slots after the first effects expire");
            air.SetPaused(true);
            yield return Capture("53-polish-keyboard-and-controller-help");
            air.StartSortie(81, MissionKind.Duel);
        }

        IEnumerator ExercisePolishHitsAndOutcome(AirCombatGame air)
        {
            air.StartSortie(81, MissionKind.Duel); air.DiagnosticControl = true; air.View.SetCameraMode(0);
            var target = air.Mission.Aircraft[1];
            target.Position = air.Mission.Player.Position + air.Mission.Player.Forward * 240;
            target.Velocity = air.Mission.Player.Velocity; target.Rotation = air.Mission.Player.Rotation;
            Note("HIT / AFTERMATH FIXTURE: an opponent is positioned in front of the camera and real projectile impacts exercise feedback. This is not sortie balance evidence.");
            yield return null;
            Check(!air.AircraftHitVisible && !air.AircraftKillVisible && air.Mission.PlayerAircraftHits == 0,
                "Polish: reticle overlap alone never lights the aircraft hit indicator");
            air.Mission.Projectiles.Add(PolishImpact(target, 13));
            air.StepForDiagnostics(new FlightInput { Throttle = .78f }, 1f / 60f);
            yield return null;
            Check(air.AircraftHitVisible && !air.AircraftKillVisible && air.Mission.PlayerAircraftHits == 1,
                "Polish: a genuine nonlethal aircraft impact lights the brief cockpit confirmation");
            yield return Capture("54-polish-confirmed-aircraft-hit");
            target.Health = 8;
            air.Mission.Projectiles.Add(PolishImpact(target, 13));
            air.StepForDiagnostics(new FlightInput { Throttle = .78f }, 1f / 60f);
            Check(air.Mission.Outcome == MissionOutcome.Victory && air.AftermathActive && air.AftermathRemaining > 2.9f,
                "Polish: final damage decides victory immediately but holds results menus for approximately three seconds");
            yield return null;
            Check(air.AircraftKillVisible && air.Mission.PlayerAircraftKills == 1,
                "Polish: confirmed destruction has a separate, stronger cockpit signal");
            float time = air.Mission.Time; var position = target.Position; var velocity = target.Velocity;
            int kills = air.Mission.Kills, score = air.Mission.Score, ammunition = air.Mission.Player.Ammo;
            air.SetPaused(true); float remaining = air.AftermathRemaining;
            yield return new WaitForSecondsRealtime(.2f);
            Check(air.AftermathRemaining == remaining && air.Mission.Time == time,
                "Polish: pausing the aftermath freezes its presentation clock and authoritative mission state");
            // UI-only fixture: open the private binding panel without changing gameplay state.
            var bindings = typeof(AirCombatGame).GetField("stickBindings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            bindings.SetValue(air, true);
            yield return SquadronKey(Key.Enter);
            Check(air.Paused && air.AftermathRemaining == remaining,
                "Polish: submit inside controller bindings cannot skip or resume the paused aftermath");
            bindings.SetValue(air, false);
            Gamepad previousPad = Gamepad.current;
            Gamepad testPad = InputSystem.AddDevice<Gamepad>("Aftermath back diagnostic gamepad");
            try
            {
                testPad.MakeCurrent();
                InputSystem.QueueStateEvent(testPad, new GamepadState().WithButton(GamepadButton.East));
                yield return null; yield return null;
                Check(!air.Paused && air.AftermathActive,
                    "Polish: gamepad B resumes the paused aftermath without abandoning the completed sortie");
                InputSystem.QueueStateEvent(testPad, new GamepadState());
                yield return null; yield return null;
            }
            finally
            {
                if (testPad.added) InputSystem.RemoveDevice(testPad);
                if (previousPad != null && previousPad.added) previousPad.MakeCurrent();
            }
            Vector3 wreckBefore; bool before = air.View.TryGetWreckPosition(target.Id, out wreckBefore);
            air.AdvanceAftermathForDiagnostics(1.1f); air.View.Sync(air.Mission, 0);
            Vector3 wreckAfter; bool after = air.View.TryGetWreckPosition(target.Id, out wreckAfter);
            Check(before && after && Vector3.Distance(wreckBefore, wreckAfter) > 40 && air.View.VisibleWreckCount > 0,
                "Polish: a destroyed aircraft visibly retains momentum and falls while the mission is frozen");
            for (int i = 0; i < 4; i++) air.View.Sync(air.Mission, .5f);
            Vector3 repeated; air.View.TryGetWreckPosition(target.Id, out repeated);
            Check(Vector3.Distance(repeated, wreckAfter) < .001f,
                "Polish: repeated render Sync calls cannot advance the absolute aftermath clock twice");
            Check(target.Position == position && target.Velocity == velocity && air.Mission.Time == time
                && air.Mission.Kills == kills && air.Mission.Score == score && air.Mission.Player.Ammo == ammunition,
                "Polish: wreck animation never edits authoritative aircraft, ammunition, score or finalized outcome");
            yield return Capture("55-polish-victory-wreck-aftermath");
            yield return SquadronKey(Key.Enter);
            Check(!air.AftermathActive && air.Mission.Outcome == MissionOutcome.Victory && air.Mission.Time == time,
                "Polish: Enter safely skips the remaining visual hold without simulating another combat event");
            yield return Capture("56-polish-victory-results");

            air.StartSortie(81, MissionKind.Duel);
            Check(!air.View.OutcomeActive && air.View.VisibleWreckCount == 0 && air.Mission.PlayerAircraftHits == 0,
                "Polish: restart clears previous wrecks, outcome clocks and hit counters");
            air.Mission.Player.Alive = false; air.Mission.Player.Health = 0;
            air.StepForDiagnostics(new FlightInput(), 1f / 60f);
            Check(air.AftermathActive && air.Mission.Outcome == MissionOutcome.Defeat,
                "Polish: player destruction also receives the outcome hold before a defeat screen");
            yield return new WaitForSecondsRealtime(.6f);
            yield return Capture("57-polish-player-wreck-camera");
            Check(air.AftermathActive, "Polish: the natural aftermath remains visible after its first second");
            yield return new WaitForSecondsRealtime(2.2f);
            Check(!air.AftermathActive && air.Mission.Outcome == MissionOutcome.Defeat,
                "Polish: the normal three-second presentation interval completes automatically");
            yield return Capture("58-polish-natural-defeat-results");
            air.StartSortie(81, MissionKind.Duel); air.Mission.Player.Alive = false; air.Mission.Player.Health = 0;
            air.StepForDiagnostics(new FlightInput(), 1f / 60f);
            yield return SquadronKey(Key.Escape); yield return SquadronKey(Key.DownArrow); yield return SquadronKey(Key.Enter);
            Check(!air.AftermathActive && !air.Paused && air.Mission.Outcome == MissionOutcome.Flying
                && air.Mission.Time == 0 && air.Mission.Player.Alive,
                "Polish: the paused aftermath can restart immediately into a clean playable sortie");
        }

        IEnumerator ExercisePolishNavigation(AirCombatGame air)
        {
            air.StartSortie(81, MissionKind.GroundAttack); air.DiagnosticControl = true;
            Check(air.ActiveGroundObjectiveMarkers == 4, "Polish: all four live mission-critical vehicles have individual navigation markers");
            yield return Capture("59-polish-ground-objectives-and-radar");
            var target = air.Mission.GroundTargets[0];
            air.Mission.IssueOrder(SquadronOrder.FormUp, target.Id);
            Check(air.Mission.SelectedTargetId == target.Id, "Polish: a known ground objective can be deliberately selected before a firing pass");
            air.Mission.Projectiles.Add(new Projectile { Position = target.Position + NVector3.UnitY * 8,
                Velocity = -NVector3.UnitY * 760, Team = 0, OwnerId = 0, Damage = 100 });
            air.StepForDiagnostics(new FlightInput { Throttle = .78f }, 1f / 30f);
            yield return null; yield return null;
            Check(!target.Alive && air.ActiveGroundObjectiveMarkers == 3 && air.Mission.SelectedTargetId != target.Id,
                "Polish: destroyed vehicles leave both active objective navigation and current target selection");
            Check(!air.AircraftHitVisible && air.Mission.PlayerAircraftHits == 0,
                "Polish: a ground-vehicle hit cannot trigger the aircraft-only confirmation indicator");
            PointDiagnosticAircraft(air, air.Mission.Player.Position - NVector3.UnitZ * 1000);
            air.View.Sync(air.Mission, 0);
            Check(air.ActiveGroundObjectiveMarkers == 3, "Polish: remaining known ground objectives retain guidance when behind the pilot");
            yield return Capture("60-polish-offscreen-ground-guidance");
        }

        static Projectile PolishImpact(AircraftState target, float damage)
        {
            return new Projectile { Position = target.Position - target.Forward * 5f,
                Velocity = target.Forward * 760f + target.Velocity, Team = 0, OwnerId = 0, Damage = damage };
        }

        IEnumerator AuditEquipmentDescriptions(AirCombatGame air)
        {
            var card = air.Campaign.Rewards[0]; string originalText = card.Description, originalTitle = card.Title;
            Note("DESCRIPTION LAYOUT FIXTURE: substitute catalog prose into the first visible reward card, measure actual GUI wrapping, then restore the earned reward unchanged.");
            try
            {
                foreach (RogueMod mod in Enum.GetValues(typeof(RogueMod)))
                {
                    card.Title = RogueCatalog.ModName(mod) + " / Mk 2";
                    card.Description = RogueCatalog.ModDescription(mod) + " Second level compounds these effects.";
                    yield return null; yield return new WaitForEndOfFrame();
                    bool fits = DescriptionsFit(air);
                    Check(fits, "Polish: maximum-level " + mod + " description fits the actual reward-card font and rectangle");
                    if (!fits) yield return Capture("51-overflow-mod-" + mod);
                }
                foreach (RogueAbility ability in Enum.GetValues(typeof(RogueAbility)))
                {
                    card.Title = RogueCatalog.AbilityName(ability); card.Description = RogueCatalog.AbilityDescription(ability);
                    yield return null; yield return new WaitForEndOfFrame();
                    bool fits = DescriptionsFit(air);
                    Check(fits, "Polish: " + ability + " ability description fits the actual reward-card font and rectangle");
                    if (!fits) yield return Capture("51-overflow-ability-" + ability);
                }
            }
            finally { card.Description = originalText; card.Title = originalTitle; }
        }
    }
}
