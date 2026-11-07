#if UNITY_EDITOR
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace IceResurfacer.Editor
{
    /// <summary>Read-only binding diagnostics; editing the Inspector never runs a simulation or refreshes preview text.</summary>
    [CustomEditor(typeof(IceHudWidget)), CanEditMultipleObjects]
    public sealed class IceHudWidgetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            foreach (var item in targets)
            {
                var widget = (IceHudWidget)item;
                if (!widget.ValidateBinding(out string error))
                    EditorGUILayout.HelpBox(widget.name + ": " + error, MessageType.Error);
                else
                    EditorGUILayout.HelpBox(widget.name + ": Required bindings are assigned. Edit the referenced TMP text and Images for the design preview. Play replaces only live text and meter fill amounts.", MessageType.Info);
                if (widget.fill != null && widget.fill.type != Image.Type.Filled)
                    EditorGUILayout.HelpBox("The meter Fill Image should use Image Type = Filled. Otherwise fillAmount will not display the live tank or load level.", MessageType.Warning);
            }
        }
    }

    [CustomEditor(typeof(IceAuthoredHud)), CanEditMultipleObjects]
    public sealed class IceAuthoredHudEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            foreach (var item in targets) Inspect((IceAuthoredHud)item);
            EditorGUILayout.HelpBox("This Inspector validates references without modifying preview values or gameplay. The reusable prefab may have no Game reference; its authored scene instance must reference IceResurfacerGame. Moving widgets in the hierarchy is safe while their explicit references remain assigned. Use one EventSystem with InputSystemUIInputModule in the scene.", MessageType.Info);
        }

        static void Inspect(IceAuthoredHud hud)
        {
            if (hud.game == null)
                EditorGUILayout.HelpBox(hud.name + ": Game is unassigned. This is expected on the reusable prefab; assign the simulator on the scene instance.", MessageType.Warning);
            if (hud.canvas == null)
                EditorGUILayout.HelpBox(hud.name + ": Assign the persistent Canvas.", MessageType.Error);
            if (hud.visibility == null)
                EditorGUILayout.HelpBox(hud.name + ": Assign the CanvasGroup that controls only this top HUD's visibility.", MessageType.Error);
            if (hud.raycaster == null)
                EditorGUILayout.HelpBox(hud.name + ": Assign the Canvas GraphicRaycaster.", MessageType.Error);
            else if (hud.canvas != null && hud.raycaster.GetComponent<Canvas>() != hud.canvas)
                EditorGUILayout.HelpBox(hud.name + ": The GraphicRaycaster must belong to the assigned Canvas.", MessageType.Error);
            if (hud.previewBadge == null)
                EditorGUILayout.HelpBox(hud.name + ": Assign a visible design-preview badge so saved placeholder values are clearly identified outside Play Mode.", MessageType.Warning);
            if (hud.widgets == null || hud.widgets.Length == 0)
            {
                EditorGUILayout.HelpBox(hud.name + ": Assign the authored widget instances to Widgets.", MessageType.Error);
                return;
            }
            for (int i = 0; i < hud.widgets.Length; i++)
            {
                var widget = hud.widgets[i];
                if (widget == null)
                    EditorGUILayout.HelpBox(hud.name + ": Widgets element " + i + " is unassigned.", MessageType.Error);
                else if (!widget.ValidateBinding(out string error))
                    EditorGUILayout.HelpBox(widget.name + ": " + error, MessageType.Error);
                for (int j = 0; widget != null && j < i; j++)
                    if (hud.widgets[j] == widget)
                        EditorGUILayout.HelpBox(widget.name + ": Widget is assigned more than once. Keep one reference per widget.", MessageType.Warning);
            }
        }
    }
}
#endif
