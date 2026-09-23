#nullable enable

using NUnit.Framework;
using Rectloom.Core.Layout;

namespace Rectloom.Core.Tests.Layout
{
    /// <summary>
    /// The flex assertions required by docs/09 section 4.
    /// </summary>
    public sealed class FlexLayoutTests
    {
        private const string ThreeItems =
            "<body><div id=\"row\"><div id=\"a\"></div><div id=\"b\"></div><div id=\"c\"></div></div></body>";

        private LayoutTestHarness _harness = null!;

        [SetUp]
        public void SetUp()
        {
            _harness = new LayoutTestHarness();
        }

        private LayoutResult SolveRow(string containerRules, string itemRules = "width: 50px; height: 20px;")
        {
            return _harness.Solve(
                ThreeItems,
                "#row { display: flex; " + containerRules + " } #row > div { " + itemRules + " }");
        }

        private LayoutResult SolveColumn(string containerRules, string itemRules = "width: 50px; height: 40px;")
        {
            return _harness.Solve(
                ThreeItems,
                "#row { display: flex; flex-direction: column; " + containerRules + " }"
                    + " #row > div { " + itemRules + " }");
        }

        private static float[] ChildX(LayoutResult root)
        {
            LayoutResult row = LayoutTestHarness.Find(root, "row");
            var values = new float[row.Children.Count];

            for (int index = 0; index < row.Children.Count; index++)
            {
                values[index] = row.Children[index].X;
            }

            return values;
        }

        private static float[] ChildY(LayoutResult root)
        {
            LayoutResult row = LayoutTestHarness.Find(root, "row");
            var values = new float[row.Children.Count];

            for (int index = 0; index < row.Children.Count; index++)
            {
                values[index] = row.Children[index].Y;
            }

            return values;
        }

        [Test]
        public void FlexRow_PlacesItemsLeftToRight()
        {
            LayoutResult root = SolveRow("width: 300px; height: 100px;");

            Assert.That(ChildX(root), Is.EqualTo(new[] { 0f, 50f, 100f }));
            Assert.That(ChildY(root), Is.EqualTo(new[] { 0f, 0f, 0f }));
        }

        [Test]
        public void FlexColumn_PlacesItemsTopToBottom()
        {
            LayoutResult root = SolveColumn("width: 200px; height: 300px; align-items: start;");

            Assert.That(ChildY(root), Is.EqualTo(new[] { 0f, 40f, 80f }));
            Assert.That(ChildX(root), Is.EqualTo(new[] { 0f, 0f, 0f }));
        }

        [Test]
        public void Gap_SeparatesItemsOnTheMainAxis()
        {
            LayoutResult root = SolveRow("width: 300px; height: 100px; gap: 10px;");

            Assert.That(ChildX(root), Is.EqualTo(new[] { 0f, 60f, 120f }));
        }

        [Test]
        public void Gap_AppliesToColumnsToo()
        {
            LayoutResult root = SolveColumn("width: 200px; height: 300px; gap: 8px;");

            Assert.That(ChildY(root), Is.EqualTo(new[] { 0f, 48f, 96f }));
        }

        [Test]
        public void JustifyStart_LeavesFreeSpaceAtTheEnd()
        {
            LayoutResult root = SolveRow("width: 300px; height: 100px; justify-content: start;");

            Assert.That(ChildX(root), Is.EqualTo(new[] { 0f, 50f, 100f }));
        }

        [Test]
        public void JustifyCenter_SplitsFreeSpaceEvenly()
        {
            LayoutResult root = SolveRow("width: 300px; height: 100px; justify-content: center;");

            Assert.That(ChildX(root), Is.EqualTo(new[] { 75f, 125f, 175f }));
        }

        [Test]
        public void JustifyEnd_PushesItemsToTheEnd()
        {
            LayoutResult root = SolveRow("width: 300px; height: 100px; justify-content: end;");

            Assert.That(ChildX(root), Is.EqualTo(new[] { 150f, 200f, 250f }));
        }

        [Test]
        public void JustifySpaceBetween_PutsNoSpaceAtTheEdges()
        {
            LayoutResult root = SolveRow("width: 300px; height: 100px; justify-content: space-between;");

            Assert.That(ChildX(root), Is.EqualTo(new[] { 0f, 125f, 250f }));
        }

        [Test]
        public void JustifySpaceAround_GivesEdgesHalfTheInnerGap()
        {
            LayoutResult root = SolveRow("width: 300px; height: 100px; justify-content: space-around;");

            Assert.That(ChildX(root), Is.EqualTo(new[] { 25f, 125f, 225f }));
        }

        [Test]
        public void JustifySpaceBetween_WithOneItem_KeepsItAtTheStart()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"row\"><div id=\"a\"></div></div></body>",
                "#row { display: flex; width: 300px; height: 100px; justify-content: space-between; }"
                    + " #a { width: 50px; height: 20px; }");

