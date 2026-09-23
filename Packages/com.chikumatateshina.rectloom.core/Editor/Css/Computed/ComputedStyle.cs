#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Css.Values;

namespace Rectloom.Core.Css.Computed
{
    /// <summary>
    /// The resolved style of one element, in typed values rather than raw strings.
    /// </summary>
    /// <remarks>
    /// This is the boundary between CSS and layout. Nothing downstream re-reads a stylesheet, and no
    /// layout code parses a string, which is what keeps the cascade out of the layout solver and the
    /// backend.
    /// <para>
    /// Lengths keep their unit because a percentage cannot be resolved until layout knows the
    /// containing block. Values that CSS resolves at computed-value time, such as font size and
    /// border width, are already in logical pixels.
    /// </para>
    /// </remarks>
    public sealed class ComputedStyle
    {
        private static readonly IReadOnlyDictionary<string, string> NoExtensionProperties =
            new Dictionary<string, string>(0, StringComparer.Ordinal);

        /// <summary>How the element generates boxes. Initial value is block.</summary>
        public CssDisplay Display { get; internal set; } = CssDisplay.Block;

        /// <summary>Declared width. Initial value is auto.</summary>
        public CssLength Width { get; internal set; } = CssLength.Auto;

        /// <summary>Declared height. Initial value is auto.</summary>
        public CssLength Height { get; internal set; } = CssLength.Auto;

        /// <summary>Lower bound on width. Initial value is auto, meaning no bound.</summary>
        public CssLength MinWidth { get; internal set; } = CssLength.Auto;

        /// <summary>Lower bound on height. Initial value is auto, meaning no bound.</summary>
        public CssLength MinHeight { get; internal set; } = CssLength.Auto;

        /// <summary>Upper bound on width. Initial value is auto, meaning no bound.</summary>
        public CssLength MaxWidth { get; internal set; } = CssLength.Auto;

        /// <summary>Upper bound on height. Initial value is auto, meaning no bound.</summary>
        public CssLength MaxHeight { get; internal set; } = CssLength.Auto;

        /// <summary>Space outside the border box. Initial value is zero on every edge.</summary>
        public EdgeSizes Margin { get; internal set; } = EdgeSizes.Zero;

        /// <summary>Space between the border box and the content. Initial value is zero on every edge.</summary>
        public EdgeSizes Padding { get; internal set; } = EdgeSizes.Zero;

        /// <summary>How the element is positioned. Initial value is static.</summary>
        public CssPosition Position { get; internal set; } = CssPosition.Static;

        /// <summary>Offset from the containing block's top edge. Initial value is auto.</summary>
        public CssLength Top { get; internal set; } = CssLength.Auto;

        /// <summary>Offset from the containing block's right edge. Initial value is auto.</summary>
        public CssLength Right { get; internal set; } = CssLength.Auto;

        /// <summary>Offset from the containing block's bottom edge. Initial value is auto.</summary>
        public CssLength Bottom { get; internal set; } = CssLength.Auto;

        /// <summary>Offset from the containing block's left edge. Initial value is auto.</summary>
        public CssLength Left { get; internal set; } = CssLength.Auto;

        /// <summary>Flex container properties.</summary>
        public FlexStyle Flex { get; internal set; } = new FlexStyle();

        /// <summary>Painted properties of the element's own box.</summary>
        public VisualStyle Visual { get; internal set; } = new VisualStyle();

        /// <summary>Inherited text properties.</summary>
        public TextStyle Text { get; internal set; } = new TextStyle();

        /// <summary>
        /// Declarations the core compiler does not interpret, keyed by lower-cased property name.
        /// </summary>
        /// <remarks>
        /// Values are kept as written so that an adapter or extension can read properties the core
        /// knows nothing about, such as <c>unity-</c> or <c>vrc-</c> ones. The core never acts on
        /// them; it only carries them through to the IR and the metadata.
        /// </remarks>
        public IReadOnlyDictionary<string, string> ExtensionProperties { get; internal set; }
            = NoExtensionProperties;

        /// <summary>
        /// Gets a value indicating whether the element takes part in normal flow.
        /// </summary>
        public bool IsInFlow => Display != CssDisplay.None && Position != CssPosition.Absolute;

        /// <summary>
        /// Gets a value indicating whether the element and its subtree produce any output.
        /// </summary>
        public bool IsRendered => Display != CssDisplay.None;

        /// <summary>
        /// Looks up an extension property.
        /// </summary>
        /// <param name="property">Lower-cased property name, for example <c>unity-interactable</c>.</param>
        /// <param name="value">The raw value when present.</param>
        /// <returns><see langword="true"/> when the element declared that property.</returns>
        public bool TryGetExtensionProperty(string property, out string value)
        {
            return ExtensionProperties.TryGetValue(property, out value!);
        }
    }
}
