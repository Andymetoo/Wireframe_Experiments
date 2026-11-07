using UnityEngine;
using UnityEngine.EventSystems;

namespace IceResurfacer.Presentation
{
    /// <summary>Retains cancellation across CanvasGroup hide/reopen and pooled-row rebinding until a fresh press.</summary>
    public sealed class IceUIClickGuard : MonoBehaviour, IPointerDownHandler, IBeginDragHandler
    {
        bool canceled;
        public bool CanDispatch => !canceled;
        public void Cancel() { canceled = true; }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) canceled = false; }
        public void OnBeginDrag(PointerEventData e) { canceled = true; }
        void OnDisable() { canceled = true; }
    }
}
