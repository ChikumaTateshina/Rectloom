#nullable enable

using NUnit.Framework;
using Rectloom.Core.Css.Values;
using UnityEngine;

namespace Rectloom.Core.Tests.Css
{
    public sealed class CssLengthTests
    {
        [TestCase("200px", CssLengthUnit.Pixel, 200f)]
        [TestCase("  12.5px  ", CssLengthUnit.Pixel, 12.5f)]
        [TestCase("-8px", CssLengthUnit.Pixel, -8f)]
        [TestCase("50%", CssLengthUnit.Percent, 50f)]
        [TestCase("33.3%", CssLengthUnit.Percent, 33.3f)]
        [TestCase("0", CssLengthUnit.Pixel, 0f)]
        [TestCase("10PX", CssLengthUnit.Pixel, 10f)]
        public void TryParse_ReadsSupportedUnits(string text, CssLengthUnit unit, float value)
        {
            Assert.That(CssLength.TryParse(text, out CssLength length), Is.True);
            Assert.That(length.Unit, Is.EqualTo(unit));
            Assert.That(length.Value, Is.EqualTo(value).Within(0.0001f));
        }

        [TestCase("auto")]
        [TestCase("AUTO")]
        public void TryParse_ReadsAuto(string text)
        {
            Assert.That(CssLength.TryParse(text, out CssLength length), Is.True);
            Assert.That(length.IsAuto, Is.True);
        }

        [TestCase("200")]
        [TestCase("1.5")]
        public void TryParse_RejectsANumberWithoutAUnit(string text)
        {
            Assert.That(
                CssLength.TryParse(text, out _),
                Is.False,
                "only a bare zero may omit its unit");
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        [TestCase("2em")]
        [TestCase("10rem")]
        [TestCase("calc(100% - 10px)")]
        [TestCase("px")]
        [TestCase("abc")]
        public void TryParse_RejectsUnsupportedValues(string? text)
        {
            Assert.That(CssLength.TryParse(text, out _), Is.False);
        }

        [Test]
        public void Resolve_ComputesPercentAgainstTheBasis()
        {
            Assert.That(CssLength.Percent(50f).Resolve(800f, 0f), Is.EqualTo(400f));
            Assert.That(CssLength.Pixels(32f).Resolve(800f, 0f), Is.EqualTo(32f));
            Assert.That(CssLength.Auto.Resolve(800f, 17f), Is.EqualTo(17f), "auto falls back");
        }

        [Test]
        public void Auto_IgnoresAnyValueGiven()
        {
            Assert.That(new CssLength(CssLengthUnit.Auto, 42f).Value, Is.Zero);
        }

        [Test]
        public void DefaultValue_IsAuto()
        {
            CssLength length = default;

            Assert.That(length, Is.EqualTo(CssLength.Auto));
            Assert.That(length.IsAuto, Is.True);
        }

        [Test]
        public void Equality_ComparesUnitAndValue()
        {
            Assert.That(CssLength.Pixels(10f) == CssLength.Pixels(10f), Is.True);
            Assert.That(CssLength.Pixels(10f) != CssLength.Percent(10f), Is.True);
            Assert.That(
                CssLength.Pixels(10f).GetHashCode(),
                Is.EqualTo(CssLength.Pixels(10f).GetHashCode()));
        }

        [Test]
        public void ToString_RoundTripsThroughTryParse()
        {
            foreach (CssLength original in new[] { CssLength.Auto, CssLength.Pixels(12.5f), CssLength.Percent(40f) })
            {
                Assert.That(CssLength.TryParse(original.ToString(), out CssLength parsed), Is.True);
                Assert.That(parsed, Is.EqualTo(original));
            }
        }
    }

    public sealed class EdgeSizesTests
    {
        [Test]
        public void All_SetsEveryEdge()
        {
            EdgeSizes edges = EdgeSizes.All(CssLength.Pixels(8f));

            Assert.That(edges.Top, Is.EqualTo(CssLength.Pixels(8f)));
            Assert.That(edges.Right, Is.EqualTo(CssLength.Pixels(8f)));
            Assert.That(edges.Bottom, Is.EqualTo(CssLength.Pixels(8f)));
            Assert.That(edges.Left, Is.EqualTo(CssLength.Pixels(8f)));
        }

        [Test]
        public void WithEdge_ChangesOnlyThatEdge()
        {
            EdgeSizes edges = EdgeSizes.Zero.WithTop(CssLength.Pixels(4f));

            Assert.That(edges.Top, Is.EqualTo(CssLength.Pixels(4f)));
            Assert.That(edges.Bottom, Is.EqualTo(CssLength.Zero));
        }

