using IceResurfacer.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    [DisallowMultipleComponent]
    public sealed class IceSurfaceSurvey : MonoBehaviour
    {
        public IceResurfacerGame game;
        public Canvas canvas;
        public GraphicRaycaster raycaster;
        public CanvasGroup visibility, body, patch;
        public IceSurveyMap map;
        public IceSurveyOverlay overlays;
        public RawImage patchImage;
        public RectTransform selectedCell;
        public TMP_Text coverage, quality, mode, status, patchDetail, patchScale, collapseCaption;
        public GameObject treatmentLegend, previewBadge;
        public Button collapseButton, closePatchButton;
        [Min(.02f)] public float refreshInterval = .1f;
        public int InspectedCell { get; private set; } = -1;
        bool pinned, collapsed;
        float nextRefresh;
        Simulation observedSimulation;
        public bool Collapsed => collapsed;
        public bool IsAvailable => isActiveAndEnabled && game != null && game.isActiveAndEnabled && visibility != null && body != null &&
            map != null && map.owner == this && collapseButton != null && (!Application.IsPlaying(gameObject) || game.AuthoredSurvey == this && IceNavigationUI.Ready(canvas, raycaster));
        public bool Visible => IsAvailable && IceNavigationUI.Driving(game) && string.IsNullOrEmpty(game.HeldQuicklook);
        public bool CanInteract => Visible && IceNavigationUI.Pointer(game);
        void OnEnable()
        {
            if (!Application.IsPlaying(gameObject)) return;
            if (game != null && (game.AuthoredSurvey == null || game.AuthoredSurvey == this)) game.AuthoredSurvey = this;
            if (previewBadge != null) previewBadge.SetActive(false);
            if (collapseButton != null) collapseButton.onClick.AddListener(ToggleCollapsed);
            if (closePatchButton != null) closePatchButton.onClick.AddListener(ClosePatch);
            ClosePatch(); SyncVisibility();
        }
        void LateUpdate()
        {
            if (!Application.IsPlaying(gameObject)) return;
            SyncVisibility();
            if (Visible && Time.unscaledTime >= nextRefresh) RefreshNow();
        }
        public void SyncVisibility()
        {
            IceNavigationUI.Group(visibility, Visible, CanInteract);
            IceNavigationUI.Group(body, Visible && !collapsed, CanInteract);
            IceNavigationUI.Group(patch, Visible && !collapsed && InspectedCell >= 0, CanInteract);
            if (!Visible || !CanInteract) { pinned = false; InspectedCell = -1; }
        }
        public void ToggleCollapsed()
        {
            if (!CanInteract) return;
            collapsed = !collapsed; game.Dashboard.SetSurveyCollapsed(collapsed); ClosePatch();
            IceNavigationUI.Text(collapseCaption, collapsed ? "MAP +" : "MAP −"); SyncVisibility();
        }
        public void Inspect(Vector2 screen, Camera camera, bool pin)
        {
            if (!CanInteract || collapsed || pinned && !pin) return;
            if (!map.TryCell(screen, camera, out int cell)) { if (!pinned) ClosePatch(); return; }
            bool changed = InspectedCell != cell; InspectedCell = cell; if (pin) pinned = true;
            if (changed) RefreshPatch(); IceNavigationUI.Group(patch, true, true);
        }
        public void EndHover() { if (!pinned) ClosePatch(); }
        public void ClosePatch() { pinned = false; InspectedCell = -1; IceNavigationUI.Group(patch, false, false); }
        public void RefreshNow()
        {
            if (!Application.IsPlaying(gameObject) || game == null || game.Sim == null) return;
            if (observedSimulation != game.Sim) { observedSimulation = game.Sim; ClosePatch(); }
            nextRefresh = Time.unscaledTime + Mathf.Max(.02f, refreshInterval);
            var surface = game.Surface; var report = game.Report;
            if (surface != null && map != null)
            {
                Texture texture = surface.DebugMode == 0 ? surface.OverviewTexture : surface.MapTexture;
                if (map.texture != texture) map.texture = texture;
                int debug = surface.DebugMode;
                if (treatmentLegend != null && treatmentLegend.activeSelf != (debug == 0)) treatmentLegend.SetActive(debug == 0);
                IceNavigationUI.Text(mode, debug == 0 ? "" : debug == 1 ? "F2 / SHAVING MASK\nOrange: excluded rim\nGreen: treatment" : "F2 / MATERIAL\nRed cut\nGreen wash\nBlue flood");
            }
            IceNavigationUI.Text(coverage, (report.Coverage * 100).ToString("0.0") + "%");
            IceNavigationUI.Text(quality, "Finish " + (report.FinishQuality * 100).ToString("0") + "%");
            var guide = game.RouteGuide;
            IceNavigationUI.Text(status, guide != null && guide.GuidanceDisplayed && guide.Progress != null ? "GPS LEG " + (guide.Progress.Leg + 1) + " / 80 ft" : "Hover: exact cells / click to pin");
            if (InspectedCell >= 0) RefreshPatch();
            if (overlays != null && Visible && !collapsed) overlays.SetVerticesDirty();
        }
        void RefreshPatch()
        {
            if (game == null || game.Sim == null || game.Surface == null) return;
            var field = game.Sim.Ice;
            if (InspectedCell < 0 || InspectedCell >= field.Cells.Length) { ClosePatch(); return; }
            var cell = field.Cells[InspectedCell];
            IceNavigationUI.Text(patchDetail, IceCoverage.Label(IceCoverage.Classify(cell, game.Sim.Config)) + "  /  cell " + InspectedCell +
                "\nCut " + cell.CutDepthMm.ToString("0.0000") + " mm (>" + IceCoverage.RequiredCutMm(game.Sim.Config).ToString("0.0000") + ")" +
                "\nWater " + cell.FloodMm.ToString("0.00") + " mm (>0.08) / wash " + cell.WashLiters.ToString("0.00") + " L" +
                "\nSnow " + cell.SnowMm.ToString("0.00") + " mm (<0.25) / groove " + cell.GougeDepthMm.ToString("0.00") + " mm");
            int cols = Mathf.Min(16, field.Columns), rows = Mathf.Min(16, field.Rows);
            int col = InspectedCell % field.Columns, row = InspectedCell / field.Columns;
            int left = Mathf.Clamp(col - cols / 2, 0, field.Columns - cols), bottom = Mathf.Clamp(row - rows / 2, 0, field.Rows - rows);
            if (patchImage != null)
            {
                if (patchImage.texture != game.Surface.MapTexture) patchImage.texture = game.Surface.MapTexture;
                Rect uv = new Rect((float)left / field.Columns, (float)bottom / field.Rows, (float)cols / field.Columns, (float)rows / field.Rows);
                if (patchImage.uvRect != uv) patchImage.uvRect = uv;
            }
            if (selectedCell != null)
            {
                selectedCell.anchorMin = new Vector2((float)(col - left) / cols, (float)(row - bottom) / rows);
                selectedCell.anchorMax = selectedCell.anchorMin + new Vector2(1f / cols, 1f / rows);
                selectedCell.offsetMin = selectedCell.offsetMax = Vector2.zero;
            }
            IceNavigationUI.Text(patchScale, (cols * field.CellSize).ToString("0.0") + " m patch / " + (field.CellSize * 100).ToString("0") + " cm samples");
        }
        void OnDisable()
        {
            if (collapseButton != null) collapseButton.onClick.RemoveListener(ToggleCollapsed);
            if (closePatchButton != null) closePatchButton.onClick.RemoveListener(ClosePatch);
            if (game != null && game.AuthoredSurvey == this) game.AuthoredSurvey = null;
            if (!Application.IsPlaying(gameObject)) return;
            ClosePatch(); IceNavigationUI.Group(visibility, false, false);
        }
    }
}
