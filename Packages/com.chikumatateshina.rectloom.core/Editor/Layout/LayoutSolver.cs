#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Css.Computed;
using Rectloom.Core.Css.Values;
using Rectloom.Core.Diagnostics;
using UnityEngine;

namespace Rectloom.Core.Layout
{
    /// <summary>
    /// Solves a layout tree into rectangles.
    /// </summary>
    /// <remarks>
    /// The supported model, and what it deliberately leaves out, is recorded in ADR-0003: there is
    /// no margin collapsing and no inline formatting context.
    /// <para>
    /// Every box uses <c>box-sizing: border-box</c>, so a declared width already includes padding
    /// and border. Percentages resolve against the containing block's content size, and percentages
    /// on margins and padding resolve against its <em>width</em> on all four edges, as CSS requires.
    /// </para>
    /// <para>
    /// The solver never reads a stylesheet and never touches a Unity object. Given the same tree,
    /// viewport and text measurer it always produces the same result.
    /// </para>
    /// </remarks>
    public sealed class LayoutSolver
    {
        /// <summary>
        /// Free space smaller than this is treated as none, so that rounding does not trigger a
        /// pointless second layout pass of every flex item.
        /// </summary>
        private const float FreeSpaceEpsilon = 0.01f;

        private readonly ITextMeasurer _measurer;
        private readonly IDiagnosticSink _diagnostics;

        /// <summary>
        /// Creates a solver.
        /// </summary>
        /// <param name="measurer">
        /// Text measurer, or null for <see cref="ApproximateTextMeasurer"/>. A real compile passes
        /// the backend's font-backed measurer.
        /// </param>
        /// <param name="diagnostics">Sink for layout diagnostics.</param>
        /// <exception cref="ArgumentNullException"><paramref name="diagnostics"/> is null.</exception>
        public LayoutSolver(ITextMeasurer? measurer, IDiagnosticSink diagnostics)
        {
            _measurer = measurer ?? ApproximateTextMeasurer.Instance;
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        /// <summary>
        /// Solves a layout tree.
        /// </summary>
        /// <param name="root">Root box, normally the body element.</param>
        /// <param name="viewport">
        /// Size of the containing block the root is laid out in, in logical pixels. This is the
        /// canvas reference resolution.
        /// </param>
        /// <returns>The solved tree.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is null.</exception>
        public LayoutResult Solve(LayoutBox root, Vector2 viewport)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            LayoutResult result = LayoutOne(
                root,
                viewport.x,
                viewport.y,
                definiteContainingHeight: true,
                fillWidth: true,
                overrideWidth: null,
                overrideHeight: null);

            Edges margin = ResolveEdges(root.Style.Margin, viewport.x);
            result.X += margin.Left;
            result.Y += margin.Top;
            return result;
        }

        /// <summary>
        /// Lays one box out inside a containing block, leaving its position to the caller.
        /// </summary>
        private LayoutResult LayoutOne(
            LayoutBox box,
            float containingWidth,
            float containingHeight,
            bool definiteContainingHeight,
            bool fillWidth,
            float? overrideWidth,
            float? overrideHeight)
        {
            ComputedStyle style = box.Style;
            var result = new LayoutResult(box);

            Edges margin = ResolveEdges(style.Margin, containingWidth);
            Edges padding = ResolveEdges(style.Padding, containingWidth);
            float border = Mathf.Max(0f, style.Visual.BorderWidth);

            float horizontalFrame = padding.Left + padding.Right + (border * 2f);
            float verticalFrame = padding.Top + padding.Bottom + (border * 2f);

            float width = ResolveWidth(
                box,
                containingWidth,
                fillWidth,
                overrideWidth,
                margin);

            width = Clamp(width, style.MinWidth, style.MaxWidth, containingWidth);
            float contentWidth = NonNegative(box, width - horizontalFrame, "width");

            bool definiteHeight = overrideHeight.HasValue
                || (!style.Height.IsAuto && HasDefiniteBasis(box, style.Height, definiteContainingHeight));

            float? definiteContentHeight = null;

            if (definiteHeight)
            {
                float outer = overrideHeight ?? style.Height.Resolve(containingHeight, 0f);
                outer = Clamp(outer, style.MinHeight, style.MaxHeight, containingHeight);
                definiteContentHeight = NonNegative(box, outer - verticalFrame, "height");
            }

            float childrenHeight = LayoutChildren(box, result, contentWidth, definiteContentHeight);

            float height = definiteContentHeight.HasValue
                ? definiteContentHeight.Value + verticalFrame
                : Clamp(childrenHeight + verticalFrame, style.MinHeight, style.MaxHeight, containingHeight);

            float contentHeight = NonNegative(box, height - verticalFrame, "height");

            result.Width = width;
            result.Height = height;
            result.ContentX = padding.Left + border;
            result.ContentY = padding.Top + border;
            result.ContentWidth = contentWidth;
            result.ContentHeight = contentHeight;

            LayoutAbsoluteChildren(box, result, contentWidth, contentHeight);
            ApplyRelativeOffset(result, style, containingWidth, containingHeight);

            return result;
        }

