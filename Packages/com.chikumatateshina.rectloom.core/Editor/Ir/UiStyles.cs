#nullable enable

using UnityEngine;

namespace Rectloom.Core.Ir
{
    /// <summary>
    /// Horizontal alignment of a text node.
    /// </summary>
    public enum UiTextAlign
    {
        /// <summary>Aligned to the left edge.</summary>
        Left = 0,

        /// <summary>Centred.</summary>
        Center = 1,

        /// <summary>Aligned to the right edge.</summary>
        Right = 2,

        /// <summary>Spread to both edges.</summary>
        Justify = 3,
    }

    /// <summary>
    /// What a node paints for itself.
    /// </summary>
    /// <remarks>
    /// Colours are nullable so that "paints nothing" stays distinct from "paints something
    /// transparent". A container that paints nothing gets no graphic component at all, which is what
    /// keeps a deep hierarchy from filling up with invisible images.
    /// </remarks>
    public sealed class UiVisualStyle
    {
        /// <summary>Background fill, or <see langword="null"/> when there is none.</summary>
        public Color? BackgroundColor { get; internal set; }

        /// <summary>Opacity from 0 to 1.</summary>
        public float Opacity { get; internal set; } = 1f;

        /// <summary>Border thickness in logical pixels.</summary>
        public float BorderWidth { get; internal set; }

        /// <summary>Border colour, or <see langword="null"/> when none was declared.</summary>
        public Color? BorderColor { get; internal set; }

        /// <summary>Corner radius in logical pixels.</summary>
        public float BorderRadius { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether this node needs a graphic component.
        /// </summary>
        public bool PaintsAnything =>
            BackgroundColor.HasValue || (BorderWidth > 0f && BorderColor.HasValue);

        /// <summary>
        /// Gets a value indicating whether the node has a visible border.
        /// </summary>
        public bool HasBorder => BorderWidth > 0f && BorderColor.HasValue;
    }

    /// <summary>
    /// How a node's text is rendered.
    /// </summary>
    /// <remarks>
    /// Values are already in logical pixels or in the form a text component wants, so the backend
    /// maps rather than computes: there is no cascade or unit resolution left to do here.
    /// </remarks>
    public sealed class UiTextStyle
    {
        /// <summary>Text colour.</summary>
        public Color Color { get; internal set; } = UnityEngine.Color.black;

        /// <summary>Font size in logical pixels.</summary>
        public float FontSize { get; internal set; } = 16f;

        /// <summary>Font weight from 100 to 900.</summary>
        public int FontWeight { get; internal set; } = 400;

        /// <summary>Whether the text is italic.</summary>
        public bool Italic { get; internal set; }

        /// <summary>Whether the text is bold, derived from <see cref="FontWeight"/>.</summary>
        public bool Bold => FontWeight >= 700;

        /// <summary>Horizontal alignment.</summary>
        public UiTextAlign Alignment { get; internal set; } = UiTextAlign.Left;

        /// <summary>Line height as a multiple of <see cref="FontSize"/>.</summary>
        public float LineHeight { get; internal set; } = 1.2f;

        /// <summary>Extra space between characters, in logical pixels.</summary>
        public float LetterSpacing { get; internal set; }

        /// <summary>Whether text may wrap onto another line.</summary>
        public bool Wrap { get; internal set; } = true;
    }
}
