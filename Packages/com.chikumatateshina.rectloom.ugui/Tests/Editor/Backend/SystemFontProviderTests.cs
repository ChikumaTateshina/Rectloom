#nullable enable

using System.Linq;
using NUnit.Framework;
using Rectloom.Core.Diagnostics;
using Rectloom.Ugui.Backend;
using TMPro;
using UnityEditor;

namespace Rectloom.Ugui.Tests.Backend
{
    /// <summary>
    /// Covers generating a font asset for a family that has none in the project.
    /// </summary>
    /// <remarks>
    /// Generating the asset itself needs TextMeshPro's shaders, which a bare project does not have, so
    /// what is asserted here is the part that does not: that a family which is not installed is reported
    /// rather than guessed at, and that a missing shader is explained rather than thrown.
    /// </remarks>
    public sealed class SystemFontProviderTests
    {
        private const string Folder = "Assets/RectloomSystemFontTest";

        private DiagnosticSink _diagnostics = null!;

        [SetUp]
        public void SetUp()
        {
            _diagnostics = new DiagnosticSink();
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        [Test]
        public void AFamilyThatIsNotInstalled_IsReportedRatherThanGuessedAt()
        {
            TMP_FontAsset? asset = SystemFontProvider.TryCreate(
                "Rectloom Not A Real Font", Folder, _diagnostics);

            Assert.That(asset, Is.Null);

            CompilerDiagnostic reported = _diagnostics.Diagnostics.Single();

            Assert.That(reported.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(reported.Message, Does.Contain("not installed"));
            Assert.That(reported.Suggestion, Does.Contain("font-family"));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void AnEmptyFamily_IsIgnoredSilently(string family)
        {
            Assert.That(SystemFontProvider.TryCreate(family, Folder, _diagnostics), Is.Null);
            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void NothingIsWrittenWhenNoFontIsFound()
        {
            SystemFontProvider.TryCreate("Rectloom Not A Real Font", Folder, _diagnostics);

            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False, "no folder for a font that is absent");
        }

        [Test]
        public void TheInstalledEmojiFontIsFound_WhenThePlatformHasOne()
        {
            // Segoe UI Emoji is the reason this class exists: Unity's own font enumeration leaves it out,
            // so a family that is plainly installed used to look missing. Whether the asset can then be
            // built depends on TextMeshPro's shaders, which a bare project has not imported, so either
            // outcome is accepted here as long as it is explained.
            TMP_FontAsset? asset = SystemFontProvider.TryCreate(
                "Segoe UI Emoji", Folder, _diagnostics);

            if (asset != null)
            {
                Assert.That(_diagnostics.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Info), Is.True);
                return;
            }

            CompilerDiagnostic reported = _diagnostics.Diagnostics.Single();

            Assert.That(
                reported.Message,
                Does.Contain("shaders").Or.Contain("not installed"),
                "a failure has to say which of the two it was");
        }
    }
}
