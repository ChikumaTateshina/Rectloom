#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rectloom.Core.Css.Ast;
using Rectloom.Core.Css.Cascade;
using Rectloom.Core.Css.Computed;
using Rectloom.Core.Css.Parsing;
using Rectloom.Core.Css.Values;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Dom;
using Rectloom.Core.Parsing;
using UnityEngine;

namespace Rectloom.Core.Tests.Css
{
    /// <summary>
    /// Drives HTML and CSS through parsing, the cascade and the computed style builder, which is the
    /// combination the rest of the compiler depends on.
    /// </summary>
    public sealed class CascadeAndComputedStyleTests
    {
        private DiagnosticSink _diagnostics = null!;

        [SetUp]
        public void SetUp()
        {
            _diagnostics = new DiagnosticSink();
        }

        private ComputedStyleTree Build(string html, params string[] stylesheets)
        {
            DomDocument document = new HtmlParser().Parse("Assets/UI/page.html", html, _diagnostics);

            var sheets = new List<CssStyleSheet>();

            for (int index = 0; index < stylesheets.Length; index++)
            {
                sheets.Add(CssParser.Parse(
                    "Assets/UI/sheet" + index + ".css",
                    stylesheets[index],
                    _diagnostics));
            }

            var cascade = new CascadeResolver(null, sheets);
            return ComputedStyleTree.Build(document, cascade, new ComputedStyleBuilder(), _diagnostics);
        }

        private static DomElement Find(DomDocument document, string id)
        {
            Assert.That(document.TryGetElementById(id, out DomElement element), Is.True, id);
            return element;
        }

        private ComputedStyle StyleFor(string html, string id, params string[] stylesheets)
        {
            DomDocument document = new HtmlParser().Parse("Assets/UI/page.html", html, _diagnostics);

            var sheets = new List<CssStyleSheet>();

            for (int index = 0; index < stylesheets.Length; index++)
            {
                sheets.Add(CssParser.Parse(
                    "Assets/UI/sheet" + index + ".css",
                    stylesheets[index],
                    _diagnostics));
            }

            var cascade = new CascadeResolver(null, sheets);
            ComputedStyleTree tree = ComputedStyleTree.Build(
                document,
                cascade,
                new ComputedStyleBuilder(),
                _diagnostics);

            return tree.GetStyle(Find(document, id));
        }

        [Test]
        public void HigherSpecificity_Wins()
        {
            ComputedStyle style = StyleFor(
                "<div id=\"x\" class=\"panel\"></div>",
                "x",
                "div { width: 10px; } .panel { width: 20px; } #x { width: 30px; }");

            Assert.That(style.Width, Is.EqualTo(CssLength.Pixels(30f)));
        }

        [Test]
        public void EqualSpecificity_LastRuleWins()
        {
            ComputedStyle style = StyleFor(
                "<div id=\"x\" class=\"a b\"></div>",
                "x",
                ".a { width: 10px; } .b { width: 20px; }");

            Assert.That(style.Width, Is.EqualTo(CssLength.Pixels(20f)));
        }

        [Test]
        public void EqualSpecificity_LaterStylesheetWins()
        {
            ComputedStyle style = StyleFor(
                "<div id=\"x\"></div>",
                "x",
                "div { width: 10px; }",
                "div { width: 20px; }");

            Assert.That(style.Width, Is.EqualTo(CssLength.Pixels(20f)));
        }

        [Test]
        public void Important_BeatsHigherSpecificity()
        {
            ComputedStyle style = StyleFor(
                "<div id=\"x\" class=\"panel\"></div>",
                "x",
                ".panel { width: 20px !important; } #x { width: 30px; }");

            Assert.That(style.Width, Is.EqualTo(CssLength.Pixels(20f)));
        }

        [Test]
        public void InlineStyle_BeatsAnyRule()
        {
            ComputedStyle style = StyleFor(
                "<div id=\"x\" style=\"width: 5px;\"></div>",
                "x",
                "#x { width: 30px; }");

            Assert.That(style.Width, Is.EqualTo(CssLength.Pixels(5f)));
        }

