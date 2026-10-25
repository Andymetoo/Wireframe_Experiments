using UnityEngine;
using UnityEngine.InputSystem;

namespace TestAnxiety
{
    // Presentation staging only. ExamSession remains the authority for progress and records.
    public sealed partial class TestAnxietyGame
    {
        Transform documentView;
        Vector2 documentScroll;
        int documentIndex, doorAttempts, quietCuePhase;
        bool archiveOpened, clockPending, clockLate, attentionPending, attentionApplied;
        bool powerIsolated, compareCopy, soundCaptions, echoStarted, finaleActive, finaleMoved, recordInHand;
        float hintUntil, footstepDistance, walkedDistance, idleDuration, echoClock, blackoutTimer;
        float seatYaw, seatPitch, finaleClock, captionUntil;
        int echoBeat;
        string caption = "";
        bool RequiresIsolation { get { return Session.Stage == ExamStage.AwaitingExit && Session.FinalReleaseChoice >= 2 && !powerIsolated; } }

        void UpdateSequence(float dt)
        {
            if (Session.NeedsArchiveRecord && !archiveOpened) OpenRecordsPassage();
            if (clockPending && mode == ViewMode.Book && transition > .97f)
            {
                Room.SetClockLate(true);
                clockLate = true;
                clockPending = false;
            }
            if (Session.NeedsQuietInterval)
            {
                bool seated = mode == ViewMode.Seated && transition > .97f;
                Session.AdvanceQuietInterval(dt, seated);
                if (seated)
                {
                    float time = Session.QuietElapsed;
                    if (quietCuePhase == 0 && time >= 2.5f)
                    { quietCuePhase++; Cue("intercom", new Vector3(-3.8f, 2.7f, 5.8f), "[the loudspeaker opens]"); }
                    if (quietCuePhase == 1 && time >= 6)
                    { quietCuePhase++; Cue("knock", Room.ExitFocus.position, "[three knocks beyond the door]", 4); }
                    if (quietCuePhase == 2 && time >= 11)
                    { quietCuePhase++; Cue("chair", new Vector3(0, .45f, -3.3f), "[a chair moves behind you]", 3); }
                    if (quietCuePhase == 3 && time >= 15)
                    { quietCuePhase++; Cue("step", new Vector3(.8f, .1f, -3.3f), "[one footstep]"); }
                    if (!Session.NeedsQuietInterval)
                    {
                        attentionPending = true;
                        attentionApplied = false;
                        blackoutTimer = .60f;
                        Cue("relay", new Vector3(-4.9f, 1.3f, -3.6f), "[the lights cut out]", 2);
                    }
                }
            }
            if (attentionPending)
            {
                blackoutTimer = Mathf.Max(0, blackoutTimer - dt);
                // The room is obscured before geometry changes. No gaze/timer race can strand the scan.
                if (!attentionApplied && blackoutTimer <= .35f)
                {
                    attentionApplied = true;
                    Room.SetAttention(true);
                    int choice = Session.GetRecordedAnswer("allocation-seat");
                    Room.SetVacantDesk(choice == 0 ? 3 : choice == 2 ? 8 : 6);
                    Cue("paperstorm", Room.MachineAnchor.position, "[a sudden burst of paper and rollers]", 3);
                }
                Room.SetBlackout(BlackoutAlpha());
                if (blackoutTimer <= 0)
                {
                    attentionPending = false;
                    Room.SetBlackout(0);
                    silenceUntil = elapsed + 1.8f;
                }
            }
            // A delayed continuation of the player's rhythm, unrelated to a question or grade.
            if (!echoStarted && Session.CurrentSection.Id == "verification" && walkedDistance > 15 && idleDuration > 1.25f && mode == ViewMode.Walk)
            { echoStarted = true; echoClock = 0; echoBeat = 0; }
            if (echoStarted && echoBeat < 3 && mode == ViewMode.Walk)
            {
                echoClock += dt;
                if (echoClock >= echoBeat * .64f)
                {
                    Vector3 behind = head.position - head.forward * (2.8f - echoBeat * .65f);
                    behind = new Vector3(Mathf.Clamp(behind.x, -4.7f, 4.7f), .12f, Mathf.Clamp(behind.z, -5.7f, 5.7f));
                    Cue("step", behind, "[footsteps]", 1.1f);
                    echoBeat++;
                }
            }
        }

        float BlackoutAlpha()
        {
            if (!attentionPending) return 0;
            if (blackoutTimer > .35f) return Mathf.Clamp01((.60f - blackoutTimer) / .09f);
            return blackoutTimer > .15f ? 1 : blackoutTimer / .15f;
        }

