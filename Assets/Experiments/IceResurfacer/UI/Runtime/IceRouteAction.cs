using UnityEngine;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    public enum IceRouteActionKind
    {
        Close, Refresh, Record, StopSave, Stop, SaveCopy, PreviewLoad, UseGps, ToggleGps, Edit, Rename, Delete,
        Confirm, Cancel, LibraryTab, RawTab, GuidesTab, Original, Fixture, Rejoin, Magenta, Amber, Folder
    }
    /// <summary>Reusable explicit command binding. Visual rearrangement never changes the action or its file target.</summary>
    [RequireComponent(typeof(IceUIClickGuard))]
    public sealed class IceRouteAction : MonoBehaviour
    {
        public IceRouteManager owner;
        public IceRouteActionKind kind;
        public Button button;
        IceUIClickGuard clickGuard;
        public void Bind(IceRouteManager manager)
        {
            if (button != null) button.onClick.RemoveListener(Invoke);
            owner = manager;
            clickGuard = GetComponent<IceUIClickGuard>();
            if (button != null) button.onClick.AddListener(Invoke);
        }
        public void Invoke() { if (owner != null && (clickGuard == null || clickGuard.CanDispatch)) owner.Activate(kind); }
        void OnDestroy() { if (button != null) button.onClick.RemoveListener(Invoke); }
    }
}
