#nullable enable

using System.Linq;
using NUnit.Framework;
using Rectloom.Core.Layout;

namespace Rectloom.Core.Tests.Layout
{
    /// <summary>
    /// Text and text-level elements share one run of formatted text instead of becoming stacked boxes.
    /// </summary>
    public sealed class InlineFormattingTests
    {
        private LayoutTestHarness _harness = null!;

        [SetUp]
        public void SetUp()
        {
            _harness = new LayoutTestHarness { UseDefaultStyleSheet = true };
        }

        private LayoutBox Paragraph(string body, string css = "")
        {
            LayoutResult root = _harness.Solve("<body>" + body + "</body>", css);
            return LayoutTestHarness.Find(root, "p").Box;
        }

        [Test]
        public void ABoldWord_StaysInTheSentence()
        {
            LayoutBox box = Paragraph("<p id=\"p\">これは<strong>重要</strong>です</p>");

            Assert.That(box.Children, Is.Empty, "one text box, not three stacked ones");
            Assert.That(box.TextContent, Is.EqualTo("これは重要です"));
            Assert.That(box.Spans!.Select(s => s.Text), Is.EqualTo(new[] { "これは", "重要", "です" }));
            Assert.That(box.Spans![1].Style.FontWeight, Is.GreaterThanOrEqualTo(700));
            Assert.That(box.Spans![0].Style.FontWeight, Is.LessThan(700));
        }

        [Test]
        public void WhiteSpace_CollapsesAcrossElementBoundaries()
        {
            LayoutBox box = Paragraph("<p id=\"p\">\n  one <em> two </em> three\n</p>");

            Assert.That(box.TextContent, Is.EqualTo("one two three"));
        }

        [Test]
        public void ALineBreak_BecomesALineBreakInTheText()
        {
            LayoutBox box = Paragraph("<p id=\"p\">first<br>second <b>bold</b></p>");

            Assert.That(box.Children, Is.Empty);
            Assert.That(box.TextContent, Is.EqualTo("first\nsecond bold"));
        }

        [Test]
        public void PlainText_HasNoSpans()
        {
            LayoutBox box = Paragraph("<p id=\"p\">just text</p>");

            Assert.That(box.TextContent, Is.EqualTo("just text"));
            Assert.That(box.Spans, Is.Null, "nothing to format, so nothing changes for existing documents");
        }

        [Test]
        public void ALink_CarriesItsTarget_AndIsBlueAndUnderlinedByDefault()
        {
            LayoutBox box = Paragraph("<p id=\"p\">see <a href=\"https://example.com/\">the site</a>.</p>");

            TextSpan link = box.Spans!.Single(s => s.Href != null);

            Assert.That(link.Text, Is.EqualTo("the site"));
            Assert.That(link.Href, Is.EqualTo("https://example.com/"));
            Assert.That(link.Style.Underline, Is.True);
            Assert.That(link.Style.Color.b, Is.GreaterThan(link.Style.Color.r), "blue");
            Assert.That(box.Spans!.Last().Text, Is.EqualTo("."));
        }

        [Test]
        public void ALinkStyledByCss_KeepsTheAuthorsStyle()
        {
            LayoutBox box = Paragraph(
                "<p id=\"p\">see <a href=\"https://example.com/\">the site</a></p>",
                "a { color: #ff0000; text-decoration: none; }");

            TextSpan link = box.Spans!.Single(s => s.Href != null);

            Assert.That(link.Style.Underline, Is.False);
            Assert.That(link.Style.Color.r, Is.GreaterThan(0.9f));
        }

        [Test]
        public void NestedElements_CombineTheirStyles()
        {
            LayoutBox box = Paragraph("<p id=\"p\">a <b>b <i>c</i></b></p>");

            TextSpan c = box.Spans!.Last();

            Assert.That(c.Text, Is.EqualTo("c"));
            Assert.That(c.Style.FontWeight, Is.GreaterThanOrEqualTo(700));
            Assert.That(c.Style.FontStyle, Is.EqualTo(Rectloom.Core.Css.Values.CssFontStyle.Italic));
        }

        [Test]
        public void AnInlineElementThatPaintsABox_StaysABox()
        {
            LayoutResult root = _harness.Solve(
                "<body><p id=\"p\">a <span id=\"tag\">b</span> c</p></body>",
                "#tag { background-color: #ffff00; }");

            LayoutBox p = LayoutTestHarness.Find(root, "p").Box;

            Assert.That(p.Children.Count, Is.EqualTo(3), "a background needs a box of its own");
        }

        [Test]
        public void TheChildrenOfAFlexContainer_StayFlexItems()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"row\" style=\"display: flex\"><span>a</span><span>b</span></div></body>");

            Assert.That(LayoutTestHarness.Find(root, "row").Box.Children.Count, Is.EqualTo(2));
        }

        [Test]
        public void TextBesideABlock_IsSplitAroundIt()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"d\">before <b>bold</b><div>block</div>after</div></body>");

            LayoutBox d = LayoutTestHarness.Find(root, "d").Box;

            Assert.That(d.Children.Count, Is.EqualTo(3));
            Assert.That(d.Children[0].TextContent, Is.EqualTo("before bold"));
            Assert.That(d.Children[0].Spans, Is.Not.Null);
            Assert.That(d.Children[2].TextContent, Is.EqualTo("after"));
        }

        [Test]
        public void ARunWithABiggerFont_IsMeasuredAtThatSize()
        {
            var measurer = new RecordingMeasurer();
            _harness.Measurer = measurer;

            Paragraph("<p id=\"p\">small <big>BIG</big></p>");

            Assert.That(measurer.SpanCalls, Is.GreaterThan(0), "measured as runs, not as plain text");
        }

        private sealed class RecordingMeasurer : ITextMeasurer
        {
            internal int SpanCalls { get; private set; }

            public TextMeasurement Measure(string text, Rectloom.Core.Css.Computed.TextStyle style, float availableWidth)
            {
                return new TextMeasurement(text.Length * 8f, style.FontSize, 1);
            }

            public TextMeasurement MeasureSpans(
                System.Collections.Generic.IReadOnlyList<TextSpan> spans,
                Rectloom.Core.Css.Computed.TextStyle style,
                float availableWidth)
            {
                SpanCalls++;
                return Measure(TextSpan.Join(spans), style, availableWidth);
            }
        }
    }
}
