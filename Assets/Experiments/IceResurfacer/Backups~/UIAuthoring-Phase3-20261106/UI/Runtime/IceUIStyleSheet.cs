using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    public enum IceUIFontRole { Primary, Secondary, Numeric }
    public enum IceUIColorRole { Text, Muted, Panel, Inset, Button, Accent, Warning, Danger, Border, Paper }
    public enum IceUISpriteRole { Panel, Button, MeterBackground, MeterFill }

    /// <summary>Shared visual choices. Spacing and borders are design conventions, never layout commands.</summary>
    [CreateAssetMenu(fileName = "Aurora UI Style", menuName = "Aurora/UI Style Sheet")]
    public sealed class IceUIStyleSheet : ScriptableObject
    {
        [Header("Typography")]
        public TMP_FontAsset primaryFont;
        public TMP_FontAsset secondaryFont;
        public TMP_FontAsset numericFont;
        [Min(1)] public float primaryFontSize = 19;
        [Min(1)] public float secondaryFontSize = 16;
        [Min(1)] public float numericFontSize = 32;

        [Header("Existing Aurora palette")]
        public Color panelColor = new Color32(211, 211, 199, 255);
        public Color insetColor = new Color32(179, 182, 169, 255);
        public Color buttonColor = new Color32(236, 233, 218, 255);
        public Color textColor = new Color32(24, 26, 22, 255);
        public Color mutedColor = new Color32(55, 57, 50, 255);
        public Color accentColor = new Color32(56, 78, 32, 255);
        public Color warningColor = new Color32(126, 68, 9, 255);
        public Color dangerColor = new Color32(146, 36, 24, 255);
        public Color borderColor = new Color32(98, 101, 88, 255);
        public Color paperColor = new Color32(235, 232, 217, 255);

        [Header("Optional artwork")]
        [Tooltip("An unassigned shared sprite preserves a widget's existing sprite.")]
        public Sprite panelSprite;
        public Sprite buttonSprite;
        public Sprite meterBackgroundSprite;
        public Sprite meterFillSprite;
        public SpriteState buttonSpriteState;
        public ColorBlock buttonColors = ColorBlock.defaultColorBlock;

        [Header("Authoring conventions (reference only)")]
        [Tooltip("Suggested horizontal and vertical gaps. Never applied to RectTransforms or layout groups.")]
        public Vector2 commonSpacing = new Vector2(12, 8);
        [Tooltip("Suggested left, right, top and bottom padding. Reference for manual authoring only.")]
        public Vector4 commonPadding = new Vector4(12, 12, 8, 8);
        [Min(0)] public float borderThickness = 1;

        public TMP_FontAsset GetFont(IceUIFontRole role)
        {
            switch (role)
            {
                case IceUIFontRole.Secondary: return secondaryFont;
                case IceUIFontRole.Numeric: return numericFont;
                default: return primaryFont;
            }
        }

        public float GetFontSize(IceUIFontRole role)
        {
            switch (role)
            {
                case IceUIFontRole.Secondary: return secondaryFontSize;
                case IceUIFontRole.Numeric: return numericFontSize;
                default: return primaryFontSize;
            }
        }

        public Color GetColor(IceUIColorRole role)
        {
            switch (role)
            {
                case IceUIColorRole.Muted: return mutedColor;
                case IceUIColorRole.Panel: return panelColor;
                case IceUIColorRole.Inset: return insetColor;
                case IceUIColorRole.Button: return buttonColor;
                case IceUIColorRole.Accent: return accentColor;
                case IceUIColorRole.Warning: return warningColor;
                case IceUIColorRole.Danger: return dangerColor;
                case IceUIColorRole.Border: return borderColor;
                case IceUIColorRole.Paper: return paperColor;
                default: return textColor;
            }
        }

        public Sprite GetSprite(IceUISpriteRole role)
        {
            switch (role)
            {
                case IceUISpriteRole.Button: return buttonSprite;
                case IceUISpriteRole.MeterBackground: return meterBackgroundSprite;
                case IceUISpriteRole.MeterFill: return meterFillSprite;
                default: return panelSprite;
            }
        }
    }
}
