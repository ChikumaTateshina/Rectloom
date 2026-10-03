#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rectloom.Core.Assets;
using Rectloom.Core.Compilation;
using Rectloom.Core.Css.Computed;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Ir;
using Rectloom.Core.Layout;
using Rectloom.Ugui.Backend;
using Rectloom.Ugui.Compilation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rectloom.Ugui.Tests.Compilation
{
    /// <summary>
    /// End-to-end compiles: HTML and CSS in, Unity objects out.
    /// </summary>
    /// <remarks>
    /// Sources are supplied in memory so a test never depends on files being imported, but the
    /// output is written for real, because "a prefab is produced" is the thing worth asserting.
    /// </remarks>
    public sealed class UguiHtmlUiCompilerTests
    {
        private const string HtmlPath = "Assets/UI/page.html";
        private const string CssPath = "Assets/UI/page.css";
        private const string OutputFolder = "Assets/RectloomTestOutput";
        private const string PrefabPath = OutputFolder + "/Panel.prefab";

        private const string Markup =
            "<body><div id=\"panel\"><h1>Settings</h1><button id=\"apply\">Apply</button></div></body>";

        private const string Styles =
            "#panel { width: 300px; padding: 10px; background-color: #202020; }"
            + " h1 { font-size: 24px; color: #ffffff; }"
            + " #apply { width: 120px; height: 32px; background-color: #3080ff; color: #ffffff; }";

        private MemorySourceLoader _sources = null!;
        private readonly List<GameObject> _sceneObjects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            // Batch mode has no graphics device and a bare project has no TextMeshPro font
            // assets; neither says anything about the backend. The framework resets this per test,
            // so it is deliberately not cleared in teardown, where the check already ran.
            LogAssert.ignoreFailingMessages = true;

            _sources = new MemorySourceLoader();
            _sources.Add(HtmlPath, Markup);
            _sources.Add(CssPath, Styles);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _sceneObjects)
            {
                if (created != null)
                {
                    UnityEngine.Object.DestroyImmediate(created);
                }
            }

            _sceneObjects.Clear();

            if (AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.DeleteAsset(OutputFolder);
            }

        }

        private UguiHtmlUiCompiler Compiler()
        {
            return new UguiHtmlUiCompiler(
                _sources,
                new EmptyAssetResolver(),
                () => new UnitTextMeasurer());
        }

        private CompileRequest Request(
            CompileOutputType output = CompileOutputType.Prefab,
            CompileMode mode = CompileMode.Create)
        {
            return new CompileRequest
            {
                HtmlAssetPath = HtmlPath,
                CssAssetPaths = new[] { CssPath },
                OutputType = output,
                OutputPath = PrefabPath,
                CompileMode = mode,
                Options = new CompilerOptions
                {
                    UseDefaultStyleSheet = false,
                    GeneratedAssetFolder = OutputFolder + "/Generated",
                },
            };
        }

        private static string Describe(CompileResult result)
        {
            return string.Join(
                "\n",
                result.Diagnostics.Select(d => d.ToString()));
        }

        [Test]
        public void CreatePrefab_WritesAUsablePrefab()
        {
            CompileResult result = Compiler().Compile(Request());

            Assert.That(result.Success, Is.True, Describe(result));
            Assert.That(result.RootObject, Is.Not.Null);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, "the prefab asset should exist on disk");

            Assert.That(prefab.GetComponent<Canvas>(), Is.Not.Null);

            Transform panel = prefab.transform.GetChild(0);
            Assert.That(panel.name, Is.EqualTo("panel"));
            Assert.That(panel.GetComponent<Image>(), Is.Not.Null);

            Transform button = panel.Find("apply");
            Assert.That(button, Is.Not.Null);
            Assert.That(button!.GetComponent<Button>(), Is.Not.Null);
            Assert.That(
                button.Find(UguiBackend.LabelObjectName)!.GetComponent<TMP_Text>().text,
                Is.EqualTo("Apply"));
        }

        [Test]
        public void CreatePrefab_ReportsStatistics()
        {
            CompileResult result = Compiler().Compile(Request());

            Assert.That(result.Statistics.ElementCount, Is.EqualTo(4), "body, div, h1 and button");
            Assert.That(result.Statistics.NodeCount, Is.EqualTo(4));
            Assert.That(result.Statistics.CreatedObjectCount, Is.GreaterThanOrEqualTo(5));
            Assert.That(result.Statistics.TotalMilliseconds, Is.GreaterThan(0d));
        }

        [Test]
        public void CreatePrefab_WhenTheOutputExists_IsRefused()
        {
            Assert.That(Compiler().Compile(Request()).Success, Is.True);

            CompileResult second = Compiler().Compile(Request());

            Assert.That(second.Success, Is.False);
            Assert.That(
                second.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Unity.OutputAlreadyExists));
        }

        [Test]
        public void RebuildPrefab_ReplacesExistingOutput()
        {
            Assert.That(Compiler().Compile(Request()).Success, Is.True);

            CompileResult rebuilt = Compiler().Compile(Request(mode: CompileMode.Rebuild));

            Assert.That(rebuilt.Success, Is.True, Describe(rebuilt));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), Is.Not.Null);
        }

        [Test]
        public void UpdateMode_IsRefusedWithoutTouchingAnything()
        {
            CompileResult result = Compiler().Compile(Request(mode: CompileMode.Update));

            Assert.That(result.Success, Is.False);
            Assert.That(result.RootObject, Is.Null);
            Assert.That(
                result.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Unity.IncrementalCompileUnavailable),
                "regenerating would discard the very data an update exists to preserve");
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), Is.Null);
        }

        [Test]
        public void SceneOutput_LeavesAnObjectInTheScene()
        {
            CompileResult result = Compiler().Compile(Request(CompileOutputType.SceneObject));

            Assert.That(result.Success, Is.True, Describe(result));
            Assert.That(result.RootObject, Is.Not.Null);
            Assert.That(result.RootObject!.scene.IsValid(), Is.True);

            _sceneObjects.Add(result.RootObject);
        }

        [Test]
        public void Validate_ProducesDiagnosticsWithoutCreatingAnything()
        {
            CompileResult result = Compiler().Validate(Request());

            Assert.That(result.Success, Is.True, Describe(result));
            Assert.That(result.RootObject, Is.Null, "validation never touches a Unity object");
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), Is.Null);
            Assert.That(result.Statistics.NodeCount, Is.EqualTo(4));
        }

        [Test]
        public void Validate_StillReportsAuthoringMistakes()
        {
            _sources.Add(CssPath, "#panel { width: 12em; }");

            CompileResult result = Compiler().Validate(Request());

            Assert.That(
                result.Diagnostics.Any(d => d.Code == DiagnosticCodes.Css.InvalidValue),
                Is.True);
        }

        [Test]
        public void MissingHtmlSource_IsFatal()
        {
            CompileRequest request = Request();
            request.HtmlAssetPath = "Assets/UI/nothing.html";

            CompileResult result = Compiler().Compile(request);

            Assert.That(result.Success, Is.False);
            Assert.That(
                result.Diagnostics.Single().Severity,
                Is.EqualTo(DiagnosticSeverity.Fatal));
        }

        [Test]
        public void RequestWithoutAnHtmlPath_IsFatal()
        {
            CompileRequest request = Request();
            request.HtmlAssetPath = null;

            CompileResult result = Compiler().Compile(request);

            Assert.That(
                result.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Internal.InvalidCompileRequest));
        }

        [Test]
        public void PrefabRequestWithoutAnOutputPath_IsFatal()
        {
            CompileRequest request = Request();
            request.OutputPath = null;

            CompileResult result = Compiler().Compile(request);

            Assert.That(result.Success, Is.False);
            Assert.That(
                result.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Internal.InvalidCompileRequest));
        }

        [Test]
        public void EmptyDocument_IsFatal()
        {
            _sources.Add(HtmlPath, "   ");

            CompileResult result = Compiler().Compile(Request());

            Assert.That(result.Success, Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), Is.Null);
        }

        [Test]
        public void MissingImageAsset_FailsBeforeAnythingIsWritten()
        {
            _sources.Add(HtmlPath, "<body><img src=\"./missing.png\"></body>");

            CompileResult result = Compiler().Compile(Request());

            Assert.That(result.Success, Is.False);
            Assert.That(
                result.Diagnostics.Any(d => d.Code == DiagnosticCodes.Asset.NotFound),
                Is.True);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),
                Is.Null,
                "a failed pass commits nothing");
        }

        [Test]
        public void HiddenRoot_IsFatal()
        {
            _sources.Add(CssPath, "body { display: none; }");

            CompileResult result = Compiler().Compile(Request());

            Assert.That(result.Success, Is.False);
            Assert.That(
                result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Fatal),
                Is.True);
        }

        [Test]
        public void StylesheetImports_AreFollowed()
        {
            _sources.Add(CssPath, "@import \"./base.css\";\n#panel { width: 300px; }");
            _sources.Add("Assets/UI/base.css", "#apply { width: 50px; height: 20px; }");

            CompileResult result = Compiler().Compile(Request());

            Assert.That(result.Success, Is.True, Describe(result));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var button = prefab.transform.GetChild(0).Find("apply")!.GetComponent<RectTransform>();

            Assert.That(button.sizeDelta, Is.EqualTo(new Vector2(50f, 20f)));
        }

        [Test]
        public void CompilingTwice_ProducesTheSameHierarchy()
        {
            CompileResult first = Compiler().Compile(Request(CompileOutputType.SceneObject));
            CompileResult second = Compiler().Compile(Request(CompileOutputType.SceneObject));

            _sceneObjects.Add(first.RootObject!);
            _sceneObjects.Add(second.RootObject!);

            Assert.That(Shape(second.RootObject!), Is.EqualTo(Shape(first.RootObject!)));
        }

        private static string Shape(GameObject root)
        {
            var lines = new List<string>();
            Walk(root.transform, 0, lines);
            return string.Join("\n", lines);
        }

        private static void Walk(Transform transform, int depth, List<string> lines)
        {
            var rect = transform as RectTransform;
            string geometry = rect == null
                ? string.Empty
                : " " + rect.anchoredPosition + " " + rect.sizeDelta;

            lines.Add(new string(' ', depth * 2) + transform.name + geometry);

            foreach (Transform child in transform)
            {
                Walk(child, depth + 1, lines);
            }
        }

        /// <summary>Supplies source files from memory.</summary>
        private sealed class MemorySourceLoader : ISourceTextLoader
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

        /// <summary>A resolver that owns nothing, so every reference misses.</summary>
        private sealed class EmptyAssetResolver : IAssetResolver
        {
            public bool TryResolve<T>(AssetReference reference, out T asset)
                where T : UnityEngine.Object
            {
                asset = null!;
                return false;
            }

            public bool Exists(AssetReference reference) => false;

            public string? GetAssetPath(AssetReference reference) => reference.Value;
        }

        /// <summary>Fixed text metrics, so results do not depend on the installed fonts.</summary>
        private sealed class UnitTextMeasurer : ITextMeasurer
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
