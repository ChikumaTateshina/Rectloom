#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Rectloom.Core.Layout
{
    /// <summary>
    /// The solved rectangle of one box.
    /// </summary>
    /// <remarks>
    /// Coordinates use the layout engine's own system: the origin is top-left, x grows right and y
    /// grows down. Converting to a <c>RectTransform</c> happens in the uGUI backend and nowhere
    /// else, so the solver stays free of Unity's y-up convention.
    /// <para>
    /// <see cref="X"/> and <see cref="Y"/> place the box's border box relative to the top-left of
    /// its parent's <em>content</em> box, which is already inside the parent's padding. A child's
    /// position therefore needs no further adjustment for the parent's padding or border.
    /// </para>
    /// </remarks>
    public sealed class LayoutResult
    {
        private static readonly IReadOnlyList<LayoutResult> NoChildren = Array.Empty<LayoutResult>();

        private readonly List<LayoutResult> _children = new List<LayoutResult>();

        /// <summary>
        /// Creates a result for a box.
        /// </summary>
        /// <param name="box">The box this result belongs to.</param>
        /// <exception cref="ArgumentNullException"><paramref name="box"/> is null.</exception>
        public LayoutResult(LayoutBox box)
        {
            Box = box ?? throw new ArgumentNullException(nameof(box));
        }

        /// <summary>The box this result was solved for.</summary>
        public LayoutBox Box { get; }

        /// <summary>Left edge of the border box, relative to the parent's content box.</summary>
        public float X { get; internal set; }

        /// <summary>Top edge of the border box, relative to the parent's content box.</summary>
        public float Y { get; internal set; }

        /// <summary>Width of the border box, which includes padding and border.</summary>
        public float Width { get; internal set; }

        /// <summary>Height of the border box, which includes padding and border.</summary>
        public float Height { get; internal set; }

        /// <summary>Left edge of the content box, relative to this box's own top-left corner.</summary>
        public float ContentX { get; internal set; }

        /// <summary>Top edge of the content box, relative to this box's own top-left corner.</summary>
        public float ContentY { get; internal set; }

        /// <summary>Width available to children, after padding and border.</summary>
        public float ContentWidth { get; internal set; }

        /// <summary>Height available to children, after padding and border.</summary>
        public float ContentHeight { get; internal set; }

        /// <summary>Child results, in source order.</summary>
        public IReadOnlyList<LayoutResult> Children => _children.Count == 0 ? NoChildren : _children;

        /// <summary>Right edge of the border box, relative to the parent's content box.</summary>
        public float Right => X + Width;

        /// <summary>Bottom edge of the border box, relative to the parent's content box.</summary>
        public float Bottom => Y + Height;

        /// <summary>
        /// Appends a child result.
        /// </summary>
        /// <param name="child">The result to append.</param>
        /// <exception cref="ArgumentNullException"><paramref name="child"/> is null.</exception>
        public void AddChild(LayoutResult child)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            _children.Add(child);
        }

        /// <summary>
        /// Walks this result and its descendants in document order.
        /// </summary>
        /// <returns>This result followed by every descendant, depth first.</returns>
        public IEnumerable<LayoutResult> DescendantsAndSelf()
        {
            yield return this;

            foreach (LayoutResult child in _children)
            {
                foreach (LayoutResult descendant in child.DescendantsAndSelf())
                {
                    yield return descendant;
                }
            }
        }

        /// <summary>
        /// Returns a deterministic <c>box x,y w x h</c> representation, used by golden tests.
        /// </summary>
        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1},{2} {3}x{4}",
                Box,
                Round(X),
                Round(Y),
                Round(Width),
                Round(Height));
        }

        private static string Round(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
