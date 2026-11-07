using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace IceResurfacer.Presentation
{
    /// <summary>Saved service/status/messages presentation of the existing dashboard and service controller.</summary>
    [DisallowMultipleComponent]
    public sealed class IceOperatorOverlay : MonoBehaviour
    {
        public IceResurfacerGame game;
        public Canvas canvas;
        public GraphicRaycaster raycaster;
        public CanvasGroup serviceGroup, statusGroup, historyGroup, performanceGroup;
        public IceServiceReadout[] serviceFields;
        public IceOperatorMessage routine, critical, driveWarning, hint;
        public IceGpsProjectedGraphic gpsProjection;
        public TMP_Text status, performance, historyAdvice, historyEmpty, routineToggleCaption;
        public Transform historyContent;
        public IceMessageHistoryRow historyRowPrefab;
        public GameObject previewBadge, historyPreviewRows;
        public IceOperatorAction[] actions;
        [Min(.02f)] public float refreshInterval = .1f;
        readonly List<IceMessageHistoryRow> rows = new List<IceMessageHistoryRow>(32);
        readonly IceUIPointerGuard pointer = new IceUIPointerGuard();
        float nextRefresh;
        int historyRevision = -1, dispatchedFrame = -1;
        bool observedBase, observedFoot, observedHistory, observedFocus;
        string routineText = "", observedNotice;
        public int RuntimeRowCount => rows.Count;
        public int ContentRefreshCount { get; private set; }
        public bool IsAvailable => isActiveAndEnabled && game != null && game.isActiveAndEnabled &&
            serviceGroup != null && statusGroup != null && historyGroup != null && performanceGroup != null &&
            serviceFields != null && serviceFields.Length > 0 && routine != null && critical != null && driveWarning != null && hint != null &&
            historyRowPrefab != null && historyContent != null && actions != null && gpsProjection != null &&
            (!Application.IsPlaying(gameObject) || game.AuthoredOperatorUI == this && IceNavigationUI.Ready(canvas, raycaster));
        public bool Visible => IsAvailable && game.Sim != null && game.Started && !game.Sim.Finished && !game.Paused && !game.HelpVisible && !game.HudHidden &&
            (game.RouteGuide == null || !game.RouteGuide.PanelOpen);
        public bool DrivingVisible => Visible && !game.Sim.OnFoot;
        public bool ServiceVisible => Visible && game.Sim.OnFoot;
        bool Interactive => Visible && game.HasFocus && (game.Sim.OnFoot || IceNavigationUI.Pointer(game));
        void OnEnable()
        {
            if (!Application.IsPlaying(gameObject)) return;
            if (game != null && (game.AuthoredOperatorUI == null || game.AuthoredOperatorUI == this)) game.AuthoredOperatorUI = this;
            if (actions != null) foreach (var action in actions) if (action != null) action.Bind(this);
            if (previewBadge != null) previewBadge.SetActive(false);
            if (historyPreviewRows != null) historyPreviewRows.SetActive(false);
            nextRefresh = 0; historyRevision = -1; SyncVisibility();
        }
        void LateUpdate()
        {
            if (!Application.IsPlaying(gameObject)) return;
            SyncVisibility(); if (Visible && Time.unscaledTime >= nextRefresh) RefreshNow();
        }
        public void SyncVisibility()
        {
            if (!Application.IsPlaying(gameObject)) return;
            // Service only asks for pointer ownership on mouse-down. Observe an
            // intervening release even if no full world-picking query ran then.
            if (Mouse.current == null || !Mouse.current.added || !Mouse.current.leftButton.isPressed) pointer.Release();
            bool visible = Visible, foot = visible && game.Sim.OnFoot, history = visible && !foot && game.Dashboard.HistoryVisible;
            bool focus = game != null && game.HasFocus;
            if (visible != observedBase || foot != observedFoot || history != observedHistory || !focus && observedFocus)
            {
                if (actions != null) foreach (var action in actions) if (action != null) action.GetComponent<IceUIClickGuard>()?.Cancel();
                pointer.Release(); IceNavigationUI.ClearFocus(transform); nextRefresh = 0;
            }
            observedBase = visible; observedFoot = foot; observedHistory = history; observedFocus = focus;
            bool interactive = visible && Interactive;
            IceNavigationUI.Group(serviceGroup, foot, interactive);
            IceNavigationUI.Group(statusGroup, visible && !foot, interactive);
            IceNavigationUI.Group(historyGroup, history, interactive);
            IceNavigationUI.Group(performanceGroup, visible && game.PerformanceVisible, interactive);
            string alert = visible && !foot ? IceDashboard.CriticalMessage(game.Sim) : "";
            bool serious = !string.IsNullOrEmpty(alert);
            float age = visible ? game.Dashboard.NoticeAge : 8;
            bool showRoutine = visible && !foot && !serious && game.Dashboard.MessagesVisible && !string.IsNullOrEmpty(game.Notice) && age < 8;
            if (visible && observedNotice != game.Notice)
            {
                observedNotice = game.Notice;
                routineText = string.IsNullOrEmpty(observedNotice) ? "" : observedNotice.Length > 142 ? observedNotice.Substring(0, 139) + "..." : observedNotice;
            }
            routine?.Present(routineText, showRoutine, interactive, Mathf.Clamp01((8 - age) / Mathf.Max(.01f, routine.fadeSeconds)));
            critical?.Present(alert, visible && !foot && serious, interactive);
            string reason = visible && !foot ? game.Sim.DriveInhibitReason : "";
            driveWarning?.Present(reason, visible && !foot && Mathf.Abs(game.RawThrottle) >= .01f && !string.IsNullOrEmpty(reason), interactive);
            string hover = visible && game.PhysicalControls != null ? game.PhysicalControls.Hint : "";
            hint?.Present(hover, visible && !string.IsNullOrEmpty(hover), interactive);
        }
        public void RefreshNow()
        {
            if (!Application.IsPlaying(gameObject) || game == null || game.Sim == null || game.Dashboard == null) return;
            nextRefresh = Time.unscaledTime + Mathf.Max(.02f, refreshInterval);
            game.Dashboard.ObserveMessages(game); SyncVisibility(); if (!Visible) return;
            ContentRefreshCount++;
            if (ServiceVisible)
            {
                if (serviceFields != null) foreach (var field in serviceFields) if (field != null) field.Refresh(game);
            }
            else
            {
                var guide = game.RouteGuide;
                IceNavigationUI.Text(status, guide != null && guide.Recorder.Recording ? "RECORDING / " + guide.Recorder.Data.distanceMeters.ToString("F1") + " m / NUM 9 SAVE" :
                    guide != null && guide.Visible ? "NUM 7 GPS / " + guide.Phase : "Q edge / E controls / C chase / L Shift rear | NUM 7 GPS");
                if (game.Dashboard.HistoryVisible)
                {
                    IceNavigationUI.Text(historyAdvice, IceDashboard.HistoryAdvice(game.Sim));
                    IceNavigationUI.Text(routineToggleCaption, game.Dashboard.MessagesVisible ? "Routine messages: on" : "Routine messages: off");
                    RefreshHistory();
                }
            }
            if (game.PerformanceVisible && game.Performance != null) IceNavigationUI.Text(performance, game.Performance.Summary);
        }
        void RefreshHistory()
        {
            var dashboard = game.Dashboard; if (historyRevision == dashboard.MessageRevision) return;
            historyRevision = dashboard.MessageRevision; var messages = dashboard.MessageHistory; int count = messages.Count;
            while (rows.Count < count) rows.Add(Instantiate(historyRowPrefab, historyContent, false));
            for (int i = 0; i < rows.Count; i++)
            {
                bool active = i < count; if (rows[i].gameObject.activeSelf != active) rows[i].gameObject.SetActive(active);
                if (active) rows[i].Bind(messages[count - 1 - i]);
            }
            if (historyEmpty != null && historyEmpty.gameObject.activeSelf != (count == 0)) historyEmpty.gameObject.SetActive(count == 0);
        }
        public bool ContainsPointer(Vector2 point)
        { SyncVisibility(); return Visible && pointer.ContainsPointer(raycaster, point); }
        public bool CanUse(IceOperatorAction action) => Application.IsPlaying(gameObject) && action != null && action.owner == this && DrivingVisible && Interactive &&
            action.isActiveAndEnabled && action.button != null && action.button.isActiveAndEnabled && action.button.IsInteractable() &&
            ((action.kind != IceOperatorActionKind.CloseHistory && action.kind != IceOperatorActionKind.RoutineMessages) || game.Dashboard.HistoryVisible);
        public void Activate(IceOperatorAction action)
        {
            if (!CanUse(action) || dispatchedFrame == Time.frameCount) return; dispatchedFrame = Time.frameCount;
            game.Dashboard.ObserveMessages(game);
            switch (action.kind)
            {
                case IceOperatorActionKind.Routes: game.RouteGuide?.ToggleRecordingPanel(); break;
                case IceOperatorActionKind.History: game.Dashboard.ToggleHistory(); break;
                case IceOperatorActionKind.CloseHistory: if (game.Dashboard.HistoryVisible) game.Dashboard.ToggleHistory(); break;
                case IceOperatorActionKind.RoutineMessages: game.Dashboard.ToggleRoutineMessages(); break;
                case IceOperatorActionKind.Help: game.ToggleHelp(); break;
                case IceOperatorActionKind.Pause: game.TogglePause(); break;
            }
            RefreshNow();
        }
        void OnDisable()
        {
            if (!Application.IsPlaying(gameObject)) return;
            pointer.Release(); IceNavigationUI.ClearFocus(transform);
            if (actions != null) foreach (var action in actions) if (action != null) { action.GetComponent<IceUIClickGuard>()?.Cancel(); action.Unbind(); }
            if (game != null && game.AuthoredOperatorUI == this) game.AuthoredOperatorUI = null;
            IceNavigationUI.Group(serviceGroup, false, false); IceNavigationUI.Group(statusGroup, false, false); IceNavigationUI.Group(historyGroup, false, false); IceNavigationUI.Group(performanceGroup, false, false);
            routine?.Present("", false, false); critical?.Present("", false, false); driveWarning?.Present("", false, false); hint?.Present("", false, false);
        }
    }
}
