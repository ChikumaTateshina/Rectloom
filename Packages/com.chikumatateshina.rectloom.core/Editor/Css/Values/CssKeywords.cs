#nullable enable

namespace Rectloom.Core.Css.Values
{
    /// <summary>
    /// Supported values of the <c>display</c> property.
    /// </summary>
    public enum CssDisplay
    {
        /// <summary>The element stacks vertically and fills the available inline size.</summary>
        Block = 0,

        /// <summary>The element lays its children out along a flex axis.</summary>
        Flex = 1,

        /// <summary>The element flows with surrounding text rather than forming its own box.</summary>
        Inline = 2,

        /// <summary>The element and its subtree produce no output at all.</summary>
        None = 3,
    }

    /// <summary>
    /// Supported values of the <c>position</c> property.
    /// </summary>
    public enum CssPosition
    {
        /// <summary>The element takes part in normal flow and ignores the offset properties.</summary>
        Static = 0,

        /// <summary>The element takes part in normal flow, then shifts by its offsets.</summary>
        Relative = 1,

        /// <summary>The element leaves normal flow and is placed inside its parent's content box.</summary>
        Absolute = 2,
    }

    /// <summary>
    /// Supported values of the <c>flex-direction</c> property.
    /// </summary>
    public enum CssFlexDirection
    {
        /// <summary>Children are laid out left to right; the main axis is horizontal.</summary>
        Row = 0,

        /// <summary>Children are laid out top to bottom; the main axis is vertical.</summary>
        Column = 1,
    }

    /// <summary>
    /// Supported values of the <c>justify-content</c> property, which distributes free space along
    /// the main axis.
    /// </summary>
    public enum CssJustifyContent
    {
        /// <summary>Children sit at the start of the main axis.</summary>
        Start = 0,

        /// <summary>Children sit centred on the main axis.</summary>
        Center = 1,

        /// <summary>Children sit at the end of the main axis.</summary>
        End = 2,

        /// <summary>Free space is split evenly between children, with none at the edges.</summary>
        SpaceBetween = 3,

        /// <summary>Each child gets equal space around it, so edge gaps are half the inner gaps.</summary>
        SpaceAround = 4,
    }

    /// <summary>
    /// Supported values of the <c>align-items</c> property, which places children on the cross axis.
    /// </summary>
    public enum CssAlignItems
    {
        /// <summary>Children sit at the start of the cross axis.</summary>
        Start = 0,

        /// <summary>Children sit centred on the cross axis.</summary>
        Center = 1,

        /// <summary>Children sit at the end of the cross axis.</summary>
        End = 2,

        /// <summary>Children without an explicit cross size fill the container's content size.</summary>
        Stretch = 3,
    }

    /// <summary>
    /// Supported values of the <c>text-align</c> property.
    /// </summary>
    public enum CssTextAlign
    {
        /// <summary>Text is aligned to the left edge.</summary>
        Left = 0,

        /// <summary>Text is centred.</summary>
        Center = 1,

        /// <summary>Text is aligned to the right edge.</summary>
        Right = 2,

        /// <summary>Text is spread to both edges.</summary>
        Justify = 3,
    }

    /// <summary>
    /// Supported values of the <c>font-style</c> property.
    /// </summary>
    public enum CssFontStyle
    {
        /// <summary>Upright text.</summary>
        Normal = 0,

        /// <summary>Slanted text.</summary>
        Italic = 1,
    }

    /// <summary>
    /// Supported values of the <c>white-space</c> property.
    /// </summary>
    public enum CssWhiteSpace
    {
        /// <summary>Runs of whitespace collapse and lines wrap.</summary>
        Normal = 0,

        /// <summary>Runs of whitespace collapse and lines never wrap.</summary>
        NoWrap = 1,

        /// <summary>Whitespace and line breaks are kept exactly as authored, and lines never wrap.</summary>
        Pre = 2,

        /// <summary>Whitespace and line breaks are kept exactly as authored, and lines wrap.</summary>
        PreWrap = 3,
    }
}
