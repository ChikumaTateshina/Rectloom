#nullable enable

using NUnit.Framework;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Layout;
using UnityEngine;

namespace Rectloom.Core.Tests.Layout
{
    /// <summary>
    /// The box model assertions required by docs/09 section 4: fixed sizes, percentages, margin and
    /// padding.
    /// </summary>
    public sealed class BoxModelLayoutTests
    {
        private LayoutTestHarness _harness = null!;

        [SetUp]
        public void SetUp()
        {
            _harness = new LayoutTestHarness();
        }

        [Test]
        public void RootFillsTheViewport()
        {
            LayoutResult root = _harness.Solve("<body></body>");

            LayoutTestHarness.AssertRect(root, 0f, 0f, 1000f, 0f);
            Assert.That(_harness.Diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void FixedWidthAndHeight_AreUsedAsIs()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"a\"></div></body>",
                "#a { width: 200px; height: 50px; }");

            LayoutTestHarness.AssertRect(LayoutTestHarness.Find(root, "a"), 0f, 0f, 200f, 50f);
        }

        [Test]
        public void AutoWidth_FillsTheContainingBlock()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"a\"></div></body>",
                "body { width: 400px; } #a { height: 10px; }");

            Assert.That(LayoutTestHarness.Find(root, "a").Width, Is.EqualTo(400f));
        }

        [Test]
        public void PercentWidth_ResolvesAgainstTheParentContentWidth()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"outer\"><div id=\"a\"></div></div></body>",
                "#outer { width: 800px; height: 100px; } #a { width: 50%; height: 10px; }");

            Assert.That(LayoutTestHarness.Find(root, "a").Width, Is.EqualTo(400f));
        }

        [Test]
        public void PercentHeight_ResolvesAgainstADefiniteParentHeight()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"outer\"><div id=\"a\"></div></div></body>",
                "#outer { width: 100px; height: 200px; } #a { height: 25%; }");

            Assert.That(LayoutTestHarness.Find(root, "a").Height, Is.EqualTo(50f));
        }

        [Test]
        public void PercentHeight_WithoutADefiniteParent_IsReported()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"outer\"><div id=\"a\"></div></div></body>",
                "#outer { width: 100px; } #a { height: 25%; }");

            Assert.That(LayoutTestHarness.Find(root, "a").Height, Is.Zero, "it falls back to content");
            Assert.That(
                _harness.Diagnostics.Diagnostics[0].Code,
                Is.EqualTo(DiagnosticCodes.Layout.InvalidPercentageContext));
        }

        [Test]
        public void BorderBoxSizing_KeepsPaddingInsideTheDeclaredWidth()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"a\"></div></body>",
                "#a { width: 200px; height: 100px; padding: 10px 20px; }");

            LayoutResult box = LayoutTestHarness.Find(root, "a");

            LayoutTestHarness.AssertRect(box, 0f, 0f, 200f, 100f);
            Assert.That(box.ContentWidth, Is.EqualTo(160f), "200 minus 20 of padding on each side");
            Assert.That(box.ContentHeight, Is.EqualTo(80f));
            Assert.That(box.ContentX, Is.EqualTo(20f));
            Assert.That(box.ContentY, Is.EqualTo(10f));
        }

        [Test]
        public void BorderWidth_CountsTowardsTheFrame()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"a\"></div></body>",
                "#a { width: 100px; height: 100px; padding: 5px; border-width: 3px; }");

            LayoutResult box = LayoutTestHarness.Find(root, "a");

            Assert.That(box.ContentWidth, Is.EqualTo(84f), "100 - 2*5 padding - 2*3 border");
            Assert.That(box.ContentX, Is.EqualTo(8f));
        }

        [Test]
        public void PaddingLargerThanTheBox_ClampsAndReports()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"a\"></div></body>",
                "#a { width: 10px; height: 10px; padding: 20px; }");

            LayoutResult box = LayoutTestHarness.Find(root, "a");

            Assert.That(box.ContentWidth, Is.Zero);
            Assert.That(
                _harness.Diagnostics.Diagnostics[0].Code,
                Is.EqualTo(DiagnosticCodes.Layout.NegativeCalculatedSize));
        }

        [Test]
        public void ChildrenStackVerticallyInBlockFlow()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"a\"></div><div id=\"b\"></div><div id=\"c\"></div></body>",
                "div { height: 30px; }");

            LayoutTestHarness.AssertRect(LayoutTestHarness.Find(root, "a"), 0f, 0f, 1000f, 30f);
            LayoutTestHarness.AssertRect(LayoutTestHarness.Find(root, "b"), 0f, 30f, 1000f, 30f);
            LayoutTestHarness.AssertRect(LayoutTestHarness.Find(root, "c"), 0f, 60f, 1000f, 30f);
            Assert.That(root.Height, Is.EqualTo(90f));
        }

        [Test]
        public void Margin_OffsetsTheBoxAndShrinksAutoWidth()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"a\"></div></body>",
                "body { width: 500px; } #a { height: 20px; margin: 10px 25px; }");

            LayoutTestHarness.AssertRect(LayoutTestHarness.Find(root, "a"), 25f, 10f, 450f, 20f);
            Assert.That(root.Height, Is.EqualTo(40f), "top and bottom margins are part of the flow");
        }

        [Test]
        public void AdjacentMargins_AreNotCollapsed()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"a\"></div><div id=\"b\"></div></body>",
                "div { height: 10px; margin: 20px 0; }");

            Assert.That(
                LayoutTestHarness.Find(root, "b").Y,
                Is.EqualTo(70f),
                "20 + 10 + 20 + 20: ADR-0003 records that margins do not collapse");
        }

        [Test]
        public void ParentPadding_MovesChildrenIntoTheContentBox()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"outer\"><div id=\"a\"></div></div></body>",
                "#outer { width: 300px; padding: 15px; } #a { height: 10px; }");

            LayoutResult outer = LayoutTestHarness.Find(root, "outer");
            LayoutResult child = LayoutTestHarness.Find(root, "a");

            Assert.That(child.X, Is.Zero, "positions are relative to the parent content box");
            Assert.That(child.Width, Is.EqualTo(270f));
            Assert.That(outer.ContentX, Is.EqualTo(15f));
            Assert.That(outer.Height, Is.EqualTo(40f), "10 of content plus 15 of padding on each side");
        }

        [Test]
        public void AutoHeight_FollowsTheContent()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"outer\"><div id=\"a\"></div><div id=\"b\"></div></div></body>",
                "#outer { width: 100px; } #a { height: 17px; } #b { height: 23px; }");

            Assert.That(LayoutTestHarness.Find(root, "outer").Height, Is.EqualTo(40f));
        }

        [Test]
        public void MinAndMaxWidth_ClampTheResult()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"a\"></div><div id=\"b\"></div></body>",
                "#a { width: 10px; min-width: 60px; height: 5px; }"
                    + " #b { width: 900px; max-width: 200px; height: 5px; }");

            Assert.That(LayoutTestHarness.Find(root, "a").Width, Is.EqualTo(60f));
            Assert.That(LayoutTestHarness.Find(root, "b").Width, Is.EqualTo(200f));
        }

        [Test]
        public void MinAndMaxHeight_ClampTheResult()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"a\"></div><div id=\"b\"></div></body>",
                "#a { height: 10px; min-height: 40px; } #b { height: 500px; max-height: 80px; }");

            Assert.That(LayoutTestHarness.Find(root, "a").Height, Is.EqualTo(40f));
            Assert.That(LayoutTestHarness.Find(root, "b").Height, Is.EqualTo(80f));
        }

        [Test]
        public void DisplayNone_RemovesTheBoxAndItsSubtree()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"a\"></div><div id=\"gone\"><div id=\"inner\"></div></div><div id=\"b\"></div></body>",
                "div { height: 10px; } #gone { display: none; }");

            Assert.That(root.Children.Count, Is.EqualTo(2));
            Assert.That(LayoutTestHarness.Find(root, "b").Y, Is.EqualTo(10f));
        }

        [Test]
        public void RelativePosition_ShiftsPaintingWithoutMovingSiblings()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"a\"></div><div id=\"b\"></div></body>",
                "div { height: 10px; } #a { position: relative; left: 15px; top: 7px; }");

            Assert.That(LayoutTestHarness.Find(root, "a").X, Is.EqualTo(15f));
            Assert.That(LayoutTestHarness.Find(root, "a").Y, Is.EqualTo(7f));
            Assert.That(
                LayoutTestHarness.Find(root, "b").Y,
                Is.EqualTo(10f),
                "the sibling keeps the space the relative box occupied");
        }

        [Test]
        public void LayoutIsDeterministic()
        {
            const string html = "<body><div id=\"a\"><p>text</p></div><div id=\"b\"></div></body>";
            const string css = "#a { width: 50%; padding: 4px; } #b { height: 12px; margin: 3px; }";

            string first = LayoutTestHarness.Describe(new LayoutTestHarness().Solve(html, css));
            string second = LayoutTestHarness.Describe(new LayoutTestHarness().Solve(html, css));

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void ViewportSize_IsTheRootContainingBlock()
        {
            _harness.Viewport = new Vector2(640f, 480f);

            LayoutResult root = _harness.Solve(
                "<body><div id=\"a\"></div></body>",
                "body { height: 100%; } #a { width: 50%; height: 50%; }");

            LayoutTestHarness.AssertRect(LayoutTestHarness.Find(root, "a"), 0f, 0f, 320f, 240f);
        }
    }
}
