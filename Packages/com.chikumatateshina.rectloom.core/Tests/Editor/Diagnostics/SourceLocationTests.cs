#nullable enable

using NUnit.Framework;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Tests.Diagnostics
{
    public sealed class SourceLocationTests
    {
        [Test]
        public void None_IsNotKnown()
        {
            Assert.That(SourceLocation.None.IsKnown, Is.False);
            Assert.That(SourceLocation.None.FilePath, Is.Null);
            Assert.That(SourceLocation.None.ToString(), Is.EqualTo("<unknown>"));
        }

        [Test]
        public void DefaultValue_EqualsNone()
        {
            SourceLocation location = default;

            Assert.That(location, Is.EqualTo(SourceLocation.None));
        }

        [Test]
        public void Constructor_KeepsFilePathLineAndColumn()
        {
            var location = new SourceLocation("Assets/UI/page.html", 12, 34);

            Assert.That(location.FilePath, Is.EqualTo("Assets/UI/page.html"));
            Assert.That(location.Line, Is.EqualTo(12));
            Assert.That(location.Column, Is.EqualTo(34));
            Assert.That(location.IsKnown, Is.True);
        }

        [Test]
        public void Constructor_ClampsLineAndColumnBelowOne()
        {
            var location = new SourceLocation("Assets/UI/page.html", 0, -5);

            Assert.That(location.Line, Is.EqualTo(SourceLocation.FirstIndex));
            Assert.That(location.Column, Is.EqualTo(SourceLocation.FirstIndex));
        }

        [Test]
        public void Constructor_WithNullPath_IsNotKnown()
        {
            var location = new SourceLocation(null, 3, 4);

            Assert.That(location.IsKnown, Is.False);
        }

        [Test]
        public void FileStart_PointsAtFirstLineAndColumn()
        {
            SourceLocation location = SourceLocation.FileStart("Assets/UI/theme.css");

            Assert.That(location.Line, Is.EqualTo(1));
            Assert.That(location.Column, Is.EqualTo(1));
            Assert.That(location.FilePath, Is.EqualTo("Assets/UI/theme.css"));
        }

        [Test]
        public void ToString_UsesPathLineColumnFormat()
        {
            var location = new SourceLocation("Assets/UI/page.html", 7, 2);

            Assert.That(location.ToString(), Is.EqualTo("Assets/UI/page.html(7,2)"));
        }

        [Test]
        public void Equality_ComparesAllParts()
        {
            var a = new SourceLocation("Assets/UI/page.html", 7, 2);
            var b = new SourceLocation("Assets/UI/page.html", 7, 2);
            var differentLine = new SourceLocation("Assets/UI/page.html", 8, 2);
            var differentFile = new SourceLocation("Assets/UI/other.html", 7, 2);

            Assert.That(a == b, Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a != differentLine, Is.True);
            Assert.That(a.Equals(differentFile), Is.False);
        }

        [Test]
        public void Equality_IsCaseSensitiveOnFilePath()
        {
            var lower = new SourceLocation("assets/ui/page.html", 1, 1);
            var upper = new SourceLocation("Assets/UI/page.html", 1, 1);

            Assert.That(lower.Equals(upper), Is.False);
        }
    }
}
