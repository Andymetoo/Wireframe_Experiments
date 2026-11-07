#if UNITY_EDITOR
using System;
using System.IO;
using IceResurfacer.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IceResurfacer.Editor
{
    /// <summary>Creates only missing Phase 5 assets; never rebuilds previous authored UI.</summary>
    public static class IceOperatorFactory
    {
        public const string Folder = "Assets/Experiments/IceResurfacer/UI/Prefabs/Operator";
        public const string RootPath = Folder + "/Aurora Operator Interface.prefab";
        public const string NoticePath = Folder + "/Aurora Operator Notice.prefab";
        public const string RowPath = Folder + "/Aurora Message History Row.prefab";
        public const string ButtonPath = Folder + "/Aurora Operator Action.prefab";
        public const string ProjectionPath = Folder + "/Aurora Projected GPS.prefab";
        public const string ScenePath = IceMenuFactory.ScenePath;
        static IceUIStyleSheet theme;
        static Sprite pixel;
        public static void RequireIsolated()
        { if (!File.Exists("UIAuthoringPhase5ValidationProject.txt")) throw new InvalidOperationException("Use the marked Phase 5 disposable project."); }
        public static void BuildBatch()
        {
            try
            {
                RequireIsolated(); BuildNewAssets(); EditorSceneManager.OpenScene(ScenePath); Install();
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
                Directory.CreateDirectory("ValidationResults"); File.Copy(ScenePath, "ValidationResults/Installed-Phase5.unity", true);
                Debug.Log("PHASE5 BUILD PASS: additive operator interface and editable variants."); EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        public static void BuildNewAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            theme = AssetDatabase.LoadAssetAtPath<IceUIStyleSheet>("Assets/Experiments/IceResurfacer/UI/Themes/Aurora UI Theme.asset");
            pixel = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Experiments/IceResurfacer/UI/Artwork/Placeholder White.png");
            if (theme == null || pixel == null) throw new InvalidOperationException("Existing Aurora theme and placeholder sprite required.");
            SaveNew(ButtonPath, ButtonTemplate); SaveNew(NoticePath, NoticeTemplate);
            SaveNew(Folder + "/Aurora Critical Notice.prefab", () => MessageVariant("Aurora Critical Notice", IceUIColorRole.Warning));
            SaveNew(Folder + "/Aurora Context Hint.prefab", () => MessageVariant("Aurora Context Hint", IceUIColorRole.Text));
            SaveNew(Folder + "/Aurora Drive Warning.prefab", () => { var root = MessageVariant("Aurora Drive Warning", IceUIColorRole.Warning); var style = root.GetComponent<IceUIStyle>(); style.applyColor = false; root.GetComponent<Image>().color = IceUITheme.AlertFace; return root; });
            SaveNew(RowPath, RowTemplate); SaveNew(ProjectionPath, ProjectionTemplate); SaveNew(RootPath, BuildRoot); AssetDatabase.SaveAssets();
        }
        static void SaveNew(string path, Func<GameObject> build)
        {
            if (File.Exists(path)) return;
            var root = build();
            try
            {
                foreach (var component in root.GetComponentsInChildren<Component>(true)) if (component != null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        static GameObject Instance(string path, Transform parent = null) => (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);
        static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; var r = go.GetComponent<RectTransform>(); r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
        }
        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        static Image Image(string name, Transform parent, float x, float y, float w, float h, IceUIColorRole role, bool blocks)
        {
            var r = Rect(name, parent, x, y, w, h); var image = r.gameObject.AddComponent<Image>(); image.sprite = pixel; image.raycastTarget = blocks;
            var style = r.gameObject.AddComponent<IceUIStyle>(); style.theme = theme; style.image = image; style.colorRole = role; style.applySprite = true; style.Apply(); return image;
        }
        static TextMeshProUGUI Text(string name, Transform parent, string content, float x, float y, float w, float h, float size = 16, IceUIColorRole role = IceUIColorRole.Text)
        {
            var r = Rect(name, parent, x, y, w, h); var text = r.gameObject.AddComponent<TextMeshProUGUI>(); text.fontSize = size; text.text = content; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow;
            var style = r.gameObject.AddComponent<IceUIStyle>(); style.theme = theme; style.text = text; style.colorRole = role; style.fontRole = size <= 16 ? IceUIFontRole.Secondary : IceUIFontRole.Primary; style.Apply(); return text;
        }
        static CanvasGroup Panel(string name, Transform parent, float x, float y, float w, float h, bool preview)
        {
            var image = Image(name, parent, x, y, w, h, IceUIColorRole.Panel, true); var group = image.gameObject.AddComponent<CanvasGroup>(); IceNavigationUI.Group(group, preview, false); return group;
        }
        static GameObject ButtonTemplate()
        {
            var root = Instance(IceMenuFactory.ButtonPath); root.name = "Aurora Operator Action"; Object.DestroyImmediate(root.GetComponent<IceMenuAction>());
            var action = root.AddComponent<IceOperatorAction>(); action.button = root.GetComponent<Button>();
            action.button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None }; return root;
        }
        static GameObject NoticeTemplate()
        {
            var root = Instance(IceMenuFactory.Folder + "/Aurora Menu Panel.prefab"); root.name = "Aurora Operator Notice";
            root.GetComponent<Image>().raycastTarget = false;
            root.GetComponent<IceUIStyle>().colorRole = IceUIColorRole.Panel; root.GetComponent<IceUIStyle>().Apply();
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(806, 58);
            var view = root.AddComponent<IceOperatorMessage>(); view.visibility = root.AddComponent<CanvasGroup>();
            view.content = Text("Message content", root.transform, "Operator notice / representative sample", 16, 9, 774, 42);
            var rect = view.content.rectTransform; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(16, 7); rect.offsetMax = new Vector2(-16, -9);
            var icon = Image("Optional icon - your artwork", root.transform, 10, 10, 28, 28, IceUIColorRole.Accent, false); icon.gameObject.SetActive(false); return root;
        }
        static GameObject MessageVariant(string name, IceUIColorRole role)
        {
            var root = Instance(NoticePath); root.name = name; var style = root.GetComponent<IceOperatorMessage>().content.GetComponent<IceUIStyle>(); style.colorRole = role; style.Apply(); return root;
        }
        static GameObject RowTemplate()
        {
            var root = Rect("Aurora Message History Row", null, 0, 0, 584, 77); var row = root.gameObject.AddComponent<IceMessageHistoryRow>();
            row.content = Text("History message", root, "02:14  Representative operator message", 0, 0, 584, 72); Stretch(row.content.rectTransform);
            var element = root.gameObject.AddComponent<LayoutElement>(); element.preferredHeight = 77; element.minHeight = 77; return root.gameObject;
        }
        static GameObject ProjectionTemplate()
        {
            var rect = Rect("Aurora Projected GPS", null, 0, 0, 1600, 900); Stretch(rect);
            var graphic = rect.gameObject.AddComponent<IceGpsProjectedGraphic>(); graphic.raycastTarget = false; graphic.color = Color.white; return rect.gameObject;
        }
        static void Projection(IceOperatorOverlay owner, Transform root)
        {
            var projection = Instance(ProjectionPath, root); projection.transform.SetAsFirstSibling(); owner.gpsProjection = projection.GetComponent<IceGpsProjectedGraphic>(); owner.gpsProjection.owner = owner;
        }
        static IceOperatorAction Action(IceOperatorOverlay owner, Transform parent, IceOperatorActionKind kind, string caption, float x, float y, float w, float h)
        {
            var root = Instance(ButtonPath, parent); root.name = caption; var rect = root.GetComponent<RectTransform>(); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
            var action = root.GetComponent<IceOperatorAction>(); action.owner = owner; action.kind = kind; root.GetComponentInChildren<TMP_Text>().text = caption; return action;
        }
        static IceOperatorMessage Message(Transform parent, string prefab, string name, float y, string preview)
        {
            var root = Instance(Folder + "/" + prefab + ".prefab", parent); root.name = name;
            var rect = root.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(398, -y); rect.sizeDelta = new Vector2(806, name == "Cockpit interaction hint" ? 76 : 58);
            var message = root.GetComponent<IceOperatorMessage>(); message.content.text = preview; IceNavigationUI.Group(message.visibility, name == "Routine operator notice", false); return message;
        }
        static GameObject BuildRoot()
        {
            var root = Rect("Aurora Operator Interface", null, 0, 0, 1600, 900); Stretch(root); var owner = root.gameObject.AddComponent<IceOperatorOverlay>();
            Projection(owner, root);
            var service = Panel("On-foot water service", root, 220, 690, 1160, 191, false); owner.serviceGroup = service;
            Text("Service heading", service.transform, "WATER SERVICE", 22, 13, 185, 30, 17, IceUIColorRole.Accent);
            Service(owner, IceServiceField.Hose, "FLOOD WATER CONNECTED", 220, 13, 380, 30, 17);
            Service(owner, IceServiceField.Reach, "8.4 / 11 m", 630, 13, 300, 30, 17);
            Service(owner, IceServiceField.Supply, "SUPPLY 100%", 22, 44, 285, 28);
            Service(owner, IceServiceField.Flow, "3.0 L/s", 330, 44, 260, 28);
            Service(owner, IceServiceField.Overflow, "OVERFLOW — CLOSE WALL VALVE", 640, 44, 495, 28);
            Service(owner, IceServiceField.Prompt, "E / CLICK  Disconnect FLOOD WATER hose", 22, 78, 1116, 30, 19);
            Service(owner, IceServiceField.Notice, "Water flowing. Watch the tank level; close the wall valve when full.", 22, 113, 1116, 38);
            Text("Editable service instructions", service.transform, "WASD walk   Mouse look   E interact   F board near the steps   ESC releases cursor", 22, 156, 1116, 25, 16, IceUIColorRole.Muted);
            var strip = Rect("Bottom actions and status", root, 398, 849, 1174, 43); owner.statusGroup = strip.gameObject.AddComponent<CanvasGroup>(); IceNavigationUI.Group(owner.statusGroup, true, false);
            Image("Status background", strip, 0, 0, 806, 43, IceUIColorRole.Panel, true);
            owner.status = Text("Navigation and recording status", strip, "Q edge / E controls / C chase / L Shift rear | NUM 7 GPS", 16, 11, 594, 23, 16, IceUIColorRole.Muted);
            Action(owner, strip, IceOperatorActionKind.Routes, "Routes [Num 8]", 620, 2, 188, 37);
            Action(owner, strip, IceOperatorActionKind.History, "Messages", 830, 2, 128, 37);
            Action(owner, strip, IceOperatorActionKind.Help, "Help F1", 966, 2, 93, 37);
            Action(owner, strip, IceOperatorActionKind.Pause, "Pause", 1067, 2, 107, 37);
            var perf = Panel("Performance readout F4", root, 28, 865, 340, 27, true); owner.performanceGroup = perf;
            perf.GetComponent<Image>().raycastTarget = false;
            owner.performance = Text("Live performance summary", perf.transform, "60 FPS / 16.7 ms   F4", 10, 3, 320, 23, 16, IceUIColorRole.Muted);
            owner.routine = Message(root, "Aurora Operator Notice", "Routine operator notice", 782, "Wall supply closed. The hose can be safely disconnected.");
            owner.hint = Message(root, "Aurora Context Hint", "Cockpit interaction hint", 764, "Flood water valve / drag to adjust the existing valve.");
            owner.driveWarning = Message(root, "Aurora Drive Warning", "Drive inhibit warning", 714, "Training assistance: stow equipment before driving.");
            owner.critical = Message(root, "Aurora Critical Notice", "Critical machinery notice", 782, "AUGER JAM — retract blade; stop conveyors, clear and reset [J]");
            History(owner, root);
            owner.actions = root.GetComponentsInChildren<IceOperatorAction>(true); owner.serviceFields = service.GetComponentsInChildren<IceServiceReadout>(true);
            owner.previewBadge = Text("Design preview - hidden in Play", root, "DESIGN PREVIEW / SAMPLE DATA", 24, 900 - 45, 365, 23, 14, IceUIColorRole.Warning).gameObject;
            owner.critical.transform.SetAsLastSibling();
            return root.gameObject;
        }
        static void Service(IceOperatorOverlay owner, IceServiceField kind, string preview, float x, float y, float w, float h, float size = 16)
        {
            var text = Text(kind.ToString(), owner.serviceGroup.transform, preview, x, y, w, h, size, kind == IceServiceField.Notice || kind == IceServiceField.Overflow ? IceUIColorRole.Warning : IceUIColorRole.Text);
            var field = text.gameObject.AddComponent<IceServiceReadout>(); field.kind = kind; field.value = text;
            if (kind == IceServiceField.Reach || kind == IceServiceField.Supply || kind == IceServiceField.Flow)
            { var style = text.GetComponent<IceUIStyle>(); style.fontRole = IceUIFontRole.Numeric; style.Apply(); }
        }
        // Only the marked, new-asset development copy may finish these defaults.
        // The normal installer and BuildNewAssets never overwrite existing assets.
        public static void FinalizeNewDefaults()
        {
            RequireIsolated(); BuildNewAssets();
            var template = PrefabUtility.LoadPrefabContents(NoticePath);
            try { template.GetComponent<Image>().raycastTarget = false; PrefabUtility.RecordPrefabInstancePropertyModifications(template.GetComponent<Image>()); PrefabUtility.SaveAsPrefabAsset(template, NoticePath); }
            finally { PrefabUtility.UnloadPrefabContents(template); }
            var root = PrefabUtility.LoadPrefabContents(RootPath);
            try
            {
                var owner = root.GetComponent<IceOperatorOverlay>(); owner.critical.transform.SetAsLastSibling();
                foreach (var message in new[] { owner.routine, owner.critical, owner.driveWarning, owner.hint }) message.GetComponent<Image>().raycastTarget = false;
                owner.performanceGroup.GetComponent<Image>().raycastTarget = false;
                if (owner.gpsProjection == null) Projection(owner, root.transform);
                foreach (var field in owner.serviceFields)
                    if (field.kind == IceServiceField.Reach || field.kind == IceServiceField.Supply || field.kind == IceServiceField.Flow)
                    { var style = field.value.GetComponent<IceUIStyle>(); style.fontRole = IceUIFontRole.Numeric; style.Apply(); }
                foreach (var component in root.GetComponentsInChildren<Component>(true)) if (component != null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                PrefabUtility.SaveAsPrefabAsset(root, RootPath); AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static void History(IceOperatorOverlay owner, Transform root)
        {
            var box = Panel("Operator message history", root, 520, 160, 650, 570, false); owner.historyGroup = box;
            Text("History heading", box.transform, "OPERATOR MESSAGES", 20, 19, 440, 28, 17, IceUIColorRole.Accent);
            Action(owner, box.transform, IceOperatorActionKind.CloseHistory, "Close", 530, 14, 100, 34);
            var toggle = Action(owner, box.transform, IceOperatorActionKind.RoutineMessages, "Routine messages: on", 20, 60, 610, 35); owner.routineToggleCaption = toggle.GetComponentInChildren<TMP_Text>();
            owner.historyAdvice = Text("Existing session advice", box.transform, "Free simulation. Equipment consequences and scoring follow the actual surface. F1 opens the operating guide.", 20, 108, 610, 48, 16, IceUIColorRole.Muted);
            var area = Image("History scroll viewport", box.transform, 20, 168, 610, 380, IceUIColorRole.Panel, true); var scroll = area.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", area.transform, 0, 0, 610, 380); Stretch(viewport); viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Live rows - editable layout", viewport, 0, 0, 586, 380); content.anchorMax = Vector2.one; content.sizeDelta = new Vector2(-24, 380);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.childControlHeight = layout.childControlWidth = true; layout.childForceExpandHeight = false; layout.spacing = 0;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content; scroll.viewport = viewport; scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
            owner.historyContent = content; owner.historyRowPrefab = AssetDatabase.LoadAssetAtPath<IceMessageHistoryRow>(RowPath);
            var samples = Rect("Sample history - hidden in Play", viewport, 0, 0, 584, 231); owner.historyPreviewRows = samples.gameObject;
            for (int i = 0; i < 3; i++) { var row = Instance(RowPath, samples); row.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -i * 77); row.GetComponent<IceMessageHistoryRow>().Bind((3 - i).ToString("00") + ":14  Representative operator notice / sample data"); }
            owner.historyEmpty = Text("Empty history message", box.transform, "No messages this session.", 20, 168, 610, 40); owner.historyEmpty.gameObject.SetActive(false);
        }
        [MenuItem("Tools/Aurora/UI/Install Missing Phase 5 Operator Interface")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
            var hud = Object.FindAnyObjectByType<IceAuthoredHud>(FindObjectsInactive.Include);
            if (hud == null || hud.game == null) throw new InvalidOperationException("Open the existing authored scene.");
            var parent = hud.canvas.transform.Find("Messages"); if (parent == null) throw new InvalidOperationException("Existing Messages group missing.");
            if (parent.GetComponentInChildren<IceOperatorOverlay>(true) != null) return;
            var root = Instance(RootPath, parent); Undo.RegisterCreatedObjectUndo(root, "Install Aurora operator interface");
            var owner = root.GetComponent<IceOperatorOverlay>(); owner.game = hud.game; owner.canvas = hud.canvas; owner.raycaster = hud.raycaster;
            PrefabUtility.RecordPrefabInstancePropertyModifications(owner); EditorSceneManager.MarkSceneDirty(hud.game.gameObject.scene);
        }
    }
}
#endif
