using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace TestAnxiety
{
    /// <summary>Scene-local composition, input and presentation. Exam rules are in ExamSession.</summary>
    public sealed partial class TestAnxietyGame : MonoBehaviour
    {
        public Material litTemplate;
        public TextAsset examData;
        public ExamSession Session { get; private set; }
        public TestAnxietyClassroom Room { get; private set; }
        public TestAnxietyScanner Scanner { get; private set; }
        public Camera ViewCamera { get; private set; }

        TestAnxietyAudio sound;
        CharacterController motor;
        Transform head;
        enum ViewMode { Walk, Book, Report, Archive, Document, Seated }
        ViewMode mode;
        bool started, paused, carrying, historicalReport, pendingShirtReveal;
        float yaw = -12, pitch = 5, gravity, elapsed, transition, silenceUntil;
        float messageUntil, rearDwell, endingTime;
        string message = "", fatalError;
        int reportIndex;
        Vector3 cameraFromPosition;
        Quaternion cameraFromRotation;
        float sensitivity = .105f;
        float preferredVolume = 1;
        const float EyeHeight = 1.62f;
        CursorLockMode previousCursor;
        bool previousCursorVisible;
        AmbientMode previousAmbientMode;
        Color previousAmbientSky, previousAmbientEquator, previousAmbientGround, previousFogColor;
        bool previousFog;
        FogMode previousFogMode;
        float previousFogStart, previousFogEnd;
        Material previousSkybox;

        void Awake()
        {
            previousCursor = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            CaptureLighting();
            try
            {
                if (examData == null) examData = Resources.Load<TextAsset>("TestAnxietyExam");
                if (litTemplate == null) litTemplate = Resources.Load<Material>("TestAnxietyLit");
                if (examData == null) throw new InvalidOperationException("TestAnxietyExam.json is missing.");
                if (litTemplate == null || litTemplate.shader == null || !litTemplate.shader.isSupported)
                    throw new InvalidOperationException("The TestAnxietyLit material requires the project's URP renderer.");
                Session = new ExamSession(JsonUtility.FromJson<ExamCatalog>(examData.text));
                Room = Child("Classroom").AddComponent<TestAnxietyClassroom>();
                Room.Build(litTemplate);
                Scanner = Child("Optical reader").AddComponent<TestAnxietyScanner>();
                Scanner.Build(Room.MachineAnchor, litTemplate);
                sound = Child("Classroom acoustics").AddComponent<TestAnxietyAudio>();
                sound.Build(transform);
                sound.SetVolume(preferredVolume);
                Scanner.OnSound = ScannerSound;
                CreatePlayer();
                ConfigureLighting();
                LockCursor(false);
            }
            catch (Exception error)
            {
                fatalError = error.Message;
                Debug.LogException(error, this);
                LockCursor(false);
            }
        }

        GameObject Child(string label)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            return child;
        }

        void CreatePlayer()
        {
            var player = Child("Candidate 014 - first person");
            player.transform.position = new Vector3(1.4f, .04f, -4.7f);
            motor = player.AddComponent<CharacterController>();
            motor.height = 1.72f;
            motor.radius = .22f;
            motor.center = new Vector3(0, .86f, 0);
            motor.stepOffset = .18f;
            motor.skinWidth = .025f;
            motor.minMoveDistance = 0;
            head = new GameObject("Eye position").transform;
            head.SetParent(player.transform, false);
            head.localPosition = Vector3.up * EyeHeight;
            var cameraObject = Child("Test Anxiety Camera");
            cameraObject.tag = "MainCamera";
            ViewCamera = cameraObject.AddComponent<Camera>();
            ViewCamera.fieldOfView = 67;
            ViewCamera.nearClipPlane = .035f;
            ViewCamera.farClipPlane = 65;
            ViewCamera.clearFlags = CameraClearFlags.SolidColor;
            ViewCamera.backgroundColor = new Color(.21f, .25f, .27f);
            cameraObject.AddComponent<AudioListener>();
            head.rotation = Quaternion.Euler(pitch, yaw, 0);
            ViewCamera.transform.SetPositionAndRotation(head.position, head.rotation);
        }

        void Update()
        {
            if (fatalError != null || Session == null) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && started && Session.Stage != ExamStage.Complete)
            {
                if (paused) SetPaused(false);
                else if (finaleActive) SetPaused(true);
                else if (mode != ViewMode.Walk) CloseInspection();
                else SetPaused(true);
                return;
            }
            if (!started || paused) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .06f);
            elapsed += dt;
            if (Session.Stage == ExamStage.Complete)
            {
                endingTime += dt;
                sound.SetSilent(true);
                sound.Tick(dt, 1);
                LockCursor(false);
                return;
            }
            if (finaleActive) { UpdateFinale(dt); return; }
            if (keyboard != null && keyboard.hKey.wasPressedThisFrame) hintUntil = elapsed + 9;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame && Session.ArchiveCollected && mode == ViewMode.Walk)
                ShowRetainedRecord(true);
            float tension = Session.SectionIndex / Mathf.Max(1f, Session.SectionCount - 1f);
            UpdateSequence(dt);
            Room.Tick(elapsed, tension);
            sound.SetMood(tension);
            sound.SetSilent(elapsed < silenceUntil || Session.NeedsQuietInterval || powerIsolated);
            sound.Tick(dt, tension);
            Scanner.ProcessingHeld = Session.NeedsQuietInterval || attentionPending;
            Scanner.Tick(dt);
            if (mode == ViewMode.Walk)
            {
                if (Cursor.lockState != CursorLockMode.Locked)
                {
                    if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) LockCursor(true);
                    return;
                }
                MovePlayer(dt);
                ObserveRear(dt);
                if (keyboard != null && keyboard.eKey.wasPressedThisFrame) Interact();
            }
            else if (mode == ViewMode.Seated)
            {
                UpdateSeatedLook(dt);
                if (keyboard != null && keyboard.eKey.wasPressedThisFrame) CloseInspection();
            }
            else if (keyboard != null && keyboard.eKey.wasPressedThisFrame) CloseInspection();
            UpdateCamera(dt);
            if (pendingShirtReveal && mode == ViewMode.Book && transition > .97f)
            {
                pendingShirtReveal = false;
                Room.RevealShirt();
            }
        }

        void MovePlayer(float dt)
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * sensitivity;
                pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -78, 78);
            }
            Vector2 input = Vector2.zero;
            if (keyboard != null)
            {
                input.x = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
                input.y = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
                // The arrow keys are also a comfortable alternative to mouse look.
                yaw += ((keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.leftArrowKey.isPressed ? 1 : 0)) * 85 * dt;
                pitch = Mathf.Clamp(pitch + ((keyboard.downArrowKey.isPressed ? 1 : 0) - (keyboard.upArrowKey.isPressed ? 1 : 0)) * 65 * dt, -78, 78);
            }
            input = Vector2.ClampMagnitude(input, 1);
            Vector3 planar = Quaternion.Euler(0, yaw, 0) * new Vector3(input.x, 0, input.y);
            gravity = motor.isGrounded ? -2 : Mathf.Max(gravity - 18 * dt, -25);
            Vector3 previousPosition = motor.transform.position;
            motor.Move((planar * 1.85f + Vector3.up * gravity) * dt);
            RecordFootsteps(Vector3.Distance(previousPosition, motor.transform.position), dt);
            head.rotation = Quaternion.Euler(pitch, yaw, 0);
            // Defensive recovery if someone edits the procedural floor incorrectly.
            if (motor.transform.position.y < -3)
            {
                motor.enabled = false;
                motor.transform.position = new Vector3(1.4f, .04f, -4.7f);
                motor.enabled = true;
                gravity = 0;
            }
        }

        void UpdateCamera(float dt)
        {
            transition = Mathf.Min(1, transition + dt / .42f);
            Transform destination = mode == ViewMode.Book ? Room.ExamView : mode == ViewMode.Report ? Scanner.ReviewView :
                mode == ViewMode.Archive ? (recordInHand ? head : Room.ArchiveView) : mode == ViewMode.Document ? documentView :
                mode == ViewMode.Seated ? Room.DeskSeatView : head;
            float t = Mathf.SmoothStep(0, 1, transition);
            ViewCamera.transform.SetPositionAndRotation(Vector3.Lerp(cameraFromPosition, destination.position, t),
                Quaternion.Slerp(cameraFromRotation, destination.rotation, t));
            ViewCamera.fieldOfView = Mathf.Lerp(ViewCamera.fieldOfView, mode == ViewMode.Walk ? 67 : 53, 1 - Mathf.Exp(-dt * 9));
        }

        void ChangeView(ViewMode next)
        {
            cameraFromPosition = ViewCamera.transform.position;
            cameraFromRotation = ViewCamera.transform.rotation;
            transition = 0;
            mode = next;
            LockCursor((next == ViewMode.Walk || next == ViewMode.Seated) && started && !paused);
        }

        void ObserveRear(float dt)
        {
            if (!Session.NeedsRearObservation || Session.BookOpen || transition < 1) return;
            bool nearby = Vector3.Distance(head.position, Room.ExamFocus.position) < 3.8f;
            // The whole rear area is empty. Do not require hitting one small chair target:
            // a player may be standing behind that chair when turning around.
            bool looking = head.forward.z < -.55f && Mathf.Abs(pitch) < 55;
            // No timer-only trigger: first read the question, close it, then inspect the empty rear area.
            rearDwell = nearby && looking ? rearDwell + dt : 0;
            if (rearDwell < .6f) return;
            if (Session.ObserveBehind())
            {
                silenceUntil = elapsed + 2;
                Cue("chair", new Vector3(0, .4f, -4.3f), "[a chair settles]", 2);
            }
        }

        string NearbyTarget()
        {
            string extension = NearbySequenceTarget();
            if (extension.Length > 0) return extension;
            if (CanReach(Scanner.InteractionFocus, 2.15f)) return "machine";
            if (CanReach(Room.ExamFocus, 2.1f)) return "desk";
            if (!archiveOpened && CanReach(Room.ExitFocus, 2)) return "exit";
            return "";
        }

        bool CanReach(Transform target, float distance)
        {
            Vector3 direction = target.position - head.position;
            return direction.magnitude < distance && Vector3.Dot(head.forward, direction.normalized) > .38f;
        }

        void Interact()
        {
            string target = NearbyTarget();
            if (InteractSequence(target)) return;
            switch (target)
            {
                case "desk":
                    if (Session.NeedsQuietInterval) SitForInterval();
                    else if (Session.NeedsArchiveRecord) Notify("RETAINED RECORD REQUIRED. Follow the corridor through the entrance to Records.", 7);
                    else if (Session.Stage == ExamStage.Answering) OpenExam();
                    else if (Session.Stage == ExamStage.Reviewing) OpenReport(false);
                    else if (Session.Stage == ExamStage.AwaitingExit && Session.History.Count > 0) OpenReport(true);
                    else Notify("Your answer sheet is at the optical reader.");
                    break;
                case "machine":
                    if (Session.Stage == ExamStage.ResultsReady && Scanner.Ready)
                    {
                        Scanner.Retrieve();
                        if (Session.RetrieveResults()) OpenReport(false);
                    }
                    else if (Session.Stage == ExamStage.Reviewing) OpenReport(false);
                    else if (Session.Stage == ExamStage.Answering)
                    {
                        if (!carrying) Notify("Take the completed answer sheet from your examination desk.");
                        else Submit();
                    }
                    else if (Session.Stage == ExamStage.AwaitingExit) OpenReport(true);
                    break;
                case "exit":
                    doorAttempts++;
                    Cue("latch", Room.ExitFocus.position, "[the latch catches]");
                    Notify("The examination is still in progress.");
                    break;
            }
        }

        void OpenExam()
        {
            bool hadShirt = Session.ShirtChanged;
            if (!Session.OpenBook()) return;
            OnBookOpened();
            bookPage = 0;
            carrying = false;
            Room.AnswerSheet.gameObject.SetActive(true);
            if (!hadShirt && Session.ShirtChanged) pendingShirtReveal = true;
            sound.PlayPaper();
            ChangeView(ViewMode.Book);
        }

        void TakeSheet()
        {
            if (!Session.CanSubmit) { Notify(Session.SubmissionBlockReason); return; }
            Session.CloseBook();
            carrying = true;
            Room.AnswerSheet.gameObject.SetActive(false);
            sound.PlayPaper();
            ChangeView(ViewMode.Walk);
            Notify("Answer sheet in hand. Feed it into the reader beside the blackboard.", 7);
        }

        void Submit()
        {
            if (!Session.BeginSubmission()) { Notify(Session.SubmissionBlockReason); return; }
            Scanner.ProcessingHeld = Session.NeedsQuietInterval;
            int[] marks = new int[Session.CurrentSection.Questions.Count];
            for (int i = 0; i < marks.Length; i++) marks[i] = Session.GetAnswer(i);
            Scanner.SetSheetMarks(marks, false);
            carrying = false;
            Scanner.BeginScan(() =>
            {
                OnSheetReturned();
            }, () =>
            {
                if (!Session.FinishScan()) return;
                int[] returned = new int[Session.LatestResult.Rows.Count];
                bool[] wrong = new bool[returned.Length];
                for (int i = 0; i < returned.Length; i++)
                {
                    returned[i] = Session.LatestResult.Rows[i].ReturnedAnswer;
                    wrong[i] = !Session.LatestResult.Rows[i].IsCorrect;
                }
                Scanner.SetSheetMarks(returned, true);
                Scanner.SetGradingMarks(wrong);
            });
            Notify(Session.NeedsQuietInterval ? "SUPERVISED INTERVAL. Return to desk 014 and take your seat. Your sheet will be held." : "Sheet accepted.", Session.NeedsQuietInterval ? 12 : 3);
        }

        void OpenReport(bool historical)
        {
            if (Session.History.Count == 0) return;
            Session.CloseBook();
            historicalReport = historical;
            reportIndex = Session.History.Count - 1;
            reportPage = 0;
            compareCopy = false;
            ChangeView(ViewMode.Report);
            sound.PlayPaper();
        }

        void AdvanceReport()
        {
            string reviewedSection = Session.CurrentSection.Id;
            if (!Session.AdvanceAfterReview()) return;
            Room.AnswerSheet.gameObject.SetActive(Session.Stage == ExamStage.Answering);
            Scanner.ResetMachine();
            CloseInspection();
            OnReportAccepted(reviewedSection);
            if (Session.Stage == ExamStage.AwaitingExit)
            {
                Room.SetFinalAtmosphere(true);
                Notify(RequiresIsolation ? "MANUAL RELEASE. Isolate the reader at the wall switch beside the classroom entrance." : "Release issued. Use the outer door in Records.", 10);
            }
            else if (!Session.NeedsArchiveRecord) Notify("Your booklet is ready.", 4);
        }

        void CloseInspection()
        {
            Session.CloseBook();
            // If closed during the transition, reveal on the next completed inspection instead.
            // The changed wording remains stored and is never mutated during display.
            sound.PlayPaper();
            ChangeView(ViewMode.Walk);
        }

        void ScannerSound(string cue)
        {
            if (cue == "feed") { sound.PlayPaper(); sound.PlayScanner(); }
            else if (cue == "process") sound.PlayScanner();
            else if (cue == "return") sound.PlayReturn();
            else if (cue == "retrieve") sound.PlayPaper();
        }

        void Begin()
        {
            started = true;
            transition = 1;
            LockCursor(true);
            Notify("Find desk 014, in the back row. Look down at the booklet and press E.", 10);
            hintUntil = elapsed + 15;
        }

        void SetPaused(bool value)
        {
            paused = value;
            sound.SetPaused(value);
            LockCursor(!value && (mode == ViewMode.Walk || mode == ViewMode.Seated) && !finaleActive);
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && fatalError == null && sound != null && started && !paused && Session != null && Session.Stage != ExamStage.Complete) SetPaused(true);
        }

        void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Notify(string text, float seconds = 4) { message = text; messageUntil = elapsed + seconds; }

        void Restart()
        {
            // Reconstruct this isolated root; no build-settings entry or scene reload is required.
            Material material = litTemplate;
            TextAsset data = examData;
            GameObject replacement = new GameObject("TEST ANXIETY");
            replacement.SetActive(false);
            var game = replacement.AddComponent<TestAnxietyGame>();
            game.litTemplate = material;
            game.examData = data;
            game.sensitivity = sensitivity;
            game.soundCaptions = soundCaptions;
            game.preferredVolume = sound != null ? sound.MasterVolume : preferredVolume;
            // Restore globals before the new root captures them. Destruction is deferred one frame.
            RestoreLighting();
            enabled = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
            replacement.SetActive(true);
        }

        bool lightingRestored;
        void CaptureLighting()
        {
            previousAmbientMode = RenderSettings.ambientMode;
            previousAmbientSky = RenderSettings.ambientSkyColor;
            previousAmbientEquator = RenderSettings.ambientEquatorColor;
            previousAmbientGround = RenderSettings.ambientGroundColor;
            previousFog = RenderSettings.fog;
            previousFogMode = RenderSettings.fogMode;
            previousFogStart = RenderSettings.fogStartDistance;
            previousFogEnd = RenderSettings.fogEndDistance;
            previousFogColor = RenderSettings.fogColor;
            previousSkybox = RenderSettings.skybox;
        }

        void ConfigureLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.26f, .29f, .29f);
            RenderSettings.ambientEquatorColor = new Color(.17f, .18f, .17f);
            RenderSettings.ambientGroundColor = new Color(.09f, .09f, .075f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.21f, .25f, .26f);
            RenderSettings.fogStartDistance = 13;
            RenderSettings.fogEndDistance = 35;
        }

        void RestoreLighting()
        {
            if (lightingRestored) return;
            lightingRestored = true;
            RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.ambientSkyColor = previousAmbientSky;
            RenderSettings.ambientEquatorColor = previousAmbientEquator;
            RenderSettings.ambientGroundColor = previousAmbientGround;
            RenderSettings.fog = previousFog;
            RenderSettings.fogMode = previousFogMode;
            RenderSettings.fogStartDistance = previousFogStart;
            RenderSettings.fogEndDistance = previousFogEnd;
            RenderSettings.fogColor = previousFogColor;
            RenderSettings.skybox = previousSkybox;
        }

        void OnDisable()
        {
            Cursor.lockState = previousCursor;
            Cursor.visible = previousCursorVisible;
        }

        void OnDestroy()
        {
            RestoreLighting();
            if (circleTexture != null) Destroy(circleTexture);
            if (ringTexture != null) Destroy(ringTexture);
        }
    }
}