        [Test]
        public void ImportantRule_BeatsInlineStyle()
        {
            ComputedStyle style = StyleFor(
                "<div id=\"x\" style=\"width: 5px;\"></div>",
                "x",
                "div { width: 30px !important; }");

            Assert.That(style.Width, Is.EqualTo(CssLength.Pixels(30f)));
        }

        [Test]
        public void UserAgentSheet_LosesToAnyAuthorRule()
        {
            DomDocument document = new HtmlParser().Parse(
                "Assets/UI/page.html",
                "<h1 id=\"x\">Title</h1>",
                _diagnostics);

            var userAgent = new[] { CssParser.Parse("ua.css", "h1 { font-size: 32px; }", _diagnostics) };
            var author = new[] { CssParser.Parse("a.css", "* { font-size: 11px; }", _diagnostics) };

            ComputedStyleTree tree = ComputedStyleTree.Build(
                document,
                new CascadeResolver(userAgent, author),
                new ComputedStyleBuilder(),
                _diagnostics);

            Assert.That(
                tree.GetStyle(Find(document, "x")).Text.FontSize,
                Is.EqualTo(11f),
                "the universal selector is weaker but the author origin still wins");
        }

        [Test]
        public void ShorthandAfterLonghand_OverwritesIt()
        {
            ComputedStyle style = StyleFor(
                "<div id=\"x\"></div>",
                "x",
                "div { margin-top: 5px; margin: 0; }");

            Assert.That(style.Margin, Is.EqualTo(EdgeSizes.Zero));
        }

        [Test]
        public void LonghandAfterShorthand_OverridesOneEdge()
        {
            ComputedStyle style = StyleFor(
                "<div id=\"x\"></div>",
                "x",
                "div { margin: 4px; margin-top: 12px; }");

            Assert.That(style.Margin.Top, Is.EqualTo(CssLength.Pixels(12f)));
            Assert.That(style.Margin.Left, Is.EqualTo(CssLength.Pixels(4f)));
        }

        [TestCase("8px", 8f, 8f, 8f, 8f)]
        [TestCase("8px 4px", 8f, 4f, 8f, 4f)]
        [TestCase("8px 4px 2px", 8f, 4f, 2f, 4f)]
        [TestCase("8px 4px 2px 1px", 8f, 4f, 2f, 1f)]
        public void EdgeShorthand_ExpandsLikeCss(string value, float top, float right, float bottom, float left)
        {
            ComputedStyle style = StyleFor(
                "<div id=\"x\"></div>",
                "x",
                "div { padding: " + value + "; }");

            Assert.That(style.Padding.Top, Is.EqualTo(CssLength.Pixels(top)));
            Assert.That(style.Padding.Right, Is.EqualTo(CssLength.Pixels(right)));
            Assert.That(style.Padding.Bottom, Is.EqualTo(CssLength.Pixels(bottom)));
            Assert.That(style.Padding.Left, Is.EqualTo(CssLength.Pixels(left)));
        }

        [Test]
        public void TextProperties_AreInherited()
        {
            ComputedStyle style = StyleFor(
                "<body><div class=\"panel\"><p id=\"x\">text</p></div></body>",
                "x",
                ".panel { color: #ff0000; font-size: 20px; text-align: center; }");

            Assert.That((Color32)style.Text.Color, Is.EqualTo(new Color32(255, 0, 0, 255)));
            Assert.That(style.Text.FontSize, Is.EqualTo(20f));
            Assert.That(style.Text.TextAlign, Is.EqualTo(CssTextAlign.Center));
        }

        [Test]
        public void BoxProperties_AreNotInherited()
        {
            ComputedStyle style = StyleFor(
                "<body><div style=\"width: 100px; background-color: red;\"><p id=\"x\"></p></div></body>",
                "x");

            Assert.That(style.Width.IsAuto, Is.True);
            Assert.That(style.Visual.BackgroundColor, Is.Null);
        }

