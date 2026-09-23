#nullable enable

using Rectloom.Core.Css.Values;

namespace Rectloom.Core.Css.Computed
{
    /// <summary>
    /// The flex container properties of an element.
    /// </summary>
    /// <remarks>
    /// These values only take effect when <see cref="ComputedStyle.Display"/> is
    /// <see cref="CssDisplay.Flex"/>. They are still computed for every element, because an author
    /// can set <c>flex-direction</c> on a rule that also switches <c>display</c> elsewhere in the
    /// cascade, and losing the value would make the result depend on declaration order.
    /// <para>
    /// The per-item properties (<c>flex-grow</c>, <c>flex-shrink</c>, <c>flex-basis</c>,
    /// <c>align-self</c> and <c>flex-wrap</c>) are out of scope for version 1.0.
    /// </para>
    /// </remarks>
    public sealed class FlexStyle
    {
        /// <summary>Which axis children are laid out along. Initial value is row.</summary>
        public CssFlexDirection Direction { get; internal set; } = CssFlexDirection.Row;

        /// <summary>How free space is distributed along the main axis. Initial value is start.</summary>
        public CssJustifyContent JustifyContent { get; internal set; } = CssJustifyContent.Start;

        /// <summary>How children are placed on the cross axis. Initial value is stretch.</summary>
        public CssAlignItems AlignItems { get; internal set; } = CssAlignItems.Stretch;

        /// <summary>Space between adjacent children along the main axis. Initial value is zero.</summary>
        public CssLength Gap { get; internal set; } = CssLength.Zero;

        /// <summary>
        /// Creates a copy of these values.
        /// </summary>
        /// <returns>An independent copy.</returns>
        public FlexStyle Clone()
        {
            return new FlexStyle
            {
                Direction = Direction,
                JustifyContent = JustifyContent,
                AlignItems = AlignItems,
                Gap = Gap,
            };
        }
    }
}
