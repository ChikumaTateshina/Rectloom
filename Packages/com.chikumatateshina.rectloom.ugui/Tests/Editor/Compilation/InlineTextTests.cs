#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rectloom.Core.Compilation;
using Rectloom.Core.Layout;
using Rectloom.Ugui.Compilation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rectloom.Ugui.Tests.Compilation
{
    /// <summary>
    /// A paragraph with formatted words compiles to one text object with TextMeshPro rich text.
    /// </summary>
    public sealed class InlineTextTests
    {
        private const string Folder = "Assets/RectloomInlineTextTest";
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

        private static GameObject Compile(string body, string css = "")
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

            Assert.That(result.Success, Is.True, string.Join("\n", result.Diagnostics.Select(d => d.ToString())));
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        private static TMP_Text Text(GameObject root, string name)
        {
            return root.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == name);
        }

        [Test]
        public void ABoldWord_IsOneTextWithABoldTag()
        {
            TMP_Text text = Text(Compile("<p id=\"p\">これは<strong>重要</strong>です</p>"), "p");

            Assert.That(text.text, Is.EqualTo("これは<b>重要</b>です"));
            Assert.That(text.richText, Is.True);
        }

        [Test]
        public void ColourSizeAndDecoration_AreWrittenAsTags()
        {
            TMP_Text text = Text(
                Compile("<p id=\"p\">a <span class=\"r\">red</span> <big>big</big> <u>u</u> <s>s</s> <em>i</em></p>",
                    ".r { color: #ff0000; } p { color: #000000; font-size: 16px; }"),
                "p");

            Assert.That(text.text, Does.Contain("<color=#FF0000FF>red</color>"));
            Assert.That(text.text, Does.Contain("<size=19>big</size>"));
            Assert.That(text.text, Does.Contain("<u>u</u>"));
            Assert.That(text.text, Does.Contain("<s>s</s>"));
            Assert.That(text.text, Does.Contain("<i>i</i>"));
        }

        [Test]
        public void MarkupTheAuthorTyped_StaysLiteral()
        {
            TMP_Text text = Text(Compile("<p id=\"p\">1 &lt; 2 <b>&lt;b&gt;</b></p>"), "p");

            Assert.That(text.text, Is.EqualTo("1 <noparse><</noparse> 2 <b><noparse><</noparse>b></b>"));
        }

        [Test]
        public void PlainText_HasNoMarkup()
        {
            TMP_Text text = Text(Compile("<p id=\"p\">a &lt;b&gt; c</p>"), "p");

            Assert.That(text.text, Is.EqualTo("a <b> c"));
            Assert.That(text.richText, Is.False, "nothing the author wrote is parsed as markup");
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
