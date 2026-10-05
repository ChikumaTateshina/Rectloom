#nullable enable

using NUnit.Framework;
using Rectloom.Core.Layout;

namespace Rectloom.Core.Tests.Layout
{
    /// <summary>
    /// Covers the flex behaviour real stylesheets rely on beyond a single unwrapped line: wrapping,
    /// <c>flex-grow</c>, <c>flex-shrink</c>, baseline alignment and auto margins.
    /// </summary>
    public sealed class FlexLinesAndBaselineTests
    {
        private LayoutTestHarness _harness = null!;

        [SetUp]
        public void SetUp()
        {
            _harness = new LayoutTestHarness();
        }

        private static float[] ChildX(LayoutResult container)
        {
            var values = new float[container.Children.Count];

            for (int index = 0; index < container.Children.Count; index++)
            {
                values[index] = container.Children[index].X;
            }

            return values;
        }

        private static float[] ChildY(LayoutResult container)
        {
            var values = new float[container.Children.Count];

            for (int index = 0; index < container.Children.Count; index++)
            {
                values[index] = container.Children[index].Y;
            }

            return values;
        }

        private static float[] ChildWidth(LayoutResult container)
        {
            var values = new float[container.Children.Count];

            for (int index = 0; index < container.Children.Count; index++)
            {
                values[index] = container.Children[index].Width;
            }

            return values;
        }

        [Test]
        public void NoWrap_KeepsEveryItemOnOneLineEvenWhenItOverflows()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"row\"><div id=\"a\"></div><div id=\"b\"></div><div id=\"c\"></div></div></body>",
                "#row { display: flex; width: 100px; height: 100px; align-items: start; }"
                    + " #row > div { width: 60px; height: 20px; flex: none; }");

            LayoutResult row = LayoutTestHarness.Find(root, "row");