        void RecordFootsteps(float distance, float dt)
        {
            // Controller vertical settling is ignored. Steps require grounded horizontal travel.
            float horizontalSpeed = new Vector2(motor.velocity.x, motor.velocity.z).magnitude;
            if (horizontalSpeed > .15f && motor.isGrounded)
            {
                float travel = Mathf.Min(distance, 1.85f * dt * 1.2f);
                footstepDistance += travel;
                walkedDistance += travel;
                idleDuration = 0;
                if (footstepDistance >= .78f)
                {
                    footstepDistance %= .78f;
                    sound.PlayFootstep(motor.transform.position + Vector3.up * .08f);
                }
            }
            else idleDuration += dt;
        }

        void Cue(string cue, Vector3 position, string text, float duration = 2)
        {
            sound.PlayCue(cue, position);
            caption = text;
            captionUntil = elapsed + duration;
        }

        void OnBookOpened()
        {
            if (!clockLate && (Session.CurrentSection.Id == "retention" || Session.CurrentSection.Id == "release")) clockPending = true;
        }

        void OnSheetReturned()
        {
            string id = Session.CurrentSection.Id;
            if (id == "verification")
            {
                silenceUntil = elapsed + 3.5f;
                sound.PlayContradiction();
            }
            if (id == "retention") Scanner.SetDisplay("ORIGINAL / VERIFIED");
            Notify(mode == ViewMode.Seated ? "The reader has resumed. E to stand." : "A sheet waits in the return tray.", 5);
        }

        void OnReportAccepted(string reviewedSection)
        {
            if (Session.NeedsArchiveRecord) OpenRecordsPassage();
            if (Session.Stage == ExamStage.AwaitingExit)
            {
                if (!archiveOpened) OpenRecordsPassage();
                Room.SetFinalRecord(Session.FinalReleaseChoice);
                Cue("bell", new Vector3(-10.5f, 2.6f, -4.8f), "[a distant dismissal bell]", 4);
            }
        }

        void OpenRecordsPassage()
        {
            archiveOpened = true;
            Room.SetArchiveOpen(true);
            Cue("latch", Room.ExitFocus.position, "[the entrance unlocks]", 3);
            if (Session.NeedsArchiveRecord)
                Notify("RECORD MISSING. The entrance has been released. Retrieve your retained copy from Records, along the corridor.", 12);
        }

        string NearbySequenceTarget()
        {
            if (RequiresIsolation && head.position.x > -4.95f && CanReach(Room.LightSwitchFocus, 1.7f)) return "switch";
            if (archiveOpened && CanReach(Room.ArchiveFocus, 2.0f)) return "archive";
            if (archiveOpened && CanReach(Room.FinalExitFocus, 1.9f)) return "outer-exit";
            for (int i = 0; i < Room.DocumentTargets.Length; i++)
                if (CanReach(Room.DocumentTargets[i], 1.6f)) return "document:" + i;
            return "";
        }

        bool InteractSequence(string target)
        {
            if (target == "archive")
            {
                ShowRetainedRecord(false);
                return true;
            }
            if (target == "outer-exit")
            {
                if (Session.Stage != ExamStage.AwaitingExit) Notify("A release record is required for this door.", 5);
                else if (RequiresIsolation) Notify("The isolation switch is beside the classroom entrance. The reader is still live.", 7);
                else BeginFinale();
                return true;
            }
            if (target == "switch")
            {
                if (RequiresIsolation)
                {
                    powerIsolated = true;
                    Room.SetPowerCut(true);
                    Scanner.SetPowered(false);
                    Cue("relay", Room.LightSwitchFocus.position, "[power disconnects]", 3);
                    Notify("The outer door in Records is released.", 7);
                }
                return true;
            }
            if (target.StartsWith("document:", System.StringComparison.Ordinal))
            {
                documentIndex = int.Parse(target.Substring(9));
                documentScroll = Vector2.zero;
                if (documentView == null) documentView = Child("Document inspection pose").transform;
                Vector3 focus = Room.DocumentTargets[documentIndex].position;
                documentView.position = Vector3.Lerp(head.position, focus + Vector3.up * .35f, .30f);
                documentView.rotation = Quaternion.LookRotation(focus - documentView.position, Vector3.up);
                ChangeView(ViewMode.Document);
                sound.PlayPaper();
                return true;
            }
            return false;
        }

        void ShowRetainedRecord(bool inHand)
        {
            recordInHand = inHand;
            Session.CloseBook();
            ChangeView(ViewMode.Archive);
            sound.PlayPaper();
        }

        void CollectRecord()
        {
            if (!Session.CollectArchiveRecord()) return;
            Room.SetArchiveCollected(true);
            CloseInspection();
            Cue("intercom", new Vector3(-10.7f, 2.7f, -1), "[the office speaker clicks]", 2);
            Notify("Retained copy collected. Return to your booklet in room 107.", 7);
        }

