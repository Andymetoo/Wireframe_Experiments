using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    /// <summary>Opt-in visual binding. Never changes text content, RectTransforms, alignment or layout.</summary>
    [DisallowMultipleComponent]
    public sealed class IceUIStyle : MonoBehaviour
    {
        public IceUIStyleSheet theme;
        [Header("Explicit targets")]
        public TMP_Text text;
        public Image image;
        public Button button;

        [Header("Shared channels (disable a channel to edit it locally)")]
        public bool applyFont = true;
        public IceUIFontRole fontRole = IceUIFontRole.Primary;
        [Tooltip("Optional. Disabled by default so each label keeps its authored size.")]
        public bool applyFontSize;
        public bool applyColor = true;
        public IceUIColorRole colorRole = IceUIColorRole.Text;
        public bool applySprite;
        public IceUISpriteRole spriteRole = IceUISpriteRole.Panel;
        [Tooltip("Applies Button colors, state sprites and transition. SpriteSwap is used when the theme has state sprites; otherwise ColorTint.")]
        public bool applyButtonStates;

        void OnEnable()
        {
            if (Application.IsPlaying(gameObject)) Apply();
        }

        /// <summary>Called once on runtime enable, or explicitly by the Undo-backed authoring tool.</summary>
        public void Apply()
        {
            if (theme == null)
            {
                Debug.LogWarning("Aurora UI Style needs a shared theme. Assign one or remove this optional styling component.", this);
                return;
            }

            if (text != null)
            {
                TMP_FontAsset font = theme.GetFont(fontRole);
                if (applyFont && font != null) text.font = font;
                if (applyFontSize) text.fontSize = theme.GetFontSize(fontRole);
                if (applyColor) text.color = theme.GetColor(colorRole);
            }
            if (image != null)
            {
                if (applyColor) image.color = theme.GetColor(colorRole);
                Sprite sprite = theme.GetSprite(spriteRole);
                if (applySprite && sprite != null) image.sprite = sprite;
            }
            if (button != null && applyButtonStates)
            {
                button.colors = theme.buttonColors;
                button.spriteState = theme.buttonSpriteState;
                var states = theme.buttonSpriteState;
                bool hasSprites = states.highlightedSprite != null || states.pressedSprite != null ||
                    states.selectedSprite != null || states.disabledSprite != null;
                button.transition = hasSprites ? Selectable.Transition.SpriteSwap : Selectable.Transition.ColorTint;
            }
        }
    }
}
