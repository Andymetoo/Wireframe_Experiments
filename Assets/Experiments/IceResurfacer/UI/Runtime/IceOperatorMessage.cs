using TMPro;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    /// <summary>Editable message template. Content/lifetime come from the existing dashboard.</summary>
    public sealed class IceOperatorMessage : MonoBehaviour
    {
        public CanvasGroup visibility;
        public TMP_Text content;
        [Tooltip("Visual fade during the existing eight-second routine notice lifetime.")]
        [Range(.01f, 8)] public float fadeSeconds = 1;
        public void Present(string message, bool show, bool interactive, float opacity = 1)
        {
            if (visibility == null) return;
            float alpha = show ? Mathf.Clamp01(opacity) : 0;
            if (visibility.alpha != alpha) visibility.alpha = alpha;
            bool blocks = show && interactive;
            if (visibility.blocksRaycasts != blocks) visibility.blocksRaycasts = blocks;
            if (visibility.interactable != blocks) visibility.interactable = blocks;
            if (show) IceNavigationUI.Text(content, message);
        }
    }
}