        void SitForInterval()
        {
            seatYaw = 0;
            seatPitch = 3;
            Room.DeskSeatView.rotation = Quaternion.Euler(seatPitch, seatYaw, 0);
            ChangeView(ViewMode.Seated);
            Cue("chair", Room.DeskSeatView.position - Vector3.up, "[your chair settles]");
            Notify("The reader will resume after the supervised interval. You may look around. E to stand.", 7);
        }

        void UpdateSeatedLook(float dt)
        {
            if (Cursor.lockState != CursorLockMode.Locked) LockCursor(true);
            if (Mouse.current != null)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                seatYaw += delta.x * sensitivity;
                seatPitch = Mathf.Clamp(seatPitch - delta.y * sensitivity, -70, 65);
            }
            var k = Keyboard.current;
            if (k != null)
            {
                seatYaw += ((k.rightArrowKey.isPressed ? 1 : 0) - (k.leftArrowKey.isPressed ? 1 : 0)) * 85 * dt;
                seatPitch = Mathf.Clamp(seatPitch + ((k.downArrowKey.isPressed ? 1 : 0) - (k.upArrowKey.isPressed ? 1 : 0)) * 65 * dt, -70, 65);
            }
            Room.DeskSeatView.rotation = Quaternion.Euler(seatPitch, seatYaw, 0);
        }

        string CurrentHint()
        {
            if (RequiresIsolation) return "Use the wall switch beside the classroom entrance, then the outer door in Records.";
            if (Session.Stage == ExamStage.AwaitingExit) return "Use the outer door at the far end of Records.";
            if (Session.NeedsQuietInterval) return "Return to desk 014 and press E to sit. You can stand and resume the interval later.";
            if (Session.NeedsArchiveRecord) return "Go through the classroom entrance. Follow the corridor to Records and inspect the retained copy.";
            if (Scanner.Busy) return "The reader is working. Your paper will wait in the return tray.";
            if (Session.Stage == ExamStage.ResultsReady) return "Collect your returned paper from the optical reader. E to retrieve.";
            if (Session.Stage == ExamStage.Reviewing) return "Review the returned paper at the reader or desk. Accept the record when ready.";
            if (carrying) return "Carry your completed sheet to the optical reader beside the board. E to insert.";
            if (Session.NeedsRearObservation) return "Close the booklet. Check the rear of the room near your examination desk.";
            if (Session.NeedsShirtRevisit) return "Return to your booklet and read the question again.";
            return "Observe the room, then mark your answers at desk 014. Your pencil marks are saved when you close the booklet.";
        }

        void BeginFinale()
        {
            finaleActive = true;
            finaleClock = 0;
            finaleMoved = false;
            Session.CloseBook();
            mode = ViewMode.Walk;
            LockCursor(false);
            Cue("latch", Room.FinalExitFocus.position, "[the outer door opens]", 3);
        }

        void UpdateFinale(float dt)
        {
            finaleClock += dt;
            sound.SetSilent(powerIsolated);
            sound.Tick(dt, 1);
            Room.Tick(elapsed, 1);
            if (!finaleMoved && finaleClock >= 1)
            {
                finaleMoved = true;
                if (!powerIsolated)
                {
                    ViewCamera.transform.SetPositionAndRotation(Room.ProctorView.position, Room.ProctorView.rotation);
                    ViewCamera.fieldOfView = 60;
                    Room.SetCandidateName(Session.FinalReleaseChoice == 1 ? "CANDIDATE 014" : "MARA BELL");
                    Scanner.SetSheetMarks(new[] { 1, 1, 1 }, false);
                    Scanner.BeginScan(() => Scanner.SetDisplay("NEXT CANDIDATE"));
                }
                else Cue("step", new Vector3(-13, .1f, -4.65f), "[footsteps recede beyond the door]", 3);
            }
            if (!powerIsolated)
            {
                Scanner.Tick(dt);
                if (finaleMoved) ViewCamera.fieldOfView = Mathf.Lerp(60, 44, Mathf.SmoothStep(0, 1, (finaleClock - 1) / 5));
            }
            if (finaleClock >= 9 && Session.CompleteAtExit())
            {
                finaleActive = false;
                endingTime = 2.5f;
                sound.SetSilent(true);
                LockCursor(false);
            }
        }

        float FinaleFade()
        {
            if (finaleClock < 1) return finaleClock;
            if (finaleClock < 2) return 2 - finaleClock;
            return Mathf.Clamp01((finaleClock - 7) / 2);
        }
    }
}