        private float ResolveWidth(
            LayoutBox box,
            float containingWidth,
            bool fillWidth,
            float? overrideWidth,
            Edges margin)
        {
            if (overrideWidth.HasValue)
            {
                return overrideWidth.Value;
            }

            ComputedStyle style = box.Style;

            if (!style.Width.IsAuto)
            {
                return style.Width.Resolve(containingWidth, containingWidth);
            }

            // An inline box hugs its content. ADR-0003 records that inline boxes are laid out as
            // shrink-to-fit blocks rather than through an inline formatting context.
            if (fillWidth && style.Display != CssDisplay.Inline)
            {
                return containingWidth - margin.Left - margin.Right;
            }

            return PreferredWidth(box, containingWidth);
        }

        /// <summary>
        /// Lays out the in-flow children and returns the height they need.
        /// </summary>
        private float LayoutChildren(
            LayoutBox box,
            LayoutResult result,
            float contentWidth,
            float? definiteContentHeight)
        {
            if (box.IsTextBox)
            {
                return MeasureText(box, contentWidth).Height;
            }

            return box.Style.Display == CssDisplay.Flex
                ? LayoutFlex(box, result, contentWidth, definiteContentHeight)
                : LayoutBlock(box, result, contentWidth, definiteContentHeight);
        }

        private float LayoutBlock(
            LayoutBox box,
            LayoutResult result,
            float contentWidth,
            float? definiteContentHeight)
        {
            float y = 0f;
            float containingHeight = definiteContentHeight ?? 0f;
            bool definiteHeight = definiteContentHeight.HasValue;

            foreach (LayoutBox child in box.Children)
            {
                if (child.Style.Position == CssPosition.Absolute)
                {
                    continue;
                }

                LayoutResult childResult = LayoutOne(
                    child,
                    contentWidth,
                    containingHeight,
                    definiteHeight,
                    fillWidth: true,
                    overrideWidth: null,
                    overrideHeight: null);

                Edges childMargin = ResolveEdges(child.Style.Margin, contentWidth);

                childResult.X += childMargin.Left + CentringOffset(child, childResult.Width, contentWidth);
                childResult.Y += y + childMargin.Top;

                // Adjacent margins are not collapsed; see ADR-0003.
                y += childMargin.Top + childResult.Height + childMargin.Bottom;

                result.AddChild(childResult);
            }

            return y;
        }

        /// <summary>
        /// Gets the extra left offset that <c>margin-left: auto; margin-right: auto</c> produces.
        /// </summary>
        /// <remarks>
        /// Two auto side margins on a box with a definite width share the free space, which is the
        /// standard way to centre a fixed-width block. Free space is only shared when there is some:
        /// a box wider than its container stays at the start rather than being pulled off it.
        /// </remarks>
        private static float CentringOffset(LayoutBox child, float childWidth, float contentWidth)
        {
            EdgeSizes margin = child.Style.Margin;

            if (!margin.Left.IsAuto || !margin.Right.IsAuto || child.Style.Width.IsAuto)
            {
                return 0f;
            }

            return Mathf.Max(0f, (contentWidth - childWidth) / 2f);
        }