        [Test]
        public void FontSizePercent_ResolvesAgainstTheParent()
        {
            ComputedStyle style = StyleFor(
                "<body><div class=\"outer\"><p id=\"x\"></p></div></body>",
                "x",
                ".outer { font-size: 20px; } p { font-size: 50%; }");

            Assert.That(style.Text.FontSize, Is.EqualTo(10f));
        }

        [Test]
        public void LineHeightInPixels_BecomesAMultipleOfTheFontSize()
        {
            ComputedStyle style = StyleFor(
                "<p id=\"x\"></p>",
                "x",
                "p { line-height: 24px; font-size: 12px; }");

            Assert.That(
                style.Text.LineHeight,
                Is.EqualTo(2f),
                "font-size is applied first, whatever order it was written in");
        }

        [TestCase("normal", 1.2f)]
        [TestCase("1.5", 1.5f)]
        [TestCase("150%", 1.5f)]
        public void LineHeight_AcceptsKeywordNumberAndPercentage(string value, float expected)
        {
            ComputedStyle style = StyleFor("<p id=\"x\"></p>", "x", "p { line-height: " + value + "; }");

            Assert.That(style.Text.LineHeight, Is.EqualTo(expected).Within(0.0001f));
        }

        [TestCase("normal", 400)]
        [TestCase("bold", 700)]
        [TestCase("600", 600)]
        public void FontWeight_AcceptsKeywordsAndNumbers(string value, int expected)
        {
            ComputedStyle style = StyleFor("<p id=\"x\"></p>", "x", "p { font-weight: " + value + "; }");

            Assert.That(style.Text.FontWeight, Is.EqualTo(expected));
        }

        [Test]
        public void FlexProperties_AreRead()
        {
            ComputedStyle style = StyleFor(
                "<div id=\"x\"></div>",
                "x",
                "div { display: flex; flex-direction: column; justify-content: space-between;"
                    + " align-items: center; gap: 12px; }");

            Assert.That(style.Display, Is.EqualTo(CssDisplay.Flex));
            Assert.That(style.Flex.Direction, Is.EqualTo(CssFlexDirection.Column));
            Assert.That(style.Flex.JustifyContent, Is.EqualTo(CssJustifyContent.SpaceBetween));
            Assert.That(style.Flex.AlignItems, Is.EqualTo(CssAlignItems.Center));
            Assert.That(style.Flex.Gap, Is.EqualTo(CssLength.Pixels(12f)));
        }

        [TestCase("flex-start", CssJustifyContent.Start)]
        [TestCase("flex-end", CssJustifyContent.End)]
        public void FlexAliases_AreAccepted(string value, CssJustifyContent expected)
        {
            ComputedStyle style = StyleFor("<div id=\"x\"></div>", "x", "div { justify-content: " + value + "; }");

            Assert.That(style.Flex.JustifyContent, Is.EqualTo(expected));
        }

