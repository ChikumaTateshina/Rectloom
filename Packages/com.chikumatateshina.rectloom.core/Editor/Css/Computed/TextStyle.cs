#nullable enable

using Rectloom.Core.Css.Values;
using UnityEngine;

namespace Rectloom.Core.Css.Computed
{
    /// <summary>
    /// The text properties of an element.
    /// </summary>
    /// <remarks>
    /// Every property here is inherited, so a child starts from its parent's values rather than from
    /// the initial ones. That is what lets a colour set on a panel reach the labels inside it.
    /// </remarks>
    public sealed class TextStyle
    {
        /// <summary>Initial font size in logical pixels.</summary>
        public const float DefaultFontSize = 16f;

        /// <summary>Font weight of normal text.</summary>
        public const int NormalWeight = 400;

        /// <summary>Font weight of bold text.</summary>
        public const int BoldWeight = 700;

        /// <summary>
        /// Line height as a multiple of the font size. Initial value is 1.2, which is what
        /// <c>line-height: normal</c> resolves to.
        /// </summary>
        public const float DefaultLineHeight = 1.2f;

        /// <summary>Text colour. Initial value is opaque black.</summary>
        public Color Color { get; internal set; } = Color.black;

        /// <summary>Font size in logical pixels. Initial value is 16.</summary>
        public float FontSize { get; internal set; } = DefaultFontSize;

        /// <summary>
        /// Font weight from 100 to 900. Initial value is 400.
        /// </summary>
        /// <remarks>
        /// Kept as a number rather than an enum because CSS allows the full numeric scale, and the
        /// backend maps it onto whatever weights the chosen font actually provides.
        /// </remarks>
        public int FontWeight { get; internal set; } = NormalWeight;

        /// <summary>Whether the text is italic. Initial value is normal.</summary>
        public CssFontStyle FontStyle { get; internal set; } = CssFontStyle.Normal;

        /// <summary>Horizontal alignment. Initial value is left.</summary>
        public CssTextAlign TextAlign { get; internal set; } = CssTextAlign.Left;

        /// <summary>
        /// Line height as a multiple of <see cref="FontSize"/>.
        /// </summary>
        /// <remarks>
        /// A length such as <c>line-height: 24px</c> is converted to a multiple using the font size
        /// computed for the same element, so the value stays meaningful when a descendant inherits
        /// it at a different size.
        /// </remarks>
        public float LineHeight { get; internal set; } = DefaultLineHeight;

        /// <summary>Extra space between characters, in logical pixels. Initial value is zero.</summary>
        public float LetterSpacing { get; internal set; }

        /// <summary>How whitespace and line breaks are treated. Initial value is normal.</summary>
        public CssWhiteSpace WhiteSpace { get; internal set; } = CssWhiteSpace.Normal;

        /// <summary>
        /// Gets a value indicating whether text is allowed to wrap onto another line.
        /// </summary>
        public bool WrapsText =>
            WhiteSpace == CssWhiteSpace.Normal || WhiteSpace == CssWhiteSpace.PreWrap;

        /// <summary>
        /// Gets a value indicating whether runs of whitespace are collapsed into one space.
        /// </summary>
        public bool CollapsesWhitespace =>
            WhiteSpace == CssWhiteSpace.Normal || WhiteSpace == CssWhiteSpace.NoWrap;

        /// <summary>
        /// Creates a copy of these values, as used when a child inherits from its parent.
        /// </summary>
        /// <returns>An independent copy.</returns>
        public TextStyle Clone()
        {
            return new TextStyle
            {
                Color = Color,
                FontSize = FontSize,
                FontWeight = FontWeight,
                FontStyle = FontStyle,
                TextAlign = TextAlign,
                LineHeight = LineHeight,
                LetterSpacing = LetterSpacing,
                WhiteSpace = WhiteSpace,
            };
        }
    }
}