        /// <summary>
        /// Lays out a flex container, returning the height its content needs.
        /// </summary>
        /// <remarks>
        /// Items are sized from <c>flex-basis</c>, grouped into lines when the container wraps, given
        /// the line's free space according to <c>flex-grow</c> and <c>flex-shrink</c>, and finally
        /// placed. Each step is a separate pass because the next one needs the previous one's answer:
        /// the line a wrapped item lands on depends on its base size, and how much it grows depends on
        /// which items share its line.
        /// </remarks>
        private float LayoutFlex(
            LayoutBox box,
            LayoutResult result,
            float contentWidth,
            float? definiteContentHeight)
        {
            FlexStyle flex = box.Style.Flex;
            bool isRow = flex.Direction == CssFlexDirection.Row;
            float containingHeight = definiteContentHeight ?? 0f;
            bool definiteHeight = definiteContentHeight.HasValue;

            float mainGap = isRow
                ? flex.ColumnGap.Resolve(contentWidth, 0f)
                : flex.RowGap.Resolve(containingHeight, 0f);

            float crossGap = isRow
                ? flex.RowGap.Resolve(containingHeight, 0f)
                : flex.ColumnGap.Resolve(contentWidth, 0f);

            var items = new List<FlexItem>();

            foreach (LayoutBox child in box.Children)
            {
                if (child.Style.Position == CssPosition.Absolute)
                {
                    continue;
                }

                items.Add(new FlexItem(child, ResolveEdges(child.Style.Margin, contentWidth)));
            }

            if (items.Count == 0)
            {
                return 0f;
            }

            var context = new FlexContext(
                this,
                flex,
                isRow,
                contentWidth,
                containingHeight,
                definiteHeight,
                mainGap,
                crossGap);

            context.MeasureItems(items);

            IReadOnlyList<FlexLine> lines = context.BuildLines(items);

            float totalCross = 0f;
            float maxLineMain = 0f;

            for (int index = 0; index < lines.Count; index++)
            {
                FlexLine line = lines[index];

                context.ResolveFlexibleLengths(items, line);
                context.MeasureLineCross(items, line, lines.Count);

                totalCross += line.CrossSize;
                maxLineMain = Mathf.Max(maxLineMain, line.MainUsed);
            }

            totalCross += crossGap * (lines.Count - 1);

            float containerCross = isRow
                ? (definiteHeight ? containingHeight : totalCross)
                : contentWidth;

            context.PlaceLines(items, lines, containerCross, totalCross);

            foreach (FlexItem item in items)
            {
                result.AddChild(item.Result!);
            }

            if (isRow)
            {
                return definiteHeight ? containingHeight : totalCross;
            }

            return definiteHeight ? containingHeight : maxLineMain;
        }

        private void LayoutAbsoluteChildren(
            LayoutBox box,
            LayoutResult result,
            float contentWidth,
            float contentHeight)
        {
            foreach (LayoutBox child in box.Children)
            {
                if (child.Style.Position != CssPosition.Absolute)
                {
                    continue;
                }

                ComputedStyle style = child.Style;
                Edges margin = ResolveEdges(style.Margin, contentWidth);

                bool hasLeft = !style.Left.IsAuto;
                bool hasRight = !style.Right.IsAuto;
                bool hasTop = !style.Top.IsAuto;
                bool hasBottom = !style.Bottom.IsAuto;

                float? width = null;

                if (style.Width.IsAuto && hasLeft && hasRight)
                {
                    width = contentWidth
                        - style.Left.Resolve(contentWidth, 0f)
                        - style.Right.Resolve(contentWidth, 0f)
                        - margin.Left
                        - margin.Right;
                }

                float? height = null;

                if (style.Height.IsAuto && hasTop && hasBottom)
                {
                    height = contentHeight
                        - style.Top.Resolve(contentHeight, 0f)
                        - style.Bottom.Resolve(contentHeight, 0f)
                        - margin.Top
                        - margin.Bottom;
                }

                LayoutResult childResult = LayoutOne(
                    child,
                    contentWidth,
                    contentHeight,
                    definiteContainingHeight: true,
                    fillWidth: false,
                    overrideWidth: width,
                    overrideHeight: height);

                childResult.X = hasLeft
                    ? style.Left.Resolve(contentWidth, 0f) + margin.Left
                    : hasRight
                        ? contentWidth - style.Right.Resolve(contentWidth, 0f) - childResult.Width - margin.Right
                        : margin.Left;

                childResult.Y = hasTop
                    ? style.Top.Resolve(contentHeight, 0f) + margin.Top
                    : hasBottom
                        ? contentHeight - style.Bottom.Resolve(contentHeight, 0f) - childResult.Height - margin.Bottom
                        : margin.Top;

                result.AddChild(childResult);
            }
        }

        private static void ApplyRelativeOffset(
            LayoutResult result,
            ComputedStyle style,
            float containingWidth,
            float containingHeight)
        {
            if (style.Position != CssPosition.Relative)
            {
                return;
            }

            // A relative box keeps the space it occupied in flow and is only painted shifted, so the
            // offset is applied after placement and nothing around it moves.
            if (!style.Left.IsAuto)
            {
                result.X += style.Left.Resolve(containingWidth, 0f);
            }
            else if (!style.Right.IsAuto)
            {
                result.X -= style.Right.Resolve(containingWidth, 0f);
            }

            if (!style.Top.IsAuto)
            {
                result.Y += style.Top.Resolve(containingHeight, 0f);
            }
            else if (!style.Bottom.IsAuto)
            {
                result.Y -= style.Bottom.Resolve(containingHeight, 0f);
            }
        }