            Assert.That(LayoutTestHarness.Find(root, "a").X, Is.Zero);
        }

        [Test]
        public void AlignStart_PutsItemsAtTheTopOfARow()
        {
            LayoutResult root = SolveRow("width: 300px; height: 100px; align-items: start;");

            Assert.That(ChildY(root), Is.EqualTo(new[] { 0f, 0f, 0f }));
        }

        [Test]
        public void AlignCenter_CentresItemsOnTheCrossAxis()
        {
            LayoutResult root = SolveRow("width: 300px; height: 100px; align-items: center;");

            Assert.That(ChildY(root), Is.EqualTo(new[] { 40f, 40f, 40f }));
        }

        [Test]
        public void AlignEnd_PutsItemsAtTheBottomOfARow()
        {
            LayoutResult root = SolveRow("width: 300px; height: 100px; align-items: end;");

            Assert.That(ChildY(root), Is.EqualTo(new[] { 80f, 80f, 80f }));
        }

        [Test]
        public void AlignStretch_FillsTheCrossAxisForItemsWithoutASize()
        {
            LayoutResult root = SolveRow(
                "width: 300px; height: 100px; align-items: stretch;",
                "width: 50px;");

            LayoutResult row = LayoutTestHarness.Find(root, "row");

            foreach (LayoutResult child in row.Children)
            {
                Assert.That(child.Height, Is.EqualTo(100f));
                Assert.That(child.Y, Is.Zero);
            }
        }

        [Test]
        public void AlignStretch_LeavesItemsWithAnExplicitSizeAlone()
        {
            LayoutResult root = SolveRow("width: 300px; height: 100px; align-items: stretch;");

            Assert.That(LayoutTestHarness.Find(root, "a").Height, Is.EqualTo(20f));
        }

        [Test]
        public void AlignStretch_InAColumnFillsTheInlineAxis()
        {
            LayoutResult root = SolveColumn(
                "width: 200px; height: 300px; align-items: stretch;",
                "height: 40px;");

            LayoutResult row = LayoutTestHarness.Find(root, "row");

            foreach (LayoutResult child in row.Children)
            {
                Assert.That(child.Width, Is.EqualTo(200f));
                Assert.That(child.X, Is.Zero);
            }
        }

        [Test]
        public void AlignCenter_InAColumnCentresOnTheInlineAxis()
        {
            LayoutResult root = SolveColumn("width: 200px; height: 300px; align-items: center;");

            Assert.That(ChildX(root), Is.EqualTo(new[] { 75f, 75f, 75f }));
        }

        [Test]
        public void AutoHeightRow_TakesTheHeightOfItsTallestItem()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"row\"><div id=\"a\"></div><div id=\"b\"></div></div></body>",
                "#row { display: flex; width: 300px; align-items: start; }"
                    + " #a { width: 10px; height: 30px; } #b { width: 10px; height: 70px; }");

            Assert.That(LayoutTestHarness.Find(root, "row").Height, Is.EqualTo(70f));
        }

        [Test]
        public void AutoHeightRow_StretchesShortItemsToTheTallest()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"row\"><div id=\"a\"></div><div id=\"b\"></div></div></body>",
                "#row { display: flex; width: 300px; align-items: stretch; }"
                    + " #a { width: 10px; } #b { width: 10px; height: 70px; }");

            Assert.That(LayoutTestHarness.Find(root, "a").Height, Is.EqualTo(70f));
        }

        [Test]
        public void ItemMargins_CountTowardsTheMainAxis()
        {
            LayoutResult root = SolveRow(
                "width: 300px; height: 100px;",
                "width: 50px; height: 20px; margin: 0 5px;");

            Assert.That(ChildX(root), Is.EqualTo(new[] { 5f, 65f, 125f }));
        }

        [Test]
        public void AutoWidthItem_ShrinksToItsContent()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"row\"><div id=\"a\">abcd</div></div></body>",
                "#row { display: flex; width: 300px; } #a { font-size: 10px; }");

            Assert.That(
                LayoutTestHarness.Find(root, "a").Width,
                Is.EqualTo(20f).Within(0.01f),
                "four characters at half the 10px font size");
        }

        [Test]
        public void NestedFlex_LaysOutInBothDirections()
        {
            LayoutResult root = _harness.Solve(
                "<body><div id=\"outer\"><div id=\"col\"><div id=\"a\"></div><div id=\"b\"></div></div>"
                    + "<div id=\"side\"></div></div></body>",
                "#outer { display: flex; width: 400px; height: 200px; }"
                    + " #col { display: flex; flex-direction: column; width: 100px; height: 200px; }"
                    + " #col > div { width: 100px; height: 60px; }"
                    + " #side { width: 300px; height: 200px; }");

            LayoutTestHarness.AssertRect(LayoutTestHarness.Find(root, "col"), 0f, 0f, 100f, 200f);
            LayoutTestHarness.AssertRect(LayoutTestHarness.Find(root, "side"), 100f, 0f, 300f, 200f);
            LayoutTestHarness.AssertRect(LayoutTestHarness.Find(root, "a"), 0f, 0f, 100f, 60f);
            LayoutTestHarness.AssertRect(LayoutTestHarness.Find(root, "b"), 0f, 60f, 100f, 60f);
        }

        [Test]
        public void FlexContainerPadding_ShrinksTheMainAxis()
        {
            LayoutResult root = SolveRow(
                "width: 300px; height: 100px; padding: 0 20px; justify-content: end;");

            Assert.That(
                ChildX(root),
                Is.EqualTo(new[] { 110f, 160f, 210f }),
                "free space is measured inside the 260px content box");
        }
    }
}