        [Test]
        public void Resolve_SumsOppositeEdgesAndTreatsAutoAsZero()
        {
            var edges = new EdgeSizes(
                CssLength.Pixels(4f),
                CssLength.Percent(10f),
                CssLength.Auto,
                CssLength.Pixels(6f));

            Assert.That(edges.ResolveHorizontal(200f), Is.EqualTo(26f).Within(0.0001f));
            Assert.That(edges.ResolveVertical(200f), Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void Equality_ComparesAllEdges()
        {
            Assert.That(EdgeSizes.All(CssLength.Zero) == EdgeSizes.Zero, Is.True);
            Assert.That(EdgeSizes.Zero != EdgeSizes.All(CssLength.Pixels(1f)), Is.True);
        }
    }

    public sealed class CssColorParserTests
    {
        private static void AssertColor(string text, byte r, byte g, byte b, byte a = 255)
        {
            Assert.That(CssColorParser.TryParse(text, out Color color), Is.True, text);

            var actual = (Color32)color;
            Assert.That(actual.r, Is.EqualTo(r), text + " red");
            Assert.That(actual.g, Is.EqualTo(g), text + " green");
            Assert.That(actual.b, Is.EqualTo(b), text + " blue");
            Assert.That(actual.a, Is.EqualTo(a), text + " alpha");
        }

        [Test]
        public void ShortHex_DoublesEachDigit()
        {
            AssertColor("#fff", 255, 255, 255);
            AssertColor("#abc", 0xAA, 0xBB, 0xCC);
            AssertColor("#f00f", 255, 0, 0, 255);
        }

        [Test]
        public void LongHex_ReadsBytes()
        {
            AssertColor("#ff8800", 255, 136, 0);
            AssertColor("#FF880080", 255, 136, 0, 128);
            AssertColor("  #102030  ", 16, 32, 48);
        }

        [Test]
        public void RgbFunction_ReadsNumbersAndPercentages()
        {
            AssertColor("rgb(255, 128, 0)", 255, 128, 0);
            AssertColor("rgb(100%, 0%, 50%)", 255, 0, 128);
            AssertColor("RGB(1,2,3)", 1, 2, 3);
        }

        [Test]
        public void RgbaFunction_ReadsAlpha()
        {
            Assert.That(CssColorParser.TryParse("rgba(0, 0, 0, 0.5)", out Color color), Is.True);
            Assert.That(color.a, Is.EqualTo(0.5f).Within(0.001f));

            Assert.That(CssColorParser.TryParse("rgba(0, 0, 0, 50%)", out Color percent), Is.True);
            Assert.That(percent.a, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void RgbFunction_AcceptsAnAlphaToo()
        {
            Assert.That(CssColorParser.TryParse("rgb(0 0 0 / 0.25)", out Color color), Is.True);
            Assert.That(color.a, Is.EqualTo(0.25f).Within(0.001f));
        }

        [Test]
        public void NamedColors_AreCaseInsensitive()
        {
            AssertColor("red", 255, 0, 0);
            AssertColor("RED", 255, 0, 0);
            AssertColor("rebeccapurple", 0x66, 0x33, 0x99);
            AssertColor("darkslategrey", 0x2F, 0x4F, 0x4F);
        }

        [Test]
        public void Transparent_IsFullyClear()
        {
            Assert.That(CssColorParser.TryParse("transparent", out Color color), Is.True);
            Assert.That(color.a, Is.Zero);
        }

        [Test]
        public void NamedColorTable_CoversTheStandardList()
        {
            Assert.That(CssNamedColors.Count, Is.EqualTo(148));
        }

        [TestCase("#ff")]
        [TestCase("#fffff")]
        [TestCase("#gggggg")]
        [TestCase("rgb(1, 2)")]
        [TestCase("rgb(1, 2, 3, 4, 5)")]
        [TestCase("hsl(0, 100%, 50%)")]
        [TestCase("notacolor")]
        [TestCase("")]
        [TestCase(null)]
        public void InvalidValues_AreRejected(string? text)
        {
            Assert.That(CssColorParser.TryParse(text, out _), Is.False);
        }

        [Test]
        public void ChannelsOutOfRange_AreClamped()
        {
            AssertColor("rgb(300, -20, 0)", 255, 0, 0);
        }
    }
}