        /// <summary>
        /// Computes the width a box would take if nothing constrained it.
        /// </summary>
        private float PreferredWidth(LayoutBox box, float containingWidth)
        {
            ComputedStyle style = box.Style;

            if (!style.Width.IsAuto)
            {
                return Clamp(
                    style.Width.Resolve(containingWidth, containingWidth),
                    style.MinWidth,
                    style.MaxWidth,
                    containingWidth);
            }

            Edges padding = ResolveEdges(style.Padding, containingWidth);
            float border = Mathf.Max(0f, style.Visual.BorderWidth);
            float frame = padding.Left + padding.Right + (border * 2f);

            float inner;

            if (box.IsTextBox)
            {
                inner = _measurer.Measure(box.TextContent!, style.Text, float.PositiveInfinity).Width;
            }
            else if (style.Display == CssDisplay.Flex && style.Flex.Direction == CssFlexDirection.Row)
            {
                inner = 0f;
                int counted = 0;

                foreach (LayoutBox child in box.Children)
                {
                    if (child.Style.Position == CssPosition.Absolute)
                    {
                        continue;
                    }

                    Edges childMargin = ResolveEdges(child.Style.Margin, containingWidth);
                    inner += PreferredWidth(child, containingWidth) + childMargin.Left + childMargin.Right;
                    counted++;
                }

                if (counted > 1)
                {
                    inner += style.Flex.ColumnGap.Resolve(containingWidth, 0f) * (counted - 1);
                }
            }
            else
            {
                inner = 0f;

                foreach (LayoutBox child in box.Children)
                {
                    if (child.Style.Position == CssPosition.Absolute)
                    {
                        continue;
                    }

                    Edges childMargin = ResolveEdges(child.Style.Margin, containingWidth);
                    inner = Mathf.Max(
                        inner,
                        PreferredWidth(child, containingWidth) + childMargin.Left + childMargin.Right);
                }
            }

            return Clamp(inner + frame, style.MinWidth, style.MaxWidth, containingWidth);
        }

        private TextMeasurement MeasureText(LayoutBox box, float contentWidth)
        {
            return _measurer.Measure(box.TextContent!, box.Style.Text, contentWidth);
        }

        /// <summary>
        /// Finds the distance from a box's border-box top down to the baseline of its first line.
        /// </summary>
        /// <returns>
        /// The offset, or <see langword="null"/> when the box contains no line of text at all, in
        /// which case CSS uses the box's own bottom edge as its baseline.
        /// </returns>
        private static float? TryGetFirstBaseline(LayoutResult result)
        {
            LayoutBox box = result.Box;

            if (box.IsTextBox)
            {
                return result.ContentY + box.Style.Text.FirstBaselineOffset;
            }

            foreach (LayoutResult child in result.Children)
            {
                if (child.Box.Style.Position == CssPosition.Absolute)
                {
                    continue;
                }

                float? inner = TryGetFirstBaseline(child);

                if (inner.HasValue)
                {
                    return result.ContentY + child.Y + inner.Value;
                }
            }

            return null;
        }

        private bool HasDefiniteBasis(LayoutBox box, CssLength length, bool definiteContainingHeight)
        {
            if (!length.IsPercent || definiteContainingHeight)
            {
                return true;
            }

            _diagnostics.Warning(
                DiagnosticCodes.Layout.InvalidPercentageContext,
                "'" + length + "' cannot be resolved because the containing block has no definite "
                    + "height. The size falls back to the content size.",
                box.Source,
                "Give the parent an explicit height, or size this box in px.");

            return false;
        }

        private float NonNegative(LayoutBox box, float value, string axis)
        {
            if (value >= 0f)
            {
                return value;
            }

            _diagnostics.Warning(
                DiagnosticCodes.Layout.NegativeCalculatedSize,
                "Computed " + axis + " is negative because padding and border exceed the declared "
                    + axis + ". It was clamped to zero.",
                box.Source,
                "Reduce the padding or border, or increase the declared " + axis + ".");

            return 0f;
        }

        private static float Clamp(float value, CssLength min, CssLength max, float basis)
        {
            if (!min.IsAuto)
            {
                value = Mathf.Max(value, min.Resolve(basis, 0f));
            }

            if (!max.IsAuto)
            {
                value = Mathf.Min(value, max.Resolve(basis, float.MaxValue));
            }

            return value;
        }

        private static Edges ResolveEdges(EdgeSizes edges, float basis)
        {
            return new Edges(
                edges.Top.Resolve(basis, 0f),
                edges.Right.Resolve(basis, 0f),
                edges.Bottom.Resolve(basis, 0f),
                edges.Left.Resolve(basis, 0f));
        }

        private readonly struct Edges
        {
            internal Edges(float top, float right, float bottom, float left)
            {
                Top = top;
                Right = right;
                Bottom = bottom;
                Left = left;
            }

            internal float Top { get; }

            internal float Right { get; }

            internal float Bottom { get; }

            internal float Left { get; }
        }

