using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    /// <summary>
    /// Queries the current authored graphics, independently of EventSystem.Update order.
    /// A press that began on UI keeps ownership until release, including outside its rect.
    /// </summary>
    public sealed class IceUIPointerGuard
    {
        readonly List<RaycastResult> results = new List<RaycastResult>(16);
        PointerEventData pointer;
        EventSystem eventSystem;
        Mouse pressOwner;

        public bool ContainsPointer(GraphicRaycaster raycaster, Vector2 screenPoint)
        {
            var mouse = Mouse.current;
            if (pressOwner != null && (pressOwner != mouse || !pressOwner.added || !pressOwner.leftButton.isPressed))
                pressOwner = null;

            bool overGraphic = Raycast(raycaster, screenPoint);
            if (mouse != null && mouse.added && mouse.leftButton.wasPressedThisFrame && overGraphic)
                pressOwner = mouse;
            return overGraphic || pressOwner != null;
        }

        bool Raycast(GraphicRaycaster raycaster, Vector2 screenPoint)
        {
            if (raycaster == null || !raycaster.isActiveAndEnabled) return false;
            var current = EventSystem.current;
            if (current == null || !current.isActiveAndEnabled) return false;
            if (pointer == null || eventSystem != current)
            {
                eventSystem = current;
                pointer = new PointerEventData(current);
            }
            pointer.Reset();
            pointer.position = screenPoint;
            results.Clear();
            raycaster.Raycast(pointer, results);
            return results.Count != 0;
        }

        public void Release()
        {
            pressOwner = null;
            results.Clear();
        }
    }
}
