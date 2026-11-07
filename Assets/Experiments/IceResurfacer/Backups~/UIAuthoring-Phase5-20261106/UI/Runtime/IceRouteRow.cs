using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    /// <summary>Visual row template. A full path is bound explicitly; hierarchy/index/caption never identify a route.</summary>
    [RequireComponent(typeof(IceUIClickGuard))]
    public sealed class IceRouteRow : MonoBehaviour
    {
        public Button button;
        public TMP_Text title, metadata, validation;
        public GameObject selectedIndicator;
        public bool previewOnly;
        public string BoundPath { get; private set; }
        IceRouteManager owner;
        IceUIClickGuard clickGuard;
        public void Bind(IceRouteManager manager, RouteLibraryCatalog.Entry entry)
        {
            Unbind(); previewOnly = false; owner = manager; BoundPath = entry.path; clickGuard = GetComponent<IceUIClickGuard>();
            IceNavigationUI.Text(title, entry.name);
            IceNavigationUI.Text(metadata, (entry.guide != null ? "Guide" : entry.raw != null && !entry.raw.complete ? "Recovery draft" : "Recording") +
                " / " + entry.date + " / " + entry.Length.ToString("F0") + " m\n" + System.IO.Path.GetFileName(entry.path));
            IceNavigationUI.Text(validation, entry.error ?? (entry.raw != null && !entry.raw.complete ? "Incomplete — preview/recovery only" : "Select to inspect / compatibility checked on activation"));
            if (button != null) button.onClick.AddListener(SelectBoundRoute);
            SetSelected(manager.SelectedPath == BoundPath);
        }
        void SelectBoundRoute() { if (!previewOnly && owner != null && BoundPath != null && (clickGuard == null || clickGuard.CanDispatch)) owner.SelectPath(BoundPath); }
        public void SetSelected(bool selected) { if (selectedIndicator != null && selectedIndicator.activeSelf != selected) selectedIndicator.SetActive(selected); }
        public void Unbind()
        {
            if (clickGuard != null) clickGuard.Cancel();
            if (button != null) button.onClick.RemoveListener(SelectBoundRoute);
            owner = null; BoundPath = null;
        }
        void OnDestroy() => Unbind();
    }
}
