#nullable enable

using System.Linq;
using NUnit.Framework;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Dom;
using Rectloom.Core.Parsing;

namespace Rectloom.Core.Tests.Parsing
{
    /// <summary>
    /// Covers the parser cases required by docs/09 section 2, plus recovery from malformed input.
    /// </summary>
    public sealed class HtmlParserTests
    {
        private const string Path = "Assets/UI/page.html";

        private DiagnosticSink _diagnostics = null!;

        [SetUp]
        public void SetUp()
        {
            _diagnostics = new DiagnosticSink();
        }

        private DomDocument Parse(string source)
        {
            return new HtmlParser().Parse(Path, source, _diagnostics);
        }

        private DomElement Root(string source)
        {
            DomElement? root = Parse(source).DocumentElement;
            Assert.That(root, Is.Not.Null, "document has no root element");
            return root!;
        }

        private static DomElement ElementAt(DomNode parent, int index)
        {
            return (DomElement)parent.Children[index];
        }

        [Test]
        public void BodyTag_BecomesTheRoot()
        {
            DomElement root = Root("<body><div></div></body>");

            Assert.That(root.TagName, Is.EqualTo("body"));
            Assert.That(root.Children.Count, Is.EqualTo(1));
            Assert.That(_diagnostics.HasErrors, Is.False);
        }

        [Test]
        public void FragmentWithoutBody_GetsASynthesisedRoot()
        {
            DomElement root = Root("<div id=\"panel\"></div>");

            Assert.That(root.TagName, Is.EqualTo("body"));
            Assert.That(ElementAt(root, 0).Id, Is.EqualTo("panel"));
        }

        [Test]
        public void HtmlAndHeadWrappers_AreUnwrapped()
        {
            DomElement root = Root("<html><head></head><body><p>x</p></body></html>");

            Assert.That(root.TagName, Is.EqualTo("body"));
            Assert.That(ElementAt(root, 0).TagName, Is.EqualTo("p"));
        }

        [Test]
        public void NestedElements_KeepTheirStructure()
        {
            DomElement root = Root("<body><div><span><p></p></span></div></body>");

            DomElement div = ElementAt(root, 0);
            DomElement span = ElementAt(div, 0);

            Assert.That(div.TagName, Is.EqualTo("div"));
            Assert.That(span.TagName, Is.EqualTo("span"));
            Assert.That(ElementAt(span, 0).TagName, Is.EqualTo("p"));
            Assert.That(span.Parent, Is.SameAs(div));
        }

        [Test]
        public void TextBetweenTags_BecomesATextNode()
        {
            DomElement root = Root("<body><h1>Settings</h1></body>");
            DomElement heading = ElementAt(root, 0);

            Assert.That(heading.Children.Count, Is.EqualTo(1));
            Assert.That(((DomText)heading.Children[0]).Text, Is.EqualTo("Settings"));
        }

        [Test]
        public void IndentationBetweenBlockElements_IsKeptAsWhitespaceText()
        {
            DomElement root = Root("<body>\n  <div></div>\n</body>");

            Assert.That(root.Children.OfType<DomText>().All(t => t.IsWhitespaceOnly), Is.True);
            Assert.That(root.ElementChildren.Count(), Is.EqualTo(1));
        }

        [Test]
        public void IndentationBeforeTheRoot_IsDropped()
        {
            DomDocument document = Parse("\n\n  <body><div></div></body>\n");

            Assert.That(document.Children.Count, Is.EqualTo(1));
            Assert.That(document.DocumentElement!.TagName, Is.EqualTo("body"));
        }

        [Test]
        public void MultipleClasses_AreSplit()
        {
            DomElement panel = ElementAt(Root("<div class=\"panel primary large\"></div>"), 0);

            Assert.That(panel.Classes, Is.EqualTo(new[] { "panel", "primary", "large" }));
        }

        [Test]
        public void Attributes_AreReadInAllQuotingStyles()
        {
            DomElement element = ElementAt(Root("<img src=\"a.png\" alt='An image' width=64 hidden>"), 0);

            Assert.That(element.GetAttribute("src"), Is.EqualTo("a.png"));
            Assert.That(element.GetAttribute("alt"), Is.EqualTo("An image"));
            Assert.That(element.GetAttribute("width"), Is.EqualTo("64"));
            Assert.That(element.GetAttribute("hidden"), Is.EqualTo(string.Empty));
        }

        [Test]
        public void TagAndAttributeNames_AreLowerCased()
        {
            DomElement element = ElementAt(Root("<DIV CLASS=\"Panel\" DATA-Role=\"x\"></DIV>"), 0);

            Assert.That(element.TagName, Is.EqualTo("div"));
            Assert.That(element.HasAttribute("class"), Is.True);
            Assert.That(element.HasAttribute("data-role"), Is.True);
            Assert.That(element.Classes, Is.EqualTo(new[] { "Panel" }), "class values keep their case");
        }

