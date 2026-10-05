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
