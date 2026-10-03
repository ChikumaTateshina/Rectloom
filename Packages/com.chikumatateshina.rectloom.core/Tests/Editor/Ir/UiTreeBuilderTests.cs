#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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
using Rectloom.Core.Tests.Layout;
using UnityEngine;

namespace Rectloom.Core.Tests.Ir
{
    /// <summary>
    /// Drives HTML and CSS all the way to the IR, which is the contract a backend reads.
    /// </summary>
    public sealed class UiTreeBuilderTests
    {
        private const string HtmlPath = "Assets/UI/page.html";
        private const string CssPath = "Assets/UI/page.css";

        private DiagnosticSink _diagnostics = null!;
        private CompilerOptions _options = null!;

        [SetUp]
        public void SetUp()
        {
            _diagnostics = new DiagnosticSink();
            _options = new CompilerOptions { UseDefaultStyleSheet = false };
        }

        private UiNode Build(string html, string css = "")
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

            LayoutBox root = LayoutTreeBuilder.Build(document, styles)!;
            Assert.That(root, Is.Not.Null);

            LayoutResult solved = new LayoutSolver(new FixedTextMeasurer(), _diagnostics)
                .Solve(root, new Vector2(1000f, 800f));

            return new UiTreeBuilder(HtmlPath, _options, _diagnostics).Build(solved);
        }

        private static UiNode Find(UiNode root, string stableId)
        {
            foreach (UiNode node in root.DescendantsAndSelf())
            {
                if (node.StableId == stableId)
                {
                    return node;
                }
            }

            Assert.Fail("no IR node with stable id '" + stableId + "'");
            return null!;
        }

        [Test]
        public void RootNode_IsTheRootKind()
        {
            UiNode root = Build("<body></body>");

            Assert.That(root.Kind, Is.EqualTo(UiNodeKind.Root));
            Assert.That(root.StableId, Is.EqualTo(StableId.RootId));
        }

        [Test]
        public void RootNode_PrefersAnExplicitId()
        {
            Assert.That(Build("<body id=\"screen\"></body>").StableId, Is.EqualTo("screen"));
        }

        [TestCase("<div id=\"x\"></div>", UiNodeKind.Container)]
        [TestCase("<div id=\"x\">text</div>", UiNodeKind.Text)]
        [TestCase("<p id=\"x\">text</p>", UiNodeKind.Text)]
        [TestCase("<h1 id=\"x\">text</h1>", UiNodeKind.Text)]
        [TestCase("<h6 id=\"x\">text</h6>", UiNodeKind.Text)]
        [TestCase("<span id=\"x\">text</span>", UiNodeKind.Text)]
        [TestCase("<span id=\"x\"></span>", UiNodeKind.Container)]
        [TestCase("<button id=\"x\">Apply</button>", UiNodeKind.Button)]
        [TestCase("<img id=\"x\" src=\"./a.png\">", UiNodeKind.Image)]
        public void ElementsMapToKinds(string html, UiNodeKind expected)
        {
            Assert.That(Find(Build(html), "x").Kind, Is.EqualTo(expected));
        }

        [Test]
        public void EmptyParagraph_IsAContainerRatherThanEmptyText()
        {
            Assert.That(Find(Build("<p id=\"x\"></p>"), "x").Kind, Is.EqualTo(UiNodeKind.Text));
            Assert.That(Find(Build("<p id=\"x\"></p>"), "x").HasText, Is.False);
        }

        [Test]
        public void AnonymousTextBox_BecomesATextNode()
        {
            UiNode container = Find(Build("<div id=\"x\">lead<span>tail</span></div>"), "x");

            Assert.That(container.Children[0].Kind, Is.EqualTo(UiNodeKind.Text));
            Assert.That(container.Children[0].TextContent, Is.EqualTo("lead"));
            Assert.That(container.Children[0].Name, Is.EqualTo("Text"));
        }

