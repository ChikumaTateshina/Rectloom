#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// Compiles the Feature Showcase sample that ships with the package.
    /// </summary>
    /// <remarks>
    /// The sample claims to use every feature the compiler supports and nothing it does not, which is a
    /// claim that goes stale the moment either side changes. Compiling the shipped files here is what
    /// keeps it true: a property the sample uses that the compiler stops accepting shows up as a
    /// warning, and a warning fails this test.
    /// </remarks>
    public sealed class FeatureShowcaseSampleTests
    {
        private const string PackagePath = "Packages/com.chikumatateshina.rectloom.ugui";
        private const string SampleFolder = "Samples~/FeatureShowcase";

        private const string Folder = "Assets/RectloomFeatureShowcaseTest";
        private const string HtmlPath = Folder + "/showcase.html";
        private const string PrefabPath = Folder + "/showcase.prefab";
        private const string GeneratedFolder = Folder + "/Generated";

        private readonly List<GameObject> _created = new List<GameObject>();

        private CompileResult _result = null!;

        [SetUp]
        public void SetUp()
        {
            // Batch mode has no graphics device and a bare project has no Japanese font asset; neither
            // says anything about the sample.
            LogAssert.ignoreFailingMessages = true;

            // Samples~ is hidden from the asset database, so the files are copied the way the Package
            // Manager's Import button would copy them.
            string package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(PackagePath).resolvedPath;
            string source = Path.Combine(package, SampleFolder);
            Directory.CreateDirectory(Folder);

            foreach (string file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(Folder, Path.GetFileName(file)), overwrite: true);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            _result = new UguiHtmlUiCompiler(measurerFactory: () => new FixedMeasurer()).Compile(
                new CompileRequest
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
                });
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

        private string Describe()
        {
            return string.Join("\n", _result.Diagnostics.Select(d => d.ToString()));
        }

        private GameObject Instantiate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, "no prefab was written");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            _created.Add(instance);
            return instance;
        }

        private static Transform Find(GameObject root, string name)
        {
            Transform? found = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            Assert.That(found, Is.Not.Null, "no object named '" + name + "'");
            return found!;
        }

        /// <summary>
        /// Diagnostics that describe the machine rather than the sample: which fonts are installed and
        /// whether the optional SVG package is present, and which extensions happen to be loaded.
        /// </summary>
        private static bool IsAboutTheEnvironment(CompilerDiagnostic diagnostic)
        {
            // Extensions are discovered from every loaded assembly, the test assemblies included.
            if (diagnostic.Code == DiagnosticCodes.Extension.ExtensionConstructionFailed)
            {
                return true;
            }

            if (diagnostic.Message.IndexOf("font", StringComparison.OrdinalIgnoreCase) >= 0
                || diagnostic.Message.IndexOf("emoji", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return !SvgRasterizer.Instance.IsAvailable
                && diagnostic.Message.IndexOf("SVG", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        [Test]
        public void TheSampleCompiles()
        {
            Assert.That(_result.Success, Is.True, Describe());
        }

        [Test]
        public void TheSampleUsesNothingTheCompilerWarnsAbout()
        {
            Assert.That(
                _result.Diagnostics
                    .Where(d => d.Severity >= DiagnosticSeverity.Warning)
                    .Where(d => !IsAboutTheEnvironment(d))
                    .Select(d => d.ToString()),
                Is.Empty);
        }

        [Test]
        public void ThePageFillsItsCanvasAndNothingMore()
        {
            GameObject root = Instantiate();
            var canvas = (RectTransform)root.transform;

            Assert.That(canvas.rect.width, Is.EqualTo(1920f).Within(0.01f));
            Assert.That(canvas.rect.height, Is.EqualTo(1080f).Within(0.01f));

            var header = (RectTransform)Find(root, "header");
            var columns = (RectTransform)Find(root, "columns");
            var footer = (RectTransform)Find(root, "footer");

            Assert.That(header.rect.height, Is.EqualTo(110f).Within(0.01f));
            Assert.That(footer.rect.height, Is.EqualTo(60f).Within(0.01f));
            Assert.That(columns.rect.height, Is.EqualTo(910f).Within(0.01f), "what the header and footer leave");
        }

        [Test]
        public void EveryCardFitsInsideItsColumn()
        {
            GameObject root = Instantiate();
            var columns = (RectTransform)Find(root, "columns");

            // 16 pixels of padding on each side of a 910 pixel band.
            const float Available = 910f - 32f;

            foreach (RectTransform column in columns)
            {
                float bottom = 0f;

                foreach (RectTransform card in column)
                {
                    bottom = Mathf.Max(bottom, -card.anchoredPosition.y + card.rect.height * (1f - card.pivot.y));
                }

                Assert.That(bottom, Is.LessThanOrEqualTo(Available + 0.5f), column.name + " overflows");
            }
        }

        [Test]
        public void TheMarkupReachesUnityComponents()
        {
            GameObject root = Instantiate();

            Assert.That(root.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length, Is.EqualTo(3));
            Assert.That(
                root.GetComponentsInChildren<UnityEngine.UI.Button>(true).Count(b => !b.interactable),
                Is.EqualTo(1),
                "unity-interactable: false");

            Assert.That(Find(root, "ok-button").GetComponent<UnityEngine.UI.Button>(), Is.Not.Null);

            CanvasGroup group = Find(root, "group").GetComponent<CanvasGroup>();
            Assert.That(group, Is.Not.Null, "component=\"UnityEngine.CanvasGroup\"");
            Assert.That(group.alpha, Is.EqualTo(0.85f).Within(0.001f), "component.alpha");

            Assert.That(
                root.GetComponentsInChildren<UnityEngine.UI.RectMask2D>(true).Length,
                Is.GreaterThanOrEqualTo(2),
                "overflow: hidden");

            Assert.That(
                root.GetComponentsInChildren<UnityEngine.UI.Image>(true).Count(i =>
                    i.sprite != null && AssetDatabase.GetAssetPath(i.sprite) == Folder + "/badge.png"),
                Is.EqualTo(5),
                "four img elements and one background-image");
        }

        private sealed class FixedMeasurer : ITextMeasurer
        {
            public TextMeasurement Measure(string text, Core.Css.Computed.TextStyle style, float availableWidth)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return TextMeasurement.Empty;
                }

                // Half an em per character, wrapped at the available width, which is close enough to a
                // real font for the cards to be as tall as they will be in the Editor.
                float width = text.Length * style.FontSize * 0.5f;
                int lines = 1;

                if (style.WrapsText && availableWidth > 0f && !float.IsPositiveInfinity(availableWidth)
                    && width > availableWidth)
                {
                    lines = Mathf.CeilToInt(width / availableWidth);
                    width = availableWidth;
                }

                return new TextMeasurement(width, lines * style.FontSize * style.LineHeight, lines);
            }
        }
    }
}
