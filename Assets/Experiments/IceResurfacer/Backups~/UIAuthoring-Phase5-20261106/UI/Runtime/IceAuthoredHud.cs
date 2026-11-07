using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    /// <summary>
    /// Connects the persistent top HUD to the existing simulator. Only live content and
    /// visibility change in Play Mode; this component never authors layout or styling.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IceAuthoredHud : MonoBehaviour
    {
        public IceResurfacerGame game;
        public Canvas canvas;
        public CanvasGroup visibility;
        public GraphicRaycaster raycaster;
        public IceHudWidget[] widgets;
        public GameObject previewBadge;
        [Min(.02f)] public float refreshInterval = .1f;

        readonly IceUIPointerGuard pointerGuard = new IceUIPointerGuard();
        float nextRefresh;
        bool reportedMissing;
        bool reportedInput;
        bool reportedMissingWidget;
        bool reportedDuplicate;
        IceResurfacerGame registeredGame;
        static EventSystem observedEvents;
        static InputSystemUIInputModule observedModule;

        public bool IsAvailable => isActiveAndEnabled && game != null && game.isActiveAndEnabled &&
            canvas != null && canvas.isActiveAndEnabled && visibility != null && visibility.isActiveAndEnabled &&
            raycaster != null && raycaster.isActiveAndEnabled && HasWidgetReferences &&
            (!Application.IsPlaying(gameObject) || game.AuthoredHud == this && InputReady);

        bool HasWidgetReferences
        {
            get
            {
                if (widgets == null || widgets.Length == 0) return false;
                for (int i = 0; i < widgets.Length; i++) if (widgets[i] == null) return false;
                return true;
            }
        }

        static bool InputReady
        {
            get
            {
                var events = EventSystem.current;
                if (events == null || !events.isActiveAndEnabled) return false;
                if (events != observedEvents || observedModule == null)
                {
                    observedEvents = events;
                    observedModule = events.GetComponent<InputSystemUIInputModule>();
                }
                return observedModule != null && observedModule.isActiveAndEnabled;
            }
        }

        public bool Visible => IsAvailable && game.Sim != null && game.Started && !game.Sim.Finished &&
            !game.HudHidden && !game.Paused && !game.HelpVisible &&
            (game.RouteGuide == null || !game.RouteGuide.PanelOpen);

        // On foot the service controller owns centered mouse input, even when a
        // background Editor window cannot grant its requested native cursor lock.
        public bool CanInteract => Visible && !game.Sim.OnFoot && !game.Looking &&
            (game.AuthoredConsole == null || !game.AuthoredConsole.ConfirmationOpen) && Cursor.lockState != CursorLockMode.Locked &&
            (Mouse.current == null || !Mouse.current.rightButton.isPressed);

        void OnEnable()
        {
            if (!Application.IsPlaying(gameObject)) return;
            Register();
            if (widgets != null)
                foreach (var widget in widgets)
                    if (widget != null) widget.Bind(this);
            if (previewBadge != null) previewBadge.SetActive(false);
            RefreshVisibility();
        }

        void Start()
        {
            if (!Application.IsPlaying(gameObject)) return;
            RefreshNow();
        }

        void LateUpdate()
        {
            if (!Application.IsPlaying(gameObject)) return;
            RefreshVisibility();
            if (Time.unscaledTime >= nextRefresh) RefreshNow();
        }

        public void RefreshNow()
        {
            if (!Application.IsPlaying(gameObject)) return;
            Register();
            nextRefresh = Time.unscaledTime + Mathf.Max(.02f, refreshInterval);
            RefreshVisibility();
            if (!InputReady)
            {
                if (!reportedInput)
                    Debug.LogError("Aurora UI: add one active EventSystem with an enabled InputSystemUIInputModule. " +
                        "The authored HUD is hidden and the legacy header remains available until UI input is configured.", this);
                reportedInput = true;
                return;
            }
            reportedInput = false;
            if (!IsAvailable)
            {
                if (!reportedMissing)
                {
                    Debug.LogError("Aurora UI: top HUD requires a live Game, enabled Canvas and GraphicRaycaster, " +
                        "a visibility CanvasGroup, and a nonempty Widgets list with no missing entries. The legacy header remains available.", this);
                    reportedMissing = true;
                }
                return;
            }
            reportedMissing = false;
            bool missingWidget = false;
            for (int i = 0; i < widgets.Length; i++)
            {
                if (widgets[i] != null) widgets[i].Refresh(game.Sim);
                else missingWidget = true;
            }
            if (missingWidget && !reportedMissingWidget)
                Debug.LogError("Aurora UI: Widgets contains an unassigned element. Assign the authored widget explicitly.", this);
            reportedMissingWidget = missingWidget;
        }

        void Register()
        {
            if (registeredGame != null && registeredGame != game && registeredGame.AuthoredHud == this)
                registeredGame.AuthoredHud = null;
            registeredGame = game;
            if (game == null) return;
            if (game.AuthoredHud == null || game.AuthoredHud == this)
            {
                game.AuthoredHud = this;
                reportedDuplicate = false;
            }
            else if (!reportedDuplicate)
            {
                Debug.LogError("Aurora UI: more than one authored top HUD is bound to this game. Enable one top HUD instance.", this);
                reportedDuplicate = true;
            }
        }

        public bool ContainsPointer(Vector2 screenPoint)
        {
            if (!Application.IsPlaying(gameObject)) return false;
            // Refresh from current game state: input may run before our LateUpdate or
            // EventSystem.Update in this frame (notably Numpad 1 and modal transitions).
            RefreshVisibility();
            if (!CanInteract)
            {
                pointerGuard.Release();
                return false;
            }
            return pointerGuard.ContainsPointer(raycaster, screenPoint);
        }

        void RefreshVisibility()
        {
            if (!Application.IsPlaying(gameObject) || visibility == null) return;
            bool visible = Visible;
            bool interactable = visible && CanInteract;
            float alpha = visible ? 1 : 0;
            if (visibility.alpha != alpha) visibility.alpha = alpha;
            if (visibility.blocksRaycasts != interactable) visibility.blocksRaycasts = interactable;
            if (visibility.interactable != interactable) visibility.interactable = interactable;
            if (!visible) pointerGuard.Release();
        }

        void OnDisable()
        {
            pointerGuard.Release();
            if (registeredGame != null && registeredGame.AuthoredHud == this) registeredGame.AuthoredHud = null;
            registeredGame = null;
            if (widgets != null)
                foreach (var widget in widgets)
                    if (widget != null) widget.Unbind(this);
            if (!Application.IsPlaying(gameObject)) return;
            if (visibility != null)
            {
                visibility.alpha = 0;
                visibility.blocksRaycasts = false;
                visibility.interactable = false;
            }
        }
    }
}