        private struct FlexItem
        {
            internal FlexItem(LayoutBox box, Edges margin)
            {
                Box = box;
                Margin = margin;
                Result = null;
                BaseMain = 0f;
                Baseline = 0f;
            }

            internal LayoutBox Box { get; }

            internal Edges Margin { get; }

            internal LayoutResult? Result { get; set; }

            /// <summary>Main size the item had before the line's free space was distributed.</summary>
            internal float BaseMain { get; set; }

            /// <summary>
            /// Distance from the item's margin-box top down to the baseline it is aligned by.
            /// </summary>
            internal float Baseline { get; set; }
        }

        /// <summary>
        /// One line of a flex container, as a contiguous run of items.
        /// </summary>
        private sealed class FlexLine
        {
            internal int Start { get; set; }

            internal int Count { get; set; }

            /// <summary>Main size the items and the gaps between them occupy.</summary>
            internal float MainUsed { get; set; }

            /// <summary>Cross size of the line.</summary>
            internal float CrossSize { get; set; }

            /// <summary>Largest baseline offset among the items aligned by baseline.</summary>
            internal float MaxBaseline { get; set; }

            /// <summary>Offset of the line along the container's cross axis.</summary>
            internal float CrossOffset { get; set; }
        }

        /// <summary>
        /// The parts of flex layout that all share one container's axes and sizes.
        /// </summary>
        /// <remarks>
        /// A class rather than a set of methods on the solver, because every step needs the same eight
        /// values and threading them through each call made the arithmetic hard to follow.
        /// </remarks>
        private sealed class FlexContext
        {
            private readonly LayoutSolver _solver;
            private readonly FlexStyle _flex;
            private readonly bool _isRow;
            private readonly float _contentWidth;
            private readonly float _containingHeight;
            private readonly bool _definiteHeight;
            private readonly float _mainGap;
            private readonly float _crossGap;

            internal FlexContext(
                LayoutSolver solver,
                FlexStyle flex,
                bool isRow,
                float contentWidth,
                float containingHeight,
                bool definiteHeight,
                float mainGap,
                float crossGap)
            {
                _solver = solver;
                _flex = flex;
                _isRow = isRow;
                _contentWidth = contentWidth;
                _containingHeight = containingHeight;
                _definiteHeight = definiteHeight;
                _mainGap = mainGap;
                _crossGap = crossGap;
            }

            /// <summary>
            /// Gets the main size available to one line, or infinity when the container does not
            /// constrain it.
            /// </summary>
            private float MainAvailable => _isRow
                ? _contentWidth
                : (_definiteHeight ? _containingHeight : float.PositiveInfinity);

            /// <summary>
            /// Lays every item out at its hypothetical main size.
            /// </summary>
            internal void MeasureItems(List<FlexItem> items)
            {
                for (int index = 0; index < items.Count; index++)
                {
                    FlexItem item = items[index];
                    bool fillCross = ShouldFillCrossBeforeMeasuring(item);

                    item.Result = Layout(
                        item,
                        ResolveBasis(item.Box),
                        fillCross ? CrossTarget(item, _contentWidth) : (float?)null,
                        fillCross);

                    item.BaseMain = MainSizeOf(item.Result);
                    items[index] = item;
                }
            }

            /// <summary>
            /// Groups items into lines, respecting <c>flex-wrap</c>.
            /// </summary>
            internal IReadOnlyList<FlexLine> BuildLines(List<FlexItem> items)
            {
                var lines = new List<FlexLine>();
                float available = MainAvailable;
                bool wraps = _flex.Wrap == CssFlexWrap.Wrap && !float.IsPositiveInfinity(available);

                var current = new FlexLine { Start = 0, Count = 0, MainUsed = 0f };

                for (int index = 0; index < items.Count; index++)
                {
                    float outer = OuterMain(items[index]);
                    float added = current.Count == 0 ? outer : outer + _mainGap;

                    // An item moves to a new line only when something is already on this one, so an
                    // item wider than the container overflows rather than sitting on an empty line.
                    if (wraps && current.Count > 0 && current.MainUsed + added > available + FreeSpaceEpsilon)
                    {
                        lines.Add(current);
                        current = new FlexLine { Start = index, Count = 1, MainUsed = outer };
                        continue;
                    }

                    current.Count++;
                    current.MainUsed += added;
                }

                lines.Add(current);
                return lines;
            }

