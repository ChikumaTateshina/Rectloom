#nullable enable

using Rectloom.Core.Css.Values;

namespace Rectloom.Core.Css.Computed
{
    /// <summary>
    /// The flex properties of an element, both as a container and as an item of its parent.
    /// </summary>
    /// <remarks>
    /// The container properties only take effect when <see cref="ComputedStyle.Display"/> is
    /// <see cref="CssDisplay.Flex"/>, and the item properties only when the element's parent is a
    /// flex container. They are still computed for every element, because an author can set
    /// <c>flex-direction</c> on a rule that also switches <c>display</c> elsewhere in the cascade, and
    /// losing the value would make the result depend on declaration order.
    /// </remarks>
    public sealed class FlexStyle
    {
        /// <summary>Which axis children are laid out along. Initial value is row.</summary>
        public CssFlexDirection Direction { get; internal set; } = CssFlexDirection.Row;

        /// <summary>Whether items that do not fit move onto a new line. Initial value is nowrap.</summary>
        public CssFlexWrap Wrap { get; internal set; } = CssFlexWrap.NoWrap;

        /// <summary>How free space is distributed along the main axis. Initial value is start.</summary>
        public CssJustifyContent JustifyContent { get; internal set; } = CssJustifyContent.Start;

        /// <summary>How children are placed on the cross axis. Initial value is stretch.</summary>
        public CssAlignItems AlignItems { get; internal set; } = CssAlignItems.Stretch;

        /// <summary>
        /// How the lines of a wrapped container are distributed along the cross axis. Initial value is
        /// stretch, which this layout model treats as start.
        /// </summary>
        public CssAlignContent AlignContent { get; internal set; } = CssAlignContent.Stretch;

        /// <summary>Space between adjacent columns. Initial value is zero.</summary>
        public CssLength ColumnGap { get; internal set; } = CssLength.Zero;

        /// <summary>Space between adjacent rows. Initial value is zero.</summary>
        public CssLength RowGap { get; internal set; } = CssLength.Zero;

        /// <summary>
        /// How much of the line's free space this item takes, relative to its siblings. Initial value
        /// is zero, meaning the item does not grow.
        /// </summary>
        public float Grow { get; internal set; }

        /// <summary>
        /// How much this item shrinks when the line overflows, relative to its siblings. Initial value
        /// is 1, so an item shrinks unless it is told not to.
        /// </summary>
        public float Shrink { get; internal set; } = 1f;

        /// <summary>
        /// The item's size along the main axis before free space is distributed. Initial value is
        /// auto, which falls back to the item's own <c>width</c> or <c>height</c>, and then to its
        /// content size.
        /// </summary>
        public CssLength Basis { get; internal set; } = CssLength.Auto;

        /// <summary>
        /// This item's own cross-axis alignment, or auto to follow the container's
        /// <see cref="AlignItems"/>. Initial value is auto.
        /// </summary>
        public CssAlignSelf AlignSelf { get; internal set; } = CssAlignSelf.Auto;

        /// <summary>
        /// Resolves the cross-axis alignment of this item inside a container.
        /// </summary>
        /// <param name="containerAlignItems">The container's <c>align-items</c>.</param>
        /// <returns>The alignment to place this item with.</returns>
        public CssAlignItems ResolveAlignment(CssAlignItems containerAlignItems)
        {
            switch (AlignSelf)
            {
                case CssAlignSelf.Start:
                    return CssAlignItems.Start;
                case CssAlignSelf.Center:
                    return CssAlignItems.Center;
                case CssAlignSelf.End:
                    return CssAlignItems.End;
                case CssAlignSelf.Stretch:
                    return CssAlignItems.Stretch;
                case CssAlignSelf.Baseline:
                    return CssAlignItems.Baseline;
                default:
                    return containerAlignItems;
            }
        }

        /// <summary>
        /// Creates a copy of these values.
        /// </summary>
        /// <returns>An independent copy.</returns>
        public FlexStyle Clone()
        {
            return new FlexStyle
            {
                Direction = Direction,
                Wrap = Wrap,
                JustifyContent = JustifyContent,
                AlignItems = AlignItems,
                AlignContent = AlignContent,
                ColumnGap = ColumnGap,
                RowGap = RowGap,
                Grow = Grow,
                Shrink = Shrink,
                Basis = Basis,
                AlignSelf = AlignSelf,
            };
        }
    }
}
