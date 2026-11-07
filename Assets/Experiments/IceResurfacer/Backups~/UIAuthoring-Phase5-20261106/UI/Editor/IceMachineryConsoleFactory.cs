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
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// One-time Editor authoring and additive, Undo-backed installation. Never runs
    /// in Play Mode and never regenerates existing prefabs or the Phase 1 header.
    /// </summary>
    public static class IceMachineryConsoleFactory
    {
        public const string Folder = "Assets/Experiments/IceResurfacer/UI/Prefabs/Machinery";
        public const string ConsolePrefabPath = Folder + "/Aurora Machinery Console.prefab";
        const string ThemePath = "Assets/Experiments/IceResurfacer/UI/Themes/Aurora UI Theme.asset";
        const string PixelPath = "Assets/Experiments/IceResurfacer/UI/Artwork/Placeholder White.png";
        static IceUIStyleSheet theme;
        static Sprite pixel, knob;

        public static void BuildNewAssetsBatch()
        {
            try
            {
                BuildNewAssets();
                Debug.Log("Aurora Phase 2: new machinery console and reusable widget prefabs authored successfully.");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Development-only additive correction in a marked disposable project; never a regeneration command.</summary>
        public static void CompleteGeneratedStateAppearanceBatch()
        {
            try
            {
                CompletePhase2AuthoringAssets();
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        /// <summary>
        /// Completes only the newly generated Phase 2 prefabs inside the disposable
        /// validation project. No layout, art rest pose, Phase 1 asset or scene changes.
        /// </summary>
        public static void CompletePhase2AuthoringAssets()
        {
            RequireEditMode();
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!project.Contains("/Validation/UIAuthoring-Phase2/") ||
                !File.Exists(Path.Combine(project, "UIAuthoringPhase2ValidationProject.txt")))
                throw new InvalidOperationException("Complete generated assets only in the marked disposable Phase 2 project.");
            LoadStyle();
            int saved = 0, customCount = 0;
            foreach (string fullPath in Directory.GetFiles(Path.Combine(project, Folder), "*.prefab", SearchOption.AllDirectories))
            {
                string path = fullPath.Replace('\\', '/').Substring(project.Length + 1);
                var contents = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                try
                {
                    foreach (var control in contents.GetComponentsInChildren<IceConsoleControl>(true))
                    {
                        bool custom = control.kind == IceConsoleControlKind.Conditioner || control.kind == IceConsoleControlKind.TankLift ||
                            control.kind == IceConsoleControlKind.FloodValve || control.kind == IceConsoleControlKind.WashValve ||
                            control.kind == IceConsoleControlKind.Blade;
                        if (!custom || control.slider != null) continue;
                        if (control.selectable == null || control.selectable.targetGraphic != CustomStateGraphic(control))
                        { ConfigureCustomState(control); changed = true; }
                        if (control.kind == IceConsoleControlKind.FloodValve || control.kind == IceConsoleControlKind.WashValve ||
                            control.kind == IceConsoleControlKind.Blade)
                            changed |= AnchorRotaryArtwork(control);
                        if (control.selectable == null || control.selectable.targetGraphic == null)
                            throw new InvalidOperationException("Missing custom-control state target in " + path);
                        customCount++;
                    }
                    changed |= ApplyRepresentativePreview(contents);
                    if (changed) { PrefabUtility.SaveAsPrefabAsset(contents, path); saved++; }
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            if (customCount != 8)
                throw new InvalidOperationException("Expected 8 custom lever/rotary controls across the console and templates; found " + customCount);
            AssetDatabase.SaveAssets();
            Debug.Log("Aurora Phase 2: verified " + customCount + " custom selectable states; saved " + saved + " new prefabs with representative preview values.");
        }

        static bool ApplyRepresentativePreview(GameObject root)
        {
            bool changed = false;
            var owner = root.GetComponent<IceMachineryConsole>();
            if (owner != null && owner.compactSummary != null)
                changed |= SetPreviewText(owner.compactSummary, "AUGERS V ON / H OFF   FLOOD 45%");
            foreach (var control in root.GetComponentsInChildren<IceConsoleControl>(true))
            {
                string preview = null;
                switch (control.kind)
                {
                    case IceConsoleControlKind.VerticalAuger: preview = "ON"; break;
                    case IceConsoleControlKind.HorizontalAuger: preview = "OFF"; break;
                    case IceConsoleControlKind.Conditioner: preview = "45% UP"; break;
                    case IceConsoleControlKind.TankLift: preview = "0% UP"; break;
                    case IceConsoleControlKind.FloodValve: preview = "45%"; break;
                    case IceConsoleControlKind.WashValve: preview = "30%"; break;
                }
                if (preview != null && control.value != null) changed |= SetPreviewText(control.value, preview);
                if ((control.kind == IceConsoleControlKind.BoardBrush || control.kind == IceConsoleControlKind.WashPump) &&
                    control.indicator != null && control.indicator.type == Image.Type.Filled && control.indicator.fillAmount != 1)
                {
                    control.indicator.fillAmount = 1;
                    changed = true;
                }
                if (control.slider != null && control.kind == IceConsoleControlKind.FloodValve &&
                    !Mathf.Approximately(control.slider.normalizedValue, .45f))
                {
                    control.slider.SetValueWithoutNotify(Mathf.Lerp(control.slider.minValue, control.slider.maxValue, .45f));
                    changed = true;
                }
            }
            return changed;
        }

        static bool SetPreviewText(TMP_Text target, string value)
        {
            if (target.text == value) return false;
            target.text = value;
            return true;
        }

        /// <summary>Creates missing new assets only. Existing prefabs are always preserved.</summary>
        public static void BuildNewAssets()
        {
            RequireEditMode();
            LoadStyle();
            EnsureFolder(Folder + "/Widgets");
            SaveNew(ConsolePrefabPath, BuildConsole);
            SaveNew(Folder + "/Widgets/Aurora Action Button.prefab", () =>
                Action(null, null, IceConsoleControlKind.Service, "Action Button", "Refill help", new Rect(0, 0, 158, 34)).gameObject);
            SaveNew(Folder + "/Widgets/Aurora Maintained Toggle.prefab", BuildToggleTemplate);
            SaveNew(Folder + "/Widgets/Aurora Momentary Button.prefab", () =>
                Action(null, null, IceConsoleControlKind.SnowBreaker, "Momentary Button", "Breaker  [6]", new Rect(0, 0, 154, 34)).gameObject);
            SaveNew(Folder + "/Widgets/Aurora Hydraulic Lever.prefab", () =>
                Hydraulic(null, null, IceConsoleControlKind.Conditioner, "Hydraulic Lever", "CONDITIONER", new Rect(0, 0, 68, 94), "100% UP").gameObject);
            SaveNew(Folder + "/Widgets/Aurora Quarter Turn Valve.prefab", () =>
                Rotary(null, null, IceConsoleControlKind.FloodValve, "Quarter Turn Valve", "FLOOD", new Rect(0, 0, 88, 88), false).gameObject);
            SaveNew(Folder + "/Widgets/Aurora Blade Wheel.prefab", () =>
                Rotary(null, null, IceConsoleControlKind.Blade, "Blade Wheel", "BLADE", new Rect(0, 0, 80, 80), true).gameObject);
            SaveNew(Folder + "/Widgets/Aurora Bound Slider.prefab", BuildSliderTemplate);
            SaveNew(Folder + "/Widgets/Aurora Collapsible Panel.prefab", BuildCollapsibleTemplate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Aurora/UI/Install Machinery Console in Current Scene")]
        public static void InstallCurrentScene()
        {
            RequireEditMode();
            InstallInScene(SceneManager.GetActiveScene());
        }

        [MenuItem("Tools/Aurora/UI/Install Machinery Console Under Selection")]
        public static void InstallUnderSelection()
        {
            RequireEditMode();
            var target = Selection.activeTransform as RectTransform;
            if (target == null) throw new InvalidOperationException("Select the intended UI RectTransform group, such as Machinery Controls.");
            var canvas = target.GetComponentInParent<Canvas>();
            var hud = canvas == null ? null : canvas.GetComponentsInChildren<IceAuthoredHud>(true).SingleOrDefault();
            if (hud == null || hud.game == null)
                throw new InvalidOperationException("The selected group must be below the existing authored HUD Canvas with an assigned Game reference.");
            InstallUnder(target, hud.game, canvas);
        }

        /// <summary>
        /// Finds the existing typed Phase 1 HUD in this scene. The one Editor-only
        /// child-name lookup selects its organizational group; runtime uses references.
        /// Does not save the scene or change the Phase 1 prefab asset.
        /// </summary>
        public static IceMachineryConsole InstallInScene(Scene scene)
        {
            RequireEditMode();
            if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("Open the authored scene first.");
            var huds = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<IceAuthoredHud>(true)).ToArray();
            if (huds.Length != 1 || huds[0].game == null || huds[0].canvas == null)
                throw new InvalidOperationException("The scene must contain exactly one existing authored HUD with explicit Game and Canvas references.");
            var hud = huds[0];
            var existing = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<IceMachineryConsole>(true)).ToArray();
            if (existing.Length != 0)
            {
                if (existing.Length != 1 || existing[0].game != hud.game)
                    throw new InvalidOperationException("This scene already has a machinery-console component. Inspect its binding before adding another.");
                Selection.activeGameObject = existing[0].gameObject;
                return existing[0];
            }
            RectTransform group = null;
            foreach (Transform child in hud.canvas.transform)
                if (child.name == "Machinery Controls")
                {
                    if (group != null) throw new InvalidOperationException("More than one Machinery Controls group exists. Select the intended group and use Install Under Selection.");
                    group = child as RectTransform;
                }
            if (group == null)
                throw new InvalidOperationException("The existing Machinery Controls group was renamed or removed. Select your intended UI group and use Install Under Selection; no existing layout will be rebuilt.");
            return InstallUnder(group, hud.game, hud.canvas);
        }

        public static IceMachineryConsole InstallUnder(RectTransform target, IceResurfacerGame game, Canvas canvas)
        {
            RequireEditMode();
            if (target == null || game == null || canvas == null || target.GetComponentInParent<Canvas>() != canvas)
                throw new ArgumentException("Supply the intended authored UI group, its Canvas, and existing simulator.");
            if (target.gameObject.scene != game.gameObject.scene)
                throw new ArgumentException("The console parent and simulator must belong to the same loaded scene.");
            if (target.GetComponentsInChildren<IceMachineryConsole>(true).Length != 0)
                throw new InvalidOperationException("The target already contains a machinery console; existing authoring is preserved.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConsolePrefabPath);
            if (prefab == null) throw new InvalidOperationException("The persistent Machinery Console prefab is missing. Import the Phase 2 assets first.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, target);
            Undo.RegisterCreatedObjectUndo(instance, "Install Aurora machinery console");
            var console = instance.GetComponent<IceMachineryConsole>();
            Undo.RecordObject(console, "Bind Aurora machinery console");
            console.game = game;
            console.canvas = canvas;
            console.raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (console.raycaster == null)
            {
                Undo.DestroyObjectImmediate(instance);
                throw new InvalidOperationException("The existing Canvas needs its Phase 1 GraphicRaycaster; the installer will not replace the Canvas.");
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(console);
            EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            Selection.activeGameObject = instance;
            Debug.Log("Installed the persistent machinery console as an additive prefab instance. Existing UI layout is unchanged. Save the scene to retain the installation.", console);
            return console;
        }

        [MenuItem("Tools/Aurora/UI/Preview Selected Console/Compact")]
        public static void PreviewCompact() { PreviewSelected(false); }
        [MenuItem("Tools/Aurora/UI/Preview Selected Console/Expanded")]
        public static void PreviewExpanded() { PreviewSelected(true); }
        static void PreviewSelected(bool expanded)
        {
            RequireEditMode();
            var selected = Selection.activeTransform;
            var console = selected == null ? null : selected.GetComponentInParent<IceMachineryConsole>();
            if (console == null) throw new InvalidOperationException("Select the machinery console or one of its children.");
            var groups = new Object[] { console.compactGroup, console.expandedGroup };
            Undo.RecordObjects(groups, "Preview Aurora machinery console");
            SetGroup(console.compactGroup, !expanded);
            SetGroup(console.expandedGroup, expanded);
            foreach (var group in groups)
            {
                EditorUtility.SetDirty(group);
                PrefabUtility.RecordPrefabInstancePropertyModifications(group);
            }
            EditorSceneManager.MarkSceneDirty(console.gameObject.scene);
            SceneView.RepaintAll();
        }

        static GameObject BuildConsole()
        {
            var root = Stretch("Aurora Machinery Console", null);
            var owner = root.gameObject.AddComponent<IceMachineryConsole>();
            var controls = new List<IceConsoleControl>();
            var compact = Panel(root, "Compact Console", new Rect(1210, 752, 362, 88));
            var expanded = Panel(root, "Expanded Console", new Rect(1210, 288, 362, 552));
            owner.compactGroup = compact.gameObject.AddComponent<CanvasGroup>();
            owner.expandedGroup = expanded.gameObject.AddComponent<CanvasGroup>();
            SetGroup(owner.compactGroup, true);
            SetGroup(owner.expandedGroup, false);
            owner.compactSummary = Label(compact, "Live Machinery Summary", "AUGERS V OFF / H OFF   FLOOD 0%", new Rect(18, 10, 326, 23), 16, IceUIColorRole.Muted);
            controls.Add(Action(compact, owner, IceConsoleControlKind.OpenConsole, "Open Console", "Open console  [Tab]", new Rect(18, 46, 326, 34)));
            controls.Add(Action(expanded, owner, IceConsoleControlKind.CloseConsole, "Close Console", "Close console  [Tab]", new Rect(18, 4, 326, 34)));

            controls.Add(Maintained(expanded, owner, IceConsoleControlKind.VerticalAuger, "Vertical Auger", "VERT [3]", new Rect(18, 68, 68, 94)));
            controls.Add(Hydraulic(expanded, owner, IceConsoleControlKind.Conditioner, "Conditioner Lift", "COND [1]", new Rect(100, 68, 68, 94), "100% UP"));
            controls.Add(Maintained(expanded, owner, IceConsoleControlKind.HorizontalAuger, "Horizontal Auger", "HORIZ [2]", new Rect(182, 68, 68, 94)));
            controls.Add(Hydraulic(expanded, owner, IceConsoleControlKind.TankLift, "Snow Tank Lift", "DUMP [7]", new Rect(264, 68, 68, 94), "0% UP"));
            Graphic(expanded, "Section Rule", new Rect(18, 191, 326, 1), IceUIColorRole.Border);
            controls.Add(Rotary(expanded, owner, IceConsoleControlKind.FloodValve, "Flood Water Valve", "FLOOD", new Rect(18, 209, 88, 88), false));
            controls.Add(Rotary(expanded, owner, IceConsoleControlKind.WashValve, "Wash Water Valve", "WASH", new Rect(132, 209, 88, 88), false));
            controls.Add(Rotary(expanded, owner, IceConsoleControlKind.Blade, "Blade Adjustment Wheel", "BLADE", new Rect(251, 212, 80, 80), true));
            controls.Add(BladeSchematic(expanded, owner));

            controls.Add(ToggleButton(expanded, owner, IceConsoleControlKind.BoardBrush, "Board Brush", "Brush  [4]", new Rect(18, 428, 154, 34)));
            controls.Add(ToggleButton(expanded, owner, IceConsoleControlKind.WashPump, "Wash Pickup Pump", "Pump  [5]", new Rect(186, 428, 158, 34)));
            controls.Add(Action(expanded, owner, IceConsoleControlKind.SnowBreaker, "Snow Breaker", "Breaker  [6]", new Rect(18, 470, 154, 34)));
            controls.Add(Action(expanded, owner, IceConsoleControlKind.Service, "Water Service Help", "Refill help", new Rect(186, 470, 158, 34)));
            controls.Add(Action(expanded, owner, IceConsoleControlKind.Finish, "Finish Run", "Finish run", new Rect(18, 512, 154, 34)));
            controls.Add(Action(expanded, owner, IceConsoleControlKind.NewSession, "New Session", "New session", new Rect(186, 512, 158, 34)));
            var badge = Node("Design Preview - hidden in Play", root, new Rect(1210, 264, 362, 20));
            Label(badge, "Preview Notice", "DESIGN PREVIEW / machinery controls", new Rect(0, 0, 362, 20), 13, IceUIColorRole.Muted);
            owner.previewBadge = badge.gameObject;
            AddConfirmation(root, owner, controls);
            owner.controls = controls.ToArray();
            ApplyRepresentativePreview(root.gameObject);
            return root.gameObject;
        }

        static void AddConfirmation(RectTransform root, IceMachineryConsole owner, List<IceConsoleControl> controls)
        {
            var modal = Stretch("Session Confirmation", root);
            var scrim = modal.gameObject.AddComponent<Image>();
            scrim.color = new Color(.1f, .1f, .09f, .65f);
            scrim.raycastTarget = true;
            owner.confirmationGroup = modal.gameObject.AddComponent<CanvasGroup>();
            SetGroup(owner.confirmationGroup, false);
            var panel = Panel(modal, "Confirmation Panel", new Rect(520, 330, 560, 220));
            Label(panel, "Title", "SESSION CONFIRMATION", new Rect(24, 18, 512, 28), 20, IceUIColorRole.Text);
            owner.confirmationMessage = Label(panel, "Live Confirmation Message", "Start a new session? The current run will end.", new Rect(24, 58, 512, 70), 18, IceUIColorRole.Text);
            owner.confirmationMessage.textWrappingMode = TextWrappingModes.Normal;
            controls.Add(Action(panel, owner, IceConsoleControlKind.ConfirmAction, "Confirm Action", "Start new session", new Rect(24, 156, 248, 42)));
            controls.Add(Action(panel, owner, IceConsoleControlKind.CancelAction, "Cancel Action", "Keep current session", new Rect(288, 156, 248, 42)));
            owner.confirmNewSession = true;
            owner.confirmFinish = false;
        }

        static IceConsoleControl Control(RectTransform root, IceMachineryConsole owner, IceConsoleControlKind kind)
        {
            var control = root.gameObject.AddComponent<IceConsoleControl>();
            control.owner = owner;
            control.kind = kind;
            control.interactionArea = root;
            return control;
        }

        static IceConsoleControl Action(RectTransform parent, IceMachineryConsole owner, IceConsoleControlKind kind, string name, string caption, Rect rect)
        {
            var root = Node(name, parent, rect);
            var face = Graphic(root, IceUIColorRole.Button, true);
            var button = root.gameObject.AddComponent<Button>();
            ConfigureButton(button, face);
            var style = root.GetComponent<IceUIStyle>();
            style.button = button;
            style.applyButtonStates = true;
            style.spriteRole = IceUISpriteRole.Button;
            style.applySprite = true;
            style.Apply();
            Label(root, "Label", caption, new Rect(8, 2, rect.width-16, rect.height-4), 17, IceUIColorRole.Text, TextAlignmentOptions.Center, true);
            var control = Control(root, owner, kind);
            control.action = button;
            return control;
        }

        static IceConsoleControl ToggleButton(RectTransform parent, IceMachineryConsole owner, IceConsoleControlKind kind, string name, string caption, Rect rect)
        {
            var control = Action(parent, owner, kind, name, caption, rect);
            var mark = Graphic((RectTransform)control.transform, "State Indicator", new Rect(7, 10, 5, rect.height-20), IceUIColorRole.Accent);
            mark.sprite = pixel;
            mark.type = Image.Type.Filled;
            mark.fillMethod = Image.FillMethod.Vertical;
            mark.fillOrigin = 0;
            mark.fillAmount = 0;
            control.indicator = mark;
            return control;
        }

        static IceConsoleControl Maintained(RectTransform parent, IceMachineryConsole owner, IceConsoleControlKind kind, string name, string caption, Rect rect)
        {
            var root = Node(name, parent, rect);
            var face = Graphic(root, IceUIColorRole.Inset, true);
            var button = root.gameObject.AddComponent<Button>();
            ConfigureButton(button, face);
            var control = Control(root, owner, kind);
            control.action = button;
            Label(root, "Label", caption, new Rect(-6, -25, rect.width+12, 23), 16, IceUIColorRole.Muted, TextAlignmentOptions.Center);
            Graphic(root, "Lever Track", new Rect(rect.width*.5f-3, 12, 6, rect.height-24), IceUIColorRole.Button);
            var grip = Graphic(root, "Lever Grip", new Rect(rect.width*.5f-21, rect.height-30, 42, 16), IceUIColorRole.Accent);
            control.movingGraphic = grip.rectTransform;
            control.animateGraphic = true;
            control.negativeOffset = Vector2.zero;
            control.positiveOffset = new Vector2(0, 50);
            control.indicator = grip;
            control.value = Label(root, "Live State", "OFF", new Rect(-6, 97, rect.width+12, 22), 16, IceUIColorRole.Muted, TextAlignmentOptions.Center);
            return control;
        }

        static IceConsoleControl Hydraulic(RectTransform parent, IceMachineryConsole owner, IceConsoleControlKind kind, string name, string caption, Rect rect, string preview)
        {
            var root = Node(name, parent, rect);
            Graphic(root, IceUIColorRole.Inset, true);
            var control = Control(root, owner, kind);
            ConfigureCustomState(control);
            Label(root, "Label", caption, new Rect(-6, -25, rect.width+12, 23), 16, IceUIColorRole.Muted, TextAlignmentOptions.Center);
            Graphic(root, "Lever Track", new Rect(rect.width*.5f-3, 12, 6, rect.height-24), IceUIColorRole.Button);
            var grip = Graphic(root, "Spring Grip", new Rect(rect.width*.5f-21, rect.height*.5f-8, 42, 16), IceUIColorRole.Warning);
            control.movingGraphic = grip.rectTransform;
            control.animateGraphic = true;
            control.negativeOffset = new Vector2(0, -Mathf.Max(0, rect.height*.5f-30));
            control.positiveOffset = new Vector2(0, Mathf.Max(0, rect.height*.5f-30));
            control.indicator = grip;
            Label(root, "Raise Label", "RAISE", new Rect(0, -1, rect.width, 20), 16, IceUIColorRole.Muted, TextAlignmentOptions.Center);
            Label(root, "Lower Label", "LOWER", new Rect(0, rect.height-20, rect.width, 20), 16, IceUIColorRole.Muted, TextAlignmentOptions.Center);
            control.value = Label(root, "Live Lift", preview, new Rect(-6, rect.height+3, rect.width+12, 22), 16, IceUIColorRole.Accent, TextAlignmentOptions.Center);
            return control;
        }

        static IceConsoleControl Rotary(RectTransform parent, IceMachineryConsole owner, IceConsoleControlKind kind, string name, string caption, Rect rect, bool blade)
        {
            var root = Node(name, parent, rect);
            var face = Graphic(root, blade ? IceUIColorRole.Inset : IceUIColorRole.Panel, true);
            if (blade) face.sprite = knob;
            var control = Control(root, owner, kind);
            Label(root, "Label", caption, new Rect(-10, -18, rect.width+20, 23), 17, IceUIColorRole.Muted, TextAlignmentOptions.Center);
            var rotor = Node(blade ? "Wheel Rotor" : "Quarter Turn Handle", root, new Rect(rect.width*.5f, rect.height*.5f, 0, 0));
            rotor.pivot = new Vector2(.5f, .5f);
            control.movingGraphic = rotor;
            control.animateGraphic = true;
            control.closedAngle = 0;
            control.openAngle = 90;
            if (blade)
            {
                var ring = Graphic(root, "Wheel Rim", new Rect(0, 0, rect.width, rect.height), IceUIColorRole.Warning);
                ring.sprite = knob;
                var inset = Graphic(root, "Wheel Face", new Rect(5, 5, rect.width-10, rect.height-10), IceUIColorRole.Inset);
                inset.sprite = knob;
                rotor.SetAsLastSibling();
                for (int i = 0; i < 3; i++)
                {
                    var spoke = Graphic(rotor, "Spoke " + (i+1), new Rect(0, -2.5f, rect.width*.4f, 5), IceUIColorRole.Warning);
                    spoke.rectTransform.pivot = new Vector2(0, .5f);
                    spoke.rectTransform.localEulerAngles = new Vector3(0, 0, 120-i*120);
                }
                var hub = Graphic(root, "Hub", new Rect(rect.width*.5f-12, rect.height*.5f-12, 24, 24), IceUIColorRole.Button);
                hub.sprite = knob;
                control.indicator = hub;
            }
            else
            {
                for (int i = 0; i < 10; i++)
                {
                    float angle = (180-i*10)*Mathf.Deg2Rad;
                    Graphic(root, "Quarter Turn Tick " + i, new Rect(rect.width*.5f+Mathf.Cos(angle)*39-1, rect.height*.5f+Mathf.Sin(angle)*39-1, 2, 2), IceUIColorRole.Button);
                }
                Graphic(rotor, "Stem", new Rect(-31, -3.5f, 31, 7), IceUIColorRole.Muted);
                var grip = Graphic(rotor, "L Grip", new Rect(-36, -16, 10, 16), IceUIColorRole.Warning);
                control.indicator = grip;
                var hub = Graphic(root, "Valve Hub", new Rect(rect.width*.5f-10, rect.height*.5f-10, 20, 20), IceUIColorRole.Muted);
                hub.sprite = knob;
                rotor.SetAsLastSibling();
            }
            control.value = Label(root, "Live Setting", blade ? "0.10 mm" : "0%", new Rect(-12, blade ? 90 : 93, rect.width+24, 23), 16, IceUIColorRole.Accent, TextAlignmentOptions.Center);
            AnchorRotaryArtwork(control);
            ConfigureCustomState(control);
            return control;
        }

        static bool AnchorRotaryArtwork(IceConsoleControl control)
        {
            // Standard authored anchors keep the mechanism and pointer center in
            // agreement when its interaction rectangle is resized in the Editor.
            // This is a one-time authoring step; no runtime layout adjustment.
            bool changed = false;
            foreach (RectTransform child in control.transform)
            {
                if (child == control.movingGraphic || child.name == "Hub" || child.name == "Valve Hub" ||
                    child.name.StartsWith("Quarter Turn Tick ", StringComparison.Ordinal))
                    changed |= PreserveRectAndAnchor(child, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
                else if (child.name == "Wheel Rim" || child.name == "Wheel Face")
                    changed |= PreserveRectAndAnchor(child, Vector2.zero, Vector2.one);
                else if (control.value != null && child == control.value.rectTransform)
                    changed |= PreserveRectAndAnchor(child, Vector2.zero, Vector2.right);
                else if (child.name == "Label")
                    changed |= PreserveRectAndAnchor(child, Vector2.up, Vector2.one);
            }
            return changed;
        }

        static bool PreserveRectAndAnchor(RectTransform target, Vector2 min, Vector2 max)
        {
            if (target.anchorMin == min && target.anchorMax == max) return false;
            Vector3 position = target.localPosition;
            Vector2 size = target.rect.size;
            target.anchorMin = min;
            target.anchorMax = max;
            target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            target.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            target.localPosition = position;
            return true;
        }

        static IceConsoleControl BladeSchematic(RectTransform parent, IceMachineryConsole owner)
        {
            var root = Node("Blade Schematic", parent, new Rect(18, 332, 326, 80));
            Graphic(root, IceUIColorRole.Inset, false);
            var control = Control(root, owner, IceConsoleControlKind.BladeStatus);
            Label(root, "Label", "BLADE / SCHEMATIC", new Rect(9, 4, 172, 21), 16, IceUIColorRole.Muted);
            control.value = Label(root, "Live Engagement", "OFF ICE", new Rect(184, 4, 136, 21), 16, IceUIColorRole.Warning);
            control.detail = Label(root, "Live Set and Contact", "SET 0.10 mm\nCONTACT 0.000 mm", new Rect(160, 27, 160, 42), 16, IceUIColorRole.Text);
            control.detail.textWrappingMode = TextWrappingModes.Normal;
            Graphic(root, "Ice Reference", new Rect(10, 58, 138, 2), IceUIColorRole.Muted);
            Label(root, "Ice Label", "ICE", new Rect(10, 59, 32, 20), 16, IceUIColorRole.Muted);
            var body = Graphic(root, "Conditioner Body", new Rect(37, 40, 76, 8), IceUIColorRole.Muted);
            var blade = Node("Blade Contact Indicator", root, new Rect(98, 58, 0, 0));
            Graphic(blade, "Blade Stem", new Rect(0, -10, 3, 10), IceUIColorRole.Warning);
            var edge = Graphic(blade, "Blade Edge", new Rect(-20, -3, 34, 3), IceUIColorRole.Warning);
            edge.rectTransform.localEulerAngles = new Vector3(0, 0, -5);
            var set = Graphic(root, "Blade Set Marker", new Rect(130, 58, 15, 1), IceUIColorRole.Warning);
            control.schematicBody = body.rectTransform;
            control.schematicBlade = blade;
            control.schematicSetMarker = set.rectTransform;
            control.schematicOffIceOffset = new Vector2(0, 7);
            control.animateGraphic = true;
            control.indicator = edge;
            return control;
        }

        static GameObject BuildToggleTemplate()
        {
            var root = Node("Maintained Toggle", null, new Rect(0, 0, 180, 40));
            var face = Graphic(root, IceUIColorRole.Button, true);
            var toggle = root.gameObject.AddComponent<Toggle>();
            ConfigureSelectable(toggle, face);
            var style = root.GetComponent<IceUIStyle>();
            style.spriteRole = IceUISpriteRole.Button;
            style.applySprite = true;
            toggle.graphic = Graphic(root, "On Indicator", new Rect(10, 12, 12, 16), IceUIColorRole.Accent);
            toggle.isOn = false;
            Label(root, "Label", "Horizontal auger", new Rect(32, 2, 138, 36), 17, IceUIColorRole.Text);
            var control = Control(root, null, IceConsoleControlKind.HorizontalAuger);
            control.maintainedToggle = toggle;
            return root.gameObject;
        }

        static GameObject BuildSliderTemplate()
        {
            var root = Node("Bound Flood Slider", null, new Rect(0, 0, 280, 72));
            var face = Graphic(root, IceUIColorRole.Panel, false);
            Label(root, "Label", "FLOOD", new Rect(12, 6, 176, 22), 16, IceUIColorRole.Muted);
            var control = Control(root, null, IceConsoleControlKind.FloodValve);
            control.value = Label(root, "Live Setting", "0%", new Rect(204, 6, 64, 22), 16, IceUIColorRole.Accent, TextAlignmentOptions.Right);
            var track = Node("Slider", root, new Rect(18, 34, 244, 26));
            var background = Graphic(track, IceUIColorRole.Inset, true);
            var trackStyle = track.GetComponent<IceUIStyle>();
            trackStyle.spriteRole = IceUISpriteRole.MeterBackground;
            trackStyle.applySprite = true;
            var fillArea = Node("Fill Area", track, new Rect(0, 11, 244, 4));
            var fill = Graphic(fillArea, "Fill", new Rect(0, 0, 244, 4), IceUIColorRole.Accent);
            var fillStyle = fill.GetComponent<IceUIStyle>();
            fillStyle.spriteRole = IceUISpriteRole.MeterFill;
            fillStyle.applySprite = true;
            var handleArea = Node("Handle Area", track, new Rect(0, 0, 244, 26));
            var handle = Graphic(handleArea, "Handle", new Rect(0, 3, 15, 20), IceUIColorRole.Button);
            var slider = track.gameObject.AddComponent<Slider>();
            ConfigureSelectable(slider, handle);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.SetValueWithoutNotify(0);
            control.slider = slider;
            control.animateGraphic = false;
            return root.gameObject;
        }

        static GameObject BuildCollapsibleTemplate()
        {
            var root = Stretch("Collapsible Machinery Panel", null);
            var owner = root.gameObject.AddComponent<IceMachineryConsole>();
            owner.confirmNewSession = false;
            owner.confirmFinish = false;
            var compact = Panel(root, "Compact Content", new Rect(0, 0, 362, 88));
            var expanded = Panel(root, "Expanded Content", new Rect(0, 0, 362, 240));
            owner.compactGroup = compact.gameObject.AddComponent<CanvasGroup>();
            owner.expandedGroup = expanded.gameObject.AddComponent<CanvasGroup>();
            SetGroup(owner.compactGroup, true);
            SetGroup(owner.expandedGroup, false);
            owner.compactSummary = Label(compact, "Live Machinery Summary", "AUGERS V OFF / H OFF   FLOOD 0%", new Rect(18, 10, 326, 23), 16, IceUIColorRole.Muted);
            var open = Action(compact, owner, IceConsoleControlKind.OpenConsole, "Open Panel", "Open console  [Tab]", new Rect(18, 46, 326, 34));
            var close = Action(expanded, owner, IceConsoleControlKind.CloseConsole, "Close Panel", "Close console  [Tab]", new Rect(18, 4, 326, 34));
            Label(expanded, "Authoring Placeholder", "Add editable controls to this container.", new Rect(18, 60, 326, 50), 17, IceUIColorRole.Muted);
            owner.controls = new[] { open, close };
            return root.gameObject;
        }

        static void ConfigureButton(Button button, Image graphic) { ConfigureSelectable(button, graphic); }
        static void ConfigureCustomState(IceConsoleControl control)
        {
            var selectable = control.GetComponent<Selectable>();
            if (selectable == null) selectable = control.gameObject.AddComponent<Selectable>();
            ConfigureSelectable(selectable, CustomStateGraphic(control));
            control.selectable = selectable;
        }
        static Image CustomStateGraphic(IceConsoleControl control)
        {
            // The blade's decorative wheel face covers its background, so tint the
            // visible hub. Other controls expose their authored background directly.
            return control.kind == IceConsoleControlKind.Blade && control.indicator != null ? control.indicator : control.GetComponent<Image>();
        }
        static void ConfigureSelectable(Selectable selectable, Image graphic)
        {
            selectable.targetGraphic = graphic;
            selectable.navigation = new Navigation { mode = Navigation.Mode.None };
            selectable.transition = Selectable.Transition.ColorTint;
            selectable.colors = theme.buttonColors;
            selectable.spriteState = theme.buttonSpriteState;
            var style = selectable.GetComponent<IceUIStyle>();
            if (style != null)
            {
                style.selectable = selectable;
                style.applyButtonStates = true;
                style.Apply();
            }
        }

        static RectTransform Panel(RectTransform parent, string name, Rect rect)
        {
            var panel = Node(name, parent, rect);
            Graphic(panel, IceUIColorRole.Panel, true);
            var style = panel.GetComponent<IceUIStyle>();
            style.spriteRole = IceUISpriteRole.Panel;
            style.applySprite = true;
            style.Apply();
            return panel;
        }

        static RectTransform Node(string name, RectTransform parent, Rect rect)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.layer = 5;
            var transform = (RectTransform)root.transform;
            if (parent != null) transform.SetParent(parent, false);
            transform.anchorMin = transform.anchorMax = new Vector2(0, 1);
            transform.pivot = new Vector2(0, 1);
            transform.anchoredPosition = new Vector2(rect.x, -rect.y);
            transform.sizeDelta = rect.size;
            return transform;
        }

        static RectTransform Stretch(string name, RectTransform parent)
        {
            var root = Node(name, parent, new Rect());
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.pivot = new Vector2(.5f, .5f);
            root.anchoredPosition = root.sizeDelta = Vector2.zero;
            return root;
        }

        static Image Graphic(RectTransform parent, string name, Rect rect, IceUIColorRole role)
        {
            return Graphic(Node(name, parent, rect), role, false);
        }

        static Image Graphic(RectTransform root, IceUIColorRole role, bool raycast)
        {
            var graphic = root.gameObject.AddComponent<Image>();
            graphic.color = theme.GetColor(role);
            graphic.raycastTarget = raycast;
            var style = root.gameObject.AddComponent<IceUIStyle>();
            style.theme = theme;
            style.image = graphic;
            style.applyFont = false;
            style.applyColor = true;
            style.colorRole = role;
            style.applySprite = false;
            return graphic;
        }

        static TMP_Text Label(RectTransform parent, string name, string content, Rect rect, float size, IceUIColorRole color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left, bool stretchWidth = false)
        {
            var root = Node(name, parent, rect);
            if (stretchWidth)
            {
                root.anchorMax = Vector2.one;
                root.sizeDelta = new Vector2(-rect.x*2, rect.height);
            }
            var text = root.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.font = theme.secondaryFont != null ? theme.secondaryFont : theme.primaryFont;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = theme.GetColor(color);
            text.raycastTarget = false;
            text.richText = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.margin = Vector4.zero;
            var style = root.gameObject.AddComponent<IceUIStyle>();
            style.theme = theme;
            style.text = text;
            style.fontRole = IceUIFontRole.Secondary;
            style.colorRole = color;
            style.applyFont = style.applyColor = true;
            style.applyFontSize = false;
            return text;
        }

        static void SetGroup(CanvasGroup group, bool visible)
        {
            group.alpha = visible ? 1 : 0;
            group.interactable = group.blocksRaycasts = visible;
        }

        static void LoadStyle()
        {
            theme = AssetDatabase.LoadAssetAtPath<IceUIStyleSheet>(ThemePath);
            pixel = AssetDatabase.LoadAssetAtPath<Sprite>(PixelPath);
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (theme == null || theme.secondaryFont == null || pixel == null)
                throw new InvalidOperationException("Phase 1 theme, TMP font and placeholder sprite must already be imported. Existing assets will not be regenerated.");
        }

        static void SaveNew(string path, Func<GameObject> factory)
        {
            if (File.Exists(Path.Combine(Application.dataPath, "..", path))) return;
            GameObject temporary = null;
            try
            {
                temporary = factory();
                ApplyRepresentativePreview(temporary);
                if (PrefabUtility.SaveAsPrefabAsset(temporary, path) == null)
                    throw new IOException("Could not save new UI prefab " + path);
            }
            finally { if (temporary != null) Object.DestroyImmediate(temporary); }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/')+1));
        }

        static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Author and install UI only in Edit Mode.");
        }
    }
}
#endif
