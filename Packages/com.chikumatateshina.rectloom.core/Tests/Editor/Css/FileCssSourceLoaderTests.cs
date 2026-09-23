#nullable enable

using System;
using System.IO;
using NUnit.Framework;
using Rectloom.Core.Css.Parsing;

namespace Rectloom.Core.Tests.Css
{
    public sealed class FileCssSourceLoaderTests
    {
        private string _root = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rectloom-loader-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "Assets", "UI"));
            File.WriteAllText(Path.Combine(_root, "Assets", "UI", "theme.css"), "div { color: red; }");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        [Test]
        public void TryLoad_ReadsAnAssetPathRelativeToTheProjectRoot()
        {
            var loader = new FileCssSourceLoader(_root);

            Assert.That(loader.TryLoad("Assets/UI/theme.css", out string source), Is.True);
            Assert.That(source, Is.EqualTo("div { color: red; }"));
        }

        [Test]
        public void TryLoad_ReportsAMissingFileWithoutThrowing()
        {
            var loader = new FileCssSourceLoader(_root);

            Assert.That(loader.TryLoad("Assets/UI/missing.css", out string source), Is.False);
            Assert.That(source, Is.Empty);
        }

        [Test]
        public void TryLoad_WithNullPath_Throws()
        {
            var loader = new FileCssSourceLoader(_root);

            Assert.Throws<ArgumentNullException>(() => loader.TryLoad(null!, out _));
        }

        [Test]
        public void ProjectRoot_DefaultsToTheOpenProject()
        {
            var loader = new FileCssSourceLoader();

            Assert.That(loader.ProjectRoot, Is.Not.Empty);
            Assert.That(Directory.Exists(Path.Combine(loader.ProjectRoot, "Assets")), Is.True);
        }

        [Test]
        public void ItResolvesImportsThroughTheFileSystem()
        {
            File.WriteAllText(
                Path.Combine(_root, "Assets", "UI", "entry.css"),
                "@import \"./theme.css\";\np { color: blue; }");

            var diagnostics = new Rectloom.Core.Diagnostics.DiagnosticSink();
            var resolver = new CssImportResolver(new FileCssSourceLoader(_root));

            var sheets = resolver.Resolve(new[] { "Assets/UI/entry.css" }, diagnostics);

            Assert.That(diagnostics.Diagnostics, Is.Empty);
            Assert.That(sheets.Count, Is.EqualTo(2));
            Assert.That(sheets[0].FilePath, Is.EqualTo("Assets/UI/theme.css"));
            Assert.That(sheets[1].FilePath, Is.EqualTo("Assets/UI/entry.css"));
        }
    }
}
