#nullable enable

using Rectloom.Core.Ir;
using TMPro;
using UnityEngine;

namespace Rectloom.Ugui.Backend
{
    /// <summary>
    /// Maps an IR text style onto a TextMeshPro component.
    /// </summary>
    /// <remarks>
    /// Mapping only: every value arrives already resolved, so nothing here computes a size or reads
    /// a style sheet. The conversions that do happen are unit changes into TextMeshPro's own
    /// conventions, which are documented at each one.
    /// </remarks>
    public static class TmpTextApplier
    {
        /// <summary>
        /// Applies text and style to a component.
        /// </summary>
        /// <param name="text">Component to write to.</param>
        /// <param name="content">Text to render.</param>
        /// <param name="style">Resolved text style.</param>
        public static void Apply(TMP_Text text, string? content, UiTextStyle style)
        {
            if (text == null || style == null)
            {
                return;
            }

            text.text = content ?? string.Empty;
            text.color = style.Color;
            text.fontSize = style.FontSize;
            text.fontStyle = ResolveFontStyle(style);
            text.alignment = ResolveAlignment(style.Alignment);
            text.enableWordWrapping = style.Wrap;
            text.richText = false;

            // TextMeshPro expresses both of these as a percentage of the font size rather than in
            // pixels, so a style that says 1.5 line height becomes 50 extra percent.
            text.lineSpacing = (style.LineHeight - 1f) * 100f;
            text.characterSpacing = style.FontSize > 0f
                ? style.LetterSpacing / style.FontSize * 100f
                : 0f;

            text.overflowMode = TextOverflowModes.Overflow;
        }

        private static FontStyles ResolveFontStyle(UiTextStyle style)
        {
            var result = FontStyles.Normal;

            if (style.Bold)
            {
                result |= FontStyles.Bold;
            }

            if (style.Italic)
            {
                result |= FontStyles.Italic;
            }

            return result;
        }

        /// <summary>
        /// Maps horizontal alignment, anchoring text to the top.
        /// </summary>
        /// <remarks>
        /// CSS has no vertical alignment within a block: text starts at the top of its box and grows
        /// down. Anchoring to the top is what makes a baked box agree with the height layout
        /// measured for it.
        /// </remarks>
        private static TextAlignmentOptions ResolveAlignment(UiTextAlign align)
        {
            switch (align)
            {
                case UiTextAlign.Center:
                    return TextAlignmentOptions.Top;
                case UiTextAlign.Right:
                    return TextAlignmentOptions.TopRight;
                case UiTextAlign.Justify:
                    return TextAlignmentOptions.TopJustified;
                default:
                    return TextAlignmentOptions.TopLeft;
            }
        }
    }
}
