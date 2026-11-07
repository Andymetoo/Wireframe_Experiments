#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IceResurfacer.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IceResurfacer.Editor
{
    /// <summary>Additive authoring of new menu assets. Existing Phase 1–3 assets are never regenerated.</summary>
    public static class IceMenuFactory
    {
        public const string Folder = "Assets/Experiments/IceResurfacer/UI/Prefabs/Menus";
        public const string RootPath = Folder + "/Aurora Session Screens.prefab";
        public const string ButtonPath = Folder + "/Aurora Menu Button.prefab";
        public const string SettingPath = Folder + "/Aurora Operator Setting.prefab";
        public const string RowPath = Folder + "/Aurora Result Row.prefab";
        public const string ScenePath = "Assets/Experiments/IceResurfacer/Scenes/IceResurfacer_AuthoredMachine.unity";
        static IceUIStyleSheet theme;
        static Sprite pixel;
        static void LoadStyle()
        {
            theme = AssetDatabase.LoadAssetAtPath<IceUIStyleSheet>("Assets/Experiments/IceResurfacer/UI/Themes/Aurora UI Theme.asset");
            pixel = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Experiments/IceResurfacer/UI/Artwork/Placeholder White.png");
            if (theme == null || pixel == null) throw new InvalidOperationException("Existing Aurora theme/art is required.");
        }
        public static void RequireIsolated()
        { if (!File.Exists("UIAuthoringPhase4ValidationProject.txt")) throw new InvalidOperationException("Use the marked Phase 4 isolated project for asset generation and mutation tests."); }
        public static void BuildBatch()
        {
            try
            {
                RequireIsolated(); BuildNewAssets(); EditorSceneManager.OpenScene(ScenePath); Install();
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                Directory.CreateDirectory("ValidationResults"); File.Copy(ScenePath, "ValidationResults/Installed-Phase4.unity", true);
                Debug.Log("PHASE4 BUILD PASS: persistent menus and reusable prefab variants."); EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        public static void BuildNewAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh(); LoadStyle();
            SaveNew(ButtonPath, BuildButtonTemplate); SaveNew(SettingPath, BuildSettingTemplate); SaveNew(RowPath, BuildRowTemplate);
            SaveNew(Folder + "/Aurora Menu Panel.prefab", () => Image("Aurora Menu Panel", null, 0, 0, 835, 440, IceUIColorRole.Paper, true).gameObject);
            SaveNew(Folder + "/Aurora Welcome.prefab", BuildWelcome);
            SaveNew(Folder + "/Aurora Pause Settings.prefab", BuildPause);
            SaveNew(Folder + "/Aurora Operator Guide.prefab", BuildHelp);
            SaveNew(Folder + "/Aurora Results.prefab", BuildResults);
            SaveNew(RootPath, BuildRoot); AssetDatabase.SaveAssets();
        }
        static void SaveNew(string path, Func<GameObject> build)
        { if (File.Exists(path)) return; var root = build(); try { PrefabUtility.SaveAsPrefabAsset(root, path); } finally { Object.DestroyImmediate(root); } }
        static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            var r = go.GetComponent<RectTransform>(); r.SetParent(parent, false); r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
        }
        static void Stretch(RectTransform r, float left = 0, float top = 0, float right = 0, float bottom = 0)
        { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(left, bottom); r.offsetMax = new Vector2(-right, -top); }
        static void Width(RectTransform r)
        { r.anchorMax = new Vector2(1, 1); r.sizeDelta = new Vector2(r.sizeDelta.x - ((RectTransform)r.parent).rect.width, r.sizeDelta.y); }
        static Image Image(string name, Transform parent, float x, float y, float w, float h, IceUIColorRole role, bool blocks = false)
        {
            var r = Rect(name, parent, x, y, w, h); var image = r.gameObject.AddComponent<Image>(); image.sprite = pixel; image.raycastTarget = blocks;
            var style = r.gameObject.AddComponent<IceUIStyle>(); style.theme = theme; style.image = image; style.colorRole = role; style.applySprite = true; style.spriteRole = IceUISpriteRole.Panel; style.Apply(); return image;
        }
        static TextMeshProUGUI Text(string name, Transform parent, string content, float x, float y, float w, float h, float size = 19, IceUIColorRole role = IceUIColorRole.Text)
        {
            var r = Rect(name, parent, x, y, w, h); var text = r.gameObject.AddComponent<TextMeshProUGUI>(); text.text = content; text.fontSize = size; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow;
            var style = r.gameObject.AddComponent<IceUIStyle>(); style.theme = theme; style.text = text; style.colorRole = role; style.Apply(); return text;
        }
        static GameObject BuildButtonTemplate()
        {
            // A true variant of the existing Phase 3 action art, with only its command binding replaced.
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(IceNavigationFactory.Folder + "/Aurora Route Action.prefab"));
            root.name = "Aurora Menu Button"; Object.DestroyImmediate(root.GetComponent<IceRouteAction>());
            var action = root.AddComponent<IceMenuAction>(); action.button = root.GetComponent<Button>(); action.kind = IceMenuActionKind.ToggleHelp;
            action.button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            root.GetComponentInChildren<TMP_Text>().text = "Operator's guide"; return root;
        }
        static GameObject BuildSettingTemplate()
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Experiments/IceResurfacer/UI/Prefabs/Machinery/Widgets/Aurora Bound Slider.prefab"));
            root.name = "Aurora Operator Setting"; var old = root.GetComponent<IceConsoleControl>(); var slider = old.slider; var value = old.value; Object.DestroyImmediate(old);
            var setting = slider.gameObject.AddComponent<IceMenuSetting>(); setting.slider = slider; setting.value = value;
            var r = root.GetComponent<RectTransform>(); r.sizeDelta = new Vector2(800, 136);
            var caption = root.transform.Find("Label").GetComponent<TextMeshProUGUI>(); caption.text = "Forward speed limit"; caption.rectTransform.anchoredPosition = Vector2.zero; caption.rectTransform.sizeDelta = new Vector2(460, 32); caption.fontSize = 23;
            value.rectTransform.anchorMin = value.rectTransform.anchorMax = Vector2.one; value.rectTransform.anchoredPosition = new Vector2(-300, 0); value.rectTransform.sizeDelta = new Vector2(300, 32); value.fontSize = 26; value.text = "4.75 mph";
            var sr = slider.GetComponent<RectTransform>(); sr.anchoredPosition = new Vector2(0, -50); sr.sizeDelta = new Vector2(800, 28); Width(sr);
            foreach (Transform child in sr) if (child is RectTransform childRect) { childRect.anchorMin = Vector2.zero; childRect.anchorMax = Vector2.one; childRect.offsetMin = childRect.offsetMax = Vector2.zero; }
            var fillArea = slider.fillRect.parent as RectTransform; fillArea.anchorMin = new Vector2(0, .5f); fillArea.anchorMax = new Vector2(1, .5f); fillArea.sizeDelta = new Vector2(0, 4); fillArea.anchoredPosition = Vector2.zero;
            slider.fillRect.offsetMin = slider.fillRect.offsetMax = Vector2.zero;
            var handleArea = slider.handleRect.parent as RectTransform; handleArea.offsetMin = new Vector2(9, 0); handleArea.offsetMax = new Vector2(-9, 0);
            slider.handleRect.pivot = new Vector2(.5f, .5f); slider.handleRect.anchorMin = Vector2.zero; slider.handleRect.anchorMax = Vector2.up; slider.handleRect.sizeDelta = new Vector2(18, 0); slider.handleRect.anchoredPosition = Vector2.zero;
            slider.minValue = 3; slider.maxValue = 8; slider.SetValueWithoutNotify(4.75f); slider.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            setting.detail = Text("Setting detail", root.transform, "7.64 km/h / saved between sessions", 0, 86, 800, 48, 18, IceUIColorRole.Muted); Width(setting.detail.rectTransform);
            root.GetComponent<Image>().raycastTarget = false; root.GetComponent<Image>().enabled = false; return root;
        }
        static GameObject BuildRowTemplate()
        {
            var root = Rect("Aurora Result Row", null, 0, 0, 384, 32); var widget = root.gameObject.AddComponent<IceResultWidget>(); widget.kind = IceResultKind.Coverage;
            Text("Caption", root, "COVERAGE", 0, 1, 182, 25, 16, IceUIColorRole.Muted);
            widget.value = Text("Value", root, "87.5%", 184, 0, 110, 26, 17);
            widget.points = Text("Points", root, "35.0 / 40", 302, 1, 82, 25, 16, IceUIColorRole.Muted); widget.pointsFormat = "{0:0.0} / 40"; return root.gameObject;
        }
        static IceMenuScreen Screen(string name, IceMenuScreenKind kind, float w, float h, Vector2 centerOffset, out RectTransform panel)
        {
            var root = Rect(name, null, 0, 0, 1600, 900); Stretch(root); var screen = root.gameObject.AddComponent<IceMenuScreen>(); screen.kind = kind; screen.visibility = root.gameObject.AddComponent<CanvasGroup>();
            var backdrop = Image("Full-screen background - replace artwork", root, 0, 0, 1600, 900, IceUIColorRole.Panel, true); Stretch(backdrop.rectTransform);
            backdrop.GetComponent<IceUIStyle>().applyColor = false; backdrop.color = new Color32(40, 42, 38, 235);
            panel = Rect("Editable paper panel", root, 0, 0, w, h); panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f); panel.anchoredPosition = centerOffset;
            var border = Image("Border artwork", panel, 0, 0, w, h, IceUIColorRole.Text); Stretch(border.rectTransform);
            var paper = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Aurora Menu Panel.prefab"), panel);
            paper.name = "Panel background - replace artwork"; Stretch(paper.GetComponent<RectTransform>(), 1, 1, 1, 1);
            screen.previewBadge = Text("Design preview - hidden in Play", root, "DESIGN PREVIEW / SAMPLE DATA", 24, 20, 450, 24, 16, IceUIColorRole.Warning).gameObject;
            return screen;
        }
        static IceMenuAction Action(IceMenuScreen screen, Transform parent, IceMenuActionKind kind, string caption, float x, float y, float w, float h = 49)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPath), parent); root.name = caption;
            var r = root.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
            var action = root.GetComponent<IceMenuAction>(); action.screen = screen; action.kind = kind; root.GetComponentInChildren<TMP_Text>().text = caption;
            PrefabUtility.RecordPrefabInstancePropertyModifications(action); return action;
        }
        static void SetNavigation(IceMenuScreen screen, params Selectable[] targets)
        { screen.navigationOrder = targets; screen.firstSelection = targets.Length == 0 ? null : targets[0]; }
        static GameObject BuildWelcome()
        {
            var screen = Screen("Welcome - Start Screen", IceMenuScreenKind.Welcome, 835, 440, new Vector2(-22.5f, 65), out var panel);
            Text("Subtitle", panel, "NORTHLINE RINK / OPERATOR SHEET", 36, 51, 730, 25, 17, IceUIColorRole.Muted);
            Text("Title", panel, "Start a resurfacing session", 34, 91, 740, 47, 28);
            Image("Divider", panel, 36, 140, 749, 1, IceUIColorRole.Border);
            Text("Introduction", panel, "Scored and practice sessions give you full control of the machine. Practice leaves the score off. Assisted training adds equipment interlocks and operating prompts.", 36, 154, 740, 76, 19, IceUIColorRole.Muted);
            Text("Driving instructions", panel, "WASD drive / RMB look / Hold Q edge, E controls, C chase / X forward", 36, 240, 740, 34, 16);
            var scored = Action(screen, panel, IceMenuActionKind.StartScored, "Start scored session", 37, 292, 365);
            var practice = Action(screen, panel, IceMenuActionKind.StartPractice, "Start practice session", 420, 292, 365);
            var training = Action(screen, panel, IceMenuActionKind.StartTraining, "Assisted training", 37, 357, 365);
            var help = Action(screen, panel, IceMenuActionKind.ToggleHelp, "Operator's guide", 420, 357, 365);
            var logo = Image("Logo artwork - optional", panel, 35, 10, 240, 32, IceUIColorRole.Text); logo.gameObject.SetActive(false);
            SetNavigation(screen, scored.button, practice.button, training.button, help.button); return screen.gameObject;
        }
        static IceMenuSetting Setting(IceMenuScreen screen, Transform parent, IceMenuSettingKind kind, float x, float y)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SettingPath), parent); root.name = kind == IceMenuSettingKind.ForwardSpeed ? "Forward speed setting" : "Seated eye fallback setting";
            root.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, -y); var setting = root.GetComponentInChildren<IceMenuSetting>(); setting.screen = screen; setting.kind = kind;
            if (kind == IceMenuSettingKind.SeatedEye)
            {
                setting.valueFormat = "{0:+0;-0;0} cm"; setting.displayMultiplier = 100; setting.value.text = "+6 cm";
                root.transform.Find("Label").GetComponent<TMP_Text>().text = "Seated eye height"; setting.slider.minValue = -.08f; setting.slider.maxValue = .12f; setting.slider.SetValueWithoutNotify(.06f);
                setting.detail.text = "The authored Driver Eye Anchor controls seated height. This fallback offset does not move that anchor.";
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(setting); return setting;
        }
        static GameObject BuildPause()
        {
            var screen = Screen("Pause - Operator Settings", IceMenuScreenKind.Pause, 900, 650, new Vector2(0, -15), out var panel);
            Text("Heading", panel, "Operator settings", 50, 35, 800, 50, 28); Image("Divider", panel, 50, 84, 800, 1, IceUIColorRole.Border);
            var resume = Action(screen, panel, IceMenuActionKind.Resume, "Resume", 50, 102, 220, 48);
            var help = Action(screen, panel, IceMenuActionKind.ToggleHelp, "Operator's guide", 290, 102, 245, 48);
            var fresh = Action(screen, panel, IceMenuActionKind.NewSessionSheet, "New session", 555, 102, 295, 48);
            var eye = Setting(screen, panel, IceMenuSettingKind.SeatedEye, 50, 172);
            var speed = Setting(screen, panel, IceMenuSettingKind.ForwardSpeed, 50, 318);
            var refill = Action(screen, panel, IceMenuActionKind.RefillPractice, "Refill practice — start with partly empty tanks", 50, 480, 800, 48);
            Text("Range and controls", panel, "Range: 3.00–8.00 mph, in 0.25 mph steps.\nEsc resumes. Numpad 1 shows or hides the instruments.", 50, 566, 800, 54, 19, IceUIColorRole.Muted);
            SetNavigation(screen, resume.button, help.button, fresh.button, eye.slider, speed.slider, refill.button); return screen.gameObject;
        }
        static ScrollRect Scroll(Transform parent, float x, float y, float w, float h, out RectTransform content)
        {
            var image = Image("Scrollable instructions", parent, x, y, w, h, IceUIColorRole.Paper, true); var scroll = image.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", image.transform, 0, 0, w, h); Stretch(viewport, 0, 0, 22); viewport.gameObject.AddComponent<RectMask2D>();
            content = Rect("Authored help sections", viewport, 0, 0, w - 22, h); content.anchorMax = Vector2.one; content.sizeDelta = new Vector2(0, h);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 18; layout.padding = new RectOffset(0, 10, 0, 12); layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 32; scroll.movementType = ScrollRect.MovementType.Clamped;
            var barImage = Image("Scrollbar", image.transform, w - 15, 0, 14, h, IceUIColorRole.Inset, true);
            barImage.rectTransform.anchorMin = new Vector2(1, 0); barImage.rectTransform.anchorMax = Vector2.one; barImage.rectTransform.offsetMin = new Vector2(-15, 0); barImage.rectTransform.offsetMax = Vector2.zero;
            var handle = Image("Thumb", barImage.transform, 0, 0, 14, 80, IceUIColorRole.Muted, true); Stretch(handle.rectTransform);
            var bar = barImage.gameObject.AddComponent<Scrollbar>(); bar.handleRect = handle.rectTransform; bar.targetGraphic = handle; bar.direction = Scrollbar.Direction.BottomToTop; bar.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.verticalScrollbar = bar; return scroll;
        }
        static void Section(Transform parent, string heading, string body)
        {
            var title = Text(heading, parent, heading, 0, 0, 1120, 32, 21); var le = title.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = le.minHeight = 32;
            var text = Text("Editable instructions", parent, body, 0, 0, 1120, 100, 19);
            var element = text.gameObject.AddComponent<LayoutElement>(); element.preferredHeight = 100;
            var height = text.gameObject.AddComponent<IceUITextHeight>(); height.text = text; height.minimumHeight = 44; height.Refresh();
        }
        static GameObject BuildHelp()
        {
            var screen = Screen("Operator Guide", IceMenuScreenKind.Help, 1240, 650, new Vector2(0, 25), out var panel);
            Text("Heading", panel, "Operator's quick guide", 34, 25, 950, 44, 28); Image("Divider", panel, 36, 76, 1170, 1, IceUIColorRole.Border);
            var close = Action(screen, panel, IceMenuActionKind.ToggleHelp, "Close F1", 1074, 26, 132, 36);
            Scroll(panel, 36, 93, 1170, 520, out var content);
            Section(content, "01  PREPARE & RESURFACE", "Back onto ice with S; hold C for chase view. Lower the conditioner, run both augers and start near 0.35 mm. Open wash, prime 10 seconds, then start pickup.\n\nFollow the previous wet edge with the outer tire. Hover the survey for exact cells; F2 shows treatment. Pickup recovers liquid; deep grooves need more cutting.");
            Section(content, "02  RETURN & EMPTY", "Stow equipment for safe travel through the threshold. Dump toward the pit at the rear of the service room; snow lands where the outlet sends it. Brief lower / raise movements shake out residue.");
            Section(content, "03  KEYBOARD & MACHINERY", "1 / Right Shift+1 lower / raise conditioner   2/3 augers   4 brush   5 pump   6 plunger   7 / Right Shift+7 raise / lower tank\n\n[ / ] flood   Right Shift+[ / ] wash   , / . fine blade   J jam reset   F mount / dismount   TAB compact\n\nW/S drive   A/D steer   CTRL brake   NUM 1 video   NUM 7 GPS / 8 routes / 9 record & save   ESC pause / speed");
            Section(content, "04  CAMERA CONTROLS", "HOLD Q edge / E controls / C chase / LEFT SHIFT rear; release restores view. X forward R stand SPACE rise RMB look");
            Section(content, "05  MACHINE LOAD", "Machine Load: 13% idle plus cutting, augers, snow and congestion. Higher load slows acceleration. Click the meter or F3 for the breakdown.");
            Section(content, "06  ON-FOOT SERVICE", "F steps off. Mouse looks freely on foot; WASD walks. E takes, connects and returns the hose and operates supply. The wall station is right of the parked machine.\n\nClose supply, uncouple, return the nozzle and F to board. Driving beyond hose reach breaks the coupling and spills water.");
            var optional = Image("Optional diagram - add your artwork", content, 0, 0, 1120, 180, IceUIColorRole.Paper); optional.gameObject.AddComponent<LayoutElement>().preferredHeight = 180; optional.gameObject.SetActive(false);
            SetNavigation(screen, close.button); return screen.gameObject;
        }
        static IceResultWidget Row(Transform parent, IceResultKind kind, string caption, float x, float y, string previewValue, string points = "", string format = "{0:0.0}%", float multiplier = 100)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RowPath), parent); root.name = caption;
            root.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, -y); var widget = root.GetComponent<IceResultWidget>(); widget.kind = kind; widget.valueFormat = format; widget.multiplier = multiplier;
            widget.value.text = previewValue; widget.points.text = points; root.transform.Find("Caption").GetComponent<TMP_Text>().text = caption;
            widget.pointsFormat = kind == IceResultKind.Coverage ? "{0:0.0} / 40" : kind == IceResultKind.FinishQuality ? "{0:0.0} / 30" : kind == IceResultKind.WaterDistribution || kind == IceResultKind.ExcessWater ? "{0:0.0} / 10" : kind == IceResultKind.Misuse ? "-{0:0.0}" : "{0:0.0} / 5";
            PrefabUtility.RecordPrefabInstancePropertyModifications(widget); return widget;
        }
        static IceResultWidget Value(Transform parent, IceResultKind kind, string name, string preview, float x, float y, float w, float h, float size)
        {
            var text = Text(name, parent, preview, x, y, w, h, size); var widget = text.gameObject.AddComponent<IceResultWidget>(); widget.kind = kind; widget.value = text; return widget;
        }
        static GameObject BuildResults()
        {
            var screen = Screen("Results - Session Review", IceMenuScreenKind.Results, 835, 610, new Vector2(-17.5f, 10), out var panel);
            Value(panel, IceResultKind.Heading, "Session heading", "FULL RUN COMPLETE", 34, 24, 755, 27, 17);
            var score = Value(panel, IceResultKind.Score, "Final score", "82.7 / 100", 32, 61, 530, 45, 28); score.valueFormat = "{0:0.0} / 100"; score.multiplier = 1;
            Value(panel, IceResultKind.Duration, "Elapsed time", "12:34", 605, 70, 190, 25, 17);
            Value(panel, IceResultKind.Mode, "Session mode", "SCORED / FREE SIMULATION", 34, 112, 755, 24, 16);
            Image("Divider", panel, 34, 140, 764, 1, IceUIColorRole.Border);
            Row(panel, IceResultKind.Coverage, "COVERAGE", 34, 156, "98.2%", "39.3 / 40");
            Row(panel, IceResultKind.FinishQuality, "SURFACE QUALITY", 34, 191, "88.0%", "26.4 / 30");
            Row(panel, IceResultKind.WaterDistribution, "WATER DISTRIBUTION", 34, 226, "84.0%", "8.4 / 10");
            Row(panel, IceResultKind.Missed, "MISSED PATCHES", 34, 261, "1.8%");
            Row(panel, IceResultKind.ExcessWater, "EXCESS WATER", 425, 156, "2.4%", "9.8 / 10");
            Row(panel, IceResultKind.Efficiency, "WATER EFFICIENCY", 425, 191, "72.0%", "3.6 / 5");
            Row(panel, IceResultKind.Handling, "TANK / SAFE RETURN", 425, 226, "2 dumps", "5.0 / 5", "{0:0} dumps", 1);
            Row(panel, IceResultKind.Misuse, "EQUIPMENT MISUSE", 425, 261, "3 faults", "-3.6", "{0:0} faults", 1);
            Text("Scoring explanation", panel, "Shaving mask excludes the outer 15.24 cm. Water and finish still include the whole sheet.", 34, 306, 755, 40, 16, IceUIColorRole.Muted);
            Row(panel, IceResultKind.WaterCoverage, "FLOOD REACHED", 34, 356, "98.6%"); Row(panel, IceResultKind.ShavingCoverage, "SHAVED REACHABLE", 425, 356, "99.1%");
            Value(panel, IceResultKind.Completion, "Completion message", "Complete sheet. Inspect the survey for finish differences.", 34, 400, 755, 42, 16);
            Row(panel, IceResultKind.SelectedSpeed, "OPERATOR SPEED LIMIT", 34, 452, "4.75 MPH", "", "{0:F2} MPH", 1);
            Row(panel, IceResultKind.OriginalSpeed, "ORIGINAL SPEED", 425, 452, "5.93 MPH", "", "{0:F2} MPH", 1);
            var scored = Action(screen, panel, IceMenuActionKind.StartScored, "New scored session", 36, 530, 229, 46);
            var practice = Action(screen, panel, IceMenuActionKind.StartPractice, "New practice session", 286, 530, 229, 46);
            var help = Action(screen, panel, IceMenuActionKind.ToggleHelp, "Operator's guide", 537, 530, 261, 46);
            SetNavigation(screen, scored.button, practice.button, help.button); return screen.gameObject;
        }
        static GameObject BuildRoot()
        {
            var root = Rect("Aurora Session Screens", null, 0, 0, 1600, 900); Stretch(root); var owner = root.gameObject.AddComponent<IceMenuScreens>();
            string[] paths = { "Aurora Welcome", "Aurora Pause Settings", "Aurora Operator Guide", "Aurora Results" };
            var screens = new List<IceMenuScreen>();
            foreach (string name in paths)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + name + ".prefab"), root);
                var screen = instance.GetComponent<IceMenuScreen>(); screen.owner = owner;
                IceNavigationUI.Group(screen.visibility, screen.kind == IceMenuScreenKind.Welcome, false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(screen); PrefabUtility.RecordPrefabInstancePropertyModifications(screen.visibility); screens.Add(screen);
            }
            owner.welcome = screens[0]; owner.pause = screens[1]; owner.help = screens[2]; owner.results = screens[3];
            owner.actions = root.GetComponentsInChildren<IceMenuAction>(true); foreach (var action in owner.actions) { action.owner = owner; PrefabUtility.RecordPrefabInstancePropertyModifications(action); }
            owner.settings = root.GetComponentsInChildren<IceMenuSetting>(true); foreach (var setting in owner.settings) { setting.owner = owner; PrefabUtility.RecordPrefabInstancePropertyModifications(setting); }
            owner.resultWidgets = owner.results.GetComponentsInChildren<IceResultWidget>(true); return root.gameObject;
        }
        [MenuItem("Tools/Aurora/UI/Install Missing Phase 4 Screens")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
            var hud = Object.FindAnyObjectByType<IceAuthoredHud>(FindObjectsInactive.Include);
            if (hud == null || hud.game == null || hud.canvas == null) throw new InvalidOperationException("Open the existing authored scene with its bound Aurora UI.");
            var parent = hud.canvas.transform.Find("Menus"); if (parent == null) throw new InvalidOperationException("Existing Menus group is missing.");
            if (parent.GetComponentInChildren<IceMenuScreens>(true) != null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RootPath); if (prefab == null) throw new InvalidOperationException("Phase 4 prefab assets are missing.");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent); Undo.RegisterCreatedObjectUndo(root, "Install Aurora Phase 4 screens");
            var owner = root.GetComponent<IceMenuScreens>(); owner.game = hud.game; owner.canvas = hud.canvas; owner.raycaster = hud.raycaster;
            PrefabUtility.RecordPrefabInstancePropertyModifications(owner); EditorSceneManager.MarkSceneDirty(hud.game.gameObject.scene);
        }
    }
}
#endif