        [Test]
        public void ComponentAttributes_ArePreserved()
        {
            DomElement element = ElementAt(
                Root("<button component=\"Example.CustomButton\" component.speed=\"1.5\">Apply</button>"),
                0);

            Assert.That(element.GetAttribute("component"), Is.EqualTo("Example.CustomButton"));
            Assert.That(element.GetAttribute("component.speed"), Is.EqualTo("1.5"));
        }

        [Test]
        public void VoidElement_NeedsNoEndTag()
        {
            DomElement root = Root("<body><img src=\"a.png\"><br><p>after</p></body>");

            Assert.That(root.ElementChildren.Select(e => e.TagName), Is.EqualTo(new[] { "img", "br", "p" }));
            Assert.That(ElementAt(root, 0).Children, Is.Empty);
            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void SelfClosingSyntax_IsAccepted()
        {
            DomElement root = Root("<body><img src=\"a.png\" /><div/><p>after</p></body>");

            Assert.That(root.ElementChildren.Select(e => e.TagName), Is.EqualTo(new[] { "img", "div", "p" }));
            Assert.That(ElementAt(root, 1).Children, Is.Empty);
        }

        [Test]
        public void EndTagForVoidElement_IsReportedAndIgnored()
        {
            DomElement root = Root("<body><br></br></body>");

            Assert.That(root.ElementChildren.Count(), Is.EqualTo(1));
            Assert.That(
                _diagnostics.Diagnostics.Select(d => d.Code),
                Is.EqualTo(new[] { DiagnosticCodes.Html.UnexpectedClosingTag }));
        }

        [Test]
        public void CommentsAndDoctype_AreSkipped()
        {
            DomElement root = Root("<!DOCTYPE html><!-- a comment --><body><div></div><!-- x --></body>");

            Assert.That(root.ElementChildren.Count(), Is.EqualTo(1));
            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void CharacterReferences_AreDecoded()
        {
            DomElement root = Root("<body><p title=\"a &amp; b\">5 &lt; 6 &#33; &#x3042;</p></body>");
            DomElement paragraph = ElementAt(root, 0);

            Assert.That(paragraph.GetAttribute("title"), Is.EqualTo("a & b"));
            Assert.That(((DomText)paragraph.Children[0]).Text, Is.EqualTo("5 < 6 ! あ"));
        }

        [Test]
        public void UnknownCharacterReference_IsLeftAlone()
        {
            DomElement paragraph = ElementAt(Root("<p>Tom &notareference; Jerry</p>"), 0);

            Assert.That(((DomText)paragraph.Children[0]).Text, Is.EqualTo("Tom &notareference; Jerry"));
        }

        [Test]
        public void JapaneseAndUnicodeContent_SurvivesParsing()
        {
            DomElement root = Root("<body><h1 id=\"見出し\" class=\"日本語\">設定 \U0001F600</h1></body>");
            DomElement heading = ElementAt(root, 0);

            Assert.That(heading.Id, Is.EqualTo("見出し"));
            Assert.That(heading.Classes, Is.EqualTo(new[] { "日本語" }));
            Assert.That(((DomText)heading.Children[0]).Text, Is.EqualTo("設定 \U0001F600"));
        }

        [Test]
        public void UnknownElement_StillParsesAsAnElement()
        {
            DomElement element = ElementAt(Root("<body><marquee>x</marquee></body>"), 0);

            Assert.That(element.TagName, Is.EqualTo("marquee"));
            Assert.That(
                _diagnostics.Diagnostics,
                Is.Empty,
                "deciding which elements are supported belongs to the IR stage, not the parser");
        }

        [Test]
        public void StrayAngleBracket_IsKeptAsText()
        {
            DomElement paragraph = ElementAt(Root("<p>a < b</p>"), 0);
            string text = string.Concat(paragraph.Children.OfType<DomText>().Select(t => t.Text));

            Assert.That(text, Is.EqualTo("a < b"));
        }

        [Test]
        public void DuplicateAttribute_KeepsTheFirstValueAndWarns()
        {
            DomElement element = ElementAt(Root("<img src=\"first.png\" src=\"second.png\">"), 0);

            Assert.That(element.GetAttribute("src"), Is.EqualTo("first.png"));
            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Html.DuplicateAttribute));
        }

        [Test]
        public void DuplicateId_IsAnError()
        {
            Parse("<body><div id=\"same\"></div><button id=\"same\"></button></body>");

            CompilerDiagnostic diagnostic = _diagnostics.Diagnostics.Single();
            Assert.That(diagnostic.Code, Is.EqualTo(DiagnosticCodes.Html.DuplicateId));
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(_diagnostics.HasErrors, Is.True);
        }

        [Test]
        public void UniqueIds_ProduceNoDiagnostic()
        {
            Parse("<body><div id=\"a\"></div><div id=\"b\"></div></body>");

            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void MismatchedEndTag_ClosesTheInnerElementImplicitly()
        {
            DomElement root = Root("<body><div><span></div></body>");

            Assert.That(ElementAt(ElementAt(root, 0), 0).TagName, Is.EqualTo("span"));
            Assert.That(
                _diagnostics.Diagnostics.Select(d => d.Code),
                Is.EqualTo(new[] { DiagnosticCodes.Html.UnclosedElement }));
        }

        [Test]
        public void EndTagWithNoMatchingStart_IsReportedAndSkipped()
        {
            DomElement root = Root("<body><div></div></span></body>");

            Assert.That(root.ElementChildren.Count(), Is.EqualTo(1));
            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Html.UnexpectedClosingTag));
        }

