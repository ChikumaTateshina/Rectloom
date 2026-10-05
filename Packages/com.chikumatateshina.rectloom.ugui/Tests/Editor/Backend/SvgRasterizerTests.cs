#nullable enable

using System.Text;
using NUnit.Framework;
using Rectloom.Ugui.Backend;
using UnityEngine;

namespace Rectloom.Ugui.Tests.Backend
{
    /// <summary>
    /// Covers turning an SVG into pixels, on its own, before anything stores or compiles the result.
    /// </summary>
    /// <remarks>
    /// Asserted on the pixels rather than on "a PNG came back", because the ways this goes wrong all
    /// produce a perfectly valid image of the wrong thing: upside down, stretched, blank, or cropped to
    /// the shapes instead of the document's own frame.
    /// </remarks>
    public sealed class SvgRasterizerTests
    {
        private Texture2D? _decoded;

        [SetUp]
        public void SetUp()
        {
            Assume.That(SvgRasterizer.Instance.IsAvailable, "needs the Vector Graphics package");
        }

        [TearDown]
        public void TearDown()
        {
            if (_decoded != null)
            {
                Object.DestroyImmediate(_decoded);
            }
        }

        private Texture2D Rasterize(string svg, int maxWidth, int maxHeight)
        {
            Assert.That(
                SvgRasterizer.Instance.TryRasterize(
                    Encoding.UTF8.GetBytes(svg), maxWidth, maxHeight, out byte[] png, out string error),
                Is.True,
                error);

            _decoded = new Texture2D(2, 2);
            Assert.That(_decoded.LoadImage(png), Is.True, "the result is a readable PNG");
            return _decoded;
        }

        /// <summary>Reads a pixel by position from the top-left, the way the SVG itself is written.</summary>
        private static Color At(Texture2D texture, float x, float y)
        {
            return texture.GetPixel(
                Mathf.Clamp(Mathf.FloorToInt(x * texture.width), 0, texture.width - 1),
                Mathf.Clamp(Mathf.FloorToInt((1f - y) * texture.height), 0, texture.height - 1));
        }

        [Test]
        public void TheImageIsTheRightWayUp()
        {
            // SVG measures down from the top and a texture measures up from the bottom, so this is the
            // mistake most likely to be made and least likely to be noticed on a symmetrical logo.
            Texture2D texture = Rasterize(
                "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10'>"
                    + "<rect width='10' height='5' fill='#ff0000'/>"
                    + "<rect y='5' width='10' height='5' fill='#0000ff'/></svg>",
                64,
                64);

            Color top = At(texture, 0.5f, 0.25f);
            Color bottom = At(texture, 0.5f, 0.75f);

            Assert.That(top.r, Is.GreaterThan(0.9f), "red is at the top");
            Assert.That(top.b, Is.LessThan(0.1f));
            Assert.That(bottom.b, Is.GreaterThan(0.9f), "blue is at the bottom");
            Assert.That(bottom.r, Is.LessThan(0.1f));
        }

        [Test]
        public void TheImageKeepsItsOwnShape()
        {
            Texture2D texture = Rasterize(
                "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 30 10'>"
                    + "<rect width='30' height='10' fill='#00ff00'/></svg>",
                120,
                120);

            Assert.That(texture.width, Is.EqualTo(120));
            Assert.That(texture.height, Is.EqualTo(40), "fitted inside the box, not stretched to it");
        }

        [Test]
        public void TheDocumentsFrameIsKept_NotJustItsShapes()
        {
            // A logo drawn with space around it has to keep that space, or it sits differently in its
            // box from how it was designed.
            Texture2D texture = Rasterize(
                "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10'>"
                    + "<rect x='5' y='5' width='5' height='5' fill='#ff0000'/></svg>",
                64,
                64);

            Assert.That(At(texture, 0.25f, 0.25f).a, Is.LessThan(0.1f), "the empty quarter stays empty");
            Assert.That(At(texture, 0.75f, 0.75f).r, Is.GreaterThan(0.9f));
            Assert.That(At(texture, 0.75f, 0.75f).a, Is.GreaterThan(0.9f));
        }

        [Test]
        public void TransparentAreasStayTransparent()
        {
            Texture2D texture = Rasterize(
                "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10'>"
                    + "<circle cx='5' cy='5' r='3' fill='#ffffff'/></svg>",
                64,
                64);

            Assert.That(At(texture, 0.02f, 0.02f).a, Is.LessThan(0.05f), "a corner outside the circle");
            Assert.That(At(texture, 0.5f, 0.5f).a, Is.GreaterThan(0.95f), "the middle of the circle");
        }

        [Test]
        public void AGradientIsDrawn()
        {
            Texture2D texture = Rasterize(
                "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10'><defs>"
                    + "<linearGradient id='g' x1='0' x2='1' y1='0' y2='0'>"
                    + "<stop offset='0' stop-color='#ff0000'/><stop offset='1' stop-color='#0000ff'/>"
                    + "</linearGradient></defs><rect width='10' height='10' fill='url(#g)'/></svg>",
                64,
                64);

            Color left = At(texture, 0.1f, 0.5f);
            Color right = At(texture, 0.9f, 0.5f);

            Assert.That(left.r, Is.GreaterThan(right.r), "red fades out from the left");
            Assert.That(right.b, Is.GreaterThan(left.b), "blue fades in towards the right");
        }

        [TestCase("")]
        [TestCase("this is not an svg")]
        [TestCase("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10'></svg>")]
        public void SomethingThatCannotBeDrawn_IsExplainedRatherThanThrown(string svg)
        {
            Assert.That(
                SvgRasterizer.Instance.TryRasterize(
                    Encoding.UTF8.GetBytes(svg), 64, 64, out byte[] png, out string error),
                Is.False);

            Assert.That(png, Is.Empty);
            Assert.That(error, Is.Not.Empty);
        }
    }
}
