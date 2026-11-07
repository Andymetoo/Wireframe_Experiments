using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    /// <summary>Shared presentation policy for the Phase 3 modules on the existing Canvas.</summary>
    public static class IceNavigationUI
    {
        public static bool Ready(Canvas canvas, GraphicRaycaster raycaster)
        {
            var events = EventSystem.current;
            return canvas != null && canvas.isActiveAndEnabled && raycaster != null && raycaster.isActiveAndEnabled &&
                events != null && events.isActiveAndEnabled && events.GetComponent<InputSystemUIInputModule>() is InputSystemUIInputModule module && module.isActiveAndEnabled;
        }
        public static bool Driving(IceResurfacerGame game) => game != null && game.isActiveAndEnabled && game.Sim != null &&
            game.Started && !game.Sim.Finished && !game.Sim.OnFoot && !game.HudHidden && !game.Paused && !game.HelpVisible &&
            (game.RouteGuide == null || !game.RouteGuide.PanelOpen);
        public static bool Pointer(IceResurfacerGame game) => game != null && game.HasFocus && !game.Looking &&
            Cursor.lockState != CursorLockMode.Locked && (Mouse.current == null || !Mouse.current.rightButton.isPressed) &&
            (game.AuthoredConsole == null || !game.AuthoredConsole.ConfirmationOpen);
        public static void Group(CanvasGroup group, bool visible, bool interactive)
        {
            if (group == null) return;
            float alpha = visible ? 1 : 0;
            if (group.alpha != alpha) group.alpha = alpha;
            bool active = visible && interactive;
            if (group.blocksRaycasts != active) group.blocksRaycasts = active;
            if (group.interactable != active) group.interactable = active;
        }
        public static void Text(TMP_Text target, string value) { if (target != null && target.text != value) target.text = value; }
        public static void ClearFocus(Transform root)
        {
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null && events.currentSelectedGameObject.transform.IsChildOf(root))
                events.SetSelectedGameObject(null);
        }
    }
}
