using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    /// <summary>Optional standard-layout adapter for wrapping scroll-list text; never moves a panel.</summary>
    [RequireComponent(typeof(LayoutElement))]
    public sealed class IceUITextHeight : MonoBehaviour
    {
        public TMP_Text text;
        [Min(0)] public float minimumHeight = 24;
        [Min(0)] public float padding = 6;
        LayoutElement element;
        string observedText;
        float observedWidth = -1, observedFontSize = -1;
        void OnEnable() { element = GetComponent<LayoutElement>(); Refresh(); }
        void LateUpdate() { if (Application.IsPlaying(gameObject)) Refresh(); }
        public void Refresh()
        {
            if (text == null) return;
            if (element == null) element = GetComponent<LayoutElement>();
            float width = text.rectTransform.rect.width;
            if (width <= 0 || observedText == text.text && Mathf.Approximately(width, observedWidth) && Mathf.Approximately(text.fontSize, observedFontSize)) return;
            observedText = text.text; observedWidth = width; observedFontSize = text.fontSize;
            float height = Mathf.Max(minimumHeight, text.GetPreferredValues(text.text, width, 0).y + padding);
            if (!Mathf.Approximately(element.preferredHeight, height)) element.preferredHeight = height;
        }
    }
}