        [Test]
        public void ElementLeftOpenAtEndOfFile_IsClosedAndReported()
        {
            DomElement root = Root("<body><div><p>text");

            Assert.That(ElementAt(ElementAt(root, 0), 0).TagName, Is.EqualTo("p"));
            Assert.That(
                _diagnostics.Diagnostics.Select(d => d.Code).Distinct(),
                Is.EqualTo(new[] { DiagnosticCodes.Html.UnclosedElement }));
            Assert.That(_diagnostics.Count, Is.EqualTo(3), "body, div and p are all left open");
        }

        [Test]
        public void UnterminatedTag_IsReported()
        {
            Parse("<body><div class=\"x\"");

            Assert.That(
                _diagnostics.Diagnostics.Any(d => d.Code == DiagnosticCodes.Html.MalformedTag),
                Is.True);
        }

        [Test]
        public void ContentAfterTheRootWasClosed_StaysInsideTheRoot()
        {
            DomElement root = Root("<body><div></div></body><p>stray</p>");

            Assert.That(root.ElementChildren.Select(e => e.TagName), Is.EqualTo(new[] { "div", "p" }));
        }

        [Test]
        public void EmptySource_ProducesADocumentWithoutARoot()
        {
            DomDocument document = Parse(string.Empty);

            Assert.That(document.DocumentElement, Is.Null);
            Assert.That(document.FilePath, Is.EqualTo(Path));
            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void Parsing_IsDeterministic()
        {
            const string source = "<body><div class=\"a b\" id=\"x\"><p>text</p><img src=\"i.png\"></div></body>";

            string first = Describe(new HtmlParser().Parse(Path, source, new DiagnosticSink()));
            string second = Describe(new HtmlParser().Parse(Path, source, new DiagnosticSink()));

            Assert.That(second, Is.EqualTo(first));
        }

        private static string Describe(DomDocument document)
        {
            return string.Join(
                "|",
                document.DescendantsAndSelf().Select(node => node switch
                {
                    DomElement element =>
                        element.TagName + "#" + (element.Id ?? "-")
                        + "." + string.Join(",", element.Classes)
                        + "[" + string.Join(",", element.Attributes.Select(a => a.Name + "=" + a.Value)) + "]"
                        + "@" + node.Source,
                    DomText text => "text:" + text.Text + "@" + node.Source,
                    _ => "document",
                }));
        }
    }

    public sealed class HtmlSourceLocationTests
    {
        private const string Path = "Assets/UI/page.html";

        [Test]
        public void ElementLocations_PointAtTheStartTag()
        {
            const string source = "<body>\n  <div>\n    <button id=\"apply\">Apply</button>\n  </div>\n</body>";
            DomDocument document = new HtmlParser().Parse(Path, source, new DiagnosticSink());

            DomElement body = document.DocumentElement!;
            DomElement div = body.ElementChildren.Single();
            DomElement button = div.ElementChildren.Single();

            Assert.That(body.Source, Is.EqualTo(new SourceLocation(Path, 1, 1)));
            Assert.That(div.Source, Is.EqualTo(new SourceLocation(Path, 2, 3)));
            Assert.That(button.Source, Is.EqualTo(new SourceLocation(Path, 3, 5)));
        }

        [Test]
        public void AttributeLocations_PointAtTheAttributeName()
        {
            DomDocument document = new HtmlParser().Parse(
                Path,
                "<img src=\"a.png\" alt=\"A\">",
                new DiagnosticSink());

            DomElement image = document.DocumentElement!.ElementChildren.Single();

            Assert.That(image.Attributes[0].Source, Is.EqualTo(new SourceLocation(Path, 1, 6)));
            Assert.That(image.Attributes[1].Source, Is.EqualTo(new SourceLocation(Path, 1, 18)));
        }

        [Test]
        public void CarriageReturnLineFeed_CountsAsOneLine()
        {
            DomDocument document = new HtmlParser().Parse(
                Path,
                "<body>\r\n<div>\r\n<p>x</p>\r\n</div>\r\n</body>",
                new DiagnosticSink());

            DomElement div = document.DocumentElement!.ElementChildren.Single();
            DomElement paragraph = div.ElementChildren.Single();

            Assert.That(div.Source.Line, Is.EqualTo(2));
            Assert.That(paragraph.Source.Line, Is.EqualTo(3));
        }

        [Test]
        public void TextLocations_PointAtTheStartOfTheRun()
        {
            DomDocument document = new HtmlParser().Parse(
                Path,
                "<p>\n  hello\n</p>",
                new DiagnosticSink());

            DomText text = document.DocumentElement!
                .ElementChildren.Single()
                .Children.OfType<DomText>()
                .Single();

            Assert.That(text.Source, Is.EqualTo(new SourceLocation(Path, 1, 4)));
        }
    }
}
