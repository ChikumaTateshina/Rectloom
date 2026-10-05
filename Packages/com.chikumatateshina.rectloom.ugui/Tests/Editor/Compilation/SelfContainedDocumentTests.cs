#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rectloom.Core.Assets;
using Rectloom.Core.Compilation;
using Rectloom.Core.Css;
using Rectloom.Core.Css.Ast;
using Rectloom.Core.Css.Cascade;
using Rectloom.Core.Css.Computed;
using Rectloom.Core.Css.Parsing;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Dom;
using Rectloom.Core.Ir;
using Rectloom.Core.Layout;
using Rectloom.Core.Parsing;
using Rectloom.Ugui.Compilation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rectloom.Ugui.Tests.Compilation
{
    /// <summary>
    /// Compiles a self-contained HTML document of the kind a design tool exports: one file with an
    /// embedded stylesheet, physical units, custom properties, a wrapping flex body, baseline-aligned
    /// rows and an image carried inline as a <c>data:</c> URI.
    /// </summary>
    /// <remarks>
    /// This is a regression test for a whole class of failure rather than for one property. A document
    /// like this used to be unusable: its stylesheet was laid out as a paragraph of text because
    /// <c>style</c> was parsed as markup, every <c>mm</c> was rejected, every <c>var()</c> resolved to
    /// nothing, and the embedded image failed asset resolution and so failed the compile outright. Each
    /// of those is covered on its own elsewhere; what this asserts is that they add up to a document
    /// that compiles.
    /// </remarks>
    public sealed class SelfContainedDocumentTests
    {
        private const string HtmlPath = "Assets/UI/caption.html";
        private const string GeneratedFolder = "Assets/RectloomSelfContainedTest";
        private const string StoredImagePath = "Assets/Generated/Embedded/image.png";

        /// <summary>A one pixel opaque red PNG, standing in for an exported photograph.</summary>
        private const string PngDataUri =
            "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z"
            + "8DwHwAFAAH/iZk9HQAAAABJRU5ErkJggg==";

        private const string Markup = @"<!DOCTYPE html>
<html lang=""ja"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>宙に浮かぶ大銀河</title>
<style>
*, *::before, *::after { box-sizing: border-box; }
html, body { margin: 0; padding: 0; }
body {
  display: flex;
  flex-wrap: wrap;
  gap: 8mm;
  padding: 8mm;
  background: #e8e8e4;
}
.caption {
  flex: none;
  position: relative;
  margin: 0 auto;
  width: var(--caption-width);
  height: var(--caption-height);
  overflow: hidden;
  background: #343434;
  color: #1a1a1a;
  font-family: ""Noto Sans JP"", ""Yu Gothic"", sans-serif;
}
@page { size: 1920px 1080px; margin: 0; }
@media print {
  body { display: block; padding: 0; background: none; }
  .caption { break-after: page; break-inside: avoid; }
}
:root {
  --caption-width: 1920px;
  --caption-height: 1080px;
  --comment-max-lines: 5;
}
.caption .b { position: absolute; margin: 0; }
.caption .t { display: flex; align-items: baseline; }
.caption .s { flex: none; width: 0; }
.caption .t p { flex: 1; min-width: 0; margin: 0; }
.caption .v { display: flex; flex-direction: column; }
.b2 {
  top: 0mm;
  left: 26.458mm;
  width: 132.292mm;
  font-size: 13.229mm;
  line-height: 1.5;
  color: #ffffff;
  letter-spacing: 0.201mm;
}
.b2 .s { height: 26.458mm; }
.b2 p {
  white-space: pre-line;
  overflow-wrap: anywhere;
  overflow: hidden;
  max-height: 1.5em;
}
.b3 {
  top: 60.762mm;
  height: 46.302mm;
  justify-content: center;
  left: 26.458mm;
  width: 455.083mm;
  font-size: 18.521mm;
  line-height: 1.25;
  color: #ffffff;
}
.b4 {
  left: 26.458mm;
  top: 115.094mm;
  width: 455.083mm;
  height: 2.646mm;
  background: #ffffff;
}
.b10 {
  left: 441.854mm;
  top: 238.972mm;
  width: 39.688mm;
  height: 39.688mm;
  object-fit: contain;
}
</style>
</head>
<body>
<article class=""caption"" data-entry-number=""1"">
<div class=""b t b2""><span class=""s""></span><p>リアル部門</p></div>
<div class=""b v b3""><p id=""title"">宙に浮かぶ大銀河</p></div>
<div class=""b b4""></div>
<img id=""logo"" class=""b b10"" alt="""" src=""{SRC}"">
</article>
</body>
</html>";

        private static readonly string Document = Markup.Replace("{SRC}", PngDataUri);

        private readonly List<GameObject> _created = new List<GameObject>();

        private MemorySources _sources = null!;
        private FakeImageStore _images = null!;
        private StoredAssetResolver _assets = null!;

        [SetUp]
        public void SetUp()
        {
            // Batch mode has no graphics device and a bare project has no Japanese font asset; neither
            // says anything about the compiler.
            LogAssert.ignoreFailingMessages = true;

            _sources = new MemorySources();
            _sources.Add(HtmlPath, Document);
            _images = new FakeImageStore(StoredImagePath);
            _assets = new StoredAssetResolver(StoredImagePath);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _created)
            {
                if (created != null)
                {
                    UnityEngine.Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            _assets.Dispose();

            // A scene-object compile writes its metadata beside the generated assets, so the folder has
            // to go with it or the next run would find a record and try to update instead of create.
            if (AssetDatabase.IsValidFolder(GeneratedFolder))
            {
                AssetDatabase.DeleteAsset(GeneratedFolder);
            }
        }

        private CompileResult Validate()
        {
            var compiler = new UguiHtmlUiCompiler(
                _sources,
                _assets,
                () => new UnitMeasurer(),
                _images);

            return compiler.Validate(new CompileRequest
            {
                HtmlAssetPath = HtmlPath,
                CssAssetPaths = Array.Empty<string>(),
                Options = new CompilerOptions
                {
                    ReferenceResolution = new Vector2(1920f, 1080f),
                    GeneratedAssetFolder = GeneratedFolder,
                },
            });
        }

        private static string Describe(CompileResult result)
        {
            return string.Join("\n", result.Diagnostics.Select(d => d.ToString()));
        }

        /// <summary>
        /// Runs the document through the same front-end stages the compiler does, and hands back the IR.
        /// </summary>
        /// <remarks>
        /// The compile result carries the generated objects rather than the IR, and the assertions here
        /// are about what was computed rather than about which components it turned into. The stylesheet
        /// resolution step is the one that matters: it is where the document's own <c>style</c> element
        /// becomes an author stylesheet.
        /// </remarks>
        private UiNode BuildIr()
        {
            var diagnostics = new DiagnosticSink();
            var options = new CompilerOptions { GeneratedAssetFolder = GeneratedFolder };

            Assert.That(_sources.TryLoad(HtmlPath, out string html), Is.True);
            DomDocument document = new HtmlParser().Parse(HtmlPath, html, diagnostics);

            IReadOnlyList<CssStyleSheet> sheets = new CssImportResolver(_sources)
                .Resolve(Array.Empty<string>(), document.StyleSheets, HtmlPath, diagnostics);

            ComputedStyleTree styles = ComputedStyleTree.Build(
                document,
                new CascadeResolver(new[] { DefaultStyleSheet.Get() }, sheets),
                new ComputedStyleBuilder(),
                diagnostics);

            LayoutBox? layoutRoot = LayoutTreeBuilder.Build(document, styles);
            Assert.That(layoutRoot, Is.Not.Null);

            LayoutResult solved = new LayoutSolver(new UnitMeasurer(), diagnostics)
                .Solve(layoutRoot!, new Vector2(1920f, 1080f));

            return new UiTreeBuilder(HtmlPath, options, diagnostics, _images).Build(solved);
        }

        private static UiNode Find(UiNode root, string stableId)
        {
            UiNode? found = root.DescendantsAndSelf().FirstOrDefault(n => n.StableId == stableId);
            Assert.That(found, Is.Not.Null, "no node with stable id '" + stableId + "'");
            return found!;
        }

        [Test]
        public void TheDocumentCompilesWithoutErrors()
        {
            CompileResult result = Validate();

            Assert.That(
                result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Error),
                Is.Empty,
                Describe(result));
        }

        [Test]
        public void CompilingToASceneObjectAppliesTheEmbeddedStyleSheet()
        {
            var compiler = new UguiHtmlUiCompiler(
                _sources,
                _assets,
                () => new UnitMeasurer(),
                _images);

            CompileResult result = compiler.Compile(new CompileRequest
            {
                HtmlAssetPath = HtmlPath,
                CssAssetPaths = Array.Empty<string>(),
                OutputType = CompileOutputType.SceneObject,
                CompileMode = CompileMode.Create,
                Options = new CompilerOptions
                {
                    ReferenceResolution = new Vector2(1920f, 1080f),
                    GeneratedAssetFolder = GeneratedFolder,
                },
            });

            Assert.That(result.Success, Is.True, Describe(result));
            Assert.That(result.RootObject, Is.Not.Null);
            _created.Add(result.RootObject!);

            // The size can only come from the custom properties the embedded stylesheet declares, so a
            // correctly sized article proves the <style> element reached the cascade.
            Transform article = result.RootObject!.transform.GetChild(0);
            var rect = (RectTransform)article;

            Assert.That(rect.rect.width, Is.EqualTo(1920f).Within(0.01f));
            Assert.That(rect.rect.height, Is.EqualTo(1080f).Within(0.01f));
            Assert.That(article.GetComponent<UnityEngine.UI.RectMask2D>(), Is.Not.Null, "overflow: hidden");
        }

        [Test]
        public void TheEmbeddedStyleSheetIsApplied()
        {
            UiNode root = BuildIr();

            // 1920x1080 comes from the custom properties declared on :root, which only reach the
            // article if the <style> element was read as CSS and var() was substituted.
            UiNode caption = root.Children.Single();

            Assert.That(caption.Rect.Width, Is.EqualTo(1920f).Within(0.01f));
            Assert.That(caption.Rect.Height, Is.EqualTo(1080f).Within(0.01f));
            Assert.That(caption.Visual.ClipsContent, Is.True, "overflow: hidden");
        }

        [Test]
        public void MillimetresBecomePixels()
        {
            UiNode root = BuildIr();
            UiNode caption = root.Children.Single();
            UiNode rule = Find(root, caption.StableId + "/div[2]");

            // 455.083mm at 96 pixels per inch, and 2.646mm tall.
            Assert.That(rule.Rect.Width, Is.EqualTo(1720.0f).Within(0.5f));
            Assert.That(rule.Rect.Height, Is.EqualTo(10.0f).Within(0.5f));
            Assert.That(rule.Rect.X, Is.EqualTo(100.0f).Within(0.5f), "26.458mm from the left");
        }

        [Test]
        public void TheStyleSheetIsNotRenderedAsText()
        {
            UiNode root = BuildIr();

            foreach (UiNode node in root.DescendantsAndSelf())
            {
                Assert.That(node.TextContent ?? string.Empty, Does.Not.Contain("box-sizing"));
                Assert.That(node.TextContent ?? string.Empty, Does.Not.Contain("--caption-width"));
            }
        }

        [Test]
        public void TheTitleAndMetaElementsProduceNoObjects()
        {
            UiNode root = BuildIr();

            Assert.That(root.Children.Count, Is.EqualTo(1), "only the article");

            foreach (UiNode node in root.DescendantsAndSelf())
            {
                Assert.That(node.SourceTag, Is.Not.EqualTo("title"));
                Assert.That(node.SourceTag, Is.Not.EqualTo("meta"));
            }
        }

        [Test]
        public void TheEmbeddedImageBecomesAProjectAsset()
        {
            UiNode logo = Find(BuildIr(), "logo");

            Assert.That(logo.Kind, Is.EqualTo(UiNodeKind.Image));
            Assert.That(logo.Asset.Value, Is.EqualTo(StoredImagePath));
            Assert.That(logo.Visual.ImageFit, Is.EqualTo(UiImageFit.Contain), "object-fit: contain");
            Assert.That(_images.StoredBytes, Is.EqualTo(70), "the decoded PNG");
        }

        [Test]
        public void JustifyContentCentresTheTitleInItsBand()
        {
            UiNode root = BuildIr();
            UiNode title = Find(root, "title");

            // The band is 46.302mm tall, the title one 18.521mm line at 1.25, so it sits in the middle.
            float band = 46.302f * 96f / 25.4f;
            float line = 18.521f * 96f / 25.4f * 1.25f;

            Assert.That(title.Rect.Y, Is.EqualTo((band - line) / 2f).Within(1f));
        }

        [Test]
        public void TheBaselineSpacerPushesTheTextBesideItDown()
        {
            UiNode root = BuildIr();
            UiNode caption = root.Children.Single();
            UiNode row = Find(root, caption.StableId + "/div[0]");

            Assert.That(row.Children.Count, Is.EqualTo(2), "the spacer and the paragraph");

            UiNode spacer = row.Children[0];
            UiNode text = row.Children[1];

            // The spacer is 26.458mm tall, so 100 pixels. The paragraph is set at 13.229mm, so 50
            // pixels, over a 1.5 line height: 12.5 of half leading plus a 40 pixel ascent puts its
            // first baseline 52.5 down. Meeting the spacer's bottom edge therefore drops it by 47.5.
            Assert.That(spacer.Rect.Y, Is.Zero);
            Assert.That(spacer.Rect.Height, Is.EqualTo(100f).Within(0.01f));
            Assert.That(
                text.Rect.Y,
                Is.EqualTo(47.5f).Within(0.05f),
                "the paragraph's first baseline meets the spacer's bottom edge");
        }

        [Test]
        public void PrintOnlyRulesAreReportedAsInformation()
        {
            CompileResult result = Validate();

            IReadOnlyList<CompilerDiagnostic> atRules = result.Diagnostics
                .Where(d => d.Code == DiagnosticCodes.Css.UnsupportedAtRule)
                .ToList();

            Assert.That(atRules.Count, Is.EqualTo(2), "@page and @media print");
            Assert.That(
                atRules.All(d => d.Severity == DiagnosticSeverity.Info),
                Is.True,
                Describe(result));
        }

        [Test]
        public void NothingInTheDocumentIsReportedAsAnUnknownProperty()
        {
            CompileResult result = Validate();

            Assert.That(
                result.Diagnostics
                    .Where(d => d.Code == DiagnosticCodes.Css.UnknownProperty)
                    .Select(d => d.Message),
                Is.Empty);
        }

        [Test]
        public void ArticleIsNotReportedAsAnUnknownElement()
        {
            CompileResult result = Validate();

            Assert.That(
                result.Diagnostics
                    .Where(d => d.Code == DiagnosticCodes.Html.UnknownElement)
                    .Select(d => d.Message),
                Is.Empty);
        }

        /// <summary>Source loader backed by strings, so no file has to be imported.</summary>
        private sealed class MemorySources : ISourceTextLoader
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

        /// <summary>
        /// Records what an embedded image would have been written as, without touching the project.
        /// </summary>
        private sealed class FakeImageStore : IEmbeddedImageStore
        {
            private readonly string _assetPath;

            internal FakeImageStore(string assetPath)
            {
                _assetPath = assetPath;
            }

            internal int StoredBytes { get; private set; }

            public bool TryStore(
                DataUriPayload payload,
                string folder,
                out string assetPath,
                out string error)
            {
                StoredBytes = payload.Bytes.Length;
                assetPath = _assetPath;
                error = string.Empty;
                return true;
            }
        }

        /// <summary>
        /// A resolver that owns exactly the path the fake store hands out, backed by a sprite made in
        /// memory so the backend can finish without a file being imported.
        /// </summary>
        private sealed class StoredAssetResolver : IAssetResolver, IDisposable
        {
            private readonly string _assetPath;
            private readonly Texture2D _texture;
            private readonly Sprite _sprite;

            internal StoredAssetResolver(string assetPath)
            {
                _assetPath = assetPath;
                _texture = new Texture2D(4, 4);
                _sprite = Sprite.Create(_texture, new Rect(0f, 0f, 4f, 4f), Vector2.zero);
            }

            public bool TryResolve<T>(AssetReference reference, out T asset)
                where T : UnityEngine.Object
            {
                if (Exists(reference) && _sprite is T typed)
                {
                    asset = typed;
                    return true;
                }

                asset = null!;
                return false;
            }

            public bool Exists(AssetReference reference)
            {
                return string.Equals(reference.Value, _assetPath, StringComparison.Ordinal);
            }

            public string? GetAssetPath(AssetReference reference) => reference.Value;

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_sprite);
                UnityEngine.Object.DestroyImmediate(_texture);
            }
        }

        /// <summary>
        /// A measurer with fixed metrics, so the assertions are about layout rather than about a font.
        /// </summary>
        private sealed class UnitMeasurer : ITextMeasurer
        {
            public TextMeasurement Measure(string text, TextStyle style, float availableWidth)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return TextMeasurement.Empty;
                }

                return new TextMeasurement(
                    text.Length * style.FontSize * 0.5f,
                    style.FontSize * style.LineHeight,
                    1);
            }
        }
    }
}
