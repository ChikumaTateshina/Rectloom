#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rectloom.Core.Css.Ast;
using Rectloom.Core.Css.Parsing;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Tests.Css
{
    public sealed class CssPathResolverTests
    {
        [TestCase("Assets/UI/theme.css", "./common.css", "Assets/UI/common.css")]
        [TestCase("Assets/UI/theme.css", "common.css", "Assets/UI/common.css")]
        [TestCase("Assets/UI/theme.css", "../shared/base.css", "Assets/shared/base.css")]
        [TestCase("Assets/UI/theme.css", "sub/nested.css", "Assets/UI/sub/nested.css")]
        [TestCase("Assets/UI/theme.css", "Assets/other/x.css", "Assets/other/x.css")]
        [TestCase("Assets/UI/theme.css", "Packages/com.example/x.css", "Packages/com.example/x.css")]
        [TestCase("Assets/UI/theme.css", ".\\windows\\style.css", "Assets/UI/windows/style.css")]
        public void Resolve_ProducesANormalisedAssetPath(string source, string reference, string expected)
        {
            Assert.That(CssPathResolver.Resolve(source, reference), Is.EqualTo(expected));
        }

        [TestCase("Assets/theme.css", "../../escape.css")]
        [TestCase("Assets/theme.css", "")]
        [TestCase("Assets/theme.css", null)]
        public void Resolve_RejectsPathsItCannotUse(string source, string? reference)
        {
            Assert.That(CssPathResolver.Resolve(source, reference), Is.Null);
        }

        [Test]
        public void GetDirectory_DropsTheFileName()
        {
            Assert.That(CssPathResolver.GetDirectory("Assets/UI/theme.css"), Is.EqualTo("Assets/UI"));
            Assert.That(CssPathResolver.GetDirectory("theme.css"), Is.Empty);
            Assert.That(CssPathResolver.GetDirectory(null), Is.Empty);
        }
    }

    public sealed class CssImportResolverTests
    {
        private DiagnosticSink _diagnostics = null!;
        private DictionaryLoader _loader = null!;

        [SetUp]
        public void SetUp()
        {
            _diagnostics = new DiagnosticSink();
            _loader = new DictionaryLoader();
        }

        private IReadOnlyList<string> ResolvedPaths(params string[] entries)
        {
            return new CssImportResolver(_loader)
                .Resolve(entries, _diagnostics)
                .Select(sheet => sheet.FilePath)
                .ToArray();
        }

        [Test]
        public void SheetWithoutImports_IsReturnedAsIs()
        {
            _loader.Add("Assets/UI/theme.css", "div { color: red; }");

            Assert.That(ResolvedPaths("Assets/UI/theme.css"), Is.EqualTo(new[] { "Assets/UI/theme.css" }));
            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void ImportedSheet_ComesBeforeTheSheetThatImportedIt()
        {
            _loader.Add("Assets/UI/theme.css", "@import \"./base.css\";\ndiv { color: red; }");
            _loader.Add("Assets/UI/base.css", "div { color: blue; }");

            Assert.That(
                ResolvedPaths("Assets/UI/theme.css"),
                Is.EqualTo(new[] { "Assets/UI/base.css", "Assets/UI/theme.css" }),
                "an imported sheet cascades beneath the importing one");
        }

        [Test]
        public void NestedImports_AreResolvedDepthFirst()
        {
            _loader.Add("Assets/a.css", "@import \"./b.css\";");
            _loader.Add("Assets/b.css", "@import \"./c.css\";");
            _loader.Add("Assets/c.css", "div { color: red; }");

            Assert.That(
                ResolvedPaths("Assets/a.css"),
                Is.EqualTo(new[] { "Assets/c.css", "Assets/b.css", "Assets/a.css" }));
        }

        [Test]
        public void SeveralImports_KeepSourceOrder()
        {
            _loader.Add("Assets/a.css", "@import \"./x.css\";\n@import \"./y.css\";");
            _loader.Add("Assets/x.css", "div { color: red; }");
            _loader.Add("Assets/y.css", "div { color: blue; }");

            Assert.That(
                ResolvedPaths("Assets/a.css"),
                Is.EqualTo(new[] { "Assets/x.css", "Assets/y.css", "Assets/a.css" }));
        }

        [Test]
        public void EntryPaths_KeepTheirOrder()
        {
            _loader.Add("Assets/a.css", "div { color: red; }");
            _loader.Add("Assets/b.css", "div { color: blue; }");

            Assert.That(
                ResolvedPaths("Assets/a.css", "Assets/b.css"),
                Is.EqualTo(new[] { "Assets/a.css", "Assets/b.css" }));
        }

        [Test]
        public void SheetReachedTwice_IsIncludedOnce()
        {
            _loader.Add("Assets/a.css", "@import \"./shared.css\";\n@import \"./b.css\";");
            _loader.Add("Assets/b.css", "@import \"./shared.css\";");
            _loader.Add("Assets/shared.css", "div { color: red; }");

            Assert.That(
                ResolvedPaths("Assets/a.css"),
                Is.EqualTo(new[] { "Assets/shared.css", "Assets/b.css", "Assets/a.css" }));
            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void DirectCycle_IsAnError()
        {
            _loader.Add("Assets/a.css", "@import \"./a.css\";");

            ResolvedPaths("Assets/a.css");

            CompilerDiagnostic diagnostic = _diagnostics.Diagnostics.Single();
            Assert.That(diagnostic.Code, Is.EqualTo(DiagnosticCodes.Css.CircularImport));
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
        }

        [Test]
        public void IndirectCycle_IsAnError()
        {
            _loader.Add("Assets/a.css", "@import \"./b.css\";");
            _loader.Add("Assets/b.css", "@import \"./c.css\";");
            _loader.Add("Assets/c.css", "@import \"./a.css\";");

            IReadOnlyList<string> resolved = ResolvedPaths("Assets/a.css");

            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Css.CircularImport));
            Assert.That(resolved, Is.EqualTo(new[] { "Assets/c.css", "Assets/b.css", "Assets/a.css" }));
        }

        [Test]
        public void CycleDiagnostic_NamesTheChain()
        {
            _loader.Add("Assets/a.css", "@import \"./b.css\";");
            _loader.Add("Assets/b.css", "@import \"./a.css\";");

            ResolvedPaths("Assets/a.css");

            Assert.That(
                _diagnostics.Diagnostics.Single().Message,
                Does.Contain("Assets/a.css -> Assets/b.css -> Assets/a.css"));
        }

        [Test]
        public void MissingSheet_IsAnError()
        {
            _loader.Add("Assets/a.css", "@import \"./missing.css\";");

            ResolvedPaths("Assets/a.css");

            CompilerDiagnostic diagnostic = _diagnostics.Diagnostics.Single();
            Assert.That(diagnostic.Code, Is.EqualTo(DiagnosticCodes.Asset.NotFound));
            Assert.That(diagnostic.Message, Does.Contain("Assets/missing.css"));
        }

        [Test]
        public void MissingEntrySheet_IsAnError()
        {
            ResolvedPaths("Assets/nothing.css");

            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Asset.NotFound));
        }

        [Test]
        public void ImportedRules_ParticipateInTheCascade()
        {
            _loader.Add("Assets/theme.css", "@import \"./base.css\";\ndiv { color: red; }");
            _loader.Add("Assets/base.css", "div { width: 10px; }");

            IReadOnlyList<CssStyleSheet> sheets = new CssImportResolver(_loader)
                .Resolve(new[] { "Assets/theme.css" }, _diagnostics);

            Assert.That(sheets.Sum(s => s.Rules.Count), Is.EqualTo(2));
        }

        [Test]
        public void NullArguments_Throw()
        {
            var resolver = new CssImportResolver(_loader);

            Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!, _diagnostics));
            Assert.Throws<ArgumentNullException>(() => resolver.Resolve(Array.Empty<string>(), null!));
            Assert.Throws<ArgumentNullException>(() => new CssImportResolver(null!));
        }

        /// <summary>
        /// An in-memory stylesheet loader, so import behaviour can be exercised without a project
        /// on disk.
        /// </summary>
        private sealed class DictionaryLoader : ICssSourceLoader
        {
            private readonly Dictionary<string, string> _files =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            internal void Add(string assetPath, string source)
            {
                _files[assetPath] = source;
            }

            public bool TryLoad(string assetPath, out string source)
            {
                return _files.TryGetValue(assetPath, out source!);
            }
        }
    }
}
