#if UNITY_EDITOR
using System.Collections.Generic;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IceResurfacer.Editor
{
    public enum IceOperatorPreviewKind { Driving, WaterService, History, Warnings }
    /// <summary>Transient presentation-only preview, coordinated with the session-screen preview.</summary>
    [InitializeOnLoad]
    public static class IceOperatorPreview
    {
        struct State { public CanvasGroup group; public float alpha; public bool interactive, blocks; }
        static readonly List<State> snapshot = new List<State>();
        public static bool Active => snapshot.Count > 0;
        static IceOperatorPreview()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Restore; EditorApplication.quitting += Restore;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingEditMode) Restore(); };
            EditorSceneManager.sceneSaving += (scene, path) => Restore(); EditorSceneManager.sceneClosing += (scene, removing) => Restore();
            PrefabStage.prefabSaving += root => Restore();
        }
        [MenuItem("Tools/Aurora/UI/Operator Preview/Driving and Status")]
        static void Driving() => Selected(IceOperatorPreviewKind.Driving);
        [MenuItem("Tools/Aurora/UI/Operator Preview/On-foot Water Service")]
        static void Service() => Selected(IceOperatorPreviewKind.WaterService);
        [MenuItem("Tools/Aurora/UI/Operator Preview/Message History")]
        static void History() => Selected(IceOperatorPreviewKind.History);
        [MenuItem("Tools/Aurora/UI/Operator Preview/Critical and Drive Warnings")]
        static void Warnings() => Selected(IceOperatorPreviewKind.Warnings);
        [MenuItem("Tools/Aurora/UI/Operator Preview/Restore Saved Presentation")]
        public static void Restore()
        {
            foreach (var state in snapshot) if (state.group != null)
            { state.group.alpha = state.alpha; state.group.interactable = state.interactive; state.group.blocksRaycasts = state.blocks; }
            snapshot.Clear(); SceneView.RepaintAll();
        }
        static void Selected(IceOperatorPreviewKind kind)
        {
            var owner = Selection.activeGameObject == null ? null : Selection.activeGameObject.GetComponentInParent<IceOperatorOverlay>(true);
            if (owner == null) owner = Object.FindAnyObjectByType<IceOperatorOverlay>(FindObjectsInactive.Include);
            if (owner != null) Show(owner, kind); else Debug.LogWarning("Open the authored scene and select Aurora Operator Interface.");
        }
        static void Group(CanvasGroup group, bool show)
        { if (group != null) { group.alpha = show ? 1 : 0; group.interactable = group.blocksRaycasts = false; } }
        public static void Show(IceOperatorOverlay owner, IceOperatorPreviewKind kind)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || owner == null || EditorUtility.IsPersistent(owner)) return;
            IceMenuPreview.Restore(); Restore();
            var canvas = owner.GetComponentInParent<Canvas>();
            foreach (var group in canvas != null ? canvas.GetComponentsInChildren<CanvasGroup>(true) : owner.GetComponentsInChildren<CanvasGroup>(true))
                snapshot.Add(new State { group = group, alpha = group.alpha, interactive = group.interactable, blocks = group.blocksRaycasts });
            bool service = kind == IceOperatorPreviewKind.WaterService;
            foreach (var menu in owner.GetComponentsInParent<Canvas>())
            {
                var menus = menu.GetComponentInChildren<IceMenuScreens>(true);
                if (menus != null) { Group(menus.welcome.visibility, false); Group(menus.pause.visibility, false); Group(menus.help.visibility, false); Group(menus.results.visibility, false); }
                var hud = menu.GetComponentInChildren<IceAuthoredHud>(true); if (hud != null) Group(hud.visibility, true);
                var console = menu.GetComponentInChildren<IceMachineryConsole>(true);
                if (console != null) { Group(console.compactGroup, !service); Group(console.expandedGroup, false); Group(console.confirmationGroup, false); }
                var survey = menu.GetComponentInChildren<IceSurfaceSurvey>(true); if (survey != null) Group(survey.visibility, !service);
                var gps = menu.GetComponentInChildren<IceGpsStatus>(true); if (gps != null) Group(gps.visibility, !service);
                var routes = menu.GetComponentInChildren<IceRouteManager>(true); if (routes != null) Group(routes.visibility, false);
            }
            Group(owner.serviceGroup, service); Group(owner.statusGroup, !service); Group(owner.performanceGroup, true);
            Group(owner.historyGroup, kind == IceOperatorPreviewKind.History);
            Group(owner.routine.visibility, !service && kind != IceOperatorPreviewKind.Warnings);
            Group(owner.critical.visibility, kind == IceOperatorPreviewKind.Warnings);
            Group(owner.driveWarning.visibility, kind == IceOperatorPreviewKind.Warnings); Group(owner.hint.visibility, false);
            SceneView.RepaintAll();
        }
    }
    [CustomEditor(typeof(IceOperatorOverlay))]
    public sealed class IceOperatorOverlayEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorGUILayout.HelpBox("Preview shows saved sample data only. It never changes the hose, simulation, preferences or history. Save, Play or Restore returns the previous presentation.", MessageType.Info);
            foreach (var kind in new[] { IceOperatorPreviewKind.Driving, IceOperatorPreviewKind.WaterService, IceOperatorPreviewKind.History, IceOperatorPreviewKind.Warnings })
                if (GUILayout.Button("Preview " + kind)) IceOperatorPreview.Show((IceOperatorOverlay)target, kind);
            if (GUILayout.Button("Restore Saved Presentation")) IceOperatorPreview.Restore();
        }
    }
}
#endif