            /// <summary>
            /// Gives a line's free space to the items that asked to grow, or takes the overflow back
            /// from the ones that may shrink.
            /// </summary>
            internal void ResolveFlexibleLengths(List<FlexItem> items, FlexLine line)
            {
                float available = MainAvailable;

                if (float.IsPositiveInfinity(available))
                {
                    return;
                }

                float free = available - line.MainUsed;

                if (Mathf.Abs(free) <= FreeSpaceEpsilon)
                {
                    return;
                }

                float[]? targets = free > 0f
                    ? Grow(items, line, free)
                    : Shrink(items, line, -free);

                if (targets == null)
                {
                    return;
                }

                line.MainUsed = _mainGap * (line.Count - 1);

                for (int offset = 0; offset < line.Count; offset++)
                {
                    int index = line.Start + offset;
                    FlexItem item = items[index];
                    bool fillCross = ShouldFillCrossBeforeMeasuring(item);

                    item.Result = Layout(
                        item,
                        targets[offset],
                        fillCross ? CrossTarget(item, _contentWidth) : (float?)null,
                        fillCross);

                    items[index] = item;
                    line.MainUsed += OuterMain(item);
                }
            }

            private float[]? Grow(List<FlexItem> items, FlexLine line, float free)
            {
                float total = 0f;

                for (int offset = 0; offset < line.Count; offset++)
                {
                    total += Mathf.Max(0f, items[line.Start + offset].Box.Style.Flex.Grow);
                }

                if (total <= 0f)
                {
                    return null;
                }

                var targets = new float[line.Count];

                for (int offset = 0; offset < line.Count; offset++)
                {
                    FlexItem item = items[line.Start + offset];
                    float grow = Mathf.Max(0f, item.Box.Style.Flex.Grow);
                    targets[offset] = item.BaseMain + (free * grow / total);
                }

                return targets;
            }

            /// <summary>
            /// Shrinks the items of an overflowing line, weighting each one by its own size.
            /// </summary>
            /// <remarks>
            /// Weighting by size as well as by <c>flex-shrink</c> is what the specification asks for,
            /// and it is what keeps a small item from being shrunk away entirely while a large one
            /// beside it gives up the same number of pixels.
            /// </remarks>
            private float[]? Shrink(List<FlexItem> items, FlexLine line, float overflow)
            {
                float weighted = 0f;

                for (int offset = 0; offset < line.Count; offset++)
                {
                    FlexItem item = items[line.Start + offset];
                    weighted += Mathf.Max(0f, item.Box.Style.Flex.Shrink) * item.BaseMain;
                }

                if (weighted <= 0f)
                {
                    return null;
                }

                var targets = new float[line.Count];

                for (int offset = 0; offset < line.Count; offset++)
                {
                    FlexItem item = items[line.Start + offset];
                    float share = Mathf.Max(0f, item.Box.Style.Flex.Shrink) * item.BaseMain / weighted;
                    targets[offset] = Mathf.Max(0f, item.BaseMain - (overflow * share));
                }

                return targets;
            }

            /// <summary>
            /// Works out a line's cross size, then stretches the items that asked to fill it.
            /// </summary>
            internal void MeasureLineCross(List<FlexItem> items, FlexLine line, int lineCount)
            {
                float maxOuter = 0f;
                float maxBaseline = 0f;
                float maxBelow = 0f;
                bool anyBaseline = false;

                for (int offset = 0; offset < line.Count; offset++)
                {
                    int index = line.Start + offset;
                    FlexItem item = items[index];
                    float outer = OuterCross(item);

                    if (_isRow && Alignment(item) == CssAlignItems.Baseline)
                    {
                        float? inner = TryGetFirstBaseline(item.Result!);

                        // A box with no line of text in it is aligned by its bottom edge, which is what
                        // makes an empty fixed-height span work as a baseline spacer.
                        item.Baseline = inner.HasValue
                            ? item.Margin.Top + inner.Value
                            : item.Margin.Top + item.Result!.Height + item.Margin.Bottom;

                        items[index] = item;
                        anyBaseline = true;
                        maxBaseline = Mathf.Max(maxBaseline, item.Baseline);
                        maxBelow = Mathf.Max(maxBelow, outer - item.Baseline);
                        continue;
                    }

                    maxOuter = Mathf.Max(maxOuter, outer);
                }

                float cross = anyBaseline ? Mathf.Max(maxOuter, maxBaseline + maxBelow) : maxOuter;

                // A container with one line and a definite cross size gives that whole size to the line,
                // so that stretching fills the container rather than only the tallest item.
                if (lineCount == 1)
                {
                    cross = _isRow
                        ? (_definiteHeight ? _containingHeight : cross)
                        : _contentWidth;
                }

                line.CrossSize = cross;
                line.MaxBaseline = maxBaseline;

                StretchItems(items, line);
            }

