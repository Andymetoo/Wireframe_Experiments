#if UNITY_EDITOR
using System.Collections.Generic;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IceResurfacer.Editor
{
    /// <summary>Applying shared visuals is deliberate, undoable, and saved as normal authored properties.</summary>
    public static class IceUIStyleAuthoring
    {
        [MenuItem("Tools/Aurora/UI/Apply Themes to Loaded UI")]
        public static void ApplyAllLoaded()
        {
            ApplyLoaded(null);
        }

        public static void ApplyLoaded(IceUIStyleSheet theme)
        {
            int count = 0;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Aurora UI theme");
            foreach (var style in Resources.FindObjectsOfTypeAll<IceUIStyle>())
            {
                // Includes inactive authored widgets and the open Prefab Stage, never imported prefab assets.
                if (EditorUtility.IsPersistent(style) || !style.gameObject.scene.IsValid() ||
                    !style.gameObject.scene.isLoaded || style.theme == null ||
                    (theme != null && style.theme != theme)) continue;
                ApplyWithUndo(style);
                count++;
            }
            Undo.CollapseUndoOperations(group);
            SceneView.RepaintAll();
            Debug.Log("Applied Aurora UI theme to " + count + " loaded style components. Save the scene or open prefab to keep the authored changes.");
        }

        public static void ApplyWithUndo(IceUIStyle style)
        {
            if (style == null || style.theme == null) return;
            var changed = new List<Object>(3);
            if (style.text != null && (style.applyFont || style.applyFontSize || style.applyColor)) changed.Add(style.text);
            if (style.image != null && (style.applyColor || style.applySprite)) changed.Add(style.image);
            if (style.StateTarget != null && style.applyButtonStates) changed.Add(style.StateTarget);
            if (changed.Count == 0) return;
            Undo.RecordObjects(changed.ToArray(), "Apply Aurora UI theme");
            style.Apply();
            foreach (var target in changed)
            {
                EditorUtility.SetDirty(target);
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
            if (style.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(style.gameObject.scene);
        }
    }

    [CustomEditor(typeof(IceUIStyleSheet)), CanEditMultipleObjects]
    public sealed class IceUIStyleSheetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Shared changes are applied explicitly in Edit Mode. This only updates enabled channels on Ice UI Style components in loaded scenes and the open Prefab Stage. Unopened prefab assets keep their authored values until opened and applied. Runtime enable also applies shared channels. Spacing and borders are reference values; no layout is changed.", MessageType.Info);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                if (GUILayout.Button("Apply Theme to Loaded UI"))
                    foreach (var item in targets) IceUIStyleAuthoring.ApplyLoaded((IceUIStyleSheet)item);
        }
    }

    [CustomEditor(typeof(IceUIStyle)), CanEditMultipleObjects]
    public sealed class IceUIStyleEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("To override a font, color, sprite, size or Selectable/Button states, first disable that shared channel here, then edit the target component. Alignment, material, RectTransform, spacing and text content always remain locally authored. Theme changes are not applied while you edit; use the button below, or Apply Theme to Loaded UI on the theme asset.", MessageType.Info);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                if (GUILayout.Button("Apply Shared Style (Undoable)"))
                {
                    foreach (var item in targets) IceUIStyleAuthoring.ApplyWithUndo((IceUIStyle)item);
                    SceneView.RepaintAll();
                }
        }
    }
}
#endif
