#nullable enable

using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using Rectloom.Core.Css.Ast;
using Rectloom.Core.Css.Computed;
using Rectloom.Core.Css.Values;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Layout;

namespace Rectloom.Core.Tests.Layout
{
    public sealed class AbsoluteLayoutTests
    {
        private const string Markup =
            "<body><div id=\"outer\"><div id=\"flow\"></div><div id=\"abs\"></div></div></body>";

        private LayoutTestHarness _harness = null!;

        [SetUp]
        public void SetUp()
        {
            _harness = new LayoutTestHarness();
        }

        private LayoutResult Solve(string absoluteRules)
        {
            return _harness.Solve(
                Markup,
                "#outer { width: 400px; height: 300px; } #flow { height: 20px; }"
                    + " #abs { position: absolute; " + absoluteRules + " }");
        }

        [Test]
        public void AbsoluteBox_IsRemovedFromNormalFlow()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"outer\"><div id=\"abs\"></div><div id=\"after\"></div></div></body>",
                "#outer { width: 400px; } #abs { position: absolute; height: 500px; }"
                    + " #after { height: 20px; }");

            Assert.That(LayoutTestHarness.Find(root, "after").Y, Is.Zero);
            Assert.That(
                LayoutTestHarness.Find(root, "outer").Height,
                Is.EqualTo(20f),
                "the absolute box does not contribute to its parent's height");
        }

        [Test]
        public void TopAndLeft_PositionInsideTheParentContentBox()
        {
            LayoutResult root = Solve("top: 30px; left: 40px; width: 100px; height: 50px;");

            LayoutTestHarness.AssertRect(LayoutTestHarness.Find(root, "abs"), 40f, 30f, 100f, 50f);
        }

        [Test]
        public void RightAndBottom_MeasureFromTheOppositeEdges()
        {
            LayoutResult root = Solve("right: 10px; bottom: 20px; width: 100px; height: 50px;");

            LayoutTestHarness.AssertRect(LayoutTestHarness.Find(root, "abs"), 290f, 230f, 100f, 50f);
        }

        [Test]
        public void OppositeOffsets_DetermineTheSize()
        {
            LayoutResult root = Solve("top: 10px; bottom: 10px; left: 20px; right: 20px;");

            LayoutTestHarness.AssertRect(LayoutTestHarness.Find(root, "abs"), 20f, 10f, 360f, 280f);
        }

        [Test]
        public void PercentOffsets_ResolveAgainstTheContentBox()
        {
            LayoutResult root = Solve("top: 10%; left: 25%; width: 50px; height: 50px;");

            LayoutTestHarness.AssertRect(LayoutTestHarness.Find(root, "abs"), 100f, 30f, 50f, 50f);
        }

        [Test]
        public void WithoutOffsets_TheBoxSitsAtTheContentOrigin()
        {
            LayoutResult root = Solve("width: 60px; height: 60px;");

            LayoutTestHarness.AssertRect(LayoutTestHarness.Find(root, "abs"), 0f, 0f, 60f, 60f);
        }

        [Test]
        public void ParentPadding_ShiftsTheContainingBlock()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"outer\"><div id=\"abs\"></div></div></body>",
                "#outer { width: 400px; height: 300px; padding: 25px; }"
                    + " #abs { position: absolute; right: 0; bottom: 0; width: 50px; height: 50px; }");

            LayoutResult abs = LayoutTestHarness.Find(root, "abs");

            Assert.That(abs.X, Is.EqualTo(300f), "the content box is 350 wide");
            Assert.That(abs.Y, Is.EqualTo(200f), "the content box is 250 tall");
        }

        [Test]
        public void AbsoluteChildOfAFlexContainer_IsAlsoOutOfFlow()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"row\"><div id=\"a\"></div><div id=\"abs\"></div></div></body>",
                "#row { display: flex; width: 300px; height: 100px; }"
                    + " #a { width: 50px; height: 20px; }"
                    + " #abs { position: absolute; left: 200px; top: 10px; width: 30px; height: 30px; }");

            Assert.That(LayoutTestHarness.Find(root, "a").X, Is.Zero);
            LayoutTestHarness.AssertRect(LayoutTestHarness.Find(root, "abs"), 200f, 10f, 30f, 30f);
        }
    }

    public sealed class TextLayoutTests
    {
        private LayoutTestHarness _harness = null!;

        [SetUp]
        public void SetUp()
        {
            _harness = new LayoutTestHarness();
        }

        [Test]
        public void TextBox_TakesOneLineHeightWhenItFits()
        {
            LayoutResult root = _harness.Solve(
                "<body><p id=\"a\">hello</p></body>",
                "#a { font-size: 20px; line-height: 1.5; }");

            Assert.That(LayoutTestHarness.Find(root, "a").Height, Is.EqualTo(30f));
        }

        [Test]
        public void TextBox_WrapsWithinItsWidth()
        {
            LayoutResult root = _harness.Solve(
                "<body><p id=\"a\">abcdefghij</p></body>",
                "#a { width: 25px; font-size: 10px; line-height: 1; }");

            Assert.That(
                LayoutTestHarness.Find(root, "a").Height,
                Is.EqualTo(20f),
                "ten characters at 5px each, five per 25px line");
        }

        [Test]
        public void NoWrap_KeepsTextOnOneLine()
        {
            LayoutResult root = _harness.Solve(
                "<body><p id=\"a\">abcdefghij</p></body>",
                "#a { width: 25px; font-size: 10px; line-height: 1; white-space: nowrap; }");

            Assert.That(LayoutTestHarness.Find(root, "a").Height, Is.EqualTo(10f));
        }

        [Test]
        public void ElementWithOnlyText_RendersItDirectly()
        {
            LayoutResult root = _harness.Solve("<body><p id=\"a\">  hello   world  </p></body>");
            LayoutResult paragraph = LayoutTestHarness.Find(root, "a");

            Assert.That(paragraph.Box.TextContent, Is.EqualTo("hello world"));
            Assert.That(paragraph.Children, Is.Empty);
        }

        [Test]
        public void TextBesideElementChildren_BecomesAnAnonymousBox()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"a\">lead<span id=\"s\">x</span></div></body>",
                "#a { font-size: 10px; line-height: 1; }");

            LayoutResult container = LayoutTestHarness.Find(root, "a");

            Assert.That(container.Children.Count, Is.EqualTo(2));
            Assert.That(container.Children[0].Box.IsAnonymous, Is.True);
            Assert.That(container.Children[0].Box.TextContent, Is.EqualTo("lead"));
            Assert.That(container.Children[1].Box.Element!.Id, Is.EqualTo("s"));
        }

        [Test]
        public void IndentationBetweenBlocks_ProducesNoBox()
        {
            LayoutResult root = _harness.Solve(
                "<body>\n  <div id=\"a\"></div>\n  <div id=\"b\"></div>\n</body>",
                "div { height: 10px; }");

            Assert.That(root.Children.Count, Is.EqualTo(2));
        }

        [Test]
        public void InlineDisplay_ShrinksToItsContent()
        {
            LayoutResult root = _harness.Solve(
                "<body><span id=\"a\">abcd</span></body>",
                "#a { display: inline; font-size: 10px; }");

            Assert.That(LayoutTestHarness.Find(root, "a").Width, Is.EqualTo(20f));
        }

        [Test]
        public void BlockText_FillsItsContainerAndWraps()
        {
            LayoutResult root = _harness.Solve(
                "<body><p id=\"a\">abcdefghij</p></body>",
                "body { width: 30px; } #a { font-size: 10px; line-height: 1; }");

            LayoutResult paragraph = LayoutTestHarness.Find(root, "a");

            Assert.That(paragraph.Width, Is.EqualTo(30f), "block-level text fills its container");
            Assert.That(paragraph.Height, Is.EqualTo(20f));
        }

        [Test]
        public void TextInheritsStyleIntoAnonymousBoxes()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"a\">text<div id=\"b\"></div></div></body>",
                "#a { font-size: 40px; line-height: 1; }");

            LayoutResult anonymous = LayoutTestHarness.Find(root, "a").Children[0];

            Assert.That(anonymous.Box.Style.Text.FontSize, Is.EqualTo(40f));
            Assert.That(anonymous.Height, Is.EqualTo(40f));
        }
    }

    public sealed class TextCollapseTests
    {
        [TestCase("  hello   world  ", "hello world")]
        [TestCase("\n  a\n  b\n", "a b")]
        [TestCase("single", "single")]
        [TestCase("   ", "")]
        [TestCase("", "")]
        public void NormalWhiteSpace_CollapsesAndTrims(string input, string expected)
        {
            Assert.That(TextCollapse.Collapse(input, CssWhiteSpace.Normal), Is.EqualTo(expected));
        }

        [TestCase(CssWhiteSpace.Pre)]
        [TestCase(CssWhiteSpace.PreWrap)]
        public void PreservingModes_KeepTheTextExactly(CssWhiteSpace whiteSpace)
        {
            const string input = "  a\n  b  ";

            Assert.That(TextCollapse.Collapse(input, whiteSpace), Is.EqualTo(input));
        }

        [Test]
        public void NoWrap_StillCollapses()
        {
            Assert.That(TextCollapse.Collapse("  a   b ", CssWhiteSpace.NoWrap), Is.EqualTo("a b"));
        }
    }

    public sealed class ApproximateTextMeasurerTests
    {
        /// <summary>
        /// Builds a text style the way the compiler does, through declarations, because computed
        /// values are read-only outside the core assembly.
        /// </summary>
        private static TextStyle Style(float fontSize = 20f, float lineHeight = 1.2f)
        {
            var declarations = new List<CssDeclaration>
            {
                Declaration("font-size", Number(fontSize) + "px"),
                Declaration("line-height", Number(lineHeight)),
            };

            return new ComputedStyleBuilder()
                .Build(declarations, null, new DiagnosticSink())
                .Text;
        }

        private static CssDeclaration Declaration(string property, string value)
        {
            return new CssDeclaration(property, value, false, SourceLocation.None);
        }

        private static string Number(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        [Test]
        public void EmptyText_MeasuresNothing()
        {
            Assert.That(
                ApproximateTextMeasurer.Instance.Measure(string.Empty, Style(), 100f).Width,
                Is.Zero);
        }

        [Test]
        public void FullWidthCharacters_AreTwiceAsWideAsLatin()
        {
            TextStyle style = Style(20f);

            float latin = ApproximateTextMeasurer.Instance
                .Measure("aa", style, float.PositiveInfinity).Width;
            float japanese = ApproximateTextMeasurer.Instance
                .Measure("設定", style, float.PositiveInfinity).Width;

            Assert.That(japanese, Is.EqualTo(latin * 2f).Within(0.01f));
        }

        [Test]
        public void Measuring_IsDeterministic()
        {
            TextStyle style = Style();

            TextMeasurement first = ApproximateTextMeasurer.Instance.Measure("hello world", style, 60f);
            TextMeasurement second = ApproximateTextMeasurer.Instance.Measure("hello world", style, 60f);

            Assert.That(second.Width, Is.EqualTo(first.Width));
            Assert.That(second.Height, Is.EqualTo(first.Height));
            Assert.That(second.LineCount, Is.EqualTo(first.LineCount));
        }

        [Test]
        public void LongTextWraps_AndShortTextDoesNot()
        {
            TextStyle style = Style(10f);

            Assert.That(
                ApproximateTextMeasurer.Instance.Measure("hello world", style, 200f).LineCount,
                Is.EqualTo(1));
            Assert.That(
                ApproximateTextMeasurer.Instance.Measure("hello world", style, 30f).LineCount,
                Is.GreaterThan(1));
        }

        [Test]
        public void ExplicitLineBreaks_AddLines()
        {
            TextStyle style = Style(10f);

            Assert.That(
                ApproximateTextMeasurer.Instance
                    .Measure("a\nb\nc", style, float.PositiveInfinity)
                    .LineCount,
                Is.EqualTo(3));
        }

        [Test]
        public void Height_IsLineCountTimesLineHeight()
        {
            TextStyle style = Style(10f, 2f);

            TextMeasurement measurement = ApproximateTextMeasurer.Instance
                .Measure("a\nb", style, float.PositiveInfinity);

            Assert.That(measurement.Height, Is.EqualTo(40f));
        }
    }
}
