#nullable enable

using System.Linq;
using NUnit.Framework;
using Rectloom.Core.Css.Parsing;
using Rectloom.Core.Css.Selectors;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Dom;
using Rectloom.Core.Parsing;

namespace Rectloom.Core.Tests.Css
{
    /// <summary>
    /// <c>[attribute]</c> selectors, which match on values fixed in the source.
    /// </summary>
    public sealed class AttributeSelectorTests
    {
        private DiagnosticSink _diagnostics = null!;

        [SetUp]
        public void SetUp()
        {
            _diagnostics = new DiagnosticSink();
        }

        private CssSelector Parse(string text)
        {
            Assert.That(
                CssSelectorParser.TryParse(text, SourceLocation.None, _diagnostics, out CssSelector selector),
                Is.True,
                text + "\n" + string.Join("\n", _diagnostics.Diagnostics));
            return selector;
        }

        private static DomElement Element(string markup)
        {
            DomDocument document = new HtmlParser().Parse("Assets/UI/page.html", "<body>" + markup + "</body>", new DiagnosticSink());
            return document.DescendantsAndSelf().OfType<DomElement>().First(e => e.TagName != "body");
        }

        private bool Matches(string selector, string markup)
        {
            return Parse(selector).Parts.Last().Matches(Element(markup));
        }

        [TestCase("[type]", "<input type=\"range\">", true)]
        [TestCase("[type]", "<input>", false)]
        [TestCase("[type=range]", "<input type=\"range\">", true)]
        [TestCase("[type=\"range\"]", "<input type=\"range\">", true)]
        [TestCase("[type='range']", "<input type=\"range\">", true)]
        [TestCase("[type=\"range\"]", "<input type=\"checkbox\">", false)]
        [TestCase("[type=\"RANGE\"]", "<input type=\"range\">", false)]
        [TestCase("[type=\"RANGE\" i]", "<input type=\"range\">", true)]
        [TestCase("[data-tags~=\"b\"]", "<div data-tags=\"a b c\"></div>", true)]
        [TestCase("[data-tags~=\"b c\"]", "<div data-tags=\"a b c\"></div>", false)]
        [TestCase("[href^=\"https\"]", "<a href=\"https://x\"></a>", true)]
        [TestCase("[href$=\".png\"]", "<a href=\"a.png\"></a>", true)]
        [TestCase("[href*=\"example\"]", "<a href=\"https://example.com\"></a>", true)]
        [TestCase("[lang|=\"ja\"]", "<p lang=\"ja-JP\"></p>", true)]
        [TestCase("[lang|=\"ja\"]", "<p lang=\"jax\"></p>", false)]
        [TestCase("[ type = \"range\" ]", "<input type=\"range\">", true)]
        [TestCase("[DATA-X]", "<div data-x=\"1\"></div>", true)]
        public void Matching(string selector, string markup, bool expected)
        {
            Assert.That(Matches(selector, markup), Is.EqualTo(expected), selector + " on " + markup);
        }

        [Test]
        public void CombinesWithTagClassAndId()
        {
            CssCompoundSelector part = Parse("input.big#volume[type=\"range\"]").Parts.Single();

            Assert.That(part.TagName, Is.EqualTo("input"));
            Assert.That(part.Attributes.Single().Name, Is.EqualTo("type"));
            Assert.That(part.Matches(Element("<input id=\"volume\" class=\"big\" type=\"range\">")), Is.True);
            Assert.That(part.Matches(Element("<input id=\"volume\" class=\"big\" type=\"checkbox\">")), Is.False);
        }

        [Test]
        public void CountsLikeAClass()
        {
            Assert.That(
                Parse("input[type=\"range\"]").Specificity.Value,
                Is.EqualTo(Parse("input.range").Specificity.Value));
        }

        [Test]
        public void WorksAsADescendant()
        {
            CssSelector selector = Parse("form [disabled]");

            Assert.That(selector.Parts.Count, Is.EqualTo(2));
            Assert.That(selector.Parts[1].Attributes.Single().Operator, Is.EqualTo(CssAttributeOperator.Exists));
        }

        [TestCase("[type")]
        [TestCase("[=range]")]
        [TestCase("[type=\"range]")]
        [TestCase("[type%=range]")]
        public void MalformedSelectors_AreReportedAndSkipped(string text)
        {
            Assert.That(
                CssSelectorParser.TryParse(text, SourceLocation.None, _diagnostics, out _),
                Is.False);
            Assert.That(_diagnostics.Diagnostics.Single().Severity, Is.EqualTo(DiagnosticSeverity.Warning));
        }

        [Test]
        public void ReadsBackAsCss()
        {
            Assert.That(Parse("input[type=range]").Parts.Single().ToString(), Is.EqualTo("input[type=\"range\"]"));
        }
    }
}
