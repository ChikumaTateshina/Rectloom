#nullable enable

using NUnit.Framework;
using Rectloom.Core.Layout;
using UnityEngine;

namespace Rectloom.Core.Tests.Layout
{
    /// <summary>
    /// Whole-document layout snapshots.
    /// </summary>
    /// <remarks>
    /// These assert on a readable rendering of the solved tree rather than on a binary artefact, as
    /// docs/09 section 9 requires, so a change in behaviour shows up as a readable diff.
    /// <para>
    /// The expectations are written out in full on purpose. A golden test that recomputes its own
    /// expectation from the same code it is testing proves nothing.
    /// </para>
    /// </remarks>
    public sealed class LayoutGoldenTests
    {
        private LayoutTestHarness _harness = null!;

        [SetUp]
        public void SetUp()
        {
            _harness = new LayoutTestHarness { Viewport = new Vector2(400f, 300f) };
        }

        [Test]
        public void SettingsPanel()
        {
            const string html =
                "<body>"
                + "<div id=\"panel\" class=\"panel\">"
                + "<h1>Settings</h1>"
                + "<p>Description</p>"
                + "<button id=\"apply\">Apply</button>"
                + "</div>"
                + "</body>";

            const string css =
                ".panel { width: 300px; padding: 10px; }"
                + " h1 { font-size: 20px; line-height: 1; }"
                + " p { font-size: 10px; line-height: 1; margin: 5px 0; }"
                + " #apply { width: 100px; height: 30px; }";

            const string expected =
                "<body> 0,0 400x90\n"
                + "  <div id=\"panel\"> 0,0 300x90\n"
                + "    <h1> \"Settings\" 0,0 280x20\n"
                + "    <p> \"Description\" 0,25 280x10\n"
                + "    <button id=\"apply\"> \"Apply\" 0,40 100x30";

            Assert.That(LayoutTestHarness.Describe(_harness.Solve(html, css)), Is.EqualTo(expected));
            Assert.That(_harness.Diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void FlexToolbar()
        {
            const string html =
                "<body>"
                + "<div id=\"bar\">"
                + "<div id=\"left\"></div>"
                + "<div id=\"right\"></div>"
                + "</div>"
                + "</body>";

            const string css =
                "#bar { display: flex; width: 400px; height: 40px;"
                + " justify-content: space-between; align-items: center; padding: 0 8px; }"
                + " #bar > div { width: 60px; height: 20px; }";

            const string expected =
                "<body> 0,0 400x40\n"
                + "  <div id=\"bar\"> 0,0 400x40\n"
                + "    <div id=\"left\"> 0,10 60x20\n"
                + "    <div id=\"right\"> 324,10 60x20";

            Assert.That(LayoutTestHarness.Describe(_harness.Solve(html, css)), Is.EqualTo(expected));
        }

        [Test]
        public void NestedColumnsWithAnAbsoluteBadge()
        {
            const string html =
                "<body>"
                + "<div id=\"card\">"
                + "<div id=\"body\"><div id=\"a\"></div><div id=\"b\"></div></div>"
                + "<div id=\"badge\"></div>"
                + "</div>"
                + "</body>";

            const string css =
                "#card { width: 200px; height: 120px; padding: 10px; }"
                + " #body { display: flex; flex-direction: column; gap: 6px; }"
                + " #body > div { height: 24px; }"
                + " #badge { position: absolute; top: 0; right: 0; width: 20px; height: 20px; }";

            const string expected =
                "<body> 0,0 400x120\n"
                + "  <div id=\"card\"> 0,0 200x120\n"
                + "    <div id=\"body\"> 0,0 180x54\n"
                + "      <div id=\"a\"> 0,0 180x24\n"
                + "      <div id=\"b\"> 0,30 180x24\n"
                + "    <div id=\"badge\"> 160,0 20x20";

            Assert.That(LayoutTestHarness.Describe(_harness.Solve(html, css)), Is.EqualTo(expected));
        }

        [Test]
        public void GoldenOutputIsStableAcrossRuns()
        {
            const string html = "<body><div id=\"a\"><p>text</p></div></body>";
            const string css = "#a { width: 50%; padding: 4px; } p { font-size: 10px; line-height: 1; }";

            string first = LayoutTestHarness.Describe(
                new LayoutTestHarness { Viewport = new Vector2(400f, 300f) }.Solve(html, css));
            string second = LayoutTestHarness.Describe(
                new LayoutTestHarness { Viewport = new Vector2(400f, 300f) }.Solve(html, css));

            Assert.That(second, Is.EqualTo(first));
        }
    }
}
