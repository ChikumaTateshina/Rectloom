#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rectloom.Core.Css;
using Rectloom.Core.Css.Ast;
using Rectloom.Core.Css.Cascade;
using Rectloom.Core.Css.Computed;
using Rectloom.Core.Css.Parsing;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Dom;
using Rectloom.Core.Layout;
using Rectloom.Core.Parsing;
using UnityEngine;

namespace Rectloom.Core.Tests.Layout
{
    /// <summary>
    /// Runs HTML and CSS through the whole front end and hands back solved rectangles.
    /// </summary>
    /// <remarks>
    /// Layout tests assert on numbers, so the harness uses a fixed-width text measurer by default
    /// rather than the approximate one. A test that cares about a box's size should not also depend
    /// on a heuristic for how wide the letter 'W' is.
    /// </remarks>
    internal sealed class LayoutTestHarness
    {
        internal const string HtmlPath = "Assets/UI/page.html";
        internal const string CssPath = "Assets/UI/page.css";

        internal LayoutTestHarness()
        {
            Diagnostics = new DiagnosticSink();
            Measurer = new FixedTextMeasurer();
            Viewport = new Vector2(1000f, 800f);
            UseDefaultStyleSheet = false;
        }

        internal DiagnosticSink Diagnostics { get; }

        internal ITextMeasurer Measurer { get; set; }

        internal Vector2 Viewport { get; set; }

        /// <summary>
        /// Whether the built-in stylesheet takes part. Off by default, so a layout assertion is not
        /// silently changed by a tweak to default margins.
        /// </summary>
        internal bool UseDefaultStyleSheet { get; set; }

        internal LayoutResult Solve(string html, string css = "")
        {
            DomDocument document = new HtmlParser().Parse(HtmlPath, html, Diagnostics);

            var authorSheets = new List<CssStyleSheet>();

            if (!string.IsNullOrEmpty(css))
            {
                authorSheets.Add(CssParser.Parse(CssPath, css, Diagnostics));
            }

            IReadOnlyList<CssStyleSheet>? userAgent = UseDefaultStyleSheet
                ? new[] { DefaultStyleSheet.Get() }
                : null;

            ComputedStyleTree styles = ComputedStyleTree.Build(
                document,
                new CascadeResolver(userAgent, authorSheets),
                new ComputedStyleBuilder(),
                Diagnostics);

            LayoutBox? root = LayoutTreeBuilder.Build(document, styles);
            Assert.That(root, Is.Not.Null, "the document produced no layout tree");

            return new LayoutSolver(Measurer, Diagnostics).Solve(root!, Viewport);
        }

        /// <summary>Finds the solved rectangle of the element with the given id.</summary>
        internal static LayoutResult Find(LayoutResult root, string id)
        {
            foreach (LayoutResult result in root.DescendantsAndSelf())
            {
                if (result.Box.Element?.Id == id)
                {
                    return result;
                }
            }

            Assert.Fail("no layout result for id '" + id + "'");
            return null!;
        }

        /// <summary>Asserts a solved rectangle, in the layout engine's top-left coordinate system.</summary>
        internal static void AssertRect(LayoutResult result, float x, float y, float width, float height)
        {
            Assert.That(result.X, Is.EqualTo(x).Within(0.01f), result.Box + " x");
            Assert.That(result.Y, Is.EqualTo(y).Within(0.01f), result.Box + " y");
            Assert.That(result.Width, Is.EqualTo(width).Within(0.01f), result.Box + " width");
            Assert.That(result.Height, Is.EqualTo(height).Within(0.01f), result.Box + " height");
        }

        /// <summary>Renders the whole tree as one deterministic string, for golden comparisons.</summary>
        internal static string Describe(LayoutResult root)
        {
            return string.Join("\n", Lines(root, 0));
        }

        private static IEnumerable<string> Lines(LayoutResult result, int depth)
        {
            yield return new string(' ', depth * 2) + result;

            foreach (LayoutResult child in result.Children)
            {
                foreach (string line in Lines(child, depth + 1))
                {
                    yield return line;
                }
            }
        }
    }

    /// <summary>
    /// A text measurer with fixed character metrics, so layout assertions stay exact.
    /// </summary>
    /// <remarks>
    /// Every character is half the font size wide and every line is exactly one line height tall.
    /// Wrapping breaks on character count rather than on words, which keeps the arithmetic in the
    /// tests obvious.
    /// </remarks>
    internal sealed class FixedTextMeasurer : ITextMeasurer
    {
        internal float AdvanceRatio { get; set; } = 0.5f;

        public TextMeasurement Measure(string text, TextStyle style, float availableWidth)
        {
            if (string.IsNullOrEmpty(text))
            {
                return TextMeasurement.Empty;
            }

            float advance = style.FontSize * AdvanceRatio;
            float lineHeight = style.FontSize * style.LineHeight;
            float unwrapped = text.Length * advance;

            if (!style.WrapsText || float.IsPositiveInfinity(availableWidth) || availableWidth <= 0f)
            {
                return new TextMeasurement(unwrapped, lineHeight, 1);
            }

            int perLine = Mathf.Max(1, Mathf.FloorToInt(availableWidth / advance));
            int lines = Mathf.CeilToInt((float)text.Length / perLine);
            float width = Mathf.Min(unwrapped, perLine * advance);

            return new TextMeasurement(width, lines * lineHeight, lines);
        }
    }
}
