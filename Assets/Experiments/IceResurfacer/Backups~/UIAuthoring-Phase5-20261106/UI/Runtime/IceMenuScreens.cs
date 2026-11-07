using IceResurfacer.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    /// <summary>Presentation of existing game flags, not a second session state machine.</summary>
    [DisallowMultipleComponent]
    public sealed class IceMenuScreens : MonoBehaviour
    {
        public IceResurfacerGame game;
        public Canvas canvas;
        public GraphicRaycaster raycaster;
        public IceMenuScreen welcome, pause, help, results;
        public IceMenuAction[] actions;
        public IceMenuSetting[] settings;
        public IceResultWidget[] resultWidgets;
        IceMenuScreen observedScreen;
        EventSystem ownedEvents;
        bool oldNavigation, ownsNavigation, observedFocus;
        int dispatchedFrame = -1;
        RunReport observedReport;
        bool haveReport;
        public int ResultRefreshCount { get; private set; }
        public bool IsAvailable => isActiveAndEnabled && game != null && game.isActiveAndEnabled &&
            ScreenReady(welcome, IceMenuScreenKind.Welcome) && ScreenReady(pause, IceMenuScreenKind.Pause) &&
            ScreenReady(help, IceMenuScreenKind.Help) && ScreenReady(results, IceMenuScreenKind.Results) &&
            actions != null && actions.Length > 0 && settings != null && resultWidgets != null && resultWidgets.Length > 0 &&
            (!Application.IsPlaying(gameObject) || game.AuthoredMenus == this && IceNavigationUI.Ready(canvas, raycaster));
        bool ScreenReady(IceMenuScreen screen, IceMenuScreenKind kind) => screen != null && screen.isActiveAndEnabled && screen.owner == this && screen.kind == kind && screen.visibility != null;
        public IceMenuScreen CurrentScreen
        {
            get
            {
                if (!IsAvailable || game.Sim == null || game.RouteGuide != null && game.RouteGuide.PanelOpen) return null;
                if (game.HelpVisible) return help;
                if (!game.HudHidden && game.Sim.Finished) return results;
                if (!game.HudHidden && !game.Started) return welcome;
                return game.Paused ? pause : null;
            }
        }
        public bool ModalVisible => CurrentScreen != null;
        void OnEnable()
        {
            if (!Application.IsPlaying(gameObject)) return;
            if (game != null && (game.AuthoredMenus == null || game.AuthoredMenus == this)) game.AuthoredMenus = this;
            if (actions != null) foreach (var action in actions) if (action != null) action.Bind(this);
            if (settings != null) foreach (var setting in settings) if (setting != null) setting.Bind(this);
            foreach (var screen in new[] { welcome, pause, help, results }) if (screen != null && screen.previewBadge != null) screen.previewBadge.SetActive(false);
            RefreshState();
        }
        void Update()
        {
            if (!Application.IsPlaying(gameObject)) return;
            RefreshState();
            if (ModalVisible && game.HasFocus && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                MoveFocus(Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed ? -1 : 1);
        }
        void LateUpdate() { if (Application.IsPlaying(gameObject)) RefreshState(); }
        public void RefreshState()
        {
            if (!Application.IsPlaying(gameObject)) return;
            var current = CurrentScreen;
            bool changed = current != observedScreen, focusChanged = game != null && observedFocus != game.HasFocus;
            if (changed || focusChanged && !game.HasFocus)
            {
                RememberSelection(); CancelInteractions();
                if (observedScreen != null) IceNavigationUI.ClearFocus(observedScreen.transform);
                observedScreen = current;
            }
            SetScreen(welcome, current); SetScreen(pause, current); SetScreen(help, current); SetScreen(results, current);
            if (current != null && game.HasFocus)
            {
                OwnNavigation();
                var selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
                if (changed || focusChanged || selected == null || !selected.transform.IsChildOf(current.transform)) SelectInitial(current);
                if (current == pause && settings != null) foreach (var setting in settings) if (setting != null) setting.Refresh();
                if (current == results && (changed || !haveReport || !SameReport(observedReport, game.Report))) RefreshResults();
            }
            else RestoreNavigation();
            if (game != null) observedFocus = game.HasFocus;
        }
        void SetScreen(IceMenuScreen screen, IceMenuScreen current)
        { if (screen != null) IceNavigationUI.Group(screen.visibility, screen == current, screen == current && game.HasFocus); }
        void RememberSelection()
        {
            var events = EventSystem.current;
            if (observedScreen != null && events != null && events.currentSelectedGameObject != null && events.currentSelectedGameObject.transform.IsChildOf(observedScreen.transform))
                observedScreen.lastSelection = events.currentSelectedGameObject;
        }
        void OwnNavigation()
        {
            var events = EventSystem.current; if (events == null) return;
            if (!ownsNavigation || ownedEvents != events)
            { RestoreNavigation(); ownedEvents = events; oldNavigation = events.sendNavigationEvents; ownsNavigation = true; }
            if (!events.sendNavigationEvents) events.sendNavigationEvents = true;
        }
        void RestoreNavigation()
        { if (ownsNavigation && ownedEvents != null) ownedEvents.sendNavigationEvents = oldNavigation; ownsNavigation = false; ownedEvents = null; }
        void SelectInitial(IceMenuScreen screen)
        {
            var events = EventSystem.current; if (events == null) return;
            var selected = screen.lastSelection != null ? screen.lastSelection.GetComponent<Selectable>() : screen.firstSelection;
            if (selected == null || !selected.IsActive() || !selected.IsInteractable()) selected = screen.firstSelection;
            if (selected != null && selected.IsActive() && selected.IsInteractable()) events.SetSelectedGameObject(selected.gameObject);
        }
        public void MoveFocus(int direction)
        {
            var screen = CurrentScreen; var events = EventSystem.current;
            if (screen == null || events == null || screen.navigationOrder == null || screen.navigationOrder.Length == 0) return;
            int index = -1; for (int i = 0; i < screen.navigationOrder.Length; i++) if (screen.navigationOrder[i] != null && screen.navigationOrder[i].gameObject == events.currentSelectedGameObject) index = i;
            for (int n = 0; n < screen.navigationOrder.Length; n++)
            {
                index = (index + direction + screen.navigationOrder.Length) % screen.navigationOrder.Length;
                var next = screen.navigationOrder[index]; if (next == null || !next.IsActive() || !next.IsInteractable()) continue;
                events.SetSelectedGameObject(next.gameObject); break;
            }
        }
        public bool CanUse(IceMenuAction action) => Application.IsPlaying(gameObject) && action != null && action.owner == this &&
            action.screen == CurrentScreen && CurrentScreen != null && game.HasFocus && action.isActiveAndEnabled && action.button != null && action.button.IsInteractable();
        public bool CanUse(IceMenuSetting setting) => Application.IsPlaying(gameObject) && setting != null && setting.owner == this &&
            setting.screen == CurrentScreen && CurrentScreen == pause && game.HasFocus && setting.isActiveAndEnabled && setting.slider != null && setting.slider.IsInteractable();
        public void Activate(IceMenuAction action)
        {
            if (!CanUse(action) || dispatchedFrame == Time.frameCount) return;
            dispatchedFrame = Time.frameCount;
            switch (action.kind)
            {
                case IceMenuActionKind.StartScored: game.NewSession(false); break;
                case IceMenuActionKind.StartPractice: game.NewSession(true); break;
                case IceMenuActionKind.StartTraining: game.NewSession(true, true, Core.SimulationMode.GuidedTraining); break;
                case IceMenuActionKind.ToggleHelp: game.ToggleHelp(); break;
                case IceMenuActionKind.Resume: if (game.Paused) game.TogglePause(); break;
                case IceMenuActionKind.NewSessionSheet: game.NewSession(false, false); break;
                case IceMenuActionKind.RefillPractice: game.StartServicePractice(); break;
            }
            RefreshState();
        }
        public void RefreshResults()
        {
            if (game == null || game.Sim == null) return;
            observedReport = game.Report; haveReport = true; ResultRefreshCount++;
            if (resultWidgets != null) foreach (var widget in resultWidgets) if (widget != null) widget.Refresh(in observedReport, game.Sim.GuidedTraining);
        }
        // RunReport is a struct: explicit comparison avoids boxing/hash allocations on every frame.
        static bool SameReport(in RunReport a, in RunReport b) => a.Coverage == b.Coverage && a.FinishQuality == b.FinishQuality && a.OverwaterFraction == b.OverwaterFraction &&
            a.MissedFraction == b.MissedFraction && a.Score == b.Score && a.ElapsedSeconds == b.ElapsedSeconds && a.SelectedMaxForwardSpeedMph == b.SelectedMaxForwardSpeedMph &&
            a.OriginalMaxForwardSpeedMph == b.OriginalMaxForwardSpeedMph && a.ShavingCoverage == b.ShavingCoverage && a.WaterCoverage == b.WaterCoverage && a.WaterDistribution == b.WaterDistribution &&
            a.OperationalEfficiency == b.OperationalEfficiency && a.CoveragePoints == b.CoveragePoints && a.FinishPoints == b.FinishPoints && a.WaterPoints == b.WaterPoints &&
            a.OverwaterPoints == b.OverwaterPoints && a.EfficiencyPoints == b.EfficiencyPoints && a.HandlingPoints == b.HandlingPoints && a.MisusePenalty == b.MisusePenalty &&
            a.Mistakes == b.Mistakes && a.TankDumps == b.TankDumps && a.ProperExit == b.ProperExit && a.Complete == b.Complete && a.Practice == b.Practice;
        public void CancelInteractions()
        {
            foreach (var guard in GetComponentsInChildren<IceUIClickGuard>(true)) guard.Cancel();
            if (settings != null) foreach (var setting in settings) if (setting != null) setting.CancelPointer();
        }
        void OnApplicationFocus(bool focused) { if (!focused) CancelInteractions(); }
        void OnApplicationPause(bool paused) { if (paused) CancelInteractions(); }
        void OnDisable()
        {
            if (!Application.IsPlaying(gameObject)) return;
            CancelInteractions(); if (observedScreen != null) IceNavigationUI.ClearFocus(observedScreen.transform);
            observedScreen = null; RestoreNavigation();
            if (actions != null) foreach (var action in actions) if (action != null) action.Unbind();
            if (settings != null) foreach (var setting in settings) if (setting != null) setting.Unbind();
            if (game != null && game.AuthoredMenus == this) game.AuthoredMenus = null;
            SetScreen(welcome, null); SetScreen(pause, null); SetScreen(help, null); SetScreen(results, null);
        }
    }
}
