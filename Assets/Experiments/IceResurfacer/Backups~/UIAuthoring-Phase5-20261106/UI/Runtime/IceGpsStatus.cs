using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    [DisallowMultipleComponent]
    public sealed class IceGpsStatus : MonoBehaviour
    {
        public IceResurfacerGame game;
        public Canvas canvas;
        public GraphicRaycaster raycaster;
        public CanvasGroup visibility;
        public TMP_Text routeName, leg, instruction, deviation, progress, warning, toggleCaption, recordingStatus;
        public Image progressFill;
        public Button toggleButton, routesButton;
        public GameObject previewBadge;
        [Min(.02f)] public float refreshInterval = .1f;
        float nextRefresh;
        public bool IsAvailable => isActiveAndEnabled && game != null && game.isActiveAndEnabled && visibility != null && routeName != null &&
            instruction != null && (!Application.IsPlaying(gameObject) || game.AuthoredGps == this && IceNavigationUI.Ready(canvas, raycaster));
        public bool Visible => IsAvailable && IceNavigationUI.Driving(game) && (string.IsNullOrEmpty(game.HeldQuicklook) || game.ChaseActive);
        void OnEnable()
        {
            if (!Application.IsPlaying(gameObject)) return;
            if (game != null && (game.AuthoredGps == null || game.AuthoredGps == this)) game.AuthoredGps = this;
            if (previewBadge != null) previewBadge.SetActive(false);
            if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);
            if (routesButton != null) routesButton.onClick.AddListener(Routes);
            SyncVisibility();
        }
        public void SyncVisibility() => IceNavigationUI.Group(visibility, Visible, Visible && IceNavigationUI.Pointer(game));
        void LateUpdate()
        {
            if (!Application.IsPlaying(gameObject)) return;
            SyncVisibility(); if (Time.unscaledTime >= nextRefresh) RefreshNow();
        }
        void Toggle() { if (Visible && IceNavigationUI.Pointer(game)) { game.RouteGuide?.Toggle(); RefreshNow(); } }
        void Routes() { if (Visible && IceNavigationUI.Pointer(game)) game.RouteGuide?.ToggleRecordingPanel(); }
        public void RefreshNow()
        {
            if (!Application.IsPlaying(gameObject) || game == null || game.Sim == null || game.RouteGuide == null) return;
            nextRefresh = Time.unscaledTime + Mathf.Max(.02f, refreshInterval);
            var guide = game.RouteGuide; var fit = guide.ActiveFit; var cursor = guide.Progress;
            IceNavigationUI.Text(routeName, guide.ActiveRouteName);
            IceNavigationUI.Text(toggleCaption, guide.Visible ? "Hide GPS [Num 7]" : "Show GPS [Num 7]");
            IceNavigationUI.Text(warning, guide.TrainingStatus);
            IceNavigationUI.Text(recordingStatus, guide.Recorder.Recording ? "Recording / Numpad 9 stops and saves" : "");
            bool live = guide.Visible && fit != null && cursor != null;
            if (!live)
            {
                IceNavigationUI.Text(leg, guide.Visible ? guide.Phase : "GPS HIDDEN");
                IceNavigationUI.Text(instruction, guide.Available ? "Show GPS to follow the selected route" : "Route calibration unavailable");
                IceNavigationUI.Text(deviation, ""); IceNavigationUI.Text(progress, "");
                if (progressFill != null && progressFill.fillAmount != 0) progressFill.fillAmount = 0; return;
            }
            int turn = cursor.UpcomingTurn(IceRouteGuide.LookAheadMeters);
            string cue = turn < 0 ? "FOLLOW CURRENT PASS" : (fit.samples[turn].curvature > 0 ? "CURVE RIGHT" : "CURVE LEFT") +
                " / " + Mathf.Max(0, (float)(fit.samples[turn].distance - cursor.Distance)).ToString("F0") + " m";
            if (cursor.OffRoute) cue = "JOIN ACTIVE ROUTE / NUM 8";
            float pace = 1.5f; int a = fit.IndexAt(cursor.Distance), b = fit.IndexAt(Math.Min(fit.Length, cursor.Distance + 6));
            for (int i = a; i <= b; i++) pace = Mathf.Min(pace, fit.samples[i].safeSpeed);
            if (!cursor.OffRoute) cue += " / " + (pace < .12f ? "CREEP" : (pace * 3.6f).ToString("F1") + " km/h");
            IceNavigationUI.Text(leg, "LEG " + (cursor.Leg + 1) + " / " + fit.legs.Count + " " + (fit.legs[cursor.Leg].direction < 0 ? "REVERSE" : "FORWARD"));
            IceNavigationUI.Text(instruction, cue);
            var intended = fit.At(cursor.Distance); float dx = game.Sim.Conditioner.X - intended.conditionerX, dz = game.Sim.Conditioner.Z - intended.conditionerZ;
            IceNavigationUI.Text(deviation, "Offset " + cursor.LateralError.ToString("F2") + " m / conditioner " + Mathf.Sqrt(dx * dx + dz * dz).ToString("F2") + " m / advisory");
            IceNavigationUI.Text(progress, cursor.Distance.ToString("F1") + " / " + fit.Length.ToString("F1") + " m — " + cursor.Status);
            if (progressFill != null)
            { float value = fit.Length > 0 ? Mathf.Clamp01((float)(cursor.Distance / fit.Length)) : 0; if (progressFill.fillAmount != value) progressFill.fillAmount = value; }
        }
        void OnDisable()
        {
            if (toggleButton != null) toggleButton.onClick.RemoveListener(Toggle);
            if (routesButton != null) routesButton.onClick.RemoveListener(Routes);
            if (game != null && game.AuthoredGps == this) game.AuthoredGps = null;
            if (Application.IsPlaying(gameObject)) IceNavigationUI.Group(visibility, false, false);
        }
    }
}
