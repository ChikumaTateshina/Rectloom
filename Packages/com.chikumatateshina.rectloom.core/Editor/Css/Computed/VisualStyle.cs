#nullable enable

using Rectloom.Core.Css.Values;
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
        /// Background image as a project asset path or GUID, or <see langword="null"/> when the
        /// element has none. Loading the asset happens in the backend.
        /// </summary>
        /// <remarks>
        /// A relative <c>url()</c> is resolved against the stylesheet that wrote it while the
        /// computed style is built, because that is the last point where the origin file is known.
        /// </remarks>
        public string? BackgroundImage { get; internal set; }

        /// <summary>
        /// Position of the <c>background-image</c> declaration, so a missing asset can be reported
        /// against the stylesheet line rather than against the element.
        /// </summary>
        public Diagnostics.SourceLocation BackgroundImageSource { get; internal set; }

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
        /// Whether content leaving this element's box is clipped to it. Initial value is visible.
        /// </summary>
        /// <remarks>
        /// Not inherited, and set from whichever of <c>overflow</c>, <c>overflow-x</c> and
        /// <c>overflow-y</c> asks for clipping. uGUI clips rectangles, not axes, so a box that clips on
        /// one axis clips on both; declaring different values per axis therefore loses the distinction
        /// rather than the clipping.
        /// </remarks>
        public CssOverflow Overflow { get; internal set; } = CssOverflow.Visible;

        /// <summary>
        /// How an image is sized inside its box. Initial value is fill, which is what an
        /// <c>img</c> with a declared width and height does in CSS.
        /// </summary>
        public CssObjectFit ObjectFit { get; internal set; } = CssObjectFit.Fill;

        /// <summary>
        /// Gets a value indicating whether content is clipped to this element's box.
        /// </summary>
        public bool ClipsContent => Overflow == CssOverflow.Hidden;

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
                BackgroundImageSource = BackgroundImageSource,
                Opacity = Opacity,
                BorderWidth = BorderWidth,
                BorderColor = BorderColor,
                BorderRadius = BorderRadius,
                Overflow = Overflow,
                ObjectFit = ObjectFit,
            };
        }
    }
}
