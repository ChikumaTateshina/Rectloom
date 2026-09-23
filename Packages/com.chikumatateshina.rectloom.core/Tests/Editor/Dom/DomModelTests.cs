#nullable enable

using System;
using System.Linq;
using NUnit.Framework;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Dom;

namespace Rectloom.Core.Tests.Dom
{
    public sealed class DomNodeTests
    {
        private static readonly SourceLocation Anywhere = new SourceLocation("Assets/UI/page.html", 1, 1);

        [Test]
        public void AppendChild_SetsParentAndIndex()
        {
            var parent = new DomElement("div", Anywhere);
            var first = new DomElement("span", Anywhere);
            var second = new DomText("text", Anywhere);

            parent.AppendChild(first);
            parent.AppendChild(second);

            Assert.That(first.Parent, Is.SameAs(parent));
            Assert.That(second.Parent, Is.SameAs(parent));
            Assert.That(first.IndexInParent, Is.EqualTo(0));
            Assert.That(second.IndexInParent, Is.EqualTo(1));
            Assert.That(parent.Children, Is.EqualTo(new DomNode[] { first, second }));
        }

        [Test]
        public void AppendChild_ReturnsTheChildForChaining()
        {
            var parent = new DomElement("div", Anywhere);
            var child = new DomElement("span", Anywhere);

            Assert.That(parent.AppendChild(child), Is.SameAs(child));
        }

        [Test]
        public void RootNode_HasNoParentAndNoIndex()
        {
            var root = new DomElement("body", Anywhere);

            Assert.That(root.Parent, Is.Null);
            Assert.That(root.IndexInParent, Is.EqualTo(-1));
        }

        [Test]
        public void AppendChild_WithNull_Throws()
        {
            var parent = new DomElement("div", Anywhere);

            Assert.Throws<ArgumentNullException>(() => parent.AppendChild(null!));
        }

        [Test]
        public void AppendChild_WhenChildAlreadyHasAParent_Throws()
        {
            var first = new DomElement("div", Anywhere);
            var second = new DomElement("div", Anywhere);
            var child = new DomElement("span", Anywhere);
            first.AppendChild(child);

            Assert.Throws<ArgumentException>(() => second.AppendChild(child));
        }

        [Test]
        public void AppendChild_WhenItWouldCreateACycle_Throws()
        {
            var grandparent = new DomElement("div", Anywhere);
            var parent = new DomElement("div", Anywhere);
            grandparent.AppendChild(parent);

            Assert.Throws<ArgumentException>(() => parent.AppendChild(grandparent));
            Assert.Throws<ArgumentException>(() => parent.AppendChild(parent));
        }

        [Test]
        public void Ancestors_WalksOutwardsNearestFirst()
        {
            var root = new DomElement("body", Anywhere);
            var middle = new DomElement("div", Anywhere);
            var leaf = new DomElement("span", Anywhere);
            root.AppendChild(middle);
            middle.AppendChild(leaf);

            Assert.That(leaf.Ancestors(), Is.EqualTo(new DomNode[] { middle, root }));
            Assert.That(root.Ancestors(), Is.Empty);
        }

        [Test]
        public void DescendantsAndSelf_WalksInDocumentOrder()
        {
            var root = new DomElement("body", Anywhere);
            var first = new DomElement("h1", Anywhere);
            var text = new DomText("Title", Anywhere);
            var second = new DomElement("p", Anywhere);
            root.AppendChild(first);
            first.AppendChild(text);
            root.AppendChild(second);

            Assert.That(
                root.DescendantsAndSelf(),
                Is.EqualTo(new DomNode[] { root, first, text, second }));
        }
    }

    public sealed class DomElementTests
    {
        private static readonly SourceLocation Anywhere = new SourceLocation("Assets/UI/page.html", 1, 1);

        private static DomElement Element(string tagName, params (string Name, string Value)[] attributes)
        {
            var element = new DomElement(tagName, Anywhere);

            foreach ((string name, string value) in attributes)
            {
                element.TryAddAttribute(new DomAttribute(name, value, Anywhere));
            }

            return element;
        }

