#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rectloom.Core.Compilation;
using Rectloom.Core.Layout;
using Rectloom.Ugui.Backend;
using Rectloom.Ugui.Compilation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rectloom.Ugui.Tests.Compilation
{
    /// <summary>
    /// <c>overflow: auto</c> and <c>overflow: scroll</c> compile to a ScrollRect over a content object.
    /// </summary>
    public sealed class ScrollTests
    {
        private const string Folder = "Assets/RectloomScrollTest";
        private const string HtmlPath = Folder + "/page.html";
        private const string PrefabPath = Folder + "/page.prefab";

        private const string Css =
            "#list { width: 200px; height: 100px; } .item { height: 40px; }";

        private const string Body =
            "<div id=\"list\"><div class=\"item\" id=\"a\"></div><div class=\"item\" id=\"b\"></div>"
            + "<div class=\"item\" id=\"c\"></div><div class=\"item\" id=\"d\"></div></div>";

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

        private static GameObject Compile(string css, string body = Body, CompileMode mode = CompileMode.Create)
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
                    CompileMode = mode,
                    Options = new CompilerOptions { GeneratedAssetFolder = Folder + "/Generated" },
                });

            Assert.That(result.Success, Is.True, string.Join("\n", result.Diagnostics.Select(d => d.ToString())));
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        private static Transform Find(GameObject root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        }

        [Test]
        public void OverflowAuto_ScrollsTheChildrenInAContentObject()
        {
            GameObject root = Compile(Css + "#list { overflow-y: auto; }");
            Transform list = Find(root, "list");

            ScrollRect scroll = list.GetComponent<ScrollRect>();
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.vertical, Is.True, "160px of items in a 100px box");
            Assert.That(scroll.horizontal, Is.False);
            Assert.That(scroll.viewport, Is.SameAs(list));
            Assert.That(list.GetComponent<RectMask2D>(), Is.Not.Null, "the box clips what is scrolled out");
            Assert.That(list.GetComponent<Graphic>(), Is.Not.Null, "something for a drag to land on");

            Transform content = list.Find(UguiBackend.ScrollContentName);
            Assert.That(scroll.content, Is.SameAs(content));
            Assert.That(((RectTransform)content).sizeDelta.y, Is.EqualTo(160f).Within(0.01f));
            Assert.That(content.Cast<Transform>().Select(t => t.name), Is.EqualTo(new[] { "a", "b", "c", "d" }));

            var last = (RectTransform)content.Find("d");
            Assert.That(last.anchoredPosition.y, Is.EqualTo(-120f).Within(0.01f), "placed as it would be without scrolling");
        }

        [Test]
        public void ContentThatFits_DoesNotScroll()
        {
            GameObject root = Compile("#list { width: 200px; height: 400px; overflow: scroll; } .item { height: 40px; }");

            ScrollRect scroll = Find(root, "list").GetComponent<ScrollRect>();
            Assert.That(scroll.vertical, Is.False);
            Assert.That(scroll.horizontal, Is.False);
        }

        [Test]
        public void OverflowHidden_OnlyClips()
        {
            GameObject root = Compile(Css + "#list { overflow: hidden; }");
            Transform list = Find(root, "list");

            Assert.That(list.GetComponent<ScrollRect>(), Is.Null);
            Assert.That(list.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(list.Find(UguiBackend.ScrollContentName), Is.Null);
        }

        [Test]
        public void AnUpdate_KeepsOneContentObject()
        {
            Compile(Css + "#list { overflow-y: auto; }");
            GameObject root = Compile(Css + "#list { overflow-y: auto; } .item { height: 50px; }", mode: CompileMode.Update);

            Transform list = Find(root, "list");
            Assert.That(list.Cast<Transform>().Count(t => t.name == UguiBackend.ScrollContentName), Is.EqualTo(1));
            Assert.That(((RectTransform)list.Find(UguiBackend.ScrollContentName)).sizeDelta.y, Is.EqualTo(200f).Within(0.01f));
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
