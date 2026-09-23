#nullable enable

using System.Linq;
using NUnit.Framework;
using Rectloom.Core.Css.Cascade;
using Rectloom.Core.Css.Parsing;
using Rectloom.Core.Css.Selectors;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Dom;
using Rectloom.Core.Parsing;

namespace Rectloom.Core.Tests.Css
{
    public sealed class CssSelectorParserTests
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
                text);
            return selector;
        }

        [Test]
        public void Universal_MatchesAnything()
        {
            CssSelector selector = Parse("*");
            CssCompoundSelector part = selector.Parts.Single();

            Assert.That(part.TagName, Is.Null);
            Assert.That(part.Id, Is.Null);
            Assert.That(part.Classes, Is.Empty);
            Assert.That(selector.Specificity.Value, Is.Zero);
        }

        [Test]
        public void TagSelector_IsLowerCased()
        {
            Assert.That(Parse("DIV").Parts.Single().TagName, Is.EqualTo("div"));
        }

        [Test]
        public void ClassAndIdKeepTheirCase()
        {
            CssCompoundSelector part = Parse("#Panel.Primary").Parts.Single();

            Assert.That(part.Id, Is.EqualTo("Panel"));
            Assert.That(part.Classes, Is.EqualTo(new[] { "Primary" }));
        }

        [Test]
        public void Compound_CombinesTagAndClass()
        {
            CssCompoundSelector part = Parse("button.primary.large").Parts.Single();

            Assert.That(part.TagName, Is.EqualTo("button"));
            Assert.That(part.Classes, Is.EqualTo(new[] { "primary", "large" }));
        }

        [Test]
        public void DescendantCombinator_IsWhitespace()
        {
            CssSelector selector = Parse("div  span");

            Assert.That(selector.Parts.Count, Is.EqualTo(2));
            Assert.That(selector.Parts[0].Combinator, Is.EqualTo(CssCombinator.None));
            Assert.That(selector.Parts[1].Combinator, Is.EqualTo(CssCombinator.Descendant));
        }

        [Test]
        public void ChildCombinator_IsReadWithOrWithoutSpaces()
        {
            foreach (string text in new[] { "div > span", "div>span" })
            {
                CssSelector selector = Parse(text);

                Assert.That(selector.Parts.Count, Is.EqualTo(2), text);
                Assert.That(selector.Parts[1].Combinator, Is.EqualTo(CssCombinator.Child), text);
            }
        }

        [Test]
        public void NonAsciiNames_AreSupported()
        {
            CssCompoundSelector part = Parse(".日本語").Parts.Single();

            Assert.That(part.Classes, Is.EqualTo(new[] { "日本語" }));
        }

        [TestCase("*", 0, 0, 0)]
        [TestCase("div", 0, 0, 1)]
        [TestCase(".panel", 0, 1, 0)]
        [TestCase("#panel", 1, 0, 0)]
        [TestCase("button.primary", 0, 1, 1)]
        [TestCase("div span", 0, 0, 2)]
        [TestCase("#a .b c", 1, 1, 1)]
        public void Specificity_CountsIdsClassesAndElements(string text, int ids, int classes, int elements)
        {
            CssSpecificity specificity = Parse(text).Specificity;

            Assert.That(specificity.Ids, Is.EqualTo(ids));
            Assert.That(specificity.Classes, Is.EqualTo(classes));
            Assert.That(specificity.Elements, Is.EqualTo(elements));
            Assert.That(specificity.Value, Is.EqualTo((ids * 100) + (classes * 10) + elements));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("div:hover")]
        [TestCase("input[type=text]")]
        [TestCase("a + b")]
        [TestCase("a ~ b")]
        [TestCase("> div")]
        [TestCase("div >")]
        [TestCase("#")]
        [TestCase(".")]
        public void UnsupportedSyntax_IsReportedAndRejected(string text)
        {
            Assert.That(
                CssSelectorParser.TryParse(text, SourceLocation.None, _diagnostics, out _),
                Is.False,
                text);
            Assert.That(
                _diagnostics.Diagnostics.Last().Code,
                Is.EqualTo(DiagnosticCodes.Css.UnknownSelectorSyntax));
        }
    }

    public sealed class SelectorMatcherTests
    {
        private DomElement Root(string html)
        {
            DomDocument document = new HtmlParser().Parse(
                "Assets/UI/page.html",
                html,
                new DiagnosticSink());
            return document.DocumentElement!;
        }

        private static CssSelector Selector(string text)
        {
            Assert.That(
                CssSelectorParser.TryParse(text, SourceLocation.None, new DiagnosticSink(), out CssSelector selector),
                Is.True,
                text);
            return selector;
        }

        private static DomElement Find(DomElement root, string id)
        {
            foreach (DomNode node in root.DescendantsAndSelf())
            {
                if (node is DomElement element && element.Id == id)
                {
                    return element;
                }
            }

            Assert.Fail("no element with id " + id);
            return null!;
        }

        [Test]
        public void TagClassAndIdSelectors_Match()
        {
            DomElement root = Root("<body><button id=\"apply\" class=\"primary large\"></button></body>");
            DomElement button = Find(root, "apply");

            Assert.That(SelectorMatcher.Matches(Selector("*"), button), Is.True);
            Assert.That(SelectorMatcher.Matches(Selector("button"), button), Is.True);
            Assert.That(SelectorMatcher.Matches(Selector(".primary"), button), Is.True);
            Assert.That(SelectorMatcher.Matches(Selector("#apply"), button), Is.True);
            Assert.That(SelectorMatcher.Matches(Selector("button.primary.large"), button), Is.True);
            Assert.That(SelectorMatcher.Matches(Selector("div"), button), Is.False);
            Assert.That(SelectorMatcher.Matches(Selector(".missing"), button), Is.False);
            Assert.That(SelectorMatcher.Matches(Selector("button.primary.missing"), button), Is.False);
        }

        [Test]
        public void ClassMatching_IsCaseSensitive()
        {
            DomElement root = Root("<div id=\"x\" class=\"Panel\"></div>");

            Assert.That(SelectorMatcher.Matches(Selector(".Panel"), Find(root, "x")), Is.True);
            Assert.That(SelectorMatcher.Matches(Selector(".panel"), Find(root, "x")), Is.False);
        }

        [Test]
        public void DescendantCombinator_MatchesAtAnyDepth()
        {
            DomElement root = Root("<body><div class=\"outer\"><div><span id=\"leaf\"></span></div></div></body>");
            DomElement leaf = Find(root, "leaf");

            Assert.That(SelectorMatcher.Matches(Selector(".outer span"), leaf), Is.True);
            Assert.That(SelectorMatcher.Matches(Selector("body span"), leaf), Is.True);
            Assert.That(SelectorMatcher.Matches(Selector("p span"), leaf), Is.False);
        }

        [Test]
        public void ChildCombinator_RequiresADirectParent()
        {
            DomElement root = Root("<body><div class=\"outer\"><div><span id=\"leaf\"></span></div></div></body>");
            DomElement leaf = Find(root, "leaf");

            Assert.That(SelectorMatcher.Matches(Selector(".outer > span"), leaf), Is.False);
            Assert.That(SelectorMatcher.Matches(Selector("div > span"), leaf), Is.True);
        }

        [Test]
        public void DescendantMatching_BacktracksAcrossACombinator()
        {
            // The nearest b is the inner one, whose parent is not the a. Committing to it would
            // wrongly reject the selector, so the matcher has to try the outer b as well.
            DomElement root = Root(
                "<body><div class=\"a\"><div class=\"b\"><div class=\"b\"><span id=\"leaf\"></span></div></div></div></body>");

            Assert.That(SelectorMatcher.Matches(Selector(".a > .b span"), Find(root, "leaf")), Is.True);
        }

        [Test]
        public void SelectorLongerThanTheTree_DoesNotMatch()
        {
            DomElement root = Root("<body><span id=\"leaf\"></span></body>");

            Assert.That(SelectorMatcher.Matches(Selector("div div div span"), Find(root, "leaf")), Is.False);
        }

        [Test]
        public void NullElement_DoesNotMatch()
        {
            Assert.That(SelectorMatcher.Matches(Selector("div"), null), Is.False);
        }
    }
}
