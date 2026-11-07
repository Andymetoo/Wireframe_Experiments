using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    public enum IceOperatorActionKind { Routes, History, Help, Pause, CloseHistory, RoutineMessages }
    [RequireComponent(typeof(IceUIClickGuard))]
    public sealed class IceOperatorAction : MonoBehaviour
    {
        public IceOperatorOverlay owner;
        public IceOperatorActionKind kind;
        public Button button;
        IceUIClickGuard guard;
        public void Bind(IceOperatorOverlay presenter)
        { Unbind(); owner = presenter; guard = GetComponent<IceUIClickGuard>(); if (button != null) button.onClick.AddListener(Click); }
        public void Unbind() { if (button != null) button.onClick.RemoveListener(Click); }
        void Click() { if (owner != null && (guard == null || guard.CanDispatch)) owner.Activate(this); }
        void OnDestroy() => Unbind();
    }
}
