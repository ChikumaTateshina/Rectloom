#nullable enable

using NUnit.Framework;
using Rectloom.Core.Assets;

namespace Rectloom.Core.Tests.Assets
{
    /// <summary>
    /// Covers reading an image that markup carries inline rather than referencing.
    /// </summary>
    /// <remarks>
    /// Decoding is tested on its own, without the asset database, because what counts as a readable
    /// payload is a rule of its own and a failure here must not be confused with a failure to write a
    /// file.
    /// </remarks>
    public sealed class DataUriTests
    {
        /// <summary>A one pixel opaque red PNG.</summary>
        private const string OnePixelPng =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/"
            + "iZk9HQAAAABJRU5ErkJggg==";

        private const string PngUri = "data:image/png;base64," + OnePixelPng;

        [TestCase("data:image/png;base64,AAAA", ExpectedResult = true)]
        [TestCase("  data:image/png;base64,AAAA", ExpectedResult = true)]
        [TestCase("DATA:image/png;base64,AAAA", ExpectedResult = true)]
        [TestCase("./icon.png", ExpectedResult = false)]
        [TestCase("Assets/UI/icon.png", ExpectedResult = false)]
        [TestCase(null, ExpectedResult = false)]
        public bool IsDataUri_RecognisesTheScheme(string? value)
        {
            return DataUri.IsDataUri(value);
        }

        [Test]
        public void TryDecode_ReadsABase64Png()
        {
            Assert.That(DataUri.TryDecode(PngUri, out DataUriPayload payload, out string error), Is.True, error);

            Assert.That(payload.MediaType, Is.EqualTo("image/png"));
            Assert.That(payload.FileExtension, Is.EqualTo(".png"));
            Assert.That(payload.Bytes.Length, Is.EqualTo(70));
            Assert.That(payload.Bytes[1], Is.EqualTo((byte)'P'), "the PNG signature survived");
        }

        [Test]
        public void TryDecode_IgnoresWhitespaceInTheData()
        {
            string wrapped = "data:image/png;base64,\n  " + OnePixelPng.Insert(20, "\n   ");

            Assert.That(DataUri.TryDecode(wrapped, out DataUriPayload payload, out string error), Is.True, error);
            Assert.That(payload.Bytes.Length, Is.EqualTo(70));
        }

        [Test]
        public void TryDecode_ReadsJpeg()
        {
            Assert.That(
                DataUri.TryDecode("data:image/jpeg;base64,AAAA", out DataUriPayload payload, out _),
                Is.True);

            Assert.That(payload.FileExtension, Is.EqualTo(".jpg"));
        }

        [TestCase("data:image/png,AAAA", "base64")]
        [TestCase("data:image/svg+xml;base64,AAAA", "SVG")]
        [TestCase("data:text/plain;base64,AAAA", "not an image format")]
        [TestCase("data:image/png;base64,====", "malformed")]
        [TestCase("data:image/png;base64,", "empty")]
        [TestCase("data:image/png;base64", "comma")]
        public void TryDecode_ExplainsWhyItCannotRead(string uri, string expectedReason)
        {
            Assert.That(DataUri.TryDecode(uri, out _, out string error), Is.False);
            Assert.That(error, Does.Contain(expectedReason));
        }

        [Test]
        public void ContentName_IsStableAndDerivedFromTheBytes()
        {
            DataUri.TryDecode(PngUri, out DataUriPayload first, out _);
            DataUri.TryDecode(PngUri, out DataUriPayload again, out _);
            DataUri.TryDecode("data:image/png;base64,AAAB", out DataUriPayload other, out _);

            Assert.That(first.ContentName, Is.EqualTo(again.ContentName), "the same bytes name one asset");
            Assert.That(first.ContentName, Is.Not.EqualTo(other.ContentName));
            Assert.That(first.ContentName, Does.EndWith(".png"));
            Assert.That(first.ContentName.Length, Is.EqualTo(16 + 4));
        }
    }
}