        [Test]
        public void UnsupportedElement_IsReportedAndCompiledAsAContainer()
        {
            UiNode node = Find(Build("<marquee id=\"x\"></marquee>"), "x");

            Assert.That(node.Kind, Is.EqualTo(UiNodeKind.Container));
            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Html.UnknownElement));
            Assert.That(
                _diagnostics.Diagnostics.Single().Severity,
                Is.EqualTo(DiagnosticSeverity.Warning));
        }

        [Test]
        public void UnsupportedElement_IsAnErrorInStrictMode()
        {
            _options.StrictMode = true;

            Build("<marquee id=\"x\"></marquee>");

            Assert.That(
                _diagnostics.Diagnostics.Single().Severity,
                Is.EqualTo(DiagnosticSeverity.Error));
        }

        [Test]
        public void LineBreak_ProducesNoNode()
        {
            UiNode paragraph = Find(Build("<p id=\"x\">a<br>b</p>"), "x");

            Assert.That(
                paragraph.Children.Select(c => c.TextContent),
                Is.EqualTo(new[] { "a", "b" }),
                "the break only splits the text; it needs no object of its own");
        }

        [Test]
        public void StructuralIds_FollowTheTreePosition()
        {
            UiNode root = Build("<body><div><span>a</span><button>b</button></div></body>");

            UiNode div = root.Children[0];
            Assert.That(div.StableId, Is.EqualTo("root/div[0]"));
            Assert.That(div.Children[0].StableId, Is.EqualTo("root/div[0]/span[0]"));
            Assert.That(div.Children[1].StableId, Is.EqualTo("root/div[0]/button[1]"));
        }

        [Test]
        public void ExplicitIdsAnchorTheirSubtree()
        {
            UiNode root = Build("<body><div id=\"panel\"><span>a</span></div></body>");

            Assert.That(root.Children[0].StableId, Is.EqualTo("panel"));
            Assert.That(root.Children[0].Children[0].StableId, Is.EqualTo("panel/span[0]"));
        }

        [Test]
        public void DuplicateId_FallsBackToTheStructuralPath()
        {
            UiNode root = Build("<body><div id=\"same\"></div><div id=\"same\"></div></body>");

            Assert.That(root.Children[0].StableId, Is.EqualTo("same"));
            Assert.That(
                root.Children[1].StableId,
                Is.EqualTo("root/div[1]"),
                "an ambiguous id cannot anchor an update, so the second element uses its position");
        }

        [Test]
        public void StableIdsAreUniqueAcrossTheDocument()
        {
            UiNode root = Build(
                "<body><div id=\"a\"><span>1</span><span>2</span></div>"
                    + "<div><span>3</span></div></body>");

            List<string> ids = root.DescendantsAndSelf().Select(n => n.StableId).ToList();

            Assert.That(ids, Is.Unique);
        }

        [Test]
        public void RectIsTakenFromTheSolvedLayout()
        {
            UiNode node = Find(
                Build(
                    "<body><div id=\"x\"></div></body>",
                    "#x { width: 120px; height: 40px; padding: 5px; }"),
                "x");

            Assert.That(node.Rect.Width, Is.EqualTo(120f));
            Assert.That(node.Rect.Height, Is.EqualTo(40f));
            Assert.That(node.Rect.ContentWidth, Is.EqualTo(110f));
            Assert.That(node.Rect.ContentX, Is.EqualTo(5f));
            Assert.That(node.Rect.HasInset, Is.True);
        }

        [Test]
        public void VisualAndTextStylesAreCarriedOver()
        {
            UiNode node = Find(
                Build(
                    "<body><p id=\"x\">text</p></body>",
                    "#x { background-color: #ff0000; opacity: 0.5; border-width: 2px;"
                        + " border-color: #00ff00; border-radius: 4px;"
                        + " color: #0000ff; font-size: 24px; font-weight: bold;"
                        + " text-align: center; letter-spacing: 1px; }"),
                "x");

            Assert.That((Color32)node.Visual.BackgroundColor!.Value, Is.EqualTo(new Color32(255, 0, 0, 255)));
            Assert.That(node.Visual.Opacity, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(node.Visual.BorderWidth, Is.EqualTo(2f));
            Assert.That(node.Visual.BorderRadius, Is.EqualTo(4f));
            Assert.That(node.Visual.HasBorder, Is.True);
            Assert.That((Color32)node.TextStyle.Color, Is.EqualTo(new Color32(0, 0, 255, 255)));
            Assert.That(node.TextStyle.FontSize, Is.EqualTo(24f));
            Assert.That(node.TextStyle.Bold, Is.True);
            Assert.That(node.TextStyle.Alignment, Is.EqualTo(UiTextAlign.Center));
            Assert.That(node.TextStyle.LetterSpacing, Is.EqualTo(1f));
        }

        [Test]
        public void PlainContainer_PaintsNothing()
        {
            Assert.That(Find(Build("<div id=\"x\"></div>"), "x").Visual.PaintsAnything, Is.False);
        }

        [Test]
        public void ImageSource_IsResolvedAgainstTheDocument()
        {
            UiNode node = Find(Build("<img id=\"x\" src=\"./icons/a.png\">"), "x");

            Assert.That(node.Asset.Kind, Is.EqualTo(AssetReferenceKind.AssetPath));
            Assert.That(node.Asset.Value, Is.EqualTo("Assets/UI/icons/a.png"));
        }

        [Test]
        public void ImageSource_AcceptsAProjectPath()
        {
            Assert.That(
                Find(Build("<img id=\"x\" src=\"Assets/Shared/b.png\">"), "x").Asset.Value,
                Is.EqualTo("Assets/Shared/b.png"));
        }

        [Test]
        public void ImageSource_AcceptsAGuid()
        {
            UiNode node = Find(
                Build("<img id=\"x\" src=\"0123456789abcdef0123456789abcdef\">"),
                "x");

            Assert.That(node.Asset.Kind, Is.EqualTo(AssetReferenceKind.Guid));
        }

        [Test]
        public void BackgroundImage_IsResolvedAgainstTheStylesheet()
        {
            UiNode node = Find(
                Build(
                    "<body><div id=\"x\"></div></body>",
                    "#x { background-image: url(\"./art/bg.png\"); }"),
                "x");

            Assert.That(
                node.Asset.Value,
                Is.EqualTo("Assets/UI/art/bg.png"),
                "a url is relative to the stylesheet that wrote it, not to the document");
        }

        [Test]
        public void ImageSourceWinsOverBackgroundImage()
        {
            UiNode node = Find(
                Build(
                    "<body><img id=\"x\" src=\"./from-src.png\"></body>",
                    "#x { background-image: url(\"./from-css.png\"); }"),
                "x");

            Assert.That(node.Asset.Value, Is.EqualTo("Assets/UI/from-src.png"));
        }

        [Test]
        public void ComponentAttributes_BecomeAComponentRequest()
        {
            UiNode node = Find(
                Build("<button id=\"x\" component=\"Example.CustomButton\""
                    + " component.mode=\"Primary\" component.speed=\"1.5\">Apply</button>"),
                "x");

            ComponentRequest request = node.Components.Single();

            Assert.That(request.TypeName, Is.EqualTo("Example.CustomButton"));
            Assert.That(request.Properties["mode"], Is.EqualTo("Primary"));
            Assert.That(request.Properties["speed"], Is.EqualTo("1.5"));
        }

        [Test]
        public void ElementWithoutComponentAttribute_RequestsNothing()
        {
            Assert.That(Find(Build("<div id=\"x\"></div>"), "x").Components, Is.Empty);
        }

        [Test]
        public void EmptyComponentAttribute_IsReported()
        {
            UiNode node = Find(Build("<div id=\"x\" component=\"\"></div>"), "x");

            Assert.That(node.Components, Is.Empty);
            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Extension.RequiredExtensionNotInstalled));
        }

        [Test]
        public void ExtensionProperties_AreCarriedToTheIr()
        {
            UiNode node = Find(
                Build(
                    "<body><button id=\"x\">Apply</button></body>",
                    "#x { unity-interactable: false; }"),
                "x");

            Assert.That(node.TryGetExtensionProperty("unity-interactable", out string value), Is.True);
            Assert.That(value, Is.EqualTo("false"));
            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void PaintedTextNode_NeedsALabelChild()
        {
            UiNode painted = Find(
                Build(
                    "<body><div id=\"x\">text</div></body>",
                    "#x { background-color: red; }"),
                "x");
            UiNode plain = Find(Build("<body><div id=\"y\">text</div></body>"), "y");

            Assert.That(painted.NeedsLabelChild, Is.True, "one object carries one graphic");
            Assert.That(plain.NeedsLabelChild, Is.False);
        }

        [Test]
        public void Button_AlwaysNeedsALabelChild()
        {
            Assert.That(
                Find(Build("<button id=\"x\">Apply</button>"), "x").NeedsLabelChild,
                Is.True);
        }

        [Test]
        public void HiddenSubtrees_AreAbsentFromTheIr()
        {
            UiNode root = Build(
                "<body><div id=\"a\"></div><div id=\"gone\"><div id=\"inner\"></div></div></body>",
                "#gone { display: none; }");

            Assert.That(
                root.DescendantsAndSelf().Any(n => n.StableId == "gone"),
                Is.False);
            Assert.That(root.Children.Count, Is.EqualTo(1));
        }

        [Test]
        public void BuildingIsDeterministic()
        {
            const string html = "<body><div class=\"p\"><p>a</p><button>b</button></div></body>";
            const string css = ".p { width: 50%; padding: 4px; } p { font-size: 10px; }";

            string Describe()
            {
                return string.Join(
                    "\n",
                    Build(html, css).DescendantsAndSelf().Select(n => n.ToString()));
            }

            string first = Describe();
            SetUp();
            string second = Describe();

            Assert.That(second, Is.EqualTo(first));
        }
    }

    public sealed class StableIdTests
    {
        [Test]
        public void Structural_JoinsParentTagAndIndex()
        {
            Assert.That(StableId.Structural("root", "div", 0), Is.EqualTo("root/div[0]"));
            Assert.That(
                StableId.Structural("root/div[0]", "button", 2),
                Is.EqualTo("root/div[0]/button[2]"));
        }

        [TestCase("", "div")]
        [TestCase("root", "")]
        [TestCase(null, "div")]
        [TestCase("root", null)]
        public void Structural_RejectsBlankParts(string? parentId, string? tagName)
        {
            Assert.Throws<System.ArgumentException>(
                () => StableId.Structural(parentId!, tagName!, 0));
        }

        [Test]
        public void Allocator_PrefersAnExplicitId()
        {
            var allocator = new StableId.Allocator();

            Assert.That(allocator.Allocate("apply", "root/button[0]"), Is.EqualTo("apply"));
            Assert.That(allocator.IsUsed("apply"), Is.True);
        }

        [Test]
        public void Allocator_FallsBackWhenNoIdIsGiven()
        {
            var allocator = new StableId.Allocator();

            Assert.That(allocator.Allocate(null, "root/div[0]"), Is.EqualTo("root/div[0]"));
            Assert.That(allocator.Allocate("   ", "root/div[1]"), Is.EqualTo("root/div[1]"));
        }

        [Test]
        public void Allocator_FallsBackWhenAnIdRepeats()
        {
            var allocator = new StableId.Allocator();
            allocator.Allocate("same", "root/div[0]");

            Assert.That(allocator.Allocate("same", "root/div[1]"), Is.EqualTo("root/div[1]"));
        }

        [Test]
        public void Allocator_AddsASuffixWhenEvenTheFallbackCollides()
        {
            var allocator = new StableId.Allocator();
            allocator.Allocate("root/div[0]", "root/span[0]");

            Assert.That(allocator.Allocate(null, "root/div[0]"), Is.EqualTo("root/div[0]#2"));
        }

        [Test]
        public void Allocator_RejectsABlankFallback()
        {
            var allocator = new StableId.Allocator();

            Assert.Throws<System.ArgumentException>(() => allocator.Allocate("x", "  "));
        }
    }
}