            /// <summary>
            /// Re-lays out the items of a line that stretch, now that its cross size is known.
            /// </summary>
            /// <remarks>
            /// Only a row needs this. A column's cross axis is its inline axis, whose size was already
            /// known before anything was measured, so its items were stretched up front.
            /// </remarks>
            private void StretchItems(List<FlexItem> items, FlexLine line)
            {
                if (!_isRow)
                {
                    return;
                }

                for (int offset = 0; offset < line.Count; offset++)
                {
                    int index = line.Start + offset;
                    FlexItem item = items[index];

                    if (Alignment(item) != CssAlignItems.Stretch || !item.Box.Style.Height.IsAuto)
                    {
                        continue;
                    }

                    float target = line.CrossSize - item.Margin.Top - item.Margin.Bottom;

                    // Only ever grown. Shrinking to the line would clip content the item had already
                    // sized itself to hold.
                    if (target <= item.Result!.Height + FreeSpaceEpsilon)
                    {
                        continue;
                    }

                    item.Result = Layout(item, MainSizeOf(item.Result), target, fillCross: false);
                    items[index] = item;
                }
            }

            /// <summary>
            /// Positions every line, and every item within its line.
            /// </summary>
            internal void PlaceLines(
                List<FlexItem> items,
                IReadOnlyList<FlexLine> lines,
                float containerCross,
                float totalCross)
            {
                float cursor = 0f;
                float between = _crossGap;
                float free = containerCross - totalCross;

                if (lines.Count > 1 && free > FreeSpaceEpsilon)
                {
                    switch (_flex.AlignContent)
                    {
                        case CssAlignContent.Center:
                            cursor = free / 2f;
                            break;
                        case CssAlignContent.End:
                            cursor = free;
                            break;
                        case CssAlignContent.SpaceBetween:
                            between = _crossGap + (free / (lines.Count - 1));
                            break;
                        case CssAlignContent.SpaceAround:
                            float around = free / lines.Count;
                            cursor = around / 2f;
                            between = _crossGap + around;
                            break;
                    }
                }
                else if (lines.Count == 1 && free > FreeSpaceEpsilon)
                {
                    switch (_flex.AlignContent)
                    {
                        case CssAlignContent.Center:
                            cursor = free / 2f;
                            break;
                        case CssAlignContent.End:
                            cursor = free;
                            break;
                    }
                }

                for (int index = 0; index < lines.Count; index++)
                {
                    FlexLine line = lines[index];
                    line.CrossOffset = cursor;

                    PlaceMainAxis(items, line);
                    PlaceCrossAxis(items, line);

                    cursor += line.CrossSize;

                    if (index < lines.Count - 1)
                    {
                        cursor += between;
                    }
                }
            }

            private void PlaceMainAxis(List<FlexItem> items, FlexLine line)
            {
                float available = MainAvailable;
                float free = float.IsPositiveInfinity(available)
                    ? 0f
                    : Mathf.Max(0f, available - line.MainUsed);

                int autoMargins = CountAutoMainMargins(items, line);
                float perAutoMargin = autoMargins > 0 ? free / autoMargins : 0f;

                float cursor = 0f;
                float between = _mainGap;

                // Auto margins swallow the free space before justify-content gets a say, which is what
                // makes "margin: 0 auto" centre a single item.
                if (autoMargins == 0)
                {
                    switch (_flex.JustifyContent)
                    {
                        case CssJustifyContent.Center:
                            cursor = free / 2f;
                            break;
                        case CssJustifyContent.End:
                            cursor = free;
                            break;
                        case CssJustifyContent.SpaceBetween:
                            // With one item there is nothing to space out, so it stays at the start.
                            between = line.Count > 1 ? _mainGap + (free / (line.Count - 1)) : _mainGap;
                            break;
                        case CssJustifyContent.SpaceAround:
                            float around = free / line.Count;
                            cursor = around / 2f;
                            between = _mainGap + around;
                            break;
                    }
                }

                for (int offset = 0; offset < line.Count; offset++)
                {
                    FlexItem item = items[line.Start + offset];
                    LayoutResult child = item.Result!;
                    EdgeSizes declared = item.Box.Style.Margin;

                    float leadingMargin = _isRow ? item.Margin.Left : item.Margin.Top;
                    float trailingMargin = _isRow ? item.Margin.Right : item.Margin.Bottom;

                    if (IsAutoLeadingMain(declared))
                    {
                        cursor += perAutoMargin;
                    }

                    if (_isRow)
                    {
                        child.X += cursor + leadingMargin;
                        cursor += leadingMargin + child.Width + trailingMargin;
                    }
                    else
                    {
                        child.Y += cursor + leadingMargin;
                        cursor += leadingMargin + child.Height + trailingMargin;
                    }

                    if (IsAutoTrailingMain(declared))
                    {
                        cursor += perAutoMargin;
                    }

                    if (offset < line.Count - 1)
                    {
                        cursor += between;
                    }
                }
            }

