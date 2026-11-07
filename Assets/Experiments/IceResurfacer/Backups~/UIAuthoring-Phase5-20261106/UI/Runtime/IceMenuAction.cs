using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    public enum IceMenuActionKind { StartScored, StartPractice, StartTraining, ToggleHelp, Resume, NewSessionSheet, RefillPractice }
    [RequireComponent(typeof(IceUIClickGuard))]
    public sealed class IceMenuAction : MonoBehaviour, ISubmitHandler
    {
        public IceMenuScreens owner;
        public IceMenuScreen screen;
        public IceMenuActionKind kind;
        public Button button;
        IceUIClickGuard guard;
        public void Bind(IceMenuScreens presenter)
        {
            Unbind(); owner = presenter; guard = GetComponent<IceUIClickGuard>();
            if (button != null) button.onClick.AddListener(Click);
        }
        public void Unbind() { if (button != null) button.onClick.RemoveListener(Click); }
        void Click() { if (owner != null && (guard == null || guard.CanDispatch)) owner.Activate(this); }
        // Keyboard submit is a new deliberate action, even after a canceled mouse press.
        // The owner rejects multiple dispatches in the same frame regardless of handler order.
        public void OnSubmit(BaseEventData e) { if (owner != null) owner.Activate(this); }
        void OnDestroy() => Unbind();
    }
}
