#nullable enable

using System.Linq;
using NUnit.Framework;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Layout;

namespace Rectloom.Core.Tests.Layout
{
    /// <summary>
    /// Covers the root box, whose rectangle becomes the generated canvas and therefore decides what the
    /// output contains.
    /// </summary>
    public sealed class RootBoxTests
    {
        private LayoutTestHarness _harness = null!;

        [SetUp]
        public void SetUp()
        {
            _harness = new LayoutTestHarness();
        }

        private CompilerDiagnostic[] Overflow()
        {
            return _harness.Diagnostics.Diagnostics
                .Where(d => d.Code == DiagnosticCodes.Layout.ContentOutsideRoot)
                .ToArray();
        }

        [Test]
        public void PaddingOnTheRoot_ThatPushesAChildOutOfIt_IsReported()
        {
            // The shape of a page laid out for print: the root carries the page margin, and a child
            // sized to the whole page then does not fit inside it. The child ends up offset from the
            // canvas by exactly that padding, which reads as the canvas being misaligned.
            LayoutResult root = _harness.Solve(
                "<body><div id=\"page\"></div></body>",
                "body { width: 1920px; height: 1080px; padding: 30px; }"
                    + " #page { width: 1920px; height: 1080px; }");

            // A child's position is relative to the root's content box, so the padding shows up as the
            // content box's own inset rather than in the child's coordinates. The backend adds it back,
            // which is how the child ends up 30px inside a canvas that is no bigger than the child.
            Assert.That(root.ContentX, Is.EqualTo(30f));
            Assert.That(LayoutTestHarness.Find(root, "page").X, Is.Zero);
            Assert.That(LayoutTestHarness.Find(root, "page").Width, Is.EqualTo(root.Width));

            CompilerDiagnostic[] reported = Overflow();

            Assert.That(reported.Length, Is.EqualTo(1));
            Assert.That(reported[0].Severity, Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(reported[0].Message, Does.Contain("30px on the right"));
            Assert.That(reported[0].Message, Does.Contain("30px on the bottom"));
            Assert.That(reported[0].Suggestion, Does.Contain("padding"));
        }

        [Test]
        public void AChildThatFitsInsideTheRoot_IsNotReported()
        {
            _harness.Solve(
                "<body><div id=\"page\"></div></body>",
                "body { width: 1920px; height: 1080px; padding: 30px; }"
                    + " #page { width: 100px; height: 100px; }");

            Assert.That(Overflow(), Is.Empty);
        }

        [Test]
        public void AChildTooLargeForTheRoot_IsReportedWithoutBlamingPadding()
        {
            _harness.Solve(
                "<body><div id=\"page\"></div></body>",
                "body { width: 100px; height: 100px; }"
                    + " #page { width: 400px; height: 100px; }");

            CompilerDiagnostic[] reported = Overflow();

            Assert.That(reported.Length, Is.EqualTo(1));
            Assert.That(reported[0].Message, Does.Contain("300px on the right"));
            Assert.That(reported[0].Message, Does.Not.Contain("on the bottom"));
            Assert.That(reported[0].Suggestion, Does.Not.Contain("padding"));
        }

        [Test]
        public void AnEmptyRoot_IsNotReported()
        {
            _harness.Solve("<body></body>", "body { width: 100px; height: 100px; padding: 10px; }");

            Assert.That(Overflow(), Is.Empty);
        }
    }
}
