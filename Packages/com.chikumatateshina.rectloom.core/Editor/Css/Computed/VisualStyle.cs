#nullable enable

using UnityEngine;

namespace Rectloom.Core.Css.Computed
{
    /// <summary>
    /// The painted properties of an element's own box.
    /// </summary>
    /// <remarks>
    /// Colours are nullable so that "not painted" is distinct from "painted with a transparent
    /// colour". A container only gets an Image component when it actually paints something, which
    /// is what keeps a deep hierarchy from filling up with invisible graphics.
    /// </remarks>
    public sealed class VisualStyle
    {
        /// <summary>Default opacity, meaning fully opaque.</summary>
        public const float DefaultOpacity = 1f;

        /// <summary>
        /// Background fill, or <see langword="null"/> when the element paints no background.
        /// </summary>
        public Color? BackgroundColor { get; internal set; }

        /// <summary>
        /// Background image reference as written in the stylesheet, or <see langword="null"/> when
        /// the element has none. Resolving it to an asset happens in the backend.
        /// </summary>
        public string? BackgroundImage { get; internal set; }

        /// <summary>
        /// Opacity from 0 to 1. Initial value is 1.
        /// </summary>
        /// <remarks>
        /// An element with a single graphic applies this to that graphic's alpha; one with children
        /// needs a CanvasGroup, because CSS opacity applies to the element and its subtree together.
        /// </remarks>
        public float Opacity { get; internal set; } = DefaultOpacity;

        /// <summary>Border thickness in logical pixels. Initial value is zero.</summary>
        public float BorderWidth { get; internal set; }

        /// <summary>
        /// Border colour, or <see langword="null"/> when no border colour was declared.
        /// </summary>
        public Color? BorderColor { get; internal set; }

        /// <summary>Corner radius in logical pixels. Initial value is zero.</summary>
        public float BorderRadius { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether this element paints anything of its own.
        /// </summary>
        /// <remarks>
        /// The backend uses this to decide whether a container needs a graphic at all.
        /// </remarks>
        public bool PaintsAnything =>
            BackgroundColor.HasValue
            || BackgroundImage != null
            || (BorderWidth > 0f && BorderColor.HasValue);

        /// <summary>
        /// Creates a copy of these values.
        /// </summary>
        /// <returns>An independent copy.</returns>
        public VisualStyle Clone()
        {
            return new VisualStyle
            {
                BackgroundColor = BackgroundColor,
                BackgroundImage = BackgroundImage,
                Opacity = Opacity,
                BorderWidth = BorderWidth,
                BorderColor = BorderColor,
                BorderRadius = BorderRadius,
            };
        }
    }
}
