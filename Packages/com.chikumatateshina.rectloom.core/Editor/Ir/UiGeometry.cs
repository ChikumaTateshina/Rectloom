#nullable enable

using System.Globalization;

namespace Rectloom.Core.Ir
{
    /// <summary>
    /// What kind of UI object a node compiles to.
    /// </summary>
    /// <remarks>
    /// The kind is decided once, when the IR is built from the styled layout tree. A backend reads
    /// it and never re-examines the element name, so adding an element type means teaching the IR
    /// builder about it rather than every backend.
    /// </remarks>
    public enum UiNodeKind
    {
        /// <summary>The document root, which becomes the canvas root object.</summary>
        Root = 0,

        /// <summary>A plain box with no content of its own.</summary>
        Container = 1,

        /// <summary>A box that renders text.</summary>
        Text = 2,

        /// <summary>A box that renders an image.</summary>
        Image = 3,

        /// <summary>A clickable box with a label.</summary>
        Button = 4,

        /// <summary>A text input field. Reserved for a later phase.</summary>
        Input = 5,

        /// <summary>A toggle. Reserved for a later phase.</summary>
        Toggle = 6,

        /// <summary>A slider. Reserved for a later phase.</summary>
        Slider = 7,

        /// <summary>A scrollable viewport. Reserved for a later phase.</summary>
        ScrollView = 8,

        /// <summary>A node whose object is built entirely by an extension.</summary>
        Custom = 9,
    }

    /// <summary>
    /// The baked rectangle of a node.
    /// </summary>
    /// <remarks>
    /// Coordinates are the layout engine's: the origin is top-left and y grows down.
    /// <see cref="X"/> and <see cref="Y"/> are relative to the parent's content box, so a child
    /// needs no further adjustment for the parent's padding or border. The backend converts to
    /// Unity's y-up convention, and it is the only place that does.
    /// </remarks>
    public readonly struct UiRect
    {
        /// <summary>
        /// Creates a rectangle.
        /// </summary>
        /// <param name="x">Left edge of the border box, relative to the parent's content box.</param>
        /// <param name="y">Top edge of the border box, relative to the parent's content box.</param>
        /// <param name="width">Width of the border box, including padding and border.</param>
        /// <param name="height">Height of the border box, including padding and border.</param>
        /// <param name="contentX">Left edge of the content box, relative to this box's corner.</param>
        /// <param name="contentY">Top edge of the content box, relative to this box's corner.</param>
        /// <param name="contentWidth">Width available to children.</param>
        /// <param name="contentHeight">Height available to children.</param>
        public UiRect(
            float x,
            float y,
            float width,
            float height,
            float contentX,
            float contentY,
            float contentWidth,
            float contentHeight)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            ContentX = contentX;
            ContentY = contentY;
            ContentWidth = contentWidth;
            ContentHeight = contentHeight;
        }

        /// <summary>Left edge of the border box, relative to the parent's content box.</summary>
        public float X { get; }

        /// <summary>Top edge of the border box, relative to the parent's content box.</summary>
        public float Y { get; }

        /// <summary>Width of the border box, including padding and border.</summary>
        public float Width { get; }

        /// <summary>Height of the border box, including padding and border.</summary>
        public float Height { get; }

        /// <summary>Left edge of the content box, relative to this box's own corner.</summary>
        public float ContentX { get; }

        /// <summary>Top edge of the content box, relative to this box's own corner.</summary>
        public float ContentY { get; }

        /// <summary>Width available to children, after padding and border.</summary>
        public float ContentWidth { get; }

        /// <summary>Height available to children, after padding and border.</summary>
        public float ContentHeight { get; }

        /// <summary>
        /// Gets a value indicating whether the content box is inset from the border box at all.
        /// </summary>
        /// <remarks>
        /// When it is not, a label child can simply stretch to its parent instead of carrying
        /// offsets.
        /// </remarks>
        public bool HasInset =>
            ContentX != 0f
            || ContentY != 0f
            || ContentWidth != Width
            || ContentHeight != Height;

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1} {2}x{3}",
                X,
                Y,
                Width,
                Height);
        }
    }
}
