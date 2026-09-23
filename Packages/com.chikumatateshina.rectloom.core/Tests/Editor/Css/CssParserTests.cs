#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rectloom.Core.Css;
using Rectloom.Core.Css.Ast;
using Rectloom.Core.Css.Parsing;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Tests.Css
{
    public sealed class CssParserTests
    {
        private const string Path = "Assets/UI/theme.css";

        private DiagnosticSink _diagnostics = null!;

        [SetUp]
        public void SetUp()
        {
            _diagnostics = new DiagnosticSink();
        }

        private CssStyleSheet Parse(string source)
        {
            return CssParser.Parse(Path, source, _diagnostics);
        }

        [Test]
        public void SimpleRule_IsParsed()
        {
            CssStyleSheet sheet = Parse(".panel { width: 200px; height: 100px; }");

            CssRule rule = sheet.Rules.Single();
            Assert.That(rule.Selectors.Single().RawText, Is.EqualTo(".panel"));
            Assert.That(
                rule.Declarations.Select(d => d.Property + "=" + d.RawValue),
                Is.EqualTo(new[] { "width=200px", "height=100px" }));
            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void PropertyNames_AreLowerCased()
        {
            CssRule rule = Parse("div { Background-Color: RED; }").Rules.Single();

            Assert.That(rule.Declarations.Single().Property, Is.EqualTo("background-color"));
            Assert.That(rule.Declarations.Single().RawValue, Is.EqualTo("RED"), "values keep their case");
        }

        [Test]
        public void SelectorList_ProducesOneRuleWithManySelectors()
        {
            CssRule rule = Parse("h1, h2 , .title { color: red; }").Rules.Single();

            Assert.That(
                rule.Selectors.Select(s => s.RawText),
                Is.EqualTo(new[] { "h1", "h2", ".title" }));
        }

        [Test]
        public void FinalSemicolon_IsOptional()
        {
            CssRule rule = Parse("div { color: red }").Rules.Single();

            Assert.That(rule.Declarations.Single().RawValue, Is.EqualTo("red"));
        }

        [Test]
        public void Comments_AreSkippedEverywhere()
        {
            CssStyleSheet sheet = Parse(
                "/* header */ div /* sel */ { /* a */ color: red; /* b */ } /* tail */");

            Assert.That(sheet.Rules.Single().Declarations.Single().Property, Is.EqualTo("color"));
            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void Important_IsStrippedFromTheValue()
        {
            CssRule rule = Parse("div { color: red !important; width: 10px; }").Rules.Single();

            Assert.That(rule.Declarations[0].Important, Is.True);
            Assert.That(rule.Declarations[0].RawValue, Is.EqualTo("red"));
            Assert.That(rule.Declarations[1].Important, Is.False);
        }

        [Test]
        public void Important_IsCaseInsensitive()
        {
            Assert.That(Parse("div { color: red !IMPORTANT; }").Rules.Single().Declarations[0].Important, Is.True);
        }

        [Test]
        public void FunctionValues_KeepTheirCommasAndParentheses()
        {
            CssRule rule = Parse("div { background-color: rgba(1, 2, 3, 0.5); }").Rules.Single();

            Assert.That(rule.Declarations.Single().RawValue, Is.EqualTo("rgba(1, 2, 3, 0.5)"));
        }

        [Test]
        public void UrlValues_SurviveSemicolonsInsideQuotes()
        {
            CssRule rule = Parse("div { background-image: url(\"./a;b.png\"); color: red; }").Rules.Single();

            Assert.That(rule.Declarations[0].RawValue, Is.EqualTo("url(\"./a;b.png\")"));
            Assert.That(rule.Declarations[1].Property, Is.EqualTo("color"));
        }

        [Test]
        public void SourceOrder_CountsRulesWithinTheSheet()
        {
            CssStyleSheet sheet = Parse("a { color: red; } b { color: blue; } c { color: green; }");

            Assert.That(sheet.Rules.Select(r => r.SourceOrder), Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void SourceLocations_PointAtTheRuleAndTheProperty()
        {
            CssStyleSheet sheet = Parse("div {\n    color: red;\n}");
            CssRule rule = sheet.Rules.Single();

            Assert.That(rule.Source, Is.EqualTo(new SourceLocation(Path, 1, 1)));
            Assert.That(rule.Declarations.Single().Source, Is.EqualTo(new SourceLocation(Path, 2, 5)));
        }

        [Test]
        public void Import_IsCollectedWithoutQuotes()
        {
            CssStyleSheet sheet = Parse("@import \"./common.css\";\ndiv { color: red; }");

            Assert.That(sheet.Imports.Single().Path, Is.EqualTo("./common.css"));
            Assert.That(sheet.Rules.Single().Selectors.Single().RawText, Is.EqualTo("div"));
            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void Import_AcceptsUrlSyntax()
        {
            Assert.That(
                Parse("@import url('./common.css');").Imports.Single().Path,
                Is.EqualTo("./common.css"));
        }

        [Test]
        public void ImportAfterARule_IsSkippedAndReported()
        {
            CssStyleSheet sheet = Parse("div { color: red; }\n@import \"./late.css\";");

            Assert.That(sheet.Imports, Is.Empty);
            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Css.MisplacedImport));
        }

        [Test]
        public void UnsupportedAtRule_IsSkippedWithItsBlock()
        {
            CssStyleSheet sheet = Parse("@media screen { div { color: red; } }\np { color: blue; }");

            Assert.That(sheet.Rules.Single().Selectors.Single().RawText, Is.EqualTo("p"));
            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Css.UnsupportedAtRule));
        }

        [Test]
        public void DeclarationWithoutAColon_IsReportedAndTheRestSurvives()
        {
            CssRule rule = Parse("div { color red; width: 10px; }").Rules.Single();

            Assert.That(rule.Declarations.Single().Property, Is.EqualTo("width"));
            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Css.InvalidValue));
        }

        [Test]
        public void DeclarationWithoutAValue_IsReported()
        {
            CssRule rule = Parse("div { color: ; width: 10px; }").Rules.Single();

            Assert.That(rule.Declarations.Single().Property, Is.EqualTo("width"));
            Assert.That(_diagnostics.CountOf(DiagnosticSeverity.Warning), Is.EqualTo(1));
        }

        [Test]
        public void UnterminatedBlock_IsReported()
        {
            CssStyleSheet sheet = Parse("div { color: red;");

            Assert.That(sheet.Rules.Single().Declarations.Single().Property, Is.EqualTo("color"));
            Assert.That(
                _diagnostics.Diagnostics.Any(d => d.Code == DiagnosticCodes.Css.UnterminatedBlock),
                Is.True);
        }

        [Test]
        public void SelectorWithoutABlock_IsReportedAndSkipped()
        {
            CssStyleSheet sheet = Parse("div");

            Assert.That(sheet.Rules, Is.Empty);
            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Css.UnterminatedBlock));
        }

        [Test]
        public void UnterminatedComment_IsReported()
        {
            Parse("div { color: red; } /* never closed");

            Assert.That(
                _diagnostics.Diagnostics.Any(d => d.Code == DiagnosticCodes.Css.UnterminatedBlock),
                Is.True);
        }

        [Test]
        public void EmptySource_ProducesAnEmptySheet()
        {
            CssStyleSheet sheet = Parse("   \n  ");

            Assert.That(sheet.Rules, Is.Empty);
            Assert.That(sheet.Imports, Is.Empty);
            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void InlineDeclarations_AreParsedWithoutBraces()
        {
            IReadOnlyList<CssDeclaration> declarations = CssParser.ParseInlineDeclarations(
                "width: 200px; height: 50px",
                new SourceLocation("Assets/UI/page.html", 3, 10),
                _diagnostics);

            Assert.That(
                declarations.Select(d => d.Property + "=" + d.RawValue),
                Is.EqualTo(new[] { "width=200px", "height=50px" }));
            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void InlineDeclarations_WhenEmpty_ReturnNothing()
        {
            Assert.That(
                CssParser.ParseInlineDeclarations(null, SourceLocation.None, _diagnostics),
                Is.Empty);
        }

        [Test]
        public void Parsing_IsDeterministic()
        {
            const string source = "@import './a.css'; div.a, #b > p { color: red; margin: 0 auto; }";

            string first = Describe(CssParser.Parse(Path, source, new DiagnosticSink()));
            string second = Describe(CssParser.Parse(Path, source, new DiagnosticSink()));

            Assert.That(second, Is.EqualTo(first));
        }

        private static string Describe(CssStyleSheet sheet)
        {
            return string.Join(
                "|",
                sheet.Imports.Select(i => "import:" + i.Path).Concat(
                    sheet.Rules.Select(r =>
                        string.Join(",", r.Selectors.Select(s => s.RawText + s.Specificity))
                        + "{" + string.Join(";", r.Declarations.Select(d => d.ToString())) + "}")));
        }
    }

    public sealed class DefaultStyleSheetTests
    {
        [Test]
        public void ItParsesWithoutDiagnostics()
        {
            var diagnostics = new DiagnosticSink();
            CssStyleSheet sheet = CssParser.Parse(
                DefaultStyleSheet.VirtualPath,
                DefaultStyleSheet.Source,
                diagnostics);

            Assert.That(diagnostics.Diagnostics, Is.Empty, "the built-in stylesheet must be valid");
            Assert.That(sheet.Rules, Is.Not.Empty);
        }

        [Test]
        public void Get_ReturnsTheSameParsedInstance()
        {
            Assert.That(DefaultStyleSheet.Get(), Is.SameAs(DefaultStyleSheet.Get()));
        }

        [Test]
        public void ItStylesTheMvpElements()
        {
            IEnumerable<string> selectors = DefaultStyleSheet.Get()
                .Rules
                .SelectMany(r => r.Selectors)
                .Select(s => s.RawText);

            Assert.That(selectors, Has.Some.EqualTo("button"));
            Assert.That(selectors, Has.Some.EqualTo("h1"));
        }
    }
}
