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
    /// no margin collapsing, no inline formatting context and no <c>flex-grow</c>.
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

                childResult.X += childMargin.Left;
                childResult.Y += y + childMargin.Top;

                // Adjacent margins are not collapsed; see ADR-0003.
                y += childMargin.Top + childResult.Height + childMargin.Bottom;

                result.AddChild(childResult);
            }

            return y;
        }

        private float LayoutFlex(
            LayoutBox box,
            LayoutResult result,
            float contentWidth,
            float? definiteContentHeight)
        {
            FlexStyle flex = box.Style.Flex;
            bool isRow = flex.Direction == CssFlexDirection.Row;
            float gap = flex.Gap.Resolve(isRow ? contentWidth : definiteContentHeight ?? 0f, 0f);

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

            float containingHeight = definiteContentHeight ?? 0f;
            bool definiteHeight = definiteContentHeight.HasValue;

            // A column stretches its children across the inline axis, which is the one whose size is
            // already known, so stretching can be decided before measuring.
            bool stretchCross = flex.AlignItems == CssAlignItems.Stretch;

            for (int index = 0; index < items.Count; index++)
            {
                FlexItem item = items[index];
                bool fillWidth = !isRow && stretchCross && item.Box.Style.Width.IsAuto;

                item.Result = LayoutOne(
                    item.Box,
                    contentWidth,
                    containingHeight,
                    definiteHeight,
                    fillWidth,
                    overrideWidth: fillWidth
                        ? contentWidth - item.Margin.Left - item.Margin.Right
                        : (float?)null,
                    overrideHeight: null);

                items[index] = item;
            }

            float mainAvailable = isRow ? contentWidth : containingHeight;
            float mainUsed = gap * (items.Count - 1);

            foreach (FlexItem item in items)
            {
                mainUsed += isRow
                    ? item.Margin.Left + item.Result!.Width + item.Margin.Right
                    : item.Margin.Top + item.Result!.Height + item.Margin.Bottom;
            }

            float crossSize = definiteContentHeight ?? 0f;

            if (isRow)
            {
                if (!definiteContentHeight.HasValue)
                {
                    crossSize = 0f;

                    foreach (FlexItem item in items)
                    {
                        crossSize = Mathf.Max(
                            crossSize,
                            item.Margin.Top + item.Result!.Height + item.Margin.Bottom);
                    }
                }

                // Stretch is applied once the cross size is known, which for an auto-height row is
                // the tallest item rather than the container's own height.
                if (stretchCross)
                {
                    StretchRowItems(items, contentWidth, crossSize);
                }
            }
            else
            {
                mainAvailable = definiteContentHeight ?? mainUsed;
            }

            float free = mainAvailable - mainUsed;
            PlaceMainAxis(items, isRow, gap, free, box.Style.Flex.JustifyContent);
            PlaceCrossAxis(items, isRow, crossSize, contentWidth, box.Style.Flex.AlignItems);

            foreach (FlexItem item in items)
            {
                result.AddChild(item.Result!);
            }

            return isRow ? crossSize : Mathf.Max(mainUsed, definiteContentHeight ?? mainUsed);
        }

        private void StretchRowItems(List<FlexItem> items, float contentWidth, float crossSize)
        {
            for (int index = 0; index < items.Count; index++)
            {
                FlexItem item = items[index];

                if (!item.Box.Style.Height.IsAuto)
                {
                    continue;
                }

                float target = crossSize - item.Margin.Top - item.Margin.Bottom;

                if (target <= item.Result!.Height)
                {
                    continue;
                }

                item.Result = LayoutOne(
                    item.Box,
                    contentWidth,
                    crossSize,
                    definiteContainingHeight: true,
                    fillWidth: false,
                    overrideWidth: item.Result.Width,
                    overrideHeight: target);

                items[index] = item;
            }
        }

        private static void PlaceMainAxis(
            List<FlexItem> items,
            bool isRow,
            float gap,
            float free,
            CssJustifyContent justify)
        {
            float cursor = 0f;
            float between = gap;

            switch (justify)
            {
                case CssJustifyContent.Center:
                    cursor = free / 2f;
                    break;
                case CssJustifyContent.End:
                    cursor = free;
                    break;
                case CssJustifyContent.SpaceBetween:
                    // With one item there is nothing to space out, so it stays at the start.
                    between = items.Count > 1 ? gap + (free / (items.Count - 1)) : gap;
                    break;
                case CssJustifyContent.SpaceAround:
                    float around = free / items.Count;
                    cursor = around / 2f;
                    between = gap + around;
                    break;
            }

            for (int index = 0; index < items.Count; index++)
            {
                FlexItem item = items[index];
                LayoutResult child = item.Result!;

                if (isRow)
                {
                    child.X += cursor + item.Margin.Left;
                    cursor += item.Margin.Left + child.Width + item.Margin.Right;
                }
                else
                {
                    child.Y += cursor + item.Margin.Top;
                    cursor += item.Margin.Top + child.Height + item.Margin.Bottom;
                }

                if (index < items.Count - 1)
                {
                    cursor += between;
                }
            }
        }

        private static void PlaceCrossAxis(
            List<FlexItem> items,
            bool isRow,
            float rowCrossSize,
            float columnCrossSize,
            CssAlignItems align)
        {
            float crossSize = isRow ? rowCrossSize : columnCrossSize;

            foreach (FlexItem item in items)
            {
                LayoutResult child = item.Result!;
                float outer = isRow
                    ? item.Margin.Top + child.Height + item.Margin.Bottom
                    : item.Margin.Left + child.Width + item.Margin.Right;

                float offset;

                switch (align)
                {
                    case CssAlignItems.Center:
                        offset = (crossSize - outer) / 2f;
                        break;
                    case CssAlignItems.End:
                        offset = crossSize - outer;
                        break;
                    default:
                        offset = 0f;
                        break;
                }

                if (isRow)
                {
                    child.Y += offset + item.Margin.Top;
                }
                else
                {
                    child.X += offset + item.Margin.Left;
                }
            }
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
                    inner += style.Flex.Gap.Resolve(containingWidth, 0f) * (counted - 1);
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
            }

            internal LayoutBox Box { get; }

            internal Edges Margin { get; }

            internal LayoutResult? Result { get; set; }
        }
    }
}
