using IceResurfacer.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    /// <summary>Presentation and command binding for the saved console; simulation values remain authoritative.</summary>
    [DisallowMultipleComponent]
    public sealed class IceMachineryConsole : MonoBehaviour
    {
        public IceResurfacerGame game;
        public Canvas canvas;
        public GraphicRaycaster raycaster;
        public CanvasGroup compactGroup;
        public CanvasGroup expandedGroup;
        public TMP_Text compactSummary;
        public IceConsoleControl[] controls;
        public GameObject previewBadge;
        [Header("Authored confirmation panel")]
        public CanvasGroup confirmationGroup;
        public TMP_Text confirmationMessage;
        public bool confirmNewSession = true;
        public bool confirmFinish;
        [TextArea] public string newSessionConfirmation = "Start a new session? The current run will be replaced.";
        [TextArea] public string finishConfirmation = "Finish the current run and view its report?";
        [Min(.02f)] public float refreshInterval = .1f;

        readonly IceUIPointerGuard pointerGuard = new IceUIPointerGuard();
        IceResurfacerGame registeredGame;
        Simulation observedSimulation;
        string observedCamera, observedQuicklook;
        bool observedCompact, observedChase, observedLooking, haveObservation;
        bool reportedBinding;
        float nextRefresh;
        IceConsoleControlKind pendingAction;
        public bool ConfirmationOpen { get; private set; }

        public bool IsAvailable => isActiveAndEnabled && game != null && game.isActiveAndEnabled &&
            canvas != null && canvas.isActiveAndEnabled && raycaster != null && raycaster.isActiveAndEnabled &&
            compactGroup != null && compactGroup.isActiveAndEnabled && expandedGroup != null && expandedGroup.isActiveAndEnabled &&
            ValidateBindings(out _) && (!Application.IsPlaying(gameObject) || game.AuthoredConsole == this && InputReady());

        public bool Visible => IsAvailable && game.Sim != null && game.Dashboard != null &&
            game.Started && !game.Sim.Finished && !game.Sim.OnFoot && !game.HudHidden &&
            !game.Paused && !game.HelpVisible && string.IsNullOrEmpty(game.HeldQuicklook) &&
            (game.RouteGuide == null || !game.RouteGuide.PanelOpen);

        bool BaseCanInteract => Visible && game.HasFocus && !game.Looking &&
            Cursor.lockState != CursorLockMode.Locked && (Mouse.current == null || !Mouse.current.rightButton.isPressed);
        public bool CanInteract => BaseCanInteract && !ConfirmationOpen;

        public bool ValidateBindings(out string error)
        {
            if (game == null) { error = "Assign the Game on the scene console instance (the reusable prefab may leave it empty)."; return false; }
            if (canvas == null || raycaster == null || raycaster.GetComponent<Canvas>() != canvas)
            { error = "Assign the persistent Canvas and its GraphicRaycaster."; return false; }
            if (compactGroup == null || expandedGroup == null || compactGroup == expandedGroup)
            { error = "Assign separate compact and expanded CanvasGroups."; return false; }
            if (compactSummary == null) { error = "Assign the live compact summary text."; return false; }
            if ((confirmNewSession || confirmFinish) && (confirmationGroup == null || confirmationMessage == null))
            { error = "Assign the confirmation CanvasGroup and message, or explicitly disable confirmations."; return false; }
            if (controls == null || controls.Length == 0) { error = "Assign the authored console controls."; return false; }
            for (int i = 0; i < controls.Length; i++)
            {
                if (controls[i] == null) { error = "The console controls list contains a missing entry."; return false; }
                if (controls[i].owner != this) { error = "Every listed control must explicitly reference this console as Owner."; return false; }
                if (!controls[i].ValidateBinding(out error)) return false;
            }
            error = null;
            return true;
        }

        static bool InputReady()
        {
            var events = EventSystem.current;
            if (events == null || !events.isActiveAndEnabled) return false;
            var module = events.GetComponent<InputSystemUIInputModule>();
            return module != null && module.isActiveAndEnabled;
        }

        void OnEnable()
        {
            if (!Application.IsPlaying(gameObject)) return;
            Register();
            if (controls != null) foreach (var control in controls) if (control != null) control.Bind(this);
            if (previewBadge != null) previewBadge.SetActive(false);
            RefreshVisibility();
        }
        void Start() { if (Application.IsPlaying(gameObject)) RefreshNow(); }
        void LateUpdate()
        {
            if (!Application.IsPlaying(gameObject)) return;
            TickInput();
            if (Time.unscaledTime >= nextRefresh) RefreshNow();
        }
        void Register()
        {
            if (registeredGame != null && registeredGame != game && registeredGame.AuthoredConsole == this)
                registeredGame.AuthoredConsole = null;
            registeredGame = game;
            if (game != null && (game.AuthoredConsole == null || game.AuthoredConsole == this)) game.AuthoredConsole = this;
        }

        /// <summary>Called after keyboard/physical input and before simulation stepping, independent of EventSystem order.</summary>
        public void TickInput()
        {
            if (!Application.IsPlaying(gameObject)) return;
            if (game == null) { ReleaseInteractions(); RefreshVisibility(); return; }
            bool compact = game.Dashboard == null || game.Dashboard.Compact;
            bool changed = haveObservation && (observedSimulation != game.Sim || observedCompact != compact ||
                observedCamera != game.CameraView || observedQuicklook != game.HeldQuicklook ||
                observedChase != game.ChaseActive || observedLooking != game.Looking);
            if (changed || !BaseCanInteract)
            {
                ReleaseInteractions();
                ConfirmationOpen = false;
            }
            observedSimulation = game.Sim;
            observedCompact = compact;
            observedCamera = game.CameraView;
            observedQuicklook = game.HeldQuicklook;
            observedChase = game.ChaseActive;
            observedLooking = game.Looking;
            haveObservation = true;
            if (controls != null) foreach (var control in controls) if (control != null) control.PollInteraction();
            RefreshVisibility();
        }

        public void RefreshNow()
        {
            if (!Application.IsPlaying(gameObject)) return;
            Register();
            nextRefresh = Time.unscaledTime + Mathf.Max(.02f, refreshInterval);
            TickInput();
            if (!IsAvailable)
            {
                if (!reportedBinding)
                {
                    ValidateBindings(out string reason);
                    Debug.LogError("Aurora machinery console is unavailable: " + (reason ??
                        "check the active InputSystemUIInputModule, enabled Canvas/groups, and duplicate console registration.") +
                        " The legacy console remains available.", this);
                    reportedBinding = true;
                }
                return;
            }
            reportedBinding = false;
            if (game.Sim == null) return;
            var commands = game.Sim.Controls;
            string summary = "AUGERS " + (commands.VerticalAuger ? "V ON" : "V OFF") + " / " +
                (commands.HorizontalAuger ? "H ON" : "H OFF") + "   FLOOD " + (commands.FloodFlow * 100).ToString("0") + "%";
            if (compactSummary.text != summary) compactSummary.text = summary;
            foreach (var control in controls) control.Refresh(game.Sim);
        }

        public bool CanUse(IceConsoleControl control)
        {
            if (control == null || control.owner != this || !control.isActiveAndEnabled ||
                !control.IsLocallyInteractable || !BaseCanInteract) return false;
            bool confirmationControl = control.kind == IceConsoleControlKind.ConfirmAction || control.kind == IceConsoleControlKind.CancelAction;
            if (confirmationControl) return ConfirmationOpen;
            if (ConfirmationOpen) return false;
            if (control.kind == IceConsoleControlKind.OpenConsole) return game.Dashboard.Compact;
            if (control.kind == IceConsoleControlKind.CloseConsole) return !game.Dashboard.Compact;
            return !game.Dashboard.Compact && control.kind != IceConsoleControlKind.BladeStatus;
        }

        public bool ContainsPointer(Vector2 screenPoint)
        {
            if (!Application.IsPlaying(gameObject)) return false;
            // Synchronize shared-Canvas raycast groups before either migrated module queries them.
            TickInput();
            if (!BaseCanInteract) { pointerGuard.Release(); return false; }
            if (ConfirmationOpen) return true;
            return pointerGuard.ContainsPointer(raycaster, screenPoint);
        }

        public void ReleaseInteractions()
        {
            if (controls != null) foreach (var control in controls) if (control != null) control.CancelInteraction();
            pointerGuard.Release();
        }

        public void Activate(IceConsoleControl control, bool? requestedState = null)
        {
            if (!Application.IsPlaying(gameObject) || !CanUse(control)) return;
            var commands = game.Sim.Controls;
            switch (control.kind)
            {
                case IceConsoleControlKind.OpenConsole: SetCompact(false); break;
                case IceConsoleControlKind.CloseConsole: SetCompact(true); break;
                case IceConsoleControlKind.VerticalAuger: commands.VerticalAuger = requestedState ?? !commands.VerticalAuger; break;
                case IceConsoleControlKind.HorizontalAuger: commands.HorizontalAuger = requestedState ?? !commands.HorizontalAuger; break;
                case IceConsoleControlKind.BoardBrush: commands.BoardBrush = requestedState ?? !commands.BoardBrush; break;
                case IceConsoleControlKind.WashPump: commands.WashPump = requestedState ?? !commands.WashPump; break;
                case IceConsoleControlKind.SnowBreaker: game.OperateSnowBreaker(); break;
                case IceConsoleControlKind.Service: game.Service(); break;
                case IceConsoleControlKind.Finish: Terminal(IceConsoleControlKind.Finish, confirmFinish); break;
                case IceConsoleControlKind.NewSession: Terminal(IceConsoleControlKind.NewSession, confirmNewSession); break;
                case IceConsoleControlKind.ConfirmAction:
                    var accepted = pendingAction;
                    ConfirmationOpen = false;
                    ReleaseInteractions();
                    ExecuteTerminal(accepted);
                    break;
                case IceConsoleControlKind.CancelAction: ConfirmationOpen = false; ReleaseInteractions(); break;
            }
            RefreshNow();
        }

        public void SetCompact(bool compact)
        {
            if (!Application.IsPlaying(gameObject) || game == null || game.Dashboard == null) return;
            ReleaseInteractions();
            ConfirmationOpen = false;
            game.Dashboard.Compact = compact;
            RefreshVisibility();
        }

        void Terminal(IceConsoleControlKind kind, bool confirm)
        {
            ReleaseInteractions();
            if (!confirm) { ExecuteTerminal(kind); return; }
            pendingAction = kind;
            ConfirmationOpen = true;
            if (confirmationMessage != null)
                confirmationMessage.text = kind == IceConsoleControlKind.NewSession ? newSessionConfirmation : finishConfirmation;
            RefreshVisibility();
        }
        void ExecuteTerminal(IceConsoleControlKind kind)
        {
            if (kind == IceConsoleControlKind.Finish) game.Finish();
            else if (kind == IceConsoleControlKind.NewSession) game.NewSession(false, false);
        }

        void RefreshVisibility()
        {
            if (!Application.IsPlaying(gameObject)) return;
            bool visible = Visible, compact = game != null && game.Dashboard != null && game.Dashboard.Compact;
            SetGroup(compactGroup, visible && compact && !ConfirmationOpen, CanInteract);
            SetGroup(expandedGroup, visible && !compact && !ConfirmationOpen, CanInteract);
            SetGroup(confirmationGroup, visible && ConfirmationOpen, BaseCanInteract);
        }
        static void SetGroup(CanvasGroup group, bool show, bool interact)
        {
            if (group == null) return;
            float alpha = show ? 1 : 0;
            bool active = show && interact;
            if (group.alpha != alpha) group.alpha = alpha;
            if (group.blocksRaycasts != active) group.blocksRaycasts = active;
            if (group.interactable != active) group.interactable = active;
        }
        void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            ReleaseInteractions();
            ConfirmationOpen = false;
        }
        void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            ReleaseInteractions();
            ConfirmationOpen = false;
        }
        void OnDisable()
        {
            ReleaseInteractions();
            ConfirmationOpen = false;
            if (registeredGame != null && registeredGame.AuthoredConsole == this) registeredGame.AuthoredConsole = null;
            registeredGame = null;
            if (controls != null) foreach (var control in controls) if (control != null) control.Unbind(this);
            if (!Application.IsPlaying(gameObject)) return;
            SetGroup(compactGroup, false, false);
            SetGroup(expandedGroup, false, false);
            SetGroup(confirmationGroup, false, false);
        }
    }
}
