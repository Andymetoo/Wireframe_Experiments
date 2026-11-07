#if UNITY_EDITOR
using System.Collections.Generic;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IceResurfacer.Editor
{
    /// <summary>Transient presentation-only preview. Restores before Save, Play, reload, close or quit.</summary>
    [InitializeOnLoad]
    public static class IceMenuPreview
    {
        struct State { public CanvasGroup group; public float alpha; public bool interactable, blocks; }
        static readonly List<State> snapshot = new List<State>();
        public static bool Active => snapshot.Count > 0;
        static IceMenuPreview()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Restore;
            EditorApplication.quitting += Restore;
            EditorApplication.playModeStateChanged += change => { if (change == PlayModeStateChange.ExitingEditMode) Restore(); };
            EditorSceneManager.sceneSaving += (scene, path) => Restore();
            EditorSceneManager.sceneClosing += (scene, removing) => Restore();
            PrefabStage.prefabSaving += root => Restore();
        }
        [MenuItem("Tools/Aurora/UI/Screen Preview/Welcome")]
        static void Welcome() => ShowSelected(IceMenuScreenKind.Welcome);
        [MenuItem("Tools/Aurora/UI/Screen Preview/Pause and Settings")]
        static void Pause() => ShowSelected(IceMenuScreenKind.Pause);
        [MenuItem("Tools/Aurora/UI/Screen Preview/Operator Guide")]
        static void Help() => ShowSelected(IceMenuScreenKind.Help);
        [MenuItem("Tools/Aurora/UI/Screen Preview/Results")]
        static void Results() => ShowSelected(IceMenuScreenKind.Results);
        [MenuItem("Tools/Aurora/UI/Screen Preview/Gameplay UI (hide menus)")]
        static void Gameplay() => ShowSelected(IceMenuScreenKind.None);
        [MenuItem("Tools/Aurora/UI/Screen Preview/Restore Saved Presentation")]
        public static void Restore()
        {
            foreach (var state in snapshot)
                if (state.group != null) { state.group.alpha = state.alpha; state.group.interactable = state.interactable; state.group.blocksRaycasts = state.blocks; }
            snapshot.Clear(); SceneView.RepaintAll();
        }
        static void ShowSelected(IceMenuScreenKind kind)
        {
            var owner = Selection.activeGameObject == null ? null : Selection.activeGameObject.GetComponentInParent<IceMenuScreens>(true);
            if (owner == null) owner = Object.FindAnyObjectByType<IceMenuScreens>(FindObjectsInactive.Include);
            if (owner == null) { Debug.LogWarning("Open the authored scene and select Aurora Session Screens."); return; }
            Show(owner, kind);
        }
        public static void Show(IceMenuScreens owner, IceMenuScreenKind kind)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || owner == null || EditorUtility.IsPersistent(owner)) return;
            Restore(); var canvas = owner.GetComponentInParent<Canvas>();
            var groups = kind == IceMenuScreenKind.None || canvas == null ?
                new[] { owner.welcome.visibility, owner.pause.visibility, owner.help.visibility, owner.results.visibility } : canvas.GetComponentsInChildren<CanvasGroup>(true);
            foreach (var group in groups)
            {
                if (group == null) continue;
                snapshot.Add(new State { group = group, alpha = group.alpha, interactable = group.interactable, blocks = group.blocksRaycasts });
                group.alpha = 0; group.interactable = group.blocksRaycasts = false;
            }
            var screen = kind == IceMenuScreenKind.Welcome ? owner.welcome : kind == IceMenuScreenKind.Pause ? owner.pause : kind == IceMenuScreenKind.Help ? owner.help : kind == IceMenuScreenKind.Results ? owner.results : null;
            if (screen != null) screen.visibility.alpha = 1;
            SceneView.RepaintAll();
        }
        [MenuItem("Tools/Aurora/UI/Fit Edited Help Text")]
        static void FitSelectedHelp() => FitHelp(Object.FindAnyObjectByType<IceMenuScreens>(FindObjectsInactive.Include));
        public static void FitHelp(IceMenuScreens owner)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || owner == null || owner.help == null || EditorUtility.IsPersistent(owner)) return;
            // An explicit, undoable authoring edit; temporary screen preview never
            // mutates saved text/layout. Handles font-asset changes as well as prose.
            foreach (var height in owner.help.GetComponentsInChildren<IceUITextHeight>(true))
            {
                var text = height.text; var element = height.GetComponent<LayoutElement>();
                if (text == null || element == null || text.rectTransform.rect.width <= 0) continue;
                Undo.RecordObject(element, "Fit edited Aurora help text");
                element.preferredHeight = Mathf.Max(height.minimumHeight, text.GetPreferredValues(text.text, text.rectTransform.rect.width, 0).y + height.padding);
                PrefabUtility.RecordPrefabInstancePropertyModifications(element);
            }
            var scroll = owner.help.GetComponentInChildren<ScrollRect>(true);
            if (scroll != null && scroll.content != null) LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            EditorSceneManager.MarkSceneDirty(owner.gameObject.scene); SceneView.RepaintAll();
        }
    }
    [CustomEditor(typeof(IceMenuScreens))]
    public sealed class IceMenuScreensEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorGUILayout.Space(); EditorGUILayout.HelpBox("Edit Mode previews only change temporary CanvasGroup visibility. Save, Play and Restore return all groups to their prior values. They never start a session or save preferences.", MessageType.Info);
            var owner = (IceMenuScreens)target;
            foreach (var kind in new[] { IceMenuScreenKind.Welcome, IceMenuScreenKind.Pause, IceMenuScreenKind.Help, IceMenuScreenKind.Results, IceMenuScreenKind.None })
                if (GUILayout.Button(kind == IceMenuScreenKind.None ? "Preview Gameplay UI" : "Preview " + kind)) IceMenuPreview.Show(owner, kind);
            if (GUILayout.Button("Restore Saved Presentation")) IceMenuPreview.Restore();
            if (GUILayout.Button("Fit Edited Help Text (Undoable)")) IceMenuPreview.FitHelp(owner);
        }
    }
}
#endif
