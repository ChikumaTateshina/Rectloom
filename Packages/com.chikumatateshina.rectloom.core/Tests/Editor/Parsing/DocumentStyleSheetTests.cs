#nullable enable

using System.Linq;
using NUnit.Framework;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Dom;
using Rectloom.Core.Parsing;

namespace Rectloom.Core.Tests.Parsing
{
    /// <summary>
    /// Covers what the parser does with the parts of a document that are not layout: the stylesheets
    /// it carries, the head-level elements that describe it, and the content it cannot compile.
    /// </summary>
    /// <remarks>
    /// The point of all of this is that a self-contained HTML file is the normal case. Anything left in
    /// the tree is laid out, so a stylesheet or a page title that reached it would be rendered as a
    /// paragraph of text.
    /// </remarks>
    public sealed class DocumentStyleSheetTests
    {
        private DiagnosticSink _diagnostics = null!;

        [SetUp]
        public void SetUp()
        {
            _diagnostics = new DiagnosticSink();
        }

        private DomDocument Parse(string html)
        {
            return new HtmlParser().Parse("Assets/UI/page.html", html, _diagnostics);
        }

        private static string TextOf(DomDocument document)
        {
            return string.Concat(document
                .DescendantsAndSelf()
                .OfType<DomText>()
                .Select(t => t.Text));
        }

        [Test]
        public void StyleElement_BecomesAnEmbeddedStyleSheet()
        {
            DomDocument document = Parse(
                "<html><head><style>div > p { color: red; }</style></head>"
                    + "<body><div><p>a</p></div></body></html>");

            Assert.That(document.StyleSheets.Count, Is.EqualTo(1));
            Assert.That(document.StyleSheets[0].IsEmbedded, Is.True);
            Assert.That(document.StyleSheets[0].Text, Is.EqualTo("div > p { color: red; }"));
        }

        [Test]
        public void StyleContent_IsNotLaidOut()
        {
            DomDocument document = Parse(
                "<html><head><title>A page</title><style>p { color: red; }</style></head>"
                    + "<body><p>visible</p></body></html>");

            Assert.That(TextOf(document), Is.EqualTo("visible"));
            Assert.That(document.Elements().Select(e => e.TagName), Is.EqualTo(new[] { "body", "p" }));
        }

        [Test]
        public void CssWithAngleBracketsInIt_IsNotTokenizedAsMarkup()
        {
            // A child combinator and a comparison in a comment are the two ways a stylesheet smuggles
            // '<' and '>' past a tokenizer that does not know it is reading raw text.
            DomDocument document = Parse(
                "<style>a > b { width: 1px; } /* 1 < 2 */</style><body><p>t</p></body>");

            Assert.That(document.StyleSheets[0].Text, Does.Contain("a > b"));
            Assert.That(document.StyleSheets[0].Text, Does.Contain("1 < 2"));
            Assert.That(TextOf(document), Is.EqualTo("t"));
        }

        [Test]
        public void SeveralStyleElements_AreCollectedInDocumentOrder()
        {
            DomDocument document = Parse(
                "<head><style>p { width: 1px; }</style><style>p { width: 2px; }</style></head>"
                    + "<body><p>a</p></body>");

            Assert.That(document.StyleSheets.Count, Is.EqualTo(2));
            Assert.That(document.StyleSheets[0].Text, Does.Contain("1px"));
            Assert.That(document.StyleSheets[1].Text, Does.Contain("2px"));
        }

        [Test]
        public void EmptyStyleElement_IsNotRecorded()
        {
            DomDocument document = Parse("<head><style></style></head><body><p>a</p></body>");

            Assert.That(document.StyleSheets, Is.Empty);
        }

        [Test]
        public void UnclosedStyleElement_IsReportedAndItsContentKept()
        {
            DomDocument document = Parse("<style>p { color: red; }");

            Assert.That(document.StyleSheets.Count, Is.EqualTo(1));
            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Html.UnclosedElement));
        }

        [Test]
        public void LinkedStyleSheet_IsCollected()
        {
            DomDocument document = Parse(
                "<head><link rel=\"stylesheet\" href=\"./theme.css\"></head><body><p>a</p></body>");

            Assert.That(document.StyleSheets.Count, Is.EqualTo(1));
            Assert.That(document.StyleSheets[0].IsEmbedded, Is.False);
            Assert.That(document.StyleSheets[0].Href, Is.EqualTo("./theme.css"));
        }

        [Test]
        public void LinkThatIsNotAStyleSheet_IsIgnored()
        {
            DomDocument document = Parse(
                "<head><link rel=\"icon\" href=\"./favicon.png\"><link rel=\"stylesheet\"></head>"
                    + "<body><p>a</p></body>");

            Assert.That(document.StyleSheets, Is.Empty);
        }

        [Test]
        public void ScriptContent_IsDroppedWithoutBeingParsed()
        {
            DomDocument document = Parse(
                "<body><script>if (a < b) { document.write(\"<p>x</p>\"); }</script><p>real</p></body>");

            Assert.That(TextOf(document), Is.EqualTo("real"));
            Assert.That(document.Elements().Select(e => e.TagName), Is.EqualTo(new[] { "body", "p" }));
        }

        [Test]
        public void MetaAndTitle_ProduceNoElements()
        {
            DomDocument document = Parse(
                "<html><head><meta charset=\"utf-8\"><title>T</title></head>"
                    + "<body><div>d</div></body></html>");

            Assert.That(document.Elements().Select(e => e.TagName), Is.EqualTo(new[] { "body", "div" }));
            Assert.That(TextOf(document), Is.EqualTo("d"));
        }

        [Test]
        public void SvgSubtree_IsDroppedAndReportedOnce()
        {
            DomDocument document = Parse(
                "<body><svg viewBox=\"0 0 1 1\"><g><path d=\"M0 0\"/><title>t</title></g></svg>"
                    + "<p>after</p></body>");

            Assert.That(document.Elements().Select(e => e.TagName), Is.EqualTo(new[] { "body", "p" }));
            Assert.That(TextOf(document), Is.EqualTo("after"));

            CompilerDiagnostic reported = _diagnostics.Diagnostics.Single();
            Assert.That(reported.Code, Is.EqualTo(DiagnosticCodes.Html.UnknownElement));
            Assert.That(reported.Message, Does.Contain("svg"));
        }

        [TestCase("article")]
        [TestCase("section")]
        [TestCase("header")]
        [TestCase("footer")]
        [TestCase("nav")]
        [TestCase("main")]
        [TestCase("aside")]
        [TestCase("figure")]
        [TestCase("figcaption")]
        [TestCase("ul")]
        [TestCase("ol")]
        [TestCase("li")]
        [TestCase("blockquote")]
        [TestCase("a")]
        [TestCase("strong")]
        [TestCase("em")]
        [TestCase("label")]
        [TestCase("small")]
        [TestCase("code")]
        public void SectioningAndTextElements_AreSupported(string tagName)
        {
            // A plain box or an inherited text style is genuinely all these tags mean for layout, so
            // compiling them to a container loses nothing and should not be reported.
            Assert.That(HtmlElements.IsSupported(tagName), Is.True);
        }

        [TestCase("table")]
        [TestCase("tr")]
        [TestCase("td")]
        [TestCase("input")]
        [TestCase("select")]
        [TestCase("textarea")]
        public void ElementsWithBehaviourOfTheirOwn_StayUnsupported(string tagName)
        {
            // Compiling one of these to a plain container would quietly lose what it is for, so it
            // keeps reporting HTML1003.
            Assert.That(HtmlElements.IsSupported(tagName), Is.False);
        }

    }
}
