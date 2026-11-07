#if UNITY_EDITOR
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEngine;

namespace IceResurfacer.Editor
{
    /// <summary>Release transient local-physics query objects before Editor tears down Play physics.</summary>
    [InitializeOnLoad]
    public static class IceOperatorEditorLifecycle
    {
        static IceOperatorEditorLifecycle()
        {
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingPlayMode) Release(); };
            AssemblyReloadEvents.beforeAssemblyReload += Release;
        }
        static void Release()
        {
            if (!EditorApplication.isPlaying) return;
            foreach (var guide in Object.FindObjectsByType<IceRouteGuide>(FindObjectsInactive.Include))
                if (Application.IsPlaying(guide.gameObject)) guide.ReleaseProjectedGuidance();
        }
    }
}
#endif