        [Test]
        public void VisualProperties_AreRead()
        {
            ComputedStyle style = StyleFor(
                "<div id=\"x\"></div>",
                "x",
                "div { background-color: #102030; background-image: url(\"./icon.png\");"
                    + " opacity: 0.5; border-width: 2px; border-color: red; border-radius: 6px; }");

            Assert.That((Color32)style.Visual.BackgroundColor!.Value, Is.EqualTo(new Color32(16, 32, 48, 255)));
            Assert.That(style.Visual.BackgroundImage, Is.EqualTo("./icon.png"));
            Assert.That(style.Visual.Opacity, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(style.Visual.BorderWidth, Is.EqualTo(2f));
            Assert.That(style.Visual.BorderRadius, Is.EqualTo(6f));
            Assert.That(style.Visual.PaintsAnything, Is.True);
        }

        [Test]
        public void ElementWithoutPaint_ReportsNothingToPaint()
        {
            Assert.That(StyleFor("<div id=\"x\"></div>", "x").Visual.PaintsAnything, Is.False);
        }

        [Test]
        public void Opacity_IsClamped()
        {
            Assert.That(StyleFor("<div id=\"x\"></div>", "x", "div { opacity: 3; }").Visual.Opacity, Is.EqualTo(1f));
            Assert.That(StyleFor("<div id=\"x\"></div>", "x", "div { opacity: -1; }").Visual.Opacity, Is.Zero);
        }

        [Test]
        public void PositionAndOffsets_AreRead()
        {
            ComputedStyle style = StyleFor(
                "<div id=\"x\"></div>",
                "x",
                "div { position: absolute; top: 10px; right: 20%; bottom: auto; left: 0; }");

            Assert.That(style.Position, Is.EqualTo(CssPosition.Absolute));
            Assert.That(style.Top, Is.EqualTo(CssLength.Pixels(10f)));
            Assert.That(style.Right, Is.EqualTo(CssLength.Percent(20f)));
            Assert.That(style.Bottom.IsAuto, Is.True);
            Assert.That(style.Left, Is.EqualTo(CssLength.Zero));
            Assert.That(style.IsInFlow, Is.False);
        }

        [Test]
        public void DisplayNone_MeansNothingIsRendered()
        {
            ComputedStyle style = StyleFor("<div id=\"x\"></div>", "x", "div { display: none; }");

            Assert.That(style.IsRendered, Is.False);
            Assert.That(style.IsInFlow, Is.False);
        }

        [Test]
        public void UnknownProperty_IsReportedAndKeptAsExtensionData()
        {
            ComputedStyle style = StyleFor("<div id=\"x\"></div>", "x", "div { wobble: 3; }");

            Assert.That(style.TryGetExtensionProperty("wobble", out string value), Is.True);
            Assert.That(value, Is.EqualTo("3"));
            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Css.UnknownProperty));
        }

        [Test]
        public void ReservedPrefixProperty_IsKeptWithoutADiagnostic()
        {
            ComputedStyle style = StyleFor(
                "<button id=\"x\"></button>",
                "x",
                "button { unity-interactable: true; vrc-something: 1; }");

            Assert.That(style.TryGetExtensionProperty("unity-interactable", out string value), Is.True);
            Assert.That(value, Is.EqualTo("true"));
            Assert.That(style.TryGetExtensionProperty("vrc-something", out _), Is.True);
            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void InvalidValue_IsReportedAndTheRestOfTheRuleSurvives()
        {
            ComputedStyle style = StyleFor(
                "<div id=\"x\"></div>",
                "x",
                "div { width: 12em; height: 40px; }");

            Assert.That(style.Width.IsAuto, Is.True, "the bad declaration is ignored");
            Assert.That(style.Height, Is.EqualTo(CssLength.Pixels(40f)));
            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Css.InvalidValue));
        }

        [Test]
        public void ContentBoxSizing_IsRejected()
        {
            StyleFor("<div id=\"x\"></div>", "x", "div { box-sizing: content-box; }");

            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Css.InvalidValue));
        }

        [Test]
        public void BorderBoxSizing_IsAcceptedSilently()
        {
            StyleFor("<div id=\"x\"></div>", "x", "div { box-sizing: border-box; }");

            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void EveryElementGetsAStyle()
        {
            ComputedStyleTree tree = Build(
                "<body><div><p>a</p><button>b</button></div></body>",
                "div { width: 10px; }");

            Assert.That(tree.Count, Is.EqualTo(4), "body, div, p and button");
        }

        [Test]
        public void ResolveOrder_IsDeterministic()
        {
            const string html = "<div id=\"x\" class=\"a b\"></div>";
            const string css = ".a { width: 1px; } .b { width: 2px; } div { width: 3px; }";

            float First()
            {
                return StyleFor(html, "x", css).Width.Value;
            }

            Assert.That(First(), Is.EqualTo(First()));
            Assert.That(First(), Is.EqualTo(2f), "last rule of equal specificity wins");
        }
    }
}
