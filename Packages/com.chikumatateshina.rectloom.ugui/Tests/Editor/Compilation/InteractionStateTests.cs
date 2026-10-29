#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rectloom.Core.Compilation;
using Rectloom.Core.Css.Cascade;
using Rectloom.Core.Css.Parsing;
using Rectloom.Core.Css.Selectors;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Layout;
using Rectloom.Ugui.Compilation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rectloom.Ugui.Tests.Compilation
{
    /// <summary>
    /// <c>:hover</c>, <c>:active</c>, <c>:focus</c> and <c>:disabled</c> backgrounds become the colour
    /// tint uGUI applies by state.
    /// </summary>
    public sealed class InteractionStateTests
    {
        private const string Folder = "Assets/RectloomStateTest";
        private const string HtmlPath = Folder + "/page.html";
        private const string PrefabPath = Folder + "/page.prefab";

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Directory.CreateDirectory(Folder);
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        private static CompileResult Compile(string body, string css)
        {
            File.WriteAllText(
                HtmlPath,
                "<html><head><style>" + css + "</style></head><body>" + body + "</body></html>",
                new UTF8Encoding(false));
            AssetDatabase.ImportAsset(HtmlPath, ImportAssetOptions.ForceSynchronousImport);

            CompileResult result = new UguiHtmlUiCompiler(measurerFactory: () => new FixedMeasurer()).Compile(
                new CompileRequest
                {
                    HtmlAssetPath = HtmlPath,
                    CssAssetPaths = Array.Empty<string>(),
                    OutputType = CompileOutputType.Prefab,
                    OutputPath = PrefabPath,
                    CompileMode = CompileMode.Create,
                    Options = new CompilerOptions { GeneratedAssetFolder = Folder + "/Generated" },
                });

            Assert.That(result.Success, Is.True, Describe(result));
            return result;
        }

        private static string Describe(CompileResult result) => string.Join("\n", result.Diagnostics.Select(d => d.ToString()));

        private static T Find<T>(string name)
            where T : Component
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)
                .GetComponentsInChildren<T>(true).Single(c => c.name == name);
        }

        private static void AssertColour(Color actual, Color expected, string what)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.01f), what + " r");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.01f), what + " g");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.01f), what + " b");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.01f), what + " a");
        }

        [Test]
        public void StateBackgrounds_BecomeTheButtonsColourBlock()
        {
            CompileResult result = Compile(
                "<button id=\"go\">Go</button>",
                "#go { background-color: #3366cc; border: none; border-radius: 0; }"
                    + "#go:hover { background-color: #4477dd; }"
                    + "#go:active { background-color: #224499; }"
                    + "#go:focus { background-color: #3355bb; }"
                    + "#go:disabled { background-color: #888888; }");

            Assert.That(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty, Describe(result));

            Button button = Find<Button>("go");
            Image image = button.GetComponent<Image>();

            Assert.That(button.transition, Is.EqualTo(Selectable.Transition.ColorTint));
            Assert.That(button.targetGraphic, Is.SameAs(image));
            AssertColour(image.color, Color.white, "the graphic is white, so each tint is the colour itself");
            AssertColour(button.colors.normalColor, new Color32(0x33, 0x66, 0xcc, 0xff), "normal");
            AssertColour(button.colors.highlightedColor, new Color32(0x44, 0x77, 0xdd, 0xff), ":hover");
            AssertColour(button.colors.pressedColor, new Color32(0x22, 0x44, 0x99, 0xff), ":active");
            AssertColour(button.colors.selectedColor, new Color32(0x33, 0x55, 0xbb, 0xff), ":focus");
            AssertColour(button.colors.disabledColor, new Color32(0x88, 0x88, 0x88, 0xff), ":disabled");
        }

        [Test]
        public void WithoutActive_PressingShowsTheHoverColour()
        {
            Compile(
                "<button id=\"go\">Go</button>",
                "#go { background-color: #3366cc; border: none; } #go:hover { background-color: #4477dd; }");

            Button button = Find<Button>("go");
            AssertColour(button.colors.pressedColor, button.colors.highlightedColor, "pressing a button means hovering it");
        }

        [Test]
        public void AButtonWithNoStateRules_KeepsUnitysDefaults()
        {
            Compile("<button id=\"go\">Go</button>", "#go { background-color: #3366cc; }");

            Button button = Find<Button>("go");
            AssertColour(button.colors.highlightedColor, ColorBlock.defaultColorBlock.highlightedColor, "default");
        }

        [Test]
        public void ChangingAnythingButTheBackground_IsReported()
        {
            CompileResult result = Compile(
                "<button id=\"go\">Go</button>",
                "#go { background-color: #3366cc; color: #ffffff; } #go:hover { color: #ffff00; }");

            Assert.That(
                result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("background-color")),
                Is.True,
                Describe(result));
        }

        [Test]
        public void AStateRule_NeverChangesTheNormalStyle()
        {
            Compile(
                "<button id=\"go\">Go</button>",
                "#go { background-color: #3366cc; border: none; } #go:hover { background-color: #ff0000; }");

            AssertColour(Find<Button>("go").colors.normalColor, new Color32(0x33, 0x66, 0xcc, 0xff), "normal");
        }

        [TestCase("button:hover", CssElementState.Hover)]
        [TestCase("a:active", CssElementState.Active)]
        [TestCase("input:focus-visible", CssElementState.Focus)]
        [TestCase("button:disabled", CssElementState.Disabled)]
        [TestCase("button:hover:active", CssElementState.Hover | CssElementState.Active)]
        public void StatesAreParsed(string text, CssElementState expected)
        {
            var diagnostics = new DiagnosticSink();

            Assert.That(CssSelectorParser.TryParse(text, SourceLocation.None, diagnostics, out CssSelector selector), Is.True);
            Assert.That(selector.State, Is.EqualTo(expected));
            Assert.That(diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void AStateOnAnAncestor_IsNotSupported()
        {
            var diagnostics = new DiagnosticSink();

            Assert.That(CssSelectorParser.TryParse(".panel:hover button", SourceLocation.None, diagnostics, out _), Is.False);
            Assert.That(diagnostics.Diagnostics.Single().Severity, Is.EqualTo(DiagnosticSeverity.Warning));
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