            private void PlaceCrossAxis(List<FlexItem> items, FlexLine line)
            {
                for (int offset = 0; offset < line.Count; offset++)
                {
                    FlexItem item = items[line.Start + offset];
                    LayoutResult child = item.Result!;
                    float outer = OuterCross(item);
                    CssAlignItems align = Alignment(item);
                    float position;

                    if (_isRow && align == CssAlignItems.Baseline)
                    {
                        position = line.MaxBaseline - item.Baseline;
                    }
                    else
                    {
                        switch (align)
                        {
                            case CssAlignItems.Center:
                                position = (line.CrossSize - outer) / 2f;
                                break;
                            case CssAlignItems.End:
                                position = line.CrossSize - outer;
                                break;
                            default:
                                position = 0f;
                                break;
                        }
                    }

                    if (_isRow)
                    {
                        child.Y += line.CrossOffset + position + item.Margin.Top;
                    }
                    else
                    {
                        child.X += line.CrossOffset + position + item.Margin.Left;
                    }
                }
            }

            private LayoutResult Layout(FlexItem item, float? mainSize, float? crossSize, bool fillCross)
            {
                float? overrideWidth = _isRow ? mainSize : crossSize;
                float? overrideHeight = _isRow ? crossSize : mainSize;

                // A stretched or explicitly sized cross axis makes the containing block definite for the
                // item, which is what lets a percentage height inside it resolve.
                bool definiteContaining = _definiteHeight || (_isRow && crossSize.HasValue);

                return _solver.LayoutOne(
                    item.Box,
                    _contentWidth,
                    _containingHeight,
                    definiteContaining,
                    fillCross && !overrideWidth.HasValue,
                    overrideWidth,
                    overrideHeight);
            }

            /// <summary>
            /// Gets the main size <c>flex-basis</c> asks for, or null to size the item from its own
            /// width or content.
            /// </summary>
            private float? ResolveBasis(LayoutBox box)
            {
                CssLength basis = box.Style.Flex.Basis;

                if (basis.IsAuto)
                {
                    return null;
                }

                if (basis.IsPercent && !_isRow && !_definiteHeight)
                {
                    // A percentage basis against an indefinite main size has nothing to resolve against.
                    return null;
                }

                return Mathf.Max(0f, basis.Resolve(_isRow ? _contentWidth : _containingHeight, 0f));
            }

            /// <summary>
            /// Gets a value indicating whether an item's cross size is known before measuring, which is
            /// only the case in a column, whose cross axis is the already-sized inline axis.
            /// </summary>
            private bool ShouldFillCrossBeforeMeasuring(FlexItem item)
            {
                return !_isRow
                    && Alignment(item) == CssAlignItems.Stretch
                    && item.Box.Style.Width.IsAuto;
            }

            private float CrossTarget(FlexItem item, float lineCross)
            {
                return _isRow
                    ? lineCross - item.Margin.Top - item.Margin.Bottom
                    : lineCross - item.Margin.Left - item.Margin.Right;
            }

            private CssAlignItems Alignment(FlexItem item)
            {
                return item.Box.Style.Flex.ResolveAlignment(_flex.AlignItems);
            }

            private float MainSizeOf(LayoutResult result) => _isRow ? result.Width : result.Height;

            private float OuterMain(FlexItem item)
            {
                return _isRow
                    ? item.Margin.Left + item.Result!.Width + item.Margin.Right
                    : item.Margin.Top + item.Result!.Height + item.Margin.Bottom;
            }

            private float OuterCross(FlexItem item)
            {
                return _isRow
                    ? item.Margin.Top + item.Result!.Height + item.Margin.Bottom
                    : item.Margin.Left + item.Result!.Width + item.Margin.Right;
            }

            private bool IsAutoLeadingMain(EdgeSizes margin) =>
                _isRow ? margin.Left.IsAuto : margin.Top.IsAuto;

            private bool IsAutoTrailingMain(EdgeSizes margin) =>
                _isRow ? margin.Right.IsAuto : margin.Bottom.IsAuto;

            private int CountAutoMainMargins(List<FlexItem> items, FlexLine line)
            {
                int count = 0;

                for (int offset = 0; offset < line.Count; offset++)
                {
                    EdgeSizes margin = items[line.Start + offset].Box.Style.Margin;

                    if (IsAutoLeadingMain(margin))
                    {
                        count++;
                    }

                    if (IsAutoTrailingMain(margin))
                    {
                        count++;
                    }
                }

                return count;
            }
        }
    }
}
