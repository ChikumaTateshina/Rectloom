#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rectloom.Core.Assets;
using Rectloom.Core.Compilation;
using Rectloom.Core.Css.Ast;
using Rectloom.Core.Css.Cascade;
using Rectloom.Core.Css.Computed;
using Rectloom.Core.Css.Parsing;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Dom;
using Rectloom.Core.Ir;
using Rectloom.Core.Layout;
using Rectloom.Core.Parsing;
using Rectloom.Ugui.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rectloom.Ugui.Tests.Backend
{
    /// <summary>
    /// Exercises the backend against real Unity objects.
    /// </summary>
    /// <remarks>
    /// The point of these tests is that generated output is ordinary uGUI: a Button really is a
    /// <c>Button</c>, and a rectangle really lands on a <c>RectTransform</c>.
    /// <para>
    /// TextMeshPro logs a message when a project has no default font asset imported, which is the
    /// case in a bare test project. That is unrelated to what is being asserted, so failing on log
    /// messages is turned off rather than letting an unimported sample font decide whether the
    /// backend works.
    /// </para>
    /// </remarks>
    public sealed class UguiBackendTests
    {
        private const string HtmlPath = "Assets/UI/page.html";
        private const string CssPath = "Assets/UI/page.css";
        private const string TestGeneratedFolder = "Assets/RectloomTestGenerated";

        private DiagnosticSink _diagnostics = null!;
        private CompilerOptions _options = null!;
        private readonly List<GameObject> _created = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            // Batch mode has no graphics device and a bare project has no TextMeshPro font
            // assets; neither says anything about the backend. The framework resets this per test,
            // so it is deliberately not cleared in teardown, where the check already ran.
            LogAssert.ignoreFailingMessages = true;
            _diagnostics = new DiagnosticSink();
            _options = new CompilerOptions
            {
                UseDefaultStyleSheet = false,
                GeneratedAssetFolder = TestGeneratedFolder,
            };
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
        }

        private GameObject Build(string html, string css = "", IAssetResolver? assets = null)
        {
            DomDocument document = new HtmlParser().Parse(HtmlPath, html, _diagnostics);

            var sheets = new List<CssStyleSheet>();

            if (css.Length > 0)
            {
                sheets.Add(CssParser.Parse(CssPath, css, _diagnostics));
            }

            ComputedStyleTree styles = ComputedStyleTree.Build(
                document,
                new CascadeResolver(null, sheets),
                new ComputedStyleBuilder(),
                _diagnostics);

            LayoutBox layoutRoot = LayoutTreeBuilder.Build(document, styles)!;
            LayoutResult solved = new LayoutSolver(new UnitTextMeasurer(), _diagnostics)
                .Solve(layoutRoot, new Vector2(1000f, 800f));

            UiNode ir = new UiTreeBuilder(HtmlPath, _options, _diagnostics).Build(solved);

            var backend = new UguiBackend(assets ?? new EmptyAssetResolver(), _options, _diagnostics);
            GameObject root = backend.Build(ir, parent: null).Root;
            _created.Add(root);
            return root;
        }

        private static GameObject Child(GameObject parent, string name)
        {
            foreach (Transform child in parent.transform)
            {
                if (child.name == name)
                {
                    return child.gameObject;
                }
            }

            Assert.Fail("no child named '" + name + "' under " + parent.name);
            return null!;
        }

        private static GameObject Descendant(GameObject root, string name)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (transform.name == name)
                {
                    return transform.gameObject;
                }
            }

            Assert.Fail("no descendant named '" + name + "'");
            return null!;
        }

        [Test]
        public void WorldSpaceRoot_MapsOnePixelToOneMillimetreAndUsesSourceName()
        {
            GameObject root = Build("<body><div id='physical'></div></body>",
                "#physical { width: 100px; height: 50px; }");
            Assert.That(root.name, Is.EqualTo(System.IO.Path.GetFileNameWithoutExtension(HtmlPath)));
            Assert.That(root.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(root.transform.localScale.x, Is.EqualTo(0.001f));
            var rect = Child(root, "physical").GetComponent<RectTransform>();
            Assert.That(rect.rect.width * rect.lossyScale.x, Is.EqualTo(0.1f).Within(0.00001f));
        }

        [Test]
        public void DocumentBackground_IsOmittedButAuthoredContentBackgroundRemains()
        {
            GameObject root = Build("<body><div id='caption'></div></body>",
                "body { background: white; } #caption { background: black; width: 20px; height: 20px; }");
            Assert.That(root.GetComponent<Image>(), Is.Null);
            Assert.That(Child(root, "caption").GetComponent<Image>().color, Is.EqualTo(Color.black));
        }

        [Test]
        public void DocumentBackground_CanBeExplicitlyEnabled()
        {
            _options.RenderDocumentBackground = true;
            GameObject root = Build("<body></body>", "body { background: white; }");
            Assert.That(root.GetComponent<Image>(), Is.Not.Null);
        }

        [Test]
        public void ScreenSpace_CanBeExplicitlyEnabled()
        {
            _options.WorldSpaceCanvas = false;
            GameObject root = Build("<body></body>");
            Assert.That(root.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void Root_BecomesACanvas()
        {
            GameObject root = Build("<body></body>");

            Assert.That(root.GetComponent<Canvas>(), Is.Not.Null);
            Assert.That(root.GetComponent<GraphicRaycaster>(), Is.Not.Null);

            var scaler = root.GetComponent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null);
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ConstantPixelSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(_options.ReferenceResolution));
        }

        [Test]
        public void ExistingCanvas_IsNotDuplicated()
        {
            var host = new GameObject("Host", typeof(RectTransform), typeof(Canvas));
            _created.Add(host);

            DomDocument document = new HtmlParser().Parse(HtmlPath, "<body></body>", _diagnostics);
            ComputedStyleTree styles = ComputedStyleTree.Build(
                document,
                new CascadeResolver(null, null),
                new ComputedStyleBuilder(),
                _diagnostics);
            LayoutResult solved = new LayoutSolver(new UnitTextMeasurer(), _diagnostics)
                .Solve(LayoutTreeBuilder.Build(document, styles)!, new Vector2(100f, 100f));
            UiNode ir = new UiTreeBuilder(HtmlPath, _options, _diagnostics).Build(solved);

            GameObject root = new UguiBackend(new EmptyAssetResolver(), _options, _diagnostics)
                .Build(ir, host.transform)
                .Root;

            Assert.That(root.GetComponent<Canvas>(), Is.Null);
            Assert.That(root.transform.parent, Is.EqualTo(host.transform));
        }

        [Test]
        public void EveryObject_HasARectTransform()
        {
            GameObject root = Build("<body><div id=\"a\"><p>text</p></div></body>");

            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(transform as RectTransform, Is.Not.Null, transform.name);
            }
        }

        [Test]
        public void TheRootIsPivotedAtItsCentre()
        {
            GameObject root = Build(
                "<body><div id=\"a\"></div></body>",
                "body { width: 400px; height: 200px; } div { width: 120px; height: 30px; }");

            var rect = root.GetComponent<RectTransform>();

            // The root's origin is what someone positions when they drop the object into a scene, so it
            // sits at the middle of the document rather than at its top-left corner.
            Assert.That(rect.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(400f, 200f)));
            Assert.That(rect.anchoredPosition, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ChildrenStillStartAtTheRootsTopLeftCorner()
        {
            GameObject root = Build(
                "<body><div id=\"a\"></div></body>",
                "body { width: 400px; height: 200px; } div { width: 120px; height: 30px; }");

            var rect = root.GetComponent<RectTransform>();
            var child = Descendant(root, "a").GetComponent<RectTransform>();

            // An anchor is a fraction of the parent's rectangle rather than an offset from its pivot, so
            // centring the root must not move anything inside it.
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            var childCorners = new Vector3[4];
            child.GetWorldCorners(childCorners);

            Assert.That(childCorners[1].x, Is.EqualTo(corners[1].x).Within(0.001f), "left edges agree");
            Assert.That(childCorners[1].y, Is.EqualTo(corners[1].y).Within(0.001f), "top edges agree");
        }

        [Test]
        public void RectIsBakedWithTopLeftAnchorsAndFlippedY()
        {
            GameObject root = Build(
                "<body><div id=\"a\"></div><div id=\"b\"></div></body>",
                "div { width: 120px; height: 30px; }");

            var first = Descendant(root, "a").GetComponent<RectTransform>();
            var second = Descendant(root, "b").GetComponent<RectTransform>();

            Assert.That(first.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(first.anchorMax, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(first.pivot, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(first.sizeDelta, Is.EqualTo(new Vector2(120f, 30f)));
            Assert.That(first.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(
                second.anchoredPosition,
                Is.EqualTo(new Vector2(0f, -30f)),
                "layout y grows down, Unity y grows up");
        }

        [Test]
        public void ParentPadding_ShiftsChildrenIntoTheContentBox()
        {
            GameObject root = Build(
                "<body><div id=\"outer\"><div id=\"a\"></div></div></body>",
                "#outer { width: 200px; padding: 12px; } #a { height: 10px; }");

            var child = Descendant(root, "a").GetComponent<RectTransform>();

            Assert.That(child.anchoredPosition, Is.EqualTo(new Vector2(12f, -12f)));
        }

        [Test]
        public void PlainContainer_GetsNoGraphic()
        {
            GameObject root = Build("<body><div id=\"a\"></div></body>", "#a { height: 10px; }");

            Assert.That(
                Descendant(root, "a").GetComponent<Graphic>(),
                Is.Null,
                "a box that paints nothing should not cost an invisible image");
        }

        [Test]
        public void BackgroundColour_BecomesAnImage()
        {
            GameObject root = Build(
                "<body><div id=\"a\"></div></body>",
                "#a { height: 10px; background-color: #ff8000; }");

            var image = Descendant(root, "a").GetComponent<Image>();

            Assert.That(image, Is.Not.Null);
            Assert.That((Color32)image.color, Is.EqualTo(new Color32(255, 128, 0, 255)));
            Assert.That(image.raycastTarget, Is.False, "a container does not swallow clicks");
        }

        [Test]
        public void TextElement_BecomesATextComponentOnTheSameObject()
        {
            GameObject root = Build(
                "<body><p id=\"a\">Hello</p></body>",
                "#a { color: #102030; font-size: 24px; text-align: center; }");

            GameObject target = Descendant(root, "a");
            var text = target.GetComponent<TMP_Text>();

            Assert.That(text, Is.Not.Null);
            Assert.That(text.text, Is.EqualTo("Hello"));
            Assert.That((Color32)text.color, Is.EqualTo(new Color32(16, 32, 48, 255)));
            Assert.That(text.fontSize, Is.EqualTo(24f));
            Assert.That(text.alignment, Is.EqualTo(TextAlignmentOptions.Top));
            Assert.That(target.transform.childCount, Is.Zero, "no label child is needed");
        }

        [Test]
        public void BoldAndItalic_MapToFontStyles()
        {
            GameObject root = Build(
                "<body><p id=\"a\">Hello</p></body>",
                "#a { font-weight: bold; font-style: italic; }");

            FontStyles style = Descendant(root, "a").GetComponent<TMP_Text>().fontStyle;

            Assert.That(style.HasFlag(FontStyles.Bold), Is.True);
            Assert.That(style.HasFlag(FontStyles.Italic), Is.True);
        }

        [Test]
        public void LineHeightAndLetterSpacing_AreConvertedToPercentages()
        {
            GameObject root = Build(
                "<body><p id=\"a\">Hello</p></body>",
                "#a { font-size: 20px; line-height: 1.5; letter-spacing: 2px; }");

            var text = Descendant(root, "a").GetComponent<TMP_Text>();

            Assert.That(text.lineSpacing, Is.EqualTo(50f).Within(0.01f));
            Assert.That(text.characterSpacing, Is.EqualTo(10f).Within(0.01f));
        }

        [Test]
        public void NoWrap_DisablesWordWrapping()
        {
            GameObject root = Build(
                "<body><p id=\"a\">Hello</p></body>",
                "#a { white-space: nowrap; }");

            Assert.That(Descendant(root, "a").GetComponent<TMP_Text>().enableWordWrapping, Is.False);
        }

        [Test]
        public void PaintedTextBox_PutsTheTextOnALabelChild()
        {
            GameObject root = Build(
                "<body><div id=\"a\">Hello</div></body>",
                "#a { background-color: red; }");

            GameObject target = Descendant(root, "a");

            Assert.That(target.GetComponent<Image>(), Is.Not.Null);
            Assert.That(target.GetComponent<TMP_Text>(), Is.Null, "one object carries one graphic");

            GameObject label = Child(target, UguiBackend.LabelObjectName);
            Assert.That(label.GetComponent<TMP_Text>().text, Is.EqualTo("Hello"));
            Assert.That(label.GetComponent<Graphic>().raycastTarget, Is.False);
        }

        [Test]
        public void Button_IsAUnityButtonWithALabel()
        {
            GameObject root = Build(
                "<body><button id=\"apply\">Apply</button></body>",
                "#apply { width: 100px; height: 30px; background-color: #e0e0e0; }");

            GameObject target = Descendant(root, "apply");
            var button = target.GetComponent<Button>();
            var image = target.GetComponent<Image>();

            Assert.That(button, Is.Not.Null);
            Assert.That(image, Is.Not.Null);
            Assert.That(button.targetGraphic, Is.EqualTo(image));
            Assert.That(button.interactable, Is.True);
            Assert.That(image.raycastTarget, Is.True, "a button has to receive clicks");
            Assert.That(
                Child(target, UguiBackend.LabelObjectName).GetComponent<TMP_Text>().text,
                Is.EqualTo("Apply"));
        }

        [Test]
        public void Button_GetsAGraphicEvenWithoutABackground()
        {
            GameObject root = Build("<body><button id=\"apply\">Apply</button></body>");

            Assert.That(
                Descendant(root, "apply").GetComponent<Image>(),
                Is.Not.Null,
                "a button with no graphic cannot be clicked");
        }

        [Test]
        public void ButtonLabel_StretchesToTheContentBox()
        {
            GameObject root = Build(
                "<body><button id=\"apply\">Apply</button></body>",
                "#apply { width: 100px; height: 40px; padding: 5px 8px; }");

            var label = Child(Descendant(root, "apply"), UguiBackend.LabelObjectName)
                .GetComponent<RectTransform>();

            Assert.That(label.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(label.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(label.offsetMin, Is.EqualTo(new Vector2(8f, 5f)));
            Assert.That(label.offsetMax, Is.EqualTo(new Vector2(-8f, -5f)));
        }

        [Test]
        public void UnityInteractableProperty_SwitchesTheButtonOff()
        {
            GameObject root = Build(
                "<body><button id=\"apply\">Apply</button></body>",
                "#apply { unity-interactable: false; }");

            Assert.That(Descendant(root, "apply").GetComponent<Button>().interactable, Is.False);
        }

        [Test]
        public void UnityRaycastTargetProperty_SwitchesAContainerOn()
        {
            GameObject root = Build(
                "<body><div id=\"a\"></div></body>",
                "#a { height: 10px; background-color: red; unity-raycast-target: true; }");

            Assert.That(Descendant(root, "a").GetComponent<Image>().raycastTarget, Is.True);
        }

        [Test]
        public void InvalidUnityPropertyValue_IsReported()
        {
            Build(
                "<body><button id=\"apply\">Apply</button></body>",
                "#apply { unity-interactable: maybe; }");

            Assert.That(
                _diagnostics.Diagnostics.Any(d => d.Code == DiagnosticCodes.Css.InvalidValue),
                Is.True);
        }

        [Test]
        public void ImageWithASprite_BecomesAnImage()
        {
            var resolver = new StubAssetResolver();
            Sprite sprite = resolver.AddSprite("Assets/UI/icon.png");

            GameObject root = Build(
                "<body><img id=\"a\" src=\"./icon.png\"></body>",
                string.Empty,
                resolver);

            var image = Descendant(root, "a").GetComponent<Image>();

            Assert.That(image, Is.Not.Null);
            Assert.That(image.sprite, Is.EqualTo(sprite));
        }

        [Test]
        public void ImageWithOnlyATexture_FallsBackToRawImage()
        {
            var resolver = new StubAssetResolver();
            Texture2D texture = resolver.AddTexture("Assets/UI/photo.png");

            GameObject root = Build(
                "<body><img id=\"a\" src=\"./photo.png\"></body>",
                string.Empty,
                resolver);

            var raw = Descendant(root, "a").GetComponent<RawImage>();

            Assert.That(raw, Is.Not.Null);
            Assert.That(raw.texture, Is.EqualTo(texture));
            Assert.That(
                _diagnostics.Diagnostics.Any(d => d.Code == DiagnosticCodes.Asset.UnsupportedType),
                Is.True,
                "the fallback is reported rather than silent");
        }

        [Test]
        public void ImageWithOnlyATexture_IsAnErrorWhenTheFallbackIsOff()
        {
            _options.AllowRawImageFallback = false;

            var resolver = new StubAssetResolver();
            resolver.AddTexture("Assets/UI/photo.png");

            GameObject root = Build(
                "<body><img id=\"a\" src=\"./photo.png\"></body>",
                string.Empty,
                resolver);

            Assert.That(Descendant(root, "a").GetComponent<Graphic>(), Is.Null);
            Assert.That(_diagnostics.HasErrors, Is.True);
        }

        [Test]
        public void MissingImageAsset_IsAnError()
        {
            Build("<body><img id=\"a\" src=\"./missing.png\"></body>");

            Assert.That(
                _diagnostics.Diagnostics.Any(
                    d => d.Code == DiagnosticCodes.Asset.NotFound && d.IsErrorOrWorse),
                Is.True);
        }

        [Test]
        public void ImageWithoutASource_IsReportedAndStillOccupiesItsBox()
        {
            GameObject root = Build("<body><img id=\"a\"></body>", "#a { width: 20px; height: 20px; }");

            Assert.That(Descendant(root, "a").GetComponent<Image>(), Is.Not.Null);
            Assert.That(
                _diagnostics.Diagnostics.Any(d => d.Code == DiagnosticCodes.Asset.NotFound),
                Is.True);
        }

        [Test]
        public void OpacityOnALeaf_GoesOnTheGraphic()
        {
            GameObject root = Build(
                "<body><div id=\"a\"></div></body>",
                "#a { height: 10px; background-color: #ffffff; opacity: 0.5; }");

            GameObject target = Descendant(root, "a");

            Assert.That(target.GetComponent<CanvasGroup>(), Is.Null);
            Assert.That(target.GetComponent<Image>().color.a, Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void OpacityOnASubtree_UsesACanvasGroup()
        {
            GameObject root = Build(
                "<body><div id=\"a\"><div id=\"b\"></div></div></body>",
                "#a { opacity: 0.25; } #b { height: 10px; }");

            var group = Descendant(root, "a").GetComponent<CanvasGroup>();

            Assert.That(group, Is.Not.Null, "CSS opacity covers the subtree, which alpha cannot");
            Assert.That(group.alpha, Is.EqualTo(0.25f).Within(0.01f));
        }

        [Test]
        public void HierarchyMirrorsTheMarkup()
        {
            GameObject root = Build(
                "<body><div id=\"panel\"><h1>Title</h1><button id=\"ok\">OK</button></div></body>");

            GameObject panel = Child(root, "panel");

            Assert.That(panel.transform.childCount, Is.EqualTo(2));
            Assert.That(panel.transform.GetChild(0).name, Is.EqualTo("h1"));
            Assert.That(panel.transform.GetChild(1).name, Is.EqualTo("ok"));
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

        /// <summary>
        /// A resolver backed by objects created in memory, so image handling can be tested without
        /// importing files.
        /// </summary>
        private sealed class StubAssetResolver : IAssetResolver
        {
            private readonly Dictionary<string, UnityEngine.Object> _assets =
                new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);

            internal Sprite AddSprite(string path)
            {
                var texture = new Texture2D(4, 4);
                Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), Vector2.zero);
                _assets[path] = sprite;
                return sprite;
            }

            internal Texture2D AddTexture(string path)
            {
                var texture = new Texture2D(4, 4);
                _assets[path] = texture;
                return texture;
            }

            public bool TryResolve<T>(AssetReference reference, out T asset)
                where T : UnityEngine.Object
            {
                asset = null!;

                if (reference.Value == null || !_assets.TryGetValue(reference.Value, out UnityEngine.Object found))
                {
                    return false;
                }

                if (found is T typed)
                {
                    asset = typed;
                    return true;
                }

                return false;
            }

            public bool Exists(AssetReference reference)
            {
                return reference.Value != null && _assets.ContainsKey(reference.Value);
            }

            public string? GetAssetPath(AssetReference reference) => reference.Value;
        }

        /// <summary>
        /// Fixed text metrics, so a baked rectangle does not depend on which font a machine has.
        /// </summary>
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
