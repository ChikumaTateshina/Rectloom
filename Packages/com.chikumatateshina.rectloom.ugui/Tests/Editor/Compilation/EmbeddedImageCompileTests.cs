#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rectloom.Core.Compilation;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Layout;
using Rectloom.Ugui.Backend;
using Rectloom.Ugui.Compilation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rectloom.Ugui.Tests.Compilation
{
    /// <summary>
    /// Compiles a document whose image is embedded as a <c>data:</c> URI, through the real store and the
    /// real asset resolver.
    /// </summary>
    /// <remarks>
    /// The other embedded-image tests either decode without writing or write without compiling. This one
    /// does the whole thing, because what broke once was neither step on its own but the order they
    /// happen in: the image is written and imported while a compile is already running, and whether the
    /// asset resolver can see it immediately afterwards decides whether the compile fails.
    /// </remarks>
    public sealed class EmbeddedImageCompileTests
    {
        private const string Folder = "Assets/RectloomEmbeddedCompileTest";
        private const string HtmlPath = Folder + "/page.html";
        private const string PrefabPath = Folder + "/page.prefab";
        private const string GeneratedFolder = Folder + "/Generated";

        /// <summary>A one pixel opaque red PNG.</summary>
        private const string PngDataUri =
            "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z"
            + "8DwHwAFAAH/iZk9HQAAAABJRU5ErkJggg==";

        private readonly List<GameObject> _created = new List<GameObject>();
        private readonly List<Texture2D> _textures = new List<Texture2D>();

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;

            Directory.CreateDirectory(Folder);
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

            foreach (Texture2D texture in _textures)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            _textures.Clear();

            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        private void WriteDocument(string body)
        {
            File.WriteAllText(
                HtmlPath,
                "<html><head><style>img { width: 64px; height: 64px; }</style></head><body>"
                    + body + "</body></html>",
                new UTF8Encoding(false));

            AssetDatabase.ImportAsset(HtmlPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static CompileRequest Request()
        {
            return new CompileRequest
            {
                HtmlAssetPath = HtmlPath,
                CssAssetPaths = Array.Empty<string>(),
                OutputType = CompileOutputType.Prefab,
                OutputPath = PrefabPath,
                CompileMode = CompileMode.Create,
                Options = new CompilerOptions
                {
                    ReferenceResolution = new Vector2(1920f, 1080f),
                    GeneratedAssetFolder = GeneratedFolder,
                },
            };
        }

        private static string Describe(CompileResult result)
        {
            return string.Join("\n", result.Diagnostics.Select(d => d.ToString()));
        }

        /// <summary>
        /// The real store and resolver, with only text measurement stubbed so the result does not depend
        /// on a font being present.
        /// </summary>
        private static UguiHtmlUiCompiler Compiler()
        {
            return new UguiHtmlUiCompiler(measurerFactory: () => new FixedMeasurer());
        }

        [Test]
        public void AnEmbeddedImage_BecomesAnAssetTheCompileCanResolve()
        {
            WriteDocument("<img id=\"logo\" src=\"" + PngDataUri + "\">");

            CompileResult result = Compiler().Compile(Request());

            Assert.That(result.Success, Is.True, Describe(result));
            Assert.That(
                result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Error),
                Is.Empty,
                Describe(result));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, "no prefab was written");

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            _created.Add(instance);

            UnityEngine.UI.Image? image = instance.GetComponentsInChildren<UnityEngine.UI.Image>(true)
                .FirstOrDefault(i => i.sprite != null);

            Assert.That(image, Is.Not.Null, "the embedded image did not reach an Image component");
        }

        [Test]
        public void AnEmbeddedImage_BesideAnEmoji_StillResolves()
        {
            // Emoji make the compiler resolve a font before the image is written, which means asset
            // database work happens earlier in the same pass. That ordering is what a regression here
            // would come from, so the two are compiled together rather than apart.
            WriteDocument("<p>hello \U0001F642</p><img id=\"logo\" src=\"" + PngDataUri + "\">");

            CompileResult result = Compiler().Compile(Request());

            Assert.That(
                result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Error),
                Is.Empty,
                Describe(result));

            Assert.That(result.Success, Is.True, Describe(result));
        }

        /// <summary>A 20 by 10 red rectangle, written as text with its colour percent-encoded.</summary>
        private const string RedSvg =
            "data:image/svg+xml,<svg xmlns='http://www.w3.org/2000/svg' width='20' height='10' "
            + "viewBox='0 0 20 10'><rect width='20' height='10' fill='%23ff0000'/></svg>";

        private static string[] StoredImages()
        {
            return AssetDatabase
                .FindAssets("t:Texture2D", new[] { GeneratedFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.Contains("/Embedded/"))
                .Distinct()
                .ToArray();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AnEmbeddedSvg_IsRenderedIntoAnOrdinarySprite(bool base64)
        {
            Assume.That(SvgRasterizer.Instance.IsAvailable, "needs the Vector Graphics package");

            string uri = base64
                ? "data:image/svg+xml;base64," + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                    Uri.UnescapeDataString(RedSvg.Substring(RedSvg.IndexOf(',') + 1))))
                : RedSvg;
            WriteDocument("<img id=\"vector\" src=\"" + uri + "\">");

            CompileResult result = Compiler().Compile(Request());

            Assert.That(result.Success, Is.True, Describe(result));
            Assert.That(
                result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning
                    && d.Code != DiagnosticCodes.Layout.ContentOutsideRoot),
                Is.Empty,
                Describe(result));

            string stored = StoredImages().Single();
            Assert.That(stored, Does.EndWith("-128x128.png"), "named for its source and the size it was fitted to");

            // Decoded from the file rather than read from the imported texture, which is not readable.
            var pixels = new Texture2D(2, 2);
            _textures.Add(pixels);
            pixels.LoadImage(File.ReadAllBytes(stored));

            Assert.That(pixels.width, Is.EqualTo(128), "a 64px box at the default 2x scale");
            Assert.That(pixels.height, Is.EqualTo(64), "the image's own 2:1 shape, not the box's");

            Color centre = pixels.GetPixel(pixels.width / 2, pixels.height / 2);
            Assert.That(centre.r, Is.GreaterThan(0.9f));
            Assert.That(centre.g, Is.LessThan(0.1f));
            Assert.That(centre.a, Is.GreaterThan(0.9f));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            _created.Add(instance);

            var image = instance.GetComponentsInChildren<Transform>(true)
                .First(t => t.name == "vector")
                .GetComponent<UnityEngine.UI.Image>();

            Assert.That(image.sprite, Is.Not.Null, "an ordinary Image with an ordinary sprite");
            Assert.That(AssetDatabase.GetAssetPath(image.sprite), Is.EqualTo(stored));
        }

        [Test]
        public void AnEmbeddedSvg_IsRenderedLargerWhenAskedTo()
        {
            Assume.That(SvgRasterizer.Instance.IsAvailable, "needs the Vector Graphics package");

            WriteDocument("<img id=\"vector\" src=\"" + RedSvg + "\">");

            CompileRequest request = Request();
            request.Options.VectorImageScale = 4f;

            Assert.That(Compiler().Compile(request).Success, Is.True);
            Assert.That(StoredImages().Single(), Does.EndWith("-256x256.png"));
            Assert.That(AssetDatabase.LoadAssetAtPath<Rectloom.Core.Metadata.RectloomDocumentMetadata>(
                Rectloom.Core.Metadata.MetadataStore.GetMetadataPath(request)).VectorImageScale, Is.EqualTo(4f));
        }

        [Test]
        public void Base64SvgBackground_IsStoredAndReusedOnUpdate()
        {
            string uri = "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(
                Uri.UnescapeDataString(RedSvg.Substring(RedSvg.IndexOf(',') + 1))));
            WriteDocument("<div id=\"vector\" style=\"width:64px;height:64px;background-image:url('" + uri + "')\"></div>");
            var request = Request();
            Assert.That(Compiler().Compile(request).Success, Is.True);
            string stored = StoredImages().Single();
            string guid = AssetDatabase.AssetPathToGUID(stored);
            request.CompileMode = CompileMode.Update;
            Assert.That(Compiler().Compile(request).Success, Is.True);
            Assert.That(StoredImages(), Is.EqualTo(new[] { stored }));
            Assert.That(AssetDatabase.AssetPathToGUID(stored), Is.EqualTo(guid));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var image = prefab.GetComponentsInChildren<Transform>(true).First(t => t.name == "vector")
                .GetComponent<UnityEngine.UI.Image>();
            Assert.That(image.sprite, Is.Not.Null);
        }

        [Test]
        public void AnSvgThatCannotBeRendered_CostsOnlyItself()
        {
            // One image that cannot be drawn is not a reason to produce nothing: the rest compiles, the
            // image's box keeps its place, and exactly one diagnostic says which image is missing.
            WriteDocument(
                "<p id=\"text\">kept</p><img id=\"vector\" src=\"data:image/svg+xml,not an svg at all\">"
                    + "<img id=\"logo\" src=\"" + PngDataUri + "\">");

            CompileResult result = Compiler().Compile(Request());

            Assert.That(result.Success, Is.True, Describe(result));

            CompilerDiagnostic[] aboutImages = result.Diagnostics
                .Where(d => d.Code == DiagnosticCodes.Asset.UnsupportedType
                    || d.Code == DiagnosticCodes.Asset.NotFound)
                .Where(d => d.Severity >= DiagnosticSeverity.Warning)
                .ToArray();

            Assert.That(aboutImages.Length, Is.EqualTo(1), Describe(result));
            Assert.That(aboutImages[0].Severity, Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(aboutImages[0].Message, Does.Contain("SVG"));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            _created.Add(instance);

            Transform vector = instance.GetComponentsInChildren<Transform>(true).First(t => t.name == "vector");
            var placeholder = vector.GetComponent<UnityEngine.UI.Image>();

            Assert.That(placeholder.sprite, Is.Null);
            Assert.That(placeholder.color.a, Is.Zero, "no white square where the image was meant to be");
            Assert.That(((RectTransform)vector).rect.width, Is.EqualTo(64f).Within(0.01f));

            Assert.That(
                instance.GetComponentsInChildren<UnityEngine.UI.Image>(true).Any(i => i.sprite != null),
                Is.True,
                "the PNG beside it is unaffected");
        }

        [Test]
        public void AnSvgThatCannotBeRendered_IsAnErrorInStrictMode()
        {
            WriteDocument("<img src=\"data:image/svg+xml,not an svg at all\">");

            CompileRequest request = Request();
            request.Options.StrictMode = true;

            CompileResult result = Compiler().Compile(request);

            Assert.That(result.Success, Is.False);
            Assert.That(
                result.Diagnostics.Any(d => d.Code == DiagnosticCodes.Asset.UnsupportedType
                    && d.Severity == DiagnosticSeverity.Error),
                Is.True,
                Describe(result));
        }

        [Test]
        public void TheSameEmbeddedImageTwice_BecomesOneAsset()
        {
            WriteDocument(
                "<img id=\"a\" src=\"" + PngDataUri + "\"><img id=\"b\" src=\"" + PngDataUri + "\">");

            CompileResult result = Compiler().Compile(Request());

            Assert.That(result.Success, Is.True, Describe(result));

            string[] written = AssetDatabase
                .FindAssets("t:Texture2D", new[] { GeneratedFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.Contains("/Embedded/"))
                .Distinct()
                .ToArray();

            Assert.That(written.Length, Is.EqualTo(1), string.Join(", ", written));
        }

        [Test]
        public void ValidateAlsoWritesTheAsset_SoBothPassesMeasureTheSameWay()
        {
            WriteDocument("<img id=\"logo\" src=\"" + PngDataUri + "\">");

            CompileResult validated = Compiler().Validate(Request());

            Assert.That(
                validated.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Error),
                Is.Empty,
                Describe(validated));
        }

        private sealed class FixedMeasurer : ITextMeasurer
        {
            public TextMeasurement Measure(string text, Core.Css.Computed.TextStyle style, float availableWidth)
            {
                return string.IsNullOrEmpty(text)
                    ? TextMeasurement.Empty
                    : new TextMeasurement(text.Length * style.FontSize * 0.5f, style.FontSize, 1);
            }
        }
    }
}
