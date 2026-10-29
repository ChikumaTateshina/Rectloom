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
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rectloom.Ugui.Tests.Compilation
{
    /// <summary>
    /// Links open a panel with the address in a field that can be copied, since a world cannot open a
    /// browser.
    /// </summary>
    public sealed class LinkTests
    {
        private const string Folder = "Assets/RectloomLinkTest";
        private const string HtmlPath = Folder + "/page.html";
        private const string PrefabPath = Folder + "/page.prefab";

        private const string Body =
            "<p id=\"p\">see <a href=\"https://example.com/a\">the site</a>.</p>"
            + "<div style=\"display: flex\"><a id=\"direct\" href=\"https://example.com/b\">B</a></div>";

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

        private static CompileResult Compile(string body, CompileMode mode = CompileMode.Create)
        {
            File.WriteAllText(HtmlPath, "<html><body>" + body + "</body></html>", new UTF8Encoding(false));
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
            return result;
        }

        private static GameObject Prefab() => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        private static GameObject[] Popups(GameObject root)
        {
            return root.transform.Cast<Transform>()
                .Where(t => t.name.StartsWith(UguiBackend.LinkPopupName, StringComparison.Ordinal))
                .Select(t => t.gameObject)
                .ToArray();
        }

        private static GameObject PopupFor(GameObject root, string href)
        {
            return Popups(root).Single(p => p.GetComponentInChildren<TMP_InputField>(true).text == href);
        }

        private static void AssertOpens(Button button, GameObject popup, bool active)
        {
            Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(button.onClick.GetPersistentTarget(0), Is.SameAs(popup));
            Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo("SetActive"));

            var serialized = new SerializedObject(button);
            bool argument = serialized.FindProperty("m_OnClick.m_PersistentCalls.m_Calls")
                .GetArrayElementAtIndex(0).FindPropertyRelative("m_Arguments.m_BoolArgument").boolValue;
            Assert.That(argument, Is.EqualTo(active));
        }

        [Test]
        public void EachAddress_GetsOneHiddenPanelWithACopyableField()
        {
            Compile(Body);
            GameObject root = Prefab();

            GameObject[] popups = Popups(root);
            Assert.That(popups.Length, Is.EqualTo(2));
            Assert.That(popups.All(p => !p.activeSelf), Is.True, "hidden until a link opens it");
            Assert.That(root.transform.GetChild(root.transform.childCount - 1).name, Does.StartWith(UguiBackend.LinkPopupName),
                "last, so it draws on top");

            TMP_InputField field = PopupFor(root, "https://example.com/a").GetComponentInChildren<TMP_InputField>(true);
            Assert.That(field.readOnly, Is.True, "copyable, not editable");
            Assert.That(field.textComponent, Is.Not.Null);
            Assert.That(field.textViewport, Is.Not.Null);

            Button close = PopupFor(root, "https://example.com/a").GetComponentsInChildren<Button>(true).Single(b => b.name == "Close");
            AssertOpens(close, PopupFor(root, "https://example.com/a"), active: false);
        }

        [Test]
        public void ALinkThatIsABox_IsAButtonThatOpensItsPanel()
        {
            Compile(Body);
            GameObject root = Prefab();

            Button button = root.GetComponentsInChildren<Button>(true).Single(b => b.name == "direct");
            AssertOpens(button, PopupFor(root, "https://example.com/b"), active: true);
        }

        [Test]
        public void ALinkInsideText_GetsAnOverlayThatOpensItsPanel()
        {
            Compile(Body);
            GameObject root = Prefab();

            Transform paragraph = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "p");
            Button overlay = paragraph.GetComponentsInChildren<Button>(true).Single();

            Assert.That(overlay.name, Is.EqualTo(UguiBackend.LinkOverlayName));
            Assert.That(overlay.GetComponent<Image>().color.a, Is.Zero, "invisible; the text shows through");
            AssertOpens(overlay, PopupFor(root, "https://example.com/a"), active: true);
        }

        [Test]
        public void ALink_IsBlueAndUnderlinedByDefault()
        {
            Compile(Body);

            TMP_Text text = Prefab().GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "p");

            Assert.That(text.text, Does.Contain("<u><color=#0645ADFF>the site</color></u>"));
        }

        [Test]
        public void AnUpdate_ReplacesOnlyTheWiringItMade()
        {
            Compile(Body);

            // The author adds a listener of their own to the link button.
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                Button direct = contents.GetComponentsInChildren<Button>(true).Single(b => b.name == "direct");
                UnityEditor.Events.UnityEventTools.AddStringPersistentListener(direct.onClick, direct.gameObject.SendMessage, "Mine");
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            Compile(Body, CompileMode.Update);
            GameObject root = Prefab();

            Assert.That(Popups(root).Length, Is.EqualTo(2), "reused, not duplicated");

            Button button = root.GetComponentsInChildren<Button>(true).Single(b => b.name == "direct");
            Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(2), "the author's and one of ours");
            Assert.That(
                Enumerable.Range(0, 2).Select(i => button.onClick.GetPersistentMethodName(i)),
                Is.EquivalentTo(new[] { "SendMessage", "SetActive" }));
        }

        [Test]
        public void ARemovedLink_TakesItsPanelWithIt()
        {
            Compile(Body);
            Compile("<p id=\"p\">no links now</p>", CompileMode.Update);

            Assert.That(Popups(Prefab()), Is.Empty);
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
