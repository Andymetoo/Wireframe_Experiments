#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IceResurfacer.Editor
{
    /// <summary>One-time additive asset authoring. Runtime only binds the saved objects.</summary>
    public static class IceNavigationFactory
    {
        public const string Folder = "Assets/Experiments/IceResurfacer/UI/Prefabs/Navigation";
        public const string SurveyPath = Folder + "/Aurora Surface Survey.prefab";
        public const string GpsPath = Folder + "/Aurora GPS Status.prefab";
        public const string ManagerPath = Folder + "/Aurora Route Manager.prefab";
        public const string RowPath = Folder + "/Aurora Route Row.prefab";
        public const string ScenePath = "Assets/Experiments/IceResurfacer/Scenes/IceResurfacer_AuthoredMachine.unity";
        static IceUIStyleSheet theme;
        static Sprite pixel;
        static Texture2D previewMap;
        static readonly List<IceRouteAction> buttons = new List<IceRouteAction>();
        static void StyleAssets()
        {
            theme = AssetDatabase.LoadAssetAtPath<IceUIStyleSheet>("Assets/Experiments/IceResurfacer/UI/Themes/Aurora UI Theme.asset");
            pixel = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Experiments/IceResurfacer/UI/Artwork/Placeholder White.png");
            previewMap = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Survey Design Preview.png");
            if (theme == null || pixel == null) throw new InvalidOperationException("Install existing Phase 1/2 assets first.");
        }
        public static void BuildBatch()
        {
            try
            {
                RequireIsolated(); BuildNewAssets(); EditorSceneManager.OpenScene(ScenePath); Install();
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                Directory.CreateDirectory("ValidationResults"); File.Copy(ScenePath, "ValidationResults/Installed-Phase3.unity", true);
                Debug.Log("PHASE3 BUILD PASS: persistent survey, GPS, manager and reusable templates."); EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        public static void RequireIsolated()
        {
            if (!File.Exists("UIAuthoringPhase3ValidationProject.txt")) throw new InvalidOperationException("Run mutation/acceptance only in the marked Phase 3 copy.");
        }
        public static void CompleteBatch()
        {
            try
            {
                RequireIsolated(); StyleAssets();
                foreach (string path in new[] { RowPath, ManagerPath, GpsPath, Folder + "/Aurora Route Action.prefab" })
                {
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        foreach (var button in root.GetComponentsInChildren<Button>(true))
                            if (button.GetComponent<IceUIClickGuard>() == null) button.gameObject.AddComponent<IceUIClickGuard>();
                        var manager = root.GetComponent<IceRouteManager>();
                        if (manager != null)
                            foreach (var label in new[] { manager.status, manager.metadata, manager.guideDetail, manager.sampleDetail, manager.previewCaption })
                            {
                                if (label == null) continue;
                                var height = label.GetComponent<IceUITextHeight>() ?? label.gameObject.AddComponent<IceUITextHeight>();
                                height.text = label; height.minimumHeight = label.GetComponent<LayoutElement>().preferredHeight;
                            }
                        var gps = root.GetComponent<IceGpsStatus>();
                        if (gps != null && gps.warning.GetComponentInParent<ScrollRect>() == null)
                        {
                            var scroll = Scroll("Calibration and loading warnings - scrollable", root.transform, 16, 174, 488, 52, out RectTransform content);
                            DockWidth(scroll.GetComponent<RectTransform>());
                            gps.warning.transform.SetParent(content, false); Preferred(gps.warning.gameObject, 40);
                            var height = gps.warning.gameObject.AddComponent<IceUITextHeight>(); height.text = gps.warning; height.minimumHeight = 40;
                        }
                        if (gps != null)
                            foreach (var label in new[] { gps.routeName, gps.leg, gps.deviation, gps.progress })
                            { label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; }
                        var row = root.GetComponent<IceRouteRow>();
                        if (row != null)
                        {
                            row.title.textWrappingMode = TextWrappingModes.NoWrap; row.title.overflowMode = TextOverflowModes.Ellipsis;
                            row.metadata.overflowMode = TextOverflowModes.Ellipsis; row.validation.overflowMode = TextOverflowModes.Ellipsis;
                        }
                        if (manager != null) manager.preview.rectTransform.pivot = new Vector2(.5f, .5f);
                        if (manager != null && manager.vehicleSampleMarker == null)
                        {
                            manager.vehicleSampleMarker = Image("Selected raw vehicle marker", manager.preview.transform, 0, 0, 10, 10, IceUIColorRole.Text).rectTransform;
                            manager.conditionerSampleMarker = Image("Selected raw conditioner marker", manager.preview.transform, 0, 0, 6, 6, IceUIColorRole.Accent).rectTransform;
                            foreach (var marker in new[] { manager.vehicleSampleMarker, manager.conditionerSampleMarker })
                            { marker.pivot = new Vector2(.5f, .5f); marker.anchoredPosition = Vector2.zero; }
                            manager.vehicleSampleMarker.GetComponent<IceUIStyle>().applyColor = false; manager.vehicleSampleMarker.GetComponent<Image>().color = Color.white;
                            manager.conditionerSampleMarker.GetComponent<IceUIStyle>().applyColor = false; manager.conditionerSampleMarker.GetComponent<Image>().color = new Color(.6f, .8f, 1);
                            manager.sampleAnnotation = Text("Selected sample number", manager.vehicleSampleMarker, "#35", 10, -8, 100, 24, 15);
                            manager.sampleAnnotation.GetComponent<IceUIStyle>().applyColor = false; manager.sampleAnnotation.color = Color.white;
                            manager.startAnnotation = Text("Raw route START", manager.preview.transform, "START", 8, -20, 100, 24, 15, IceUIColorRole.Accent).rectTransform;
                            manager.endAnnotation = Text("Raw route END", manager.preview.transform, "END", 8, 12, 100, 24, 15, IceUIColorRole.Warning).rectTransform;
                        }
                        if (root.name == "Aurora Route Action" && root.GetComponent<IceRouteAction>() == null)
                        {
                            var action = root.AddComponent<IceRouteAction>(); action.button = root.GetComponent<Button>(); action.kind = IceRouteActionKind.Refresh;
                        }
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                AssetDatabase.SaveAssets(); EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        public static void BuildNewAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play before authoring.");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh(); StyleAssets();
            CreatePreview(); StyleAssets();
            SaveNew(RowPath, BuildRow); SaveNew(SurveyPath, BuildSurvey); SaveNew(GpsPath, BuildGps); SaveNew(ManagerPath, BuildManager);
            SaveNew(Folder + "/Aurora Route Action.prefab", () => Button("Route action", null, "Refresh", 0, 0, 220, 42).gameObject);
            SaveNew(Folder + "/Aurora Section Header.prefab", () => Text("Section", null, "SECTION HEADER", 0, 0, 300, 32, 19, IceUIColorRole.Muted).gameObject);
            SaveNew(Folder + "/Aurora Status Message.prefab", () => Text("Status", null, "Example status / DESIGN PREVIEW", 0, 0, 500, 76, 18).gameObject);
            AssetDatabase.SaveAssets();
        }
        static void SaveNew(string path, Func<GameObject> build)
        {
            if (File.Exists(path)) return;
            GameObject root = build(); try { PrefabUtility.SaveAsPrefabAsset(root, path); } finally { Object.DestroyImmediate(root); }
        }
        static void CreatePreview()
        {
            string path = Folder + "/Survey Design Preview.png"; if (File.Exists(path)) return;
            var texture = new Texture2D(120, 240, TextureFormat.RGBA32, false);
            var pixels = new Color32[120 * 240];
            for (int y = 0; y < 240; y++) for (int x = 0; x < 120; x++)
            {
                float px = (x + .5f) / 120f * 30 - 15, pz = (y + .5f) / 240f * 60 - 30;
                float dx = Mathf.Max(0, Mathf.Abs(px) - 6), dz = Mathf.Max(0, Mathf.Abs(pz) - 21);
                int index = y * 120 + x;
                pixels[index] = dx * dx + dz * dz > 81 ? new Color32(13, 24, 32, 0) : IceSurfaceView.TreatmentColor((IceTreatmentState)Mathf.Clamp(x / 20, 0, 5));
            }
            texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.filterMode = FilterMode.Point; importer.SaveAndReimport();
        }
        static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h); return rt;
        }
        static void Stretch(RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top); }
        static CanvasGroup Group(GameObject go, bool shown = true)
        { var g = go.AddComponent<CanvasGroup>(); g.alpha = shown ? 1 : 0; g.blocksRaycasts = g.interactable = shown; return g; }
        static Image Image(string name, Transform parent, float x, float y, float w, float h, IceUIColorRole role, bool blocks = false)
        {
            var rt = Rect(name, parent, x, y, w, h); var image = rt.gameObject.AddComponent<Image>(); image.sprite = pixel; image.raycastTarget = blocks;
            var style = rt.gameObject.AddComponent<IceUIStyle>(); style.theme = theme; style.image = image; style.colorRole = role;
            style.applySprite = true; style.spriteRole = IceUISpriteRole.Panel; style.Apply(); return image;
        }
        static TextMeshProUGUI Text(string name, Transform parent, string value, float x, float y, float w, float h, float size = 18, IceUIColorRole role = IceUIColorRole.Text)
        {
            var rt = Rect(name, parent, x, y, w, h); var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value; text.fontSize = size; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow;
            var style = rt.gameObject.AddComponent<IceUIStyle>(); style.theme = theme; style.text = text; style.colorRole = role; style.Apply(); return text;
        }
        static Button Button(string name, Transform parent, string caption, float x, float y, float w, float h)
        {
            var image = Image(name, parent, x, y, w, h, IceUIColorRole.Panel, true); var b = image.gameObject.AddComponent<Button>(); b.targetGraphic = image;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            var style = image.GetComponent<IceUIStyle>(); style.button = b; style.applyButtonStates = true; style.spriteRole = IceUISpriteRole.Button; style.Apply();
            var t = Text("Caption", image.transform, caption, 0, 0, w, h, 18); Stretch(t.rectTransform, 8, 4, 8, 4); t.alignment = TextAlignmentOptions.Center;
            return b;
        }
        static TMP_InputField Input(string name, Transform parent, string example, float x, float y, float w, float h)
        {
            var image = Image(name, parent, x, y, w, h, IceUIColorRole.Inset, true); var input = image.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = image; input.characterLimit = 80; input.lineType = TMP_InputField.LineType.SingleLine;
            var viewport = Rect("Text viewport", image.transform, 0, 0, w, h); Stretch(viewport, 10, 3, 10, 3); viewport.gameObject.AddComponent<RectMask2D>();
            var text = Text("Text", viewport, "", 0, 0, w, h, 20); Stretch(text.rectTransform); input.textViewport = viewport; input.textComponent = text;
            var placeholder = Text("Placeholder", viewport, "Enter a route name", 0, 0, w, h, 20, IceUIColorRole.Muted); Stretch(placeholder.rectTransform); input.placeholder = placeholder;
            input.SetTextWithoutNotify(example); return input;
        }
        static Slider Slider(string name, Transform parent, float x, float y, float w, float min, float max, float value, bool whole = false)
        {
            var rt = Rect(name, parent, x, y, w, 28); var slider = rt.gameObject.AddComponent<Slider>();
            var bg = Image("Track", rt, 0, 10, w, 8, IceUIColorRole.Inset); bg.rectTransform.anchorMax = new Vector2(1, 1); bg.rectTransform.sizeDelta = new Vector2(0, 8);
            var area = Rect("Handle area", rt, 0, 0, w, 28); Stretch(area, 8, 0, 8, 0);
            var handle = Image("Handle", area, 0, 0, 16, 28, IceUIColorRole.Accent, true);
            slider.handleRect = handle.rectTransform; slider.targetGraphic = handle; slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = whole; slider.SetValueWithoutNotify(value);
            slider.navigation = new Navigation { mode = Navigation.Mode.None }; return slider;
        }
        static ScrollRect Scroll(string name, Transform parent, float x, float y, float w, float h, out RectTransform content)
        {
            var image = Image(name, parent, x, y, w, h, IceUIColorRole.Inset, true); var scroll = image.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", image.transform, 0, 0, w, h); Stretch(viewport, 3, 3, 18, 3); viewport.gameObject.AddComponent<RectMask2D>();
            content = Rect("Content", viewport, 0, 0, w - 21, h); content.anchorMax = new Vector2(1, 1); content.sizeDelta = new Vector2(0, h);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 8; layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 30; scroll.movementType = ScrollRect.MovementType.Clamped;
            var track = Image("Scrollbar", image.transform, w - 15, 3, 12, h - 6, IceUIColorRole.Inset, true);
            track.rectTransform.anchorMin = new Vector2(1, 0); track.rectTransform.anchorMax = Vector2.one; track.rectTransform.offsetMin = new Vector2(-15, 3); track.rectTransform.offsetMax = new Vector2(-3, -3);
            var handle = Image("Handle", track.transform, 0, 0, 12, 40, IceUIColorRole.Muted, true); Stretch(handle.rectTransform);
            var bar = track.gameObject.AddComponent<Scrollbar>(); bar.handleRect = handle.rectTransform; bar.targetGraphic = handle; bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None }; scroll.verticalScrollbar = bar; return scroll;
        }
        static void Preferred(GameObject go, float height) { var e = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>(); e.preferredHeight = height; e.minHeight = height; }
        static void DockWidth(RectTransform rt) { rt.anchorMax = new Vector2(1, 1); rt.sizeDelta = new Vector2(rt.sizeDelta.x - ((RectTransform)rt.parent).rect.width, rt.sizeDelta.y); }
        static GameObject Badge(Transform parent) => Text("Design Preview - hidden in Play", parent, "DESIGN PREVIEW", 12, 4, 220, 18, 12, IceUIColorRole.Warning).gameObject;
        static GameObject BuildRow()
        {
            var image = Image("Aurora Route Row", null, 0, 0, 620, 116, IceUIColorRole.Panel, true); Preferred(image.gameObject, 116);
            var row = image.gameObject.AddComponent<IceRouteRow>(); row.button = image.gameObject.AddComponent<Button>(); row.button.targetGraphic = image;
            row.button.navigation = new Navigation { mode = Navigation.Mode.None };
            var style = image.GetComponent<IceUIStyle>(); style.button = row.button; style.applyButtonStates = true; style.Apply();
            row.title = Text("Route name", image.transform, "DESIGN PREVIEW / Example recording", 18, 7, 584, 26, 21); DockWidth(row.title.rectTransform);
            row.metadata = Text("File metadata", image.transform, "Recording / example date / 740 m\nPreview file — never a saved route", 18, 36, 584, 44, 16, IceUIColorRole.Muted); DockWidth(row.metadata.rectTransform);
            row.validation = Text("Validation", image.transform, "Select to inspect", 18, 82, 584, 26, 15); DockWidth(row.validation.rectTransform);
            row.selectedIndicator = Image("Selected indicator", image.transform, 0, 0, 6, 116, IceUIColorRole.Accent).gameObject;
            var selected = row.selectedIndicator.GetComponent<RectTransform>(); selected.anchorMin = Vector2.zero; selected.anchorMax = new Vector2(0, 1); selected.offsetMin = Vector2.zero; selected.offsetMax = new Vector2(6, 0);
            row.previewOnly = true; return image.gameObject;
        }
        static GameObject BuildSurvey()
        {
            var root = Rect("Aurora Surface Survey", null, 28, 24, 362, 342); var owner = root.gameObject.AddComponent<IceSurfaceSurvey>(); owner.visibility = Group(root.gameObject);
            Image("Header background", root, 0, 0, 362, 48, IceUIColorRole.Panel, true).rectTransform.anchorMax = new Vector2(1, 1);
            // Header and body artwork are independently replaceable, and collapse hides its actual body hit region.
            var header = root.Find("Header background").GetComponent<RectTransform>(); header.sizeDelta = new Vector2(0, 48);
            Text("Header", root, "SURFACE SURVEY", 18, 12, 215, 26, 18);
            owner.collapseButton = Button("Collapse", root, "MAP −", 246, 8, 98, 31); owner.collapseCaption = owner.collapseButton.GetComponentInChildren<TMP_Text>();
            var cr = owner.collapseButton.GetComponent<RectTransform>(); cr.anchorMin = cr.anchorMax = Vector2.one; cr.anchoredPosition = new Vector2(-116, -8);
            var body = Rect("Survey body", root, 0, 48, 362, 294); Stretch(body, 0, 48); owner.body = Group(body.gameObject);
            var bg = Image("Panel background - replace artwork", body, 0, 0, 362, 294, IceUIColorRole.Panel, true); Stretch(bg.rectTransform);
            var frame = Image("Map frame", body, 18, 0, 142, 273, IceUIColorRole.Inset); var map = Rect("Live map - resize independently", frame.transform, 0, 0, 142, 273); Stretch(map);
            owner.map = map.gameObject.AddComponent<IceSurveyMap>(); owner.map.owner = owner; owner.map.texture = previewMap; owner.map.raycastTarget = true;
            var overlay = Rect("Map indicators and route footprints", map, 0, 0, 142, 273); Stretch(overlay);
            owner.overlays = overlay.gameObject.AddComponent<IceSurveyOverlay>(); owner.overlays.map = owner.map; owner.overlays.raycastTarget = false;
            Text("Treatment label", body, "TREATMENT", 178, 7, 164, 25, 17, IceUIColorRole.Accent);
            owner.coverage = Text("Coverage", body, "68.4%", 178, 37, 164, 40, 32, IceUIColorRole.Accent);
            owner.quality = Text("Finish quality", body, "Finish 82%", 178, 79, 164, 24, 17);
            var legend = Rect("Treatment legend", body, 178, 112, 174, 145); owner.treatmentLegend = legend.gameObject;
            IceTreatmentState[] states = { IceTreatmentState.Uncut, IceTreatmentState.CutButDry, IceTreatmentState.WetButUncut, IceTreatmentState.SnowCovered, IceTreatmentState.Overwatered, IceTreatmentState.Resurfaced };
            for (int i = 0; i < states.Length; i++)
            {
                var swatch = Image(states[i] + " swatch", legend, 0, i * 24 + 6, 8, 8, IceUIColorRole.Accent); swatch.GetComponent<IceUIStyle>().applyColor = false; swatch.color = IceSurfaceView.TreatmentColor(states[i]);
                Text(states[i] + " label", legend, IceCoverage.Label(states[i]), 12, i * 24, 158, 23, 15, IceUIColorRole.Muted);
            }
            owner.mode = Text("F2 mode legend", body, "", 178, 112, 164, 145, 16, IceUIColorRole.Muted);
            owner.status = Text("Status", body, "Hover: exact cells", 178, 262, 174, 26, 13, IceUIColorRole.Muted);
            var patch = Rect("Patch inspection - independently movable", root, 380, 130, 330, 430); owner.patch = Group(patch.gameObject);
            Image("Patch background", patch, 0, 0, 330, 430, IceUIColorRole.Panel, true); Text("Patch header", patch, "PATCH DETAIL", 12, 10, 245, 26, 19, IceUIColorRole.Accent);
            owner.closePatchButton = Button("Close patch", patch, "×", 282, 8, 36, 30);
            var patchRect = Rect("Actual cell texture", patch, 12, 46, 210, 210); owner.patchImage = patchRect.gameObject.AddComponent<RawImage>(); owner.patchImage.texture = previewMap; owner.patchImage.raycastTarget = false;
            var marker = Image("Inspected cell border", patchRect, 0, 0, 16, 16, IceUIColorRole.Text); marker.color = new Color(1, 1, 1, .25f); marker.GetComponent<IceUIStyle>().applyColor = false; owner.selectedCell = marker.rectTransform;
            owner.patchScale = Text("Patch scale", patch, "3.2 m patch / 20 cm samples", 12, 263, 306, 24, 16, IceUIColorRole.Muted);
            owner.patchDetail = Text("Actual cell detail", patch, "DESIGN PREVIEW / Resurfaced\nCut 0.0350 mm / water 0.14 mm\nSnow 0.12 mm / groove 0.01 mm", 12, 294, 306, 126, 16);
            owner.previewBadge = Badge(root); return root.gameObject;
        }
        static GameObject BuildGps()
        {
            var root = Rect("Aurora GPS Status", null, 398, 145, 520, 230); var owner = root.gameObject.AddComponent<IceGpsStatus>(); owner.visibility = Group(root.gameObject);
            var bg = Image("Status background - replace artwork", root, 0, 0, 520, 230, IceUIColorRole.Panel, true); Stretch(bg.rectTransform);
            owner.routeName = Text("Selected route", root, "DESIGN PREVIEW / Example route", 16, 24, 488, 26, 19, IceUIColorRole.Muted); DockWidth(owner.routeName.rectTransform);
            owner.leg = Text("Leg and direction", root, "LEG 4 / 18 FORWARD", 16, 52, 488, 23, 17); DockWidth(owner.leg.rectTransform);
            owner.instruction = Text("Navigation instruction", root, "CURVE RIGHT / 12 m / 3.2 km/h", 16, 77, 488, 28, 19, IceUIColorRole.Accent); DockWidth(owner.instruction.rectTransform);
            owner.deviation = Text("Lateral deviation", root, "Offset 0.12 m / conditioner 0.08 m / advisory", 16, 108, 488, 24, 16, IceUIColorRole.Muted); DockWidth(owner.deviation.rectTransform);
            owner.progress = Text("Route progress", root, "142.0 / 740.0 m — following active pass", 16, 134, 488, 24, 16); DockWidth(owner.progress.rectTransform);
            var track = Image("Progress track", root, 16, 161, 488, 5, IceUIColorRole.Inset); DockWidth(track.rectTransform);
            owner.progressFill = Image("Progress fill", track.transform, 0, 0, 488, 5, IceUIColorRole.Accent); Stretch(owner.progressFill.rectTransform); owner.progressFill.type = UnityEngine.UI.Image.Type.Filled; owner.progressFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; owner.progressFill.fillAmount = .19f;
            owner.warning = Text("Calibration and loading status", root, "Example route / GPS Ready", 16, 174, 488, 52, 14, IceUIColorRole.Warning); DockWidth(owner.warning.rectTransform);
            owner.toggleButton = Button("Show hide GPS", root, "Hide GPS [Num 7]", 0, 238, 240, 36); owner.toggleCaption = owner.toggleButton.GetComponentInChildren<TMP_Text>();
            owner.routesButton = Button("Open route manager", root, "Routes [Num 8]", 252, 238, 252, 36);
            owner.recordingStatus = Text("Recording status", root, "", 16, 282, 488, 30, 17, IceUIColorRole.Warning);
            owner.previewBadge = Badge(root); return root.gameObject;
        }
        static IceRouteAction Action(Transform parent, IceRouteManager owner, IceRouteActionKind kind, string caption)
        {
            var b = Button(kind.ToString(), parent, caption, 0, 0, 300, 42); Preferred(b.gameObject, 42);
            var action = b.gameObject.AddComponent<IceRouteAction>(); action.kind = kind; action.owner = owner; action.button = b; buttons.Add(action); return action;
        }
        static RectTransform FlowRow(Transform parent, string name, float height)
        {
            var row = Rect(name, parent, 0, 0, 600, height); Preferred(row.gameObject, height);
            var flow = row.gameObject.AddComponent<HorizontalLayoutGroup>(); flow.spacing = 8; flow.childControlWidth = true; flow.childForceExpandWidth = true;
            flow.childControlHeight = true; flow.childForceExpandHeight = true; return row;
        }
        static void Pair(Transform parent, IceRouteManager owner, IceRouteActionKind a, string aa, IceRouteActionKind b, string bb)
        { var row = FlowRow(parent, aa + " / " + bb, 42); Action(row, owner, a, aa); Action(row, owner, b, bb); }
        static TMP_Text FlowText(Transform parent, string name, string text, float height = 48, float size = 17)
        { var label = Text(name, parent, text, 0, 0, 620, height, size); Preferred(label.gameObject, height); return label; }
        static GameObject BuildManager()
        {
            buttons.Clear(); var root = Rect("Aurora Route Manager", null, 0, 0, 1448, 820); root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f); root.anchoredPosition = Vector2.zero;
            var owner = root.gameObject.AddComponent<IceRouteManager>(); owner.visibility = Group(root.gameObject);
            var bg = Image("Window background - replace artwork", root, 0, 0, 1448, 820, IceUIColorRole.Panel, true); Stretch(bg.rectTransform);
            var main = Rect("Manager content", root, 0, 0, 1448, 820); Stretch(main, 18, 22, 18, 18); owner.mainGroup = Group(main.gameObject);
            var heading = Text("Header", main, "ROUTE MANAGER / Numpad 8", 0, 0, 900, 40, 26); DockWidth(heading.rectTransform);
            owner.activeRoute = Text("Active GPS", main, "GPS: DESIGN PREVIEW / Example route / Shown", 0, 43, 1350, 32, 20); DockWidth(owner.activeRoute.rectTransform);
            var close = Action(main, owner, IceRouteActionKind.Close, "Close & drive"); var closeRect = close.GetComponent<RectTransform>(); closeRect.anchorMin = closeRect.anchorMax = Vector2.one; closeRect.anchoredPosition = new Vector2(-210, 0); closeRect.sizeDelta = new Vector2(210, 38);
            var left = Rect("Library column", main, 0, 88, 620, 670); left.anchorMin = new Vector2(0, 0); left.anchorMax = new Vector2(.45f, 1); left.offsetMin = new Vector2(0, 0); left.offsetMax = new Vector2(-10, -88);
            owner.tabCaption = Text("Section header", left, "Route library / DESIGN PREVIEW", 0, 0, 620, 29, 20); DockWidth(owner.tabCaption.rectTransform);
            var tabs = Rect("Tabs", left, 0, 34, 620, 40); DockWidth(tabs); var layout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 6; layout.childForceExpandWidth = true; layout.childControlWidth = true;
            Action(tabs, owner, IceRouteActionKind.LibraryTab, "Library"); Action(tabs, owner, IceRouteActionKind.RawTab, "Raw / recovery"); Action(tabs, owner, IceRouteActionKind.GuidesTab, "Guides");
            owner.recordingName = Input("Recording name", left, "My route / DESIGN PREVIEW", 0, 86, 620, 44); DockWidth(owner.recordingName.GetComponent<RectTransform>());
            var commands = Rect("Recording actions", left, 0, 142, 620, 144); DockWidth(commands);
            var v = commands.gameObject.AddComponent<VerticalLayoutGroup>(); v.spacing = 8; v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandHeight = false;
            Pair(commands, owner, IceRouteActionKind.Record, "Record new route", IceRouteActionKind.StopSave, "Stop & save");
            Pair(commands, owner, IceRouteActionKind.SaveCopy, "Save recording / copy", IceRouteActionKind.Refresh, "Refresh files");
            Pair(commands, owner, IceRouteActionKind.Original, "Original reference", IceRouteActionKind.Fixture, "Labeled test route");
            var list = Scroll("Saved files - scrollable", left, 0, 302, 620, 340, out RectTransform content);
            Stretch(list.GetComponent<RectTransform>(), 0, 302); owner.listContent = content; owner.rowPrefab = AssetDatabase.LoadAssetAtPath<IceRouteRow>(RowPath);
            var previews = Rect("Design preview rows - excluded from storage", content, 0, 0, 600, 364); Preferred(previews.gameObject, 364); owner.previewRows = previews.gameObject;
            var pv = previews.gameObject.AddComponent<VerticalLayoutGroup>(); pv.spacing = 8; pv.childControlWidth = pv.childControlHeight = true; pv.childForceExpandHeight = false;
            for (int i = 0; i < 3; i++)
            {
                var sample = (GameObject)PrefabUtility.InstantiatePrefab(owner.rowPrefab.gameObject, previews);
                var row = sample.GetComponent<IceRouteRow>(); row.previewOnly = true; row.title.text = "DESIGN PREVIEW / " + (i == 1 ? "Example calibrated guide" : "Example recording " + (i + 1));
                row.metadata.text = (i == 1 ? "Guide" : "Recording") + " / Example date / 740 m\nSample only — never a file"; row.SetSelected(i == 1);
            }
            var right = Rect("Preview and actions column", main, 650, 88, 740, 670); right.anchorMin = new Vector2(.45f, 0); right.anchorMax = Vector2.one; right.offsetMin = new Vector2(10, 0); right.offsetMax = new Vector2(0, -88);
            var detailScroll = Scroll("Details and actions - scrollable", right, 0, 0, 740, 670, out RectTransform details); Stretch(detailScroll.GetComponent<RectTransform>());
            var plot = Rect("Route preview viewport", details, 0, 0, 680, 250); Preferred(plot.gameObject, 250);
            var raw = Rect("Cached route texture", plot, 0, 0, 680, 250); Stretch(raw); owner.preview = raw.gameObject.AddComponent<RawImage>(); owner.preview.texture = previewMap; owner.preview.raycastTarget = false;
            var aspect = raw.gameObject.AddComponent<AspectRatioFitter>(); aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent; aspect.aspectRatio = 900f / 760;
            owner.previewCaption = FlowText(details, "Preview orientation", "+Z / NORTH — DESIGN PREVIEW", 40, 15);
            Pair(details, owner, IceRouteActionKind.UseGps, "Use selected as GPS", IceRouteActionKind.ToggleGps, "Show / hide GPS [Num 7]");
            Pair(details, owner, IceRouteActionKind.PreviewLoad, "Load / preview selected", IceRouteActionKind.Edit, "Edit route in Editor");
            Pair(details, owner, IceRouteActionKind.Rename, "Rename library entry", IceRouteActionKind.Delete, "Delete selected file…");
            owner.metadata = FlowText(details, "Selected file metadata", "DESIGN PREVIEW / Example calibrated guide\nExample.guide.json / Complete", 80);
            owner.status = FlowText(details, "Recording and validation status", "DESIGN PREVIEW / GPS Ready. Choose a saved route.", 110);
            FlowText(details, "Raw section header", "RAW RECORDING / SAMPLE INSPECTION", 30, 19);
            Action(details, owner, IceRouteActionKind.Stop, "Stop recording without saving");
            owner.sampleSlider = Slider("Sample scrubber", details, 0, 0, 620, 0, 100, 35, true); Preferred(owner.sampleSlider.gameObject, 28);
            owner.sampleDetail = FlowText(details, "Raw sample detail", "DESIGN PREVIEW / 1,200 points / 740 m / 420 s\n#35 FORWARD\nVehicle 2.4, 16.0 / conditioner 2.4, 13.8", 150, 16);
            FlowText(details, "Guide section header", "GUIDE LAYERS / EXPLICIT REJOIN", 30, 19);
            var dropdown = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources()); dropdown.name = "Guide preview layer"; dropdown.transform.SetParent(details, false); Preferred(dropdown, 36);
            owner.layerDropdown = dropdown.GetComponent<TMP_Dropdown>(); owner.layerDropdown.ClearOptions(); owner.layerDropdown.AddOptions(new List<string> { "Paths", "Intended", "Actual cut", "Actual water", "Missed", "Excess" });
            foreach (var label in dropdown.GetComponentsInChildren<TMP_Text>(true)) { label.font = theme.GetFont(IceUIFontRole.Primary); label.fontSize = 18; }
            owner.legSlider = Slider("Leg to rejoin", details, 0, 0, 620, 0, 18, 3, true); Preferred(owner.legSlider.gameObject, 28);
            Action(details, owner, IceRouteActionKind.Rejoin, "Rejoin selected leg / guide cursor only");
            owner.guideDetail = FlowText(details, "Guide diagnostics", "DESIGN PREVIEW / Leg 4 / 18\nMissed cut 0.4 m² / water 0.2 m²", 220, 16);
            Pair(details, owner, IceRouteActionKind.Magenta, "GPS magenta", IceRouteActionKind.Amber, "GPS amber");
            FlowText(details, "GPS opacity label", "GPS opacity (0.45–1)", 26, 16);
            owner.opacitySlider = Slider("GPS opacity", details, 0, 0, 620, .45f, 1, .95f); Preferred(owner.opacitySlider.gameObject, 28);
            Action(details, owner, IceRouteActionKind.Folder, "Open routes folder");
            var modal = Rect("Per-file confirmation", root, 0, 0, 1448, 820); Stretch(modal); owner.confirmationGroup = Group(modal.gameObject, false);
            var shade = Image("Modal background", modal, 0, 0, 1448, 820, IceUIColorRole.Panel, true); Stretch(shade.rectTransform);
            var box = Rect("Confirmation content", modal, 0, 0, 900, 330); box.anchorMin = box.anchorMax = box.pivot = new Vector2(.5f, .5f); box.anchoredPosition = Vector2.zero;
            owner.confirmationMessage = Text("Confirmation message", box, "Rename/delete confirmation", 20, 12, 860, 120, 23);
            owner.renameInput = Input("New library name", box, "Example route", 20, 143, 860, 48);
            var yes = Action(box, owner, IceRouteActionKind.Confirm, "Confirm"); yes.GetComponent<RectTransform>().anchoredPosition = new Vector2(20, -240); yes.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 54);
            var no = Action(box, owner, IceRouteActionKind.Cancel, "Cancel"); no.GetComponent<RectTransform>().anchoredPosition = new Vector2(460, -240); no.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 54);
            owner.previewBadge = Badge(root); owner.actions = buttons.ToArray(); return root.gameObject;
        }
        [MenuItem("Tools/Aurora/UI/Install Missing Phase 3 Panels")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play before installing.");
            StyleAssets();
            var hud = Object.FindFirstObjectByType<IceAuthoredHud>(FindObjectsInactive.Include);
            if (hud == null || hud.game == null) throw new InvalidOperationException("Open the authored scene with its existing Aurora UI and Game binding.");
            var root = hud.canvas.transform;
            InstallOne<IceSurfaceSurvey>(SurveyPath, root.Find("Surface Survey"), hud);
            InstallOne<IceGpsStatus>(GpsPath, root.Find("Navigation"), hud);
            InstallOne<IceRouteManager>(ManagerPath, root.Find("Menus"), hud);
            EditorSceneManager.MarkSceneDirty(hud.game.gameObject.scene);
        }
        static void InstallOne<T>(string path, Transform parent, IceAuthoredHud hud) where T : Component
        {
            if (parent == null) throw new InvalidOperationException("Missing existing Aurora UI organization group.");
            if (parent.GetComponentInChildren<T>(true) != null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (prefab == null) throw new InvalidOperationException("Missing Phase 3 prefab: " + path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent); Undo.RegisterCreatedObjectUndo(instance, "Install Aurora Phase 3 panel");
            var binding = new SerializedObject(instance.GetComponent<T>());
            binding.FindProperty("game").objectReferenceValue = hud.game; binding.FindProperty("canvas").objectReferenceValue = hud.canvas;
            binding.FindProperty("raycaster").objectReferenceValue = hud.raycaster; binding.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.GetComponent<T>());
        }
    }
    [CustomEditor(typeof(IceSurveyMap))]
    public sealed class IceSurveyMapEditor : UnityEditor.Editor
    { public override void OnInspectorGUI() { DrawDefaultInspector(); } }
}
#endif