        [Test]
        public void TagName_IsLowerCased()
        {
            Assert.That(new DomElement("DIV", Anywhere).TagName, Is.EqualTo("div"));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void Constructor_WithBlankTagName_Throws(string? tagName)
        {
            Assert.Throws<ArgumentException>(() => new DomElement(tagName!, Anywhere));
        }

        [Test]
        public void IdAttribute_BecomesId()
        {
            Assert.That(Element("button", ("id", "apply")).Id, Is.EqualTo("apply"));
        }

        [Test]
        public void IdAttribute_KeepsItsCase()
        {
            Assert.That(Element("div", ("id", "MainPanel")).Id, Is.EqualTo("MainPanel"));
        }

        [Test]
        public void IdAttribute_WhenEmpty_LeavesIdNull()
        {
            Assert.That(Element("div", ("id", "   ")).Id, Is.Null);
        }

        [Test]
        public void ElementWithoutIdAttribute_HasNullId()
        {
            Assert.That(Element("div").Id, Is.Null);
        }

        [Test]
        public void ClassAttribute_SplitsOnWhitespaceAndKeepsOrder()
        {
            DomElement element = Element("div", ("class", "panel  primary\tlarge"));

            Assert.That(element.Classes, Is.EqualTo(new[] { "panel", "primary", "large" }));
            Assert.That(element.HasClass("primary"), Is.True);
            Assert.That(element.HasClass("missing"), Is.False);
        }

        [Test]
        public void ClassAttribute_IsCaseSensitive()
        {
            DomElement element = Element("div", ("class", "Panel"));

            Assert.That(element.HasClass("Panel"), Is.True);
            Assert.That(element.HasClass("panel"), Is.False);
        }

        [Test]
        public void ClassAttribute_DropsDuplicates()
        {
            Assert.That(Element("div", ("class", "a b a")).Classes, Is.EqualTo(new[] { "a", "b" }));
        }

        [Test]
        public void Attributes_KeepSourceOrder()
        {
            DomElement element = Element("img", ("src", "a.png"), ("alt", "A"), ("width", "10"));

            Assert.That(
                element.Attributes.Select(a => a.Name),
                Is.EqualTo(new[] { "src", "alt", "width" }));
        }

        [Test]
        public void TryAddAttribute_WhenNameRepeats_KeepsFirstAndReportsFailure()
        {
            var element = new DomElement("img", Anywhere);

            Assert.That(element.TryAddAttribute(new DomAttribute("src", "first.png", Anywhere)), Is.True);
            Assert.That(element.TryAddAttribute(new DomAttribute("src", "second.png", Anywhere)), Is.False);
            Assert.That(element.GetAttribute("src"), Is.EqualTo("first.png"));
            Assert.That(element.Attributes.Count, Is.EqualTo(1));
        }

        [Test]
        public void GetAttribute_WhenAbsent_ReturnsNull()
        {
            Assert.That(Element("div").GetAttribute("src"), Is.Null);
            Assert.That(Element("div").HasAttribute("src"), Is.False);
        }

        [Test]
        public void ElementChildren_SkipsText()
        {
            var root = new DomElement("body", Anywhere);
            root.AppendChild(new DomText("ignored", Anywhere));
            var child = new DomElement("div", Anywhere);
            root.AppendChild(child);

            Assert.That(root.ElementChildren, Is.EqualTo(new[] { child }));
        }
    }

    public sealed class DomTextTests
    {
        private static readonly SourceLocation Anywhere = new SourceLocation("Assets/UI/page.html", 1, 1);

        [TestCase(" \n\t", true)]
        [TestCase("", true)]
        [TestCase(" x ", false)]
        public void IsWhitespaceOnly_DetectsIndentationText(string text, bool expected)
        {
            Assert.That(new DomText(text, Anywhere).IsWhitespaceOnly, Is.EqualTo(expected));
        }

        [Test]
        public void Constructor_WithNullText_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new DomText(null!, Anywhere));
        }
    }

    public sealed class DomDocumentTests
    {
        private static readonly SourceLocation Anywhere = new SourceLocation("Assets/UI/page.html", 1, 1);

        [Test]
        public void DocumentElement_IsTheFirstElementChild()
        {
            var document = new DomDocument("Assets/UI/page.html");
            var root = new DomElement("body", Anywhere);
            document.AppendChild(root);

            Assert.That(document.DocumentElement, Is.SameAs(root));
        }

        [Test]
        public void EmptyDocument_HasNoDocumentElement()
        {
            Assert.That(new DomDocument("Assets/UI/page.html").DocumentElement, Is.Null);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void Constructor_WithBlankFilePath_Throws(string? filePath)
        {
            Assert.Throws<ArgumentException>(() => new DomDocument(filePath!));
        }

        [Test]
        public void TryGetElementById_FindsTheFirstOccurrence()
        {
            var document = new DomDocument("Assets/UI/page.html");
            var root = new DomElement("body", Anywhere);
            document.AppendChild(root);

            var first = new DomElement("div", Anywhere);
            first.TryAddAttribute(new DomAttribute("id", "panel", Anywhere));
            var second = new DomElement("span", Anywhere);
            second.TryAddAttribute(new DomAttribute("id", "panel", Anywhere));
            root.AppendChild(first);
            root.AppendChild(second);

            Assert.That(document.TryGetElementById("panel", out DomElement found), Is.True);
            Assert.That(found, Is.SameAs(first));
            Assert.That(document.TryGetElementById("missing", out _), Is.False);
        }
    }

    public sealed class HtmlElementsTests
    {
        [TestCase("img", true)]
        [TestCase("br", true)]
        [TestCase("input", true)]
        [TestCase("div", false)]
        [TestCase("button", false)]
        public void IsVoid_MatchesTheHtmlVoidElements(string tagName, bool expected)
        {
            Assert.That(HtmlElements.IsVoid(tagName), Is.EqualTo(expected));
        }

        [TestCase("h1", 1)]
        [TestCase("h6", 6)]
        [TestCase("div", 0)]
        public void GetHeadingLevel_ReturnsZeroForNonHeadings(string tagName, int expected)
        {
            Assert.That(HtmlElements.GetHeadingLevel(tagName), Is.EqualTo(expected));
            Assert.That(HtmlElements.IsHeading(tagName), Is.EqualTo(expected != 0));
        }

        [Test]
        public void IsSupported_CoversTheMvpElementSet()
        {
            string[] mvp =
            {
                "body", "div", "span", "p", "h1", "h2", "h3", "h4", "h5", "h6", "button", "img", "br",
            };

            foreach (string tagName in mvp)
            {
                Assert.That(HtmlElements.IsSupported(tagName), Is.True, tagName);
            }

            Assert.That(HtmlElements.IsSupported("marquee"), Is.False);
        }

        [TestCase("html", true)]
        [TestCase("head", true)]
        [TestCase("body", false)]
        public void IsDocumentWrapper_MatchesHtmlAndHead(string tagName, bool expected)
        {
            Assert.That(HtmlElements.IsDocumentWrapper(tagName), Is.EqualTo(expected));
        }
    }
}
