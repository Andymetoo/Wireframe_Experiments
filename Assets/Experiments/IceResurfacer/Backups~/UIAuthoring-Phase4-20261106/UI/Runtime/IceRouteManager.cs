using System;
using System.Collections.Generic;
using System.IO;
using IceResurfacer.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    /// <summary>Persistent library/recording/guides presentation over the unchanged route commands and storage.</summary>
    [DisallowMultipleComponent]
    public sealed class IceRouteManager : MonoBehaviour
    {
        public IceResurfacerGame game;
        public Canvas canvas;
        public GraphicRaycaster raycaster;
        public CanvasGroup visibility, confirmationGroup, mainGroup;
        public TMP_InputField recordingName, renameInput;
        public TMP_Text activeRoute, status, metadata, sampleDetail, guideDetail, confirmationMessage, tabCaption, previewCaption;
        public RawImage preview;
        [Header("Editable raw-preview annotations")]
        public RectTransform vehicleSampleMarker, conditionerSampleMarker, startAnnotation, endAnnotation;
        public TMP_Text sampleAnnotation;
        public Slider sampleSlider, legSlider, opacitySlider;
        public TMP_Dropdown layerDropdown;
        public Transform listContent;
        public IceRouteRow rowPrefab;
        public GameObject previewRows, previewBadge;
        public IceRouteAction[] actions;
        [Min(.05f)] public float refreshInterval = .1f;
        readonly RouteLibraryCatalog catalog = new RouteLibraryCatalog();
        readonly List<IceRouteRow> rows = new List<IceRouteRow>();
        readonly RouteRecordingPreview rawPreview = new RouteRecordingPreview();
        readonly TrainingGuideView guidePreview = new TrainingGuideView();
        public string SelectedPath { get; private set; }
        public int RuntimeRowCount => rows.Count;
        public int CatalogCount => catalog.entries.Count;
        public bool ConfirmationOpen => confirmation.Length > 0;
        public bool TextEditing => Visible && (ConfirmationOpen || HasInputFocus(recordingName) || HasInputFocus(renameInput));
        static bool HasInputFocus(TMP_InputField input) => input != null && (input.isFocused || EventSystem.current != null && EventSystem.current.currentSelectedGameObject == input.gameObject);
        string confirmation = "", confirmationPath, confirmationIdentity, message = "Choose a saved route, then Use as GPS.", observedSavedPath;
        bool wasVisible, dirty = true;
        int observedTab = -1, revision, lastActionFrame = -1;
        IceRouteActionKind lastAction;
        float nextRefresh, nextPreview, saveGuardUntil;
        RouteLibraryCatalog.Entry fittedEntry;
        RouteFit fittedPreview;
        TrainingRouteDefinition previewDefinition;
        RecordedRouteData previewGuideRaw;
        RecordedRouteData displayedRaw;
        public bool IsAvailable => isActiveAndEnabled && game != null && game.isActiveAndEnabled && visibility != null && mainGroup != null &&
            recordingName != null && listContent != null && rowPrefab != null && rowPrefab.button != null && preview != null && actions != null && actions.Length > 0 &&
            (!Application.IsPlaying(gameObject) || game.AuthoredRoutes == this && IceNavigationUI.Ready(canvas, raycaster));
        public bool Visible => IsAvailable && game.RouteGuide != null && game.RouteGuide.PanelOpen && !game.HudHidden;
        public bool CanInteract => Visible && game.HasFocus;
        IceRouteGuide Guide => game.RouteGuide;
        // Existing panel state remains authoritative, including calls from Editor menus and other legacy UI.
        int Tab => !Guide.AdvancedPanel ? 0 : Guide.TrainingPanelOpen ? 2 : 1;
        RouteLibraryCatalog.Entry Selected => SelectedPath == null ? null : catalog.entries.Find(e => string.Equals(e.path, SelectedPath, StringComparison.Ordinal));
        void OnEnable()
        {
            if (!Application.IsPlaying(gameObject)) return;
            if (game != null && (game.AuthoredRoutes == null || game.AuthoredRoutes == this)) game.AuthoredRoutes = this;
            if (previewBadge != null) previewBadge.SetActive(false);
            if (previewRows != null) previewRows.SetActive(false);
            if (actions != null) foreach (var action in actions) if (action != null) action.Bind(this);
            if (recordingName != null) recordingName.onValueChanged.AddListener(SetRecordingName);
            if (sampleSlider != null) sampleSlider.onValueChanged.AddListener(SampleChanged);
            if (legSlider != null) legSlider.onValueChanged.AddListener(LegChanged);
            if (opacitySlider != null) opacitySlider.onValueChanged.AddListener(OpacityChanged);
            if (layerDropdown != null) layerDropdown.onValueChanged.AddListener(LayerChanged);
            SyncVisibility();
        }
        void SetRecordingName(string name) { if (CanInteract) Guide.RecordingName = name; }
        void SampleChanged(float value) { if (CanInteract) RefreshDetails(); }
        void LegChanged(float value) { if (CanInteract) RefreshDetails(); }
        void OpacityChanged(float value) { if (!CanInteract) return; var color = Guide.GpsColor; color.a = value; Guide.GpsColor = color; }
        void LayerChanged(int value) { if (CanInteract) { dirty = true; nextPreview = 0; } }
        public void SyncVisibility()
        {
            bool visible = Visible;
            if (wasVisible && !visible)
            {
                IceNavigationUI.ClearFocus(transform); confirmation = ""; confirmationPath = null;
                foreach (var guard in GetComponentsInChildren<IceUIClickGuard>(true)) guard.Cancel();
                // Cancel selected Button press state before a hidden/reopened panel can receive its release.
                if (actions != null) foreach (var a in actions) if (a != null && a.button != null)
                { a.button.OnPointerExit(new PointerEventData(EventSystem.current)); a.button.OnPointerUp(new PointerEventData(EventSystem.current)); }
            }
            bool opened = !wasVisible && visible;
            if (opened) { RefreshLibrary(); dirty = true; }
            wasVisible = visible;
            IceNavigationUI.Group(visibility, visible, CanInteract);
            IceNavigationUI.Group(mainGroup, visible, CanInteract && !ConfirmationOpen);
            IceNavigationUI.Group(confirmationGroup, visible && ConfirmationOpen, CanInteract);
            if (opened) RefreshNow();
        }
        void LateUpdate()
        {
            if (!Application.IsPlaying(gameObject)) return;
            SyncVisibility(); if (!Visible) return;
            if (observedTab != Tab) { observedTab = Tab; RebindRows(); dirty = true; }
            if (Guide.LastSavedPath != observedSavedPath) { observedSavedPath = Guide.LastSavedPath; RefreshLibrary(); }
            if (Time.unscaledTime >= nextRefresh) RefreshNow();
        }
        public void RefreshLibrary()
        {
            if (!Application.IsPlaying(gameObject) || game == null || game.RouteGuide == null) return;
            try
            {
                catalog.Refresh(Guide.Library, Guide.GuideDirectory);
                // Recovery drafts retain the existing raw-details load workflow, and can never activate as GPS.
                foreach (string path in Guide.Library.ListFiles())
                {
                    if (catalog.entries.Exists(e => e.path == path)) continue;
                    var entry = new RouteLibraryCatalog.Entry { path = path, name = Path.GetFileName(path), date = "Recovery" };
                    try { entry.raw = RouteRecordingStorage.Read(path); entry.name = entry.raw.name; } catch (Exception e) { entry.error = e.Message; }
                    catalog.entries.Add(entry);
                }
                if (SelectedPath != null && Selected == null) { SelectedPath = null; confirmation = ""; confirmationPath = null; }
                RebindRows(); dirty = true;
            }
            catch (Exception e) { message = "Couldn't read saved routes: " + e.Message; }
        }
        void RebindRows()
        {
            if (listContent == null || rowPrefab == null) return;
            int index = 0;
            foreach (var entry in catalog.entries)
            {
                if (Tab == 1 && entry.guide != null || Tab == 2 && entry.guide == null) continue;
                IceRouteRow row;
                if (index < rows.Count) row = rows[index];
                else { row = Instantiate(rowPrefab, listContent); rows.Add(row); }
                row.gameObject.SetActive(true); row.Bind(this, entry); index++;
            }
            for (int i = index; i < rows.Count; i++) { rows[i].Unbind(); rows[i].gameObject.SetActive(false); }
        }
        public bool SelectPath(string path)
        {
            if (!CanInteract || ConfirmationOpen || Guide.Recorder.Recording) return false;
            var entry = catalog.entries.Find(e => e.path == path); if (entry == null) return false;
            SelectedPath = entry.path; dirty = true;
            foreach (var row in rows) row.SetSelected(row.BoundPath == SelectedPath);
            message = entry.Valid ? "Selected: " + entry.name + " — compatibility and safety checked on activation." : entry.error;
            RefreshNow(); return entry.Valid;
        }
        public bool CanActivate(IceRouteActionKind action)
        {
            if (!CanInteract) return false;
            if (ConfirmationOpen) return action == IceRouteActionKind.Confirm || action == IceRouteActionKind.Cancel || action == IceRouteActionKind.Close;
            var selected = Selected; bool valid = selected != null && selected.Valid, recording = Guide.Recorder.Recording;
            switch (action)
            {
                case IceRouteActionKind.Confirm: case IceRouteActionKind.Cancel: return false;
                case IceRouteActionKind.Record: return Guide.CanRecord && !recording;
                case IceRouteActionKind.StopSave: case IceRouteActionKind.Stop: return recording;
                case IceRouteActionKind.SaveCopy: return !recording && (Guide.HasUnsavedRecording || selected == null ?
                    Guide.Recorder.Data != null && Guide.Recorder.Data.complete : valid && selected.raw.complete);
                case IceRouteActionKind.UseGps: return !recording && valid && selected.raw.complete;
                case IceRouteActionKind.PreviewLoad: return !recording && valid;
                case IceRouteActionKind.Edit: case IceRouteActionKind.Rename: case IceRouteActionKind.Delete: return !recording && valid;
                case IceRouteActionKind.Rejoin: return !recording && Guide.ActiveFit != null && Guide.Progress != null && Guide.ActiveFit.legs.Count > 0;
                case IceRouteActionKind.Original: case IceRouteActionKind.Fixture: return !recording;
                default: return true;
            }
        }
        public void Activate(IceRouteActionKind action)
        {
            if (!Application.IsPlaying(gameObject) || !CanActivate(action)) return;
            if (lastActionFrame == Time.frameCount && lastAction == action) return;
            bool save = action == IceRouteActionKind.StopSave || action == IceRouteActionKind.SaveCopy || action == IceRouteActionKind.Confirm;
            if (save && Time.unscaledTime < saveGuardUntil) return;
            if (save) saveGuardUntil = Time.unscaledTime + .4f;
            lastActionFrame = Time.frameCount; lastAction = action;
            try
            {
                var entry = Selected;
                switch (action)
                {
                    case IceRouteActionKind.Close: Guide.ToggleRecordingPanel(); break;
                    case IceRouteActionKind.Refresh: RefreshLibrary(); break;
                    case IceRouteActionKind.Folder: Guide.OpenRecordingsFolder(); break;
                    case IceRouteActionKind.Record:
                        bool started = Guide.StartRecording(); message = Guide.RecordingStatus;
                        if (started) Guide.ToggleRecordingPanel(); break;
                    case IceRouteActionKind.StopSave: Guide.StopAndSaveRecording(); message = Guide.RecordingStatus; RefreshLibrary(); break;
                    case IceRouteActionKind.Stop: Guide.StopRecording(); message = Guide.RecordingStatus; RefreshLibrary(); break;
                    case IceRouteActionKind.SaveCopy:
                        if (Guide.HasUnsavedRecording || entry == null) { Guide.SaveRecording(); message = Guide.RecordingStatus; }
                        else { string copy = RouteLibraryCatalog.SaveCopy(entry, Guide.Library, Guide.GuideDirectory); RefreshLibrary(); SelectPath(copy); message = "New copy saved. Existing routes remain unchanged."; }
                        RefreshLibrary(); break;
                    case IceRouteActionKind.PreviewLoad:
                        if (entry.guide == null) { Guide.LoadRecording(entry.path); message = Guide.RecordingStatus; }
                        else { Guide.LoadTrainingGuide(entry.path); message = Guide.TrainingStatus; }
                        dirty = true; break;
                    case IceRouteActionKind.UseGps: Guide.UseSavedRouteAsGps(entry.path); message = Guide.TrainingStatus; dirty = true; break;
                    case IceRouteActionKind.ToggleGps: Guide.Toggle(); message = Guide.TrainingStatus; break;
                    case IceRouteActionKind.Edit: message = Guide.EditSavedRoute(entry.path); break;
                    case IceRouteActionKind.Rename: confirmation = "rename"; confirmationPath = entry.path; confirmationIdentity = entry.guide?.id ?? entry.raw?.id; renameInput.SetTextWithoutNotify(entry.name); break;
                    case IceRouteActionKind.Delete: confirmation = "delete"; confirmationPath = entry.path; confirmationIdentity = entry.guide?.id ?? entry.raw?.id; break;
                    case IceRouteActionKind.Cancel: confirmation = ""; confirmationPath = null; IceNavigationUI.ClearFocus(transform); break;
                    case IceRouteActionKind.Confirm: Confirm(); break;
                    case IceRouteActionKind.LibraryTab: Guide.ShowLibraryPanel(); break;
                    case IceRouteActionKind.RawTab: Guide.ShowRawDetails(); break;
                    case IceRouteActionKind.GuidesTab: Guide.ShowTrainingPanel(); break;
                    case IceRouteActionKind.Original: Guide.UseOriginalReference(); SelectedPath = null; dirty = true; RebindRows(); break;
                    case IceRouteActionKind.Fixture: Guide.SelectTestFixture(); SelectedPath = null; dirty = true; RebindRows(); break;
                    case IceRouteActionKind.Rejoin: Guide.RejoinLeg(Mathf.RoundToInt(legSlider.value)); message = Guide.TrainingStatus; dirty = true; break;
                    case IceRouteActionKind.Magenta: Guide.GpsColor = new Color(1, .035f, .72f, Guide.GpsColor.a); break;
                    case IceRouteActionKind.Amber: Guide.GpsColor = new Color(1, .5f, .03f, Guide.GpsColor.a); break;
                }
            }
            catch (Exception e) { message = "Route action failed: " + e.Message; confirmation = ""; confirmationPath = null; }
            SyncVisibility(); RefreshNow();
        }
        void Confirm()
        {
            // Resolve the explicit captured file again; no row index or mutable selection can redirect this operation.
            var entry = catalog.entries.Find(e => e.path == confirmationPath);
            if (entry == null) throw new InvalidOperationException("The selected file is no longer in this library. Refresh and select it again.");
            string identity = entry.guide == null ? RouteRecordingStorage.Read(entry.path).id : TrainingGuideStorage.Read(entry.path).id;
            if (identity != confirmationIdentity) throw new InvalidOperationException("This file was replaced after selection. Refresh and select it again.");
            if (confirmation == "rename")
            { RouteLibraryCatalog.Rename(entry, renameInput.text); Guide.RenameActive(entry.path, renameInput.text.Trim()); message = "Library name saved; recorded trajectory unchanged."; }
            else if (confirmation == "delete")
            { RouteLibraryCatalog.DeleteConfirmed(entry, Guide.Library, Guide.GuideDirectory); SelectedPath = null; message = "Selected file deleted. Other routes and drafts remain."; }
            confirmation = ""; confirmationPath = null; IceNavigationUI.ClearFocus(transform); RefreshLibrary();
        }
        public void RefreshNow()
        {
            if (!Application.IsPlaying(gameObject) || game == null || game.RouteGuide == null) return;
            nextRefresh = Time.unscaledTime + Mathf.Max(.05f, refreshInterval);
            if (recordingName != null && !recordingName.isFocused && recordingName.text != Guide.RecordingName) recordingName.SetTextWithoutNotify(Guide.RecordingName);
            IceNavigationUI.Text(activeRoute, "GPS: " + Guide.ActiveRouteName + " / " + (Guide.Visible ? "Shown" : "Hidden"));
            IceNavigationUI.Text(tabCaption, Tab == 0 ? "Route library" : Tab == 1 ? "Raw recordings / recovery" : "Training guides / diagnostics");
            IceNavigationUI.Text(status, Guide.Recorder.Recording ? Guide.RecordingStatus : Guide.HasUnsavedRecording ? Guide.RecordingStatus + "\n" + message : message);
            if (actions != null) foreach (var action in actions) if (action != null && action.button != null) action.button.interactable = CanActivate(action.kind);
            foreach (var row in rows) if (row.button != null) row.button.interactable = CanInteract && !ConfirmationOpen && !Guide.Recorder.Recording;
            if (renameInput != null) renameInput.gameObject.SetActive(confirmation == "rename");
            IceNavigationUI.Text(confirmationMessage, confirmation == "rename" ? "Rename the library entry; trajectory and file stay unchanged." :
                "Delete only this saved file?\n" + (Selected?.name ?? "") + "\n" + (confirmationPath == null ? "" : Path.GetFileName(confirmationPath)));
            RefreshDetails();
            if (!Visible) return;
            if (dirty || Tab == 1 && Guide.Recorder.Recording && Time.unscaledTime >= nextPreview || Tab == 2 && Time.unscaledTime >= nextPreview)
                RefreshPreview();
        }
        void RefreshDetails()
        {
            var entry = Selected;
            var data = Tab == 1 ? Guide.Recorder.Recording ? Guide.Recorder.Data : entry?.raw ?? Guide.Recorder.Data : entry?.raw;
            IceNavigationUI.Text(metadata, entry == null ? "No saved file selected" : entry.name + "\n" + Path.GetFileName(entry.path) + "\n" +
                (entry.error ?? (entry.raw.complete ? "Complete" : "Incomplete recovery; GPS unavailable")));
            if (sampleSlider != null)
            {
                sampleSlider.maxValue = data == null ? 0 : Mathf.Max(0, data.samples.Count - 1);
                sampleSlider.interactable = CanInteract && data != null && data.samples.Count > 0;
            }
            if (data != null && data.samples.Count > 0)
            {
                int index = Mathf.Clamp(sampleSlider == null ? data.samples.Count - 1 : Mathf.RoundToInt(sampleSlider.value), 0, data.samples.Count - 1); var p = data.samples[index];
                IceNavigationUI.Text(sampleDetail, data.name + " / " + data.samples.Count + " points / " + data.distanceMeters.ToString("F2") + " m / " + data.durationSeconds.ToString("F2") + " s\n" +
                    data.provenance + "\n#" + p.sequence + " t=" + p.seconds.ToString("F2") + " s / " + (p.direction < 0 ? "REVERSE" : p.direction > 0 ? "FORWARD" : "STOPPED") +
                    "\nVehicle " + p.vehicleX.ToString("F3") + ", " + p.vehicleZ.ToString("F3") + " heading " + (p.vehicleHeadingRadians * Mathf.Rad2Deg).ToString("F1") + "°" +
                    "\nConditioner " + p.conditionerX.ToString("F3") + ", " + p.conditionerZ.ToString("F3") + " heading " + (p.conditionerHeadingRadians * Mathf.Rad2Deg).ToString("F1") + "° / lift " + p.conditionerLift.ToString("F2"));
            }
            else IceNavigationUI.Text(sampleDetail, "Record a route or select a saved file to inspect raw samples.");
            var fit = Guide.ActiveFit; var progress = Guide.Progress; var footprints = Guide.Footprints;
            if (legSlider != null) { legSlider.maxValue = fit == null ? 0 : Mathf.Max(0, fit.legs.Count - 1); legSlider.interactable = CanInteract && fit != null && fit.legs.Count > 0; }
            string detail = Guide.TrainingStatus;
            if (fit != null && fit.legs.Count > 0)
            {
                int index = Mathf.Clamp(legSlider == null ? 0 : Mathf.RoundToInt(legSlider.value), 0, fit.legs.Count - 1); var l = fit.legs[index];
                detail += "\nRejoin leg " + (index + 1) + " / " + fit.legs.Count + ": " + l.label + " / " + l.start.ToString("F1") + "–" + l.end.ToString("F1") + " m";
            }
            if (progress != null) detail += "\nCurrent leg " + (progress.Leg + 1) + " / " + progress.Distance.ToString("F1") + " m / " + progress.Status;
            if (footprints != null) detail += "\nMissed cut " + (footprints.MissedBladeCells * .04f).ToString("F1") + " m² / water " + (footprints.MissedWaterCells * .04f).ToString("F1") + " m²" +
                "\nExcess revisit cut " + (footprints.ExcessBladeCells * .04f).ToString("F1") + " m² / water " + (footprints.ExcessWaterCells * .04f).ToString("F1") + " m²";
            IceNavigationUI.Text(guideDetail, detail);
            if (opacitySlider != null && !Mathf.Approximately(opacitySlider.value, Guide.GpsColor.a)) opacitySlider.SetValueWithoutNotify(Guide.GpsColor.a);
            RefreshAnnotations();
        }
        void RefreshPreview()
        {
            dirty = false; nextPreview = Time.unscaledTime + .5f;
            var entry = Selected;
            var raw = Tab == 1 ? Guide.Recorder.Recording ? Guide.Recorder.Data : entry?.raw ?? Guide.Recorder.Data : entry?.raw;
            if (raw != null && (Tab == 1 || entry?.guide == null))
            {
                rawPreview.Rebuild(raw); if (preview.texture != rawPreview.Texture) preview.texture = rawPreview.Texture;
                displayedRaw = raw; RefreshAnnotations();
                IceNavigationUI.Text(previewCaption, "+Z / NORTH — raw path: color changes every 40 m; orange reverse / gray conditioner"); return;
            }
            Guide.PrepareTrainingPreview();
            displayedRaw = null; RefreshAnnotations();
            RouteFit fit = Guide.ActiveFit; TrainingRouteDefinition definition = Guide.SelectedGuide;
            if (entry?.guide != null)
            {
                definition = entry.guide;
                if (fittedEntry != entry)
                {
                    fittedEntry = entry;
                    try { fittedPreview = RouteFitter.Fit(definition, entry.raw); }
                    catch (Exception e) { fittedPreview = null; message = "Guide preview: " + e.Message; }
                }
                fit = fittedPreview;
            }
            if (fit != null)
            {
                if (previewDefinition != definition) { previewDefinition = definition; previewGuideRaw = definition == null ? null : entry?.guide == definition ? entry.raw : TrainingGuideStorage.Raw(definition); }
                bool activeFootprints = entry == null || entry.guide != null && Guide.SelectedGuide != null && entry.guide.id == Guide.SelectedGuide.id;
                guidePreview.Rebuild(previewGuideRaw, definition, fit, Tab == 2 && activeFootprints ? Guide.Footprints : null,
                    (GuideViewLayer)(layerDropdown == null ? 0 : layerDropdown.value), ++revision);
                if (preview.texture != guidePreview.Texture) preview.texture = guidePreview.Texture;
            }
            else if (preview.texture != null) preview.texture = null;
            IceNavigationUI.Text(previewCaption, "+Z / NORTH — raw gray / fitted cyan / conditioner gold");
        }
        void RefreshAnnotations()
        {
            bool visible = displayedRaw != null && displayedRaw.samples.Count > 0 && preview.texture == rawPreview.Texture;
            foreach (var marker in annotationTargets ?? (annotationTargets = new[] { vehicleSampleMarker, conditionerSampleMarker, startAnnotation, endAnnotation }))
                if (marker != null && marker.gameObject.activeSelf != visible) marker.gameObject.SetActive(visible);
            if (!visible) return;
            int index = Mathf.Clamp(sampleSlider == null ? displayedRaw.samples.Count - 1 : Mathf.RoundToInt(sampleSlider.value), 0, displayedRaw.samples.Count - 1);
            var p = displayedRaw.samples[index]; var first = displayedRaw.samples[0]; var end = displayedRaw.samples[displayedRaw.samples.Count - 1];
            PlaceAnnotation(vehicleSampleMarker, p.vehicleX, p.vehicleZ); PlaceAnnotation(conditionerSampleMarker, p.conditionerX, p.conditionerZ);
            PlaceAnnotation(startAnnotation, first.vehicleX, first.vehicleZ); PlaceAnnotation(endAnnotation, end.vehicleX, end.vehicleZ);
            IceNavigationUI.Text(sampleAnnotation, "#" + p.sequence);
        }
        RectTransform[] annotationTargets;
        void PlaceAnnotation(RectTransform marker, float x, float z)
        {
            if (marker == null) return;
            Vector2 at = rawPreview.ToGui(new Rect(0, 0, 1, 1), x, z); at.y = 1 - at.y;
            if (marker.anchorMin != at || marker.anchorMax != at) marker.anchorMin = marker.anchorMax = at;
        }
        void OnDisable()
        {
            if (game != null && game.AuthoredRoutes == this) game.AuthoredRoutes = null;
            if (recordingName != null) recordingName.onValueChanged.RemoveListener(SetRecordingName);
            if (sampleSlider != null) sampleSlider.onValueChanged.RemoveListener(SampleChanged);
            if (legSlider != null) legSlider.onValueChanged.RemoveListener(LegChanged);
            if (opacitySlider != null) opacitySlider.onValueChanged.RemoveListener(OpacityChanged);
            if (layerDropdown != null) layerDropdown.onValueChanged.RemoveListener(LayerChanged);
            if (!Application.IsPlaying(gameObject)) return;
            IceNavigationUI.ClearFocus(transform); confirmation = ""; wasVisible = false; IceNavigationUI.Group(visibility, false, false);
        }
        void OnDestroy() { rawPreview.Dispose(); guidePreview.Dispose(); foreach (var row in rows) if (row != null) row.Unbind(); }
    }
}