            Assert.That(ChildX(row), Is.EqualTo(new[] { 0f, 60f, 120f }));
            Assert.That(ChildY(row), Is.EqualTo(new[] { 0f, 0f, 0f }));
        }

        [Test]
        public void Wrap_MovesAnItemThatDoesNotFitOntoANewLine()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"row\"><div id=\"a\"></div><div id=\"b\"></div><div id=\"c\"></div></div></body>",
                "#row { display: flex; flex-wrap: wrap; width: 100px; align-items: start; }"
                    + " #row > div { width: 60px; height: 20px; flex: none; }");

            LayoutResult row = LayoutTestHarness.Find(root, "row");

            Assert.That(ChildX(row), Is.EqualTo(new[] { 0f, 0f, 0f }));
            Assert.That(ChildY(row), Is.EqualTo(new[] { 0f, 20f, 40f }));
            Assert.That(row.Height, Is.EqualTo(60f), "three lines of 20");
        }

        [Test]
        public void Wrap_UsesRowGapBetweenLinesAndColumnGapWithinThem()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"row\"><div id=\"a\"></div><div id=\"b\"></div><div id=\"c\"></div></div></body>",
                "#row { display: flex; flex-wrap: wrap; width: 100px; gap: 10px 4px; align-items: start; }"
                    + " #row > div { width: 45px; height: 20px; flex: none; }");

            LayoutResult row = LayoutTestHarness.Find(root, "row");

            // 45 + 4 + 45 = 94 fits in 100; adding the third would need 143.
            Assert.That(ChildX(row), Is.EqualTo(new[] { 0f, 49f, 0f }));
            Assert.That(ChildY(row), Is.EqualTo(new[] { 0f, 0f, 30f }), "20 tall plus a 10 row gap");
        }

        [Test]
        public void FlexGrow_SharesTheFreeSpace()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"row\"><div id=\"a\"></div><div id=\"b\"></div></div></body>",
                "#row { display: flex; width: 300px; height: 50px; }"
                    + " #a { flex: 1; } #b { flex: 2; }");

            LayoutResult row = LayoutTestHarness.Find(root, "row");

            Assert.That(ChildWidth(row), Is.EqualTo(new[] { 100f, 200f }));
            Assert.That(ChildX(row), Is.EqualTo(new[] { 0f, 100f }));
        }

        [Test]
        public void FlexNone_KeepsItsSizeWhileASiblingGrows()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"row\"><span id=\"spacer\"></span><div id=\"body\"></div></div></body>",
                "#row { display: flex; width: 200px; height: 50px; }"
                    + " #spacer { flex: none; width: 40px; } #body { flex: 1; }");

            LayoutResult row = LayoutTestHarness.Find(root, "row");

            Assert.That(LayoutTestHarness.Find(root, "spacer").Width, Is.EqualTo(40f));
            Assert.That(LayoutTestHarness.Find(root, "body").Width, Is.EqualTo(160f));
            Assert.That(row.Children.Count, Is.EqualTo(2));
        }

        [Test]
        public void FlexGrowWithAGap_SharesWhatIsLeftAfterTheGap()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"row\"><div id=\"a\"></div><div id=\"b\"></div></div></body>",
                "#row { display: flex; width: 220px; height: 50px; column-gap: 20px; }"
                    + " #row > div { flex: 1; }");

            LayoutResult row = LayoutTestHarness.Find(root, "row");

            Assert.That(ChildWidth(row), Is.EqualTo(new[] { 100f, 100f }));
            Assert.That(ChildX(row), Is.EqualTo(new[] { 0f, 120f }));
        }

        [Test]
        public void FlexShrink_TakesBackTheOverflowInProportionToSize()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"row\"><div id=\"a\"></div><div id=\"b\"></div></div></body>",
                "#row { display: flex; width: 150px; height: 50px; }"
                    + " #a { width: 100px; } #b { width: 200px; }");

            LayoutResult row = LayoutTestHarness.Find(root, "row");

            // 150 pixels of overflow, weighted 100:200, so 50 comes off a and 100 off b.
            Assert.That(ChildWidth(row), Is.EqualTo(new[] { 50f, 100f }));
        }

        [Test]
        public void ShrinkZero_ProtectsAnItemFromBeingSqueezed()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"row\"><div id=\"a\"></div><div id=\"b\"></div></div></body>",
                "#row { display: flex; width: 150px; height: 50px; }"
                    + " #a { width: 100px; flex-shrink: 0; } #b { width: 200px; }");

            Assert.That(LayoutTestHarness.Find(root, "a").Width, Is.EqualTo(100f));
            Assert.That(LayoutTestHarness.Find(root, "b").Width, Is.EqualTo(50f));
        }

        [Test]
        public void AlignItemsBaseline_LinesUpTheFirstLinesOfTwoItems()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"row\"><p id=\"small\">a</p><p id=\"large\">b</p></div></body>",
                "#row { display: flex; align-items: baseline; width: 400px; }"
                    + " #small { font-size: 10px; line-height: 1; width: 50px; }"
                    + " #large { font-size: 40px; line-height: 1; width: 50px; }");

            // With no leading, each baseline sits 0.8 of the font size below the top, so the smaller
            // text drops by 32 - 8 to meet the larger one.
            Assert.That(LayoutTestHarness.Find(root, "small").Y, Is.EqualTo(24f).Within(0.01f));
            Assert.That(LayoutTestHarness.Find(root, "large").Y, Is.Zero);
        }

        [Test]
        public void AlignItemsBaseline_UsesTheBottomEdgeOfAnEmptyItem()
        {
            // The spacer trick: a zero-width box of a fixed height has no line of text, so its own
            // bottom edge is its baseline and it pushes the text beside it down to meet it.
            LayoutResult root = _harness.Solve(
                "<body><div id=\"row\"><span id=\"spacer\"></span><p id=\"text\">a</p></div></body>",
                "#row { display: flex; align-items: baseline; width: 400px; }"
                    + " #spacer { flex: none; width: 0; height: 100px; }"
                    + " #text { flex: 1; font-size: 20px; line-height: 1; }");

            Assert.That(LayoutTestHarness.Find(root, "spacer").Y, Is.Zero);
            Assert.That(
                LayoutTestHarness.Find(root, "text").Y,
                Is.EqualTo(84f).Within(0.01f),
                "100 down, less the text's own 16 pixel ascent");
        }

        [Test]
        public void AlignContentCenter_CentresTheLinesInABoxThatIsTallerThanThem()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"row\"><div id=\"a\"></div><div id=\"b\"></div></div></body>",
                "#row { display: flex; flex-wrap: wrap; align-content: center; align-items: start;"
                    + " width: 100px; height: 200px; }"
                    + " #row > div { width: 80px; height: 20px; flex: none; }");

            LayoutResult row = LayoutTestHarness.Find(root, "row");

            // Two lines of 20 in 200, so 160 is free and half of it goes above.
            Assert.That(ChildY(row), Is.EqualTo(new[] { 80f, 100f }));
        }

        [Test]
        public void AutoSideMargins_CentreABlockOfKnownWidth()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"inner\"></div></body>",
                "body { width: 400px; } #inner { width: 100px; height: 20px; margin: 0 auto; }");

            Assert.That(LayoutTestHarness.Find(root, "inner").X, Is.EqualTo(150f));
        }

        [Test]
        public void AutoSideMargins_LeaveAnOversizedBlockAtTheStart()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"inner\"></div></body>",
                "body { width: 100px; } #inner { width: 400px; height: 20px; margin: 0 auto; }");

            Assert.That(LayoutTestHarness.Find(root, "inner").X, Is.Zero);
        }

        [Test]
        public void AutoSideMargins_CentreAFlexItem()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"row\"><div id=\"inner\"></div></div></body>",
                "#row { display: flex; width: 400px; height: 50px; }"
                    + " #inner { width: 100px; flex: none; margin: 0 auto; }");

            Assert.That(LayoutTestHarness.Find(root, "inner").X, Is.EqualTo(150f));
        }

        [Test]
        public void MaxHeightInEm_ClampsATextBoxToItsFirstLines()
        {
            LayoutResult root = _harness.Solve(
                "<body><p id=\"clamped\">aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa</p></body>",
                "#clamped { width: 50px; font-size: 10px; line-height: 1.5;"
                    + " max-height: 1.5em; overflow: hidden; }");

            LayoutResult clamped = LayoutTestHarness.Find(root, "clamped");

            Assert.That(clamped.Height, Is.EqualTo(15f).Within(0.01f), "one line of 1.5em");
            Assert.That(clamped.Box.Style.Visual.ClipsContent, Is.True);
        }
    }
}
