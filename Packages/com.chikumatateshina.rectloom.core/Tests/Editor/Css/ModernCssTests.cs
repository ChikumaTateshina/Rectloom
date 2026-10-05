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
    /// Covers the CSS that real stylesheets are written with: physical units, custom properties,
    /// shorthands, overflow and the flex item properties.
    /// </summary>
    /// <remarks>
    /// Asserted through the whole front end rather than on the value parsers, because most of these
    /// only work if parsing, the cascade and the computed-style passes agree about when a value is
    /// resolved. An <c>em</c> that survived into layout would be a silent zero.
    /// </remarks>
    public sealed class ModernCssTests
    {
        private DiagnosticSink _diagnostics = null!;

        [SetUp]
        public void SetUp()
        {
            _diagnostics = new DiagnosticSink();
        }

        private ComputedStyle StyleFor(string html, string id, string css)
        {
            DomDocument document = new HtmlParser().Parse("Assets/UI/page.html", html, _diagnostics);

            var sheets = new List<CssStyleSheet>
            {
                CssParser.Parse("Assets/UI/page.css", css, _diagnostics),
            };

            ComputedStyleTree tree = ComputedStyleTree.Build(
                document,
                new CascadeResolver(null, sheets),
                new ComputedStyleBuilder(),
                _diagnostics);

            Assert.That(document.TryGetElementById(id, out DomElement element), Is.True, id);
            return tree.GetStyle(element);
        }

        private ComputedStyle Divided(string declarations)
        {
            return StyleFor("<body><div id=\"x\"></div></body>", "x", "#x { " + declarations + " }");
        }

        private void AssertNoDiagnostics()
        {
            Assert.That(
                _diagnostics.Diagnostics.Select(d => d.Code + ": " + d.Message),
                Is.Empty);
        }

        [TestCase("8mm", 30.2362f)]
        [TestCase("1in", 96f)]
        [TestCase("2.54cm", 96f)]
        [TestCase("12pt", 16f)]
        [TestCase("1pc", 16f)]
        [TestCase("4q", 3.7795f)]
        public void PhysicalUnits_BecomePixels(string value, float expected)
        {
            ComputedStyle style = Divided("width: " + value + ";");

            Assert.That(style.Width.Unit, Is.EqualTo(CssLengthUnit.Pixel));
            Assert.That(style.Width.Value, Is.EqualTo(expected).Within(0.001f));
            AssertNoDiagnostics();
        }

        [Test]
        public void Em_ResolvesAgainstTheElementsOwnFontSize()
        {
            ComputedStyle style = Divided("font-size: 20px; max-height: 1.5em; padding: 0.5em;");

            Assert.That(style.MaxHeight, Is.EqualTo(CssLength.Pixels(30f)));
            Assert.That(style.Padding.Top, Is.EqualTo(CssLength.Pixels(10f)));
            AssertNoDiagnostics();
        }

        [Test]
        public void Em_OnFontSizeResolvesAgainstTheParent()
        {
            ComputedStyle style = StyleFor(
                "<body><div><p id=\"x\">a</p></div></body>",
                "x",
                "div { font-size: 20px; } p { font-size: 1.5em; width: 2em; }");

            Assert.That(style.Text.FontSize, Is.EqualTo(30f));
            Assert.That(style.Width, Is.EqualTo(CssLength.Pixels(60f)), "2em of the element's own size");
        }

        [Test]
        public void Rem_ResolvesAgainstTheRootFontSize()
        {
            ComputedStyle style = StyleFor(
                "<body><div id=\"x\"></div></body>",
                "x",
                "body { font-size: 10px; } #x { font-size: 40px; width: 3rem; }");

            Assert.That(style.Width, Is.EqualTo(CssLength.Pixels(30f)));
        }

        [Test]
        public void CustomProperties_AreSubstitutedAndInherited()
        {
            ComputedStyle style = StyleFor(
                "<body><div id=\"x\"></div></body>",
                "x",
                ":root { --w: 1920px; --h: var(--w); }"
                    + " #x { width: var(--w); height: var(--h); }");

            Assert.That(style.Width, Is.EqualTo(CssLength.Pixels(1920f)));
            Assert.That(style.Height, Is.EqualTo(CssLength.Pixels(1920f)));
            Assert.That(style.CustomProperties["--w"], Is.EqualTo("1920px"));
            AssertNoDiagnostics();
        }

        [Test]
        public void CustomProperties_AreOverriddenByADescendant()
        {
            ComputedStyle style = StyleFor(
                "<body><div><p id=\"x\">a</p></div></body>",
                "x",
                "body { --gap: 4px; } div { --gap: 12px; } p { width: var(--gap); }");

            Assert.That(style.Width, Is.EqualTo(CssLength.Pixels(12f)));
        }

        [Test]
        public void AnUnresolvedVariable_IsReportedAndTheDeclarationIsDropped()
        {
            ComputedStyle style = Divided("width: var(--missing); height: 10px;");

            Assert.That(style.Width.IsAuto, Is.True);
            Assert.That(style.Height, Is.EqualTo(CssLength.Pixels(10f)));
            Assert.That(
                _diagnostics.Diagnostics.Single().Message,
                Does.Contain("--missing"));
        }

        [Test]
        public void RootSelector_MatchesTheDocumentRoot()
        {
            ComputedStyle root = StyleFor(
                "<body id=\"b\"><div id=\"x\"></div></body>",
                "b",
                ":root { width: 50px; }");

            Assert.That(root.Width, Is.EqualTo(CssLength.Pixels(50f)));

            _diagnostics.Clear();

            ComputedStyle child = StyleFor(
                "<body id=\"b\"><div id=\"x\"></div></body>",
                "x",
                ":root { width: 50px; }");

            Assert.That(child.Width.IsAuto, Is.True, ":root is not every element");
        }

        [Test]
        public void PseudoElementSelectors_AreSkippedWithoutAWarning()
        {
            ComputedStyle style = Divided("width: 10px;");

            Assert.That(style.Width, Is.EqualTo(CssLength.Pixels(10f)));

            _diagnostics.Clear();

            StyleFor(
                "<body><div id=\"x\"></div></body>",
                "x",
                "*, *::before, *::after { box-sizing: border-box; }");

            Assert.That(
                _diagnostics.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning),
                Is.Empty,
                "a pseudo-element selector is information, not a warning");

            Assert.That(_diagnostics.Diagnostics.Count, Is.EqualTo(2), "one per pseudo-element");
        }

        [Test]
        public void BackgroundShorthand_SetsTheColourAndClearsTheImage()
        {
            ComputedStyle style = Divided("background-image: url(\"./a.png\"); background: #e8e8e4;");

            Assert.That(
                (Color32)style.Visual.BackgroundColor!.Value,
                Is.EqualTo(new Color32(0xe8, 0xe8, 0xe4, 0xff)));
            Assert.That(style.Visual.BackgroundImage, Is.Null, "the shorthand resets what it covers");
            AssertNoDiagnostics();
        }

        [Test]
        public void BackgroundShorthand_AcceptsNoneAndPaintingKeywords()
        {
            ComputedStyle style = Divided("background-color: red; background: none;");

            Assert.That(style.Visual.BackgroundColor, Is.Null);
            Assert.That(style.Visual.PaintsAnything, Is.False);

            _diagnostics.Clear();

            ComputedStyle positioned = Divided("background: #fff no-repeat center / cover;");

            Assert.That((Color32)positioned.Visual.BackgroundColor!.Value, Is.EqualTo(new Color32(255, 255, 255, 255)));
            AssertNoDiagnostics();
        }

        [Test]
        public void BorderShorthand_SetsWidthAndColour()
        {
            ComputedStyle style = Divided("border: 2px solid #ff0000;");

            Assert.That(style.Visual.BorderWidth, Is.EqualTo(2f));
            Assert.That((Color32)style.Visual.BorderColor!.Value, Is.EqualTo(new Color32(255, 0, 0, 255)));
            AssertNoDiagnostics();
        }

        [Test]
        public void BorderNone_RemovesAnEarlierBorder()
        {
            ComputedStyle style = Divided("border-width: 4px; border-color: red; border: none;");

            Assert.That(style.Visual.BorderWidth, Is.Zero);
            Assert.That(style.Visual.BorderColor, Is.Null);
        }

        [TestCase("hidden")]
        [TestCase("clip")]
        [TestCase("auto")]
        [TestCase("scroll")]
        public void Overflow_ClipsContent(string value)
        {
            ComputedStyle style = Divided("overflow: " + value + ";");

            Assert.That(style.Visual.ClipsContent, Is.True);
            AssertNoDiagnostics();
        }

        [Test]
        public void OverflowVisible_DoesNotClip()
        {
            Assert.That(Divided("overflow: visible;").Visual.ClipsContent, Is.False);
        }

        [Test]
        public void OverflowOnOneAxis_ClipsTheWholeBox()
        {
            Assert.That(Divided("overflow-y: hidden;").Visual.ClipsContent, Is.True);
            AssertNoDiagnostics();
        }

        [Test]
        public void FlexShorthand_One_GrowsFromZero()
        {
            ComputedStyle style = Divided("flex: 1;");

            Assert.That(style.Flex.Grow, Is.EqualTo(1f));
            Assert.That(style.Flex.Shrink, Is.EqualTo(1f));
            Assert.That(style.Flex.Basis, Is.EqualTo(CssLength.Pixels(0f)));
            AssertNoDiagnostics();
        }

        [Test]
        public void FlexShorthand_None_FreezesTheItem()
        {
            ComputedStyle style = Divided("flex: none;");

            Assert.That(style.Flex.Grow, Is.Zero);
            Assert.That(style.Flex.Shrink, Is.Zero);
            Assert.That(style.Flex.Basis.IsAuto, Is.True);
        }

        [Test]
        public void FlexShorthand_ReadsThreeValues()
        {
            ComputedStyle style = Divided("flex: 2 0 40mm;");

            Assert.That(style.Flex.Grow, Is.EqualTo(2f));
            Assert.That(style.Flex.Shrink, Is.Zero);
            Assert.That(style.Flex.Basis.Value, Is.EqualTo(151.181f).Within(0.01f));
        }

        [Test]
        public void FlexContainerProperties_AreRead()
        {
            ComputedStyle style = Divided(
                "display: flex; flex-wrap: wrap; align-content: space-between;"
                    + " align-items: baseline; gap: 8mm 4mm;");

            Assert.That(style.Flex.Wrap, Is.EqualTo(CssFlexWrap.Wrap));
            Assert.That(style.Flex.AlignContent, Is.EqualTo(CssAlignContent.SpaceBetween));
            Assert.That(style.Flex.AlignItems, Is.EqualTo(CssAlignItems.Baseline));
            Assert.That(style.Flex.RowGap.Value, Is.EqualTo(30.2362f).Within(0.001f));
            Assert.That(style.Flex.ColumnGap.Value, Is.EqualTo(15.1181f).Within(0.001f));
            AssertNoDiagnostics();
        }

        [Test]
        public void FlexFlow_SetsDirectionAndWrapTogether()
        {
            ComputedStyle style = Divided("flex-flow: column wrap;");

            Assert.That(style.Flex.Direction, Is.EqualTo(CssFlexDirection.Column));
            Assert.That(style.Flex.Wrap, Is.EqualTo(CssFlexWrap.Wrap));
        }

        [Test]
        public void AlignSelf_OverridesTheContainer()
        {
            ComputedStyle style = Divided("align-self: center;");

            Assert.That(
                style.Flex.ResolveAlignment(CssAlignItems.Start),
                Is.EqualTo(CssAlignItems.Center));

            Assert.That(
                new FlexStyle().ResolveAlignment(CssAlignItems.End),
                Is.EqualTo(CssAlignItems.End),
                "auto follows the container");
        }

        [Test]
        public void FontFamily_IsKeptAsAList()
        {
            ComputedStyle style = Divided(
                "font-family: \"Noto Sans JP\", 'Yu Gothic', sans-serif;");

            Assert.That(
                style.Text.FontFamily,
                Is.EqualTo(new[] { "Noto Sans JP", "Yu Gothic", "sans-serif" }));

            AssertNoDiagnostics();
        }

        [Test]
        public void FontFamily_IsInherited()
        {
            ComputedStyle style = StyleFor(
                "<body><div><p id=\"x\">a</p></div></body>",
                "x",
                "body { font-family: Meiryo; }");

            Assert.That(style.Text.FontFamily, Is.EqualTo(new[] { "Meiryo" }));
        }

        [TestCase("underline", true, false)]
        [TestCase("line-through", false, true)]
        [TestCase("underline line-through", true, true)]
        [TestCase("none", false, false)]
        [TestCase("underline solid red", true, false)]
        public void TextDecoration_IsRead(string value, bool underline, bool lineThrough)
        {
            ComputedStyle style = Divided("text-decoration: " + value + ";");

            Assert.That(style.Text.Underline, Is.EqualTo(underline));
            Assert.That(style.Text.LineThrough, Is.EqualTo(lineThrough));
            AssertNoDiagnostics();
        }

        [Test]
        public void ObjectFit_IsRead()
        {
            Assert.That(Divided("object-fit: contain;").Visual.ObjectFit, Is.EqualTo(CssObjectFit.Contain));
            AssertNoDiagnostics();
        }

        [Test]
        public void PropertiesWithNoUiMeaning_AreAcceptedSilently()
        {
            ComputedStyle style = Divided(
                "overflow-wrap: anywhere; break-after: page; break-inside: avoid;"
                    + " cursor: pointer; transition: all 0.2s; background-repeat: no-repeat;"
                    + " content: \"\"; word-break: keep-all; width: 10px;");

            Assert.That(style.Width, Is.EqualTo(CssLength.Pixels(10f)));
            AssertNoDiagnostics();
        }

        [Test]
        public void APropertyThatWouldChangeTheResult_IsStillReported()
        {
            Divided("text-transform: uppercase;");

            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Css.UnknownProperty));
        }

        [Test]
        public void PrintAtRules_AreInformationRatherThanWarnings()
        {
            StyleFor(
                "<body><div id=\"x\"></div></body>",
                "x",
                "@page { size: 1920px 1080px; }"
                    + " @media print { #x { width: 1px; } }"
                    + " #x { width: 10px; }");

            Assert.That(
                _diagnostics.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning),
                Is.Empty);

            Assert.That(_diagnostics.Diagnostics.Count, Is.EqualTo(2));
        }

        [Test]
        public void WhiteSpacePreLine_KeepsLineBreaksAndWraps()
        {
            ComputedStyle style = Divided("white-space: pre-line;");

            Assert.That(style.Text.WhiteSpace, Is.EqualTo(CssWhiteSpace.PreWrap));
            Assert.That(style.Text.WrapsText, Is.True);
            AssertNoDiagnostics();
        }

        [Test]
        public void LetterSpacingInMillimetres_IsRead()
        {
            ComputedStyle style = Divided("letter-spacing: 0.201mm;");

            Assert.That(style.Text.LetterSpacing, Is.EqualTo(0.7598f).Within(0.001f));
            AssertNoDiagnostics();
        }

        [Test]
        public void FirstBaselineOffset_PutsHalfTheLeadingAboveTheText()
        {
            ComputedStyle style = Divided("font-size: 20px; line-height: 1.5;");

            // 20 * 1.5 = 30 tall, so 5 above and 5 below, plus a 16 pixel ascent.
            Assert.That(style.Text.FirstBaselineOffset, Is.EqualTo(21f).Within(0.001f));
        }
    }
}
