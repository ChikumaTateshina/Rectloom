#nullable enable

using System;
using NUnit.Framework;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Tests.Diagnostics
{
    public sealed class CompilerDiagnosticTests
    {
        private static readonly SourceLocation Location =
            new SourceLocation("Assets/UI/theme.css", 9, 5);

        [Test]
        public void Constructor_KeepsAllParts()
        {
            var diagnostic = new CompilerDiagnostic(
                DiagnosticCodes.Css.InvalidValue,
                DiagnosticSeverity.Warning,
                "Invalid value for width.",
                Location,
                "Use px or %.");

            Assert.That(diagnostic.Code, Is.EqualTo("CSS1002"));
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(diagnostic.Message, Is.EqualTo("Invalid value for width."));
            Assert.That(diagnostic.Location, Is.EqualTo(Location));
            Assert.That(diagnostic.Suggestion, Is.EqualTo("Use px or %."));
        }

        [Test]
        public void Location_IsMirroredByFilePathLineAndColumn()
        {
            var diagnostic = CompilerDiagnostic.Error(
                DiagnosticCodes.Html.DuplicateId,
                "Duplicate id.",
                Location);

            Assert.That(diagnostic.FilePath, Is.EqualTo("Assets/UI/theme.css"));
            Assert.That(diagnostic.Line, Is.EqualTo(9));
            Assert.That(diagnostic.Column, Is.EqualTo(5));
        }

        [Test]
        public void Constructor_WithoutLocation_HasNoKnownPosition()
        {
            var diagnostic = CompilerDiagnostic.Info(
                DiagnosticCodes.Internal.InvalidCompileRequest,
                "No HTML source.");

            Assert.That(diagnostic.Location, Is.EqualTo(SourceLocation.None));
            Assert.That(diagnostic.FilePath, Is.Null);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void Constructor_WithBlankCode_Throws(string? code)
        {
            Assert.Throws<ArgumentException>(
                () => new CompilerDiagnostic(code!, DiagnosticSeverity.Error, "message"));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void Constructor_WithBlankMessage_Throws(string? message)
        {
            Assert.Throws<ArgumentException>(
                () => new CompilerDiagnostic("CSS1001", DiagnosticSeverity.Error, message!));
        }

        [Test]
        public void Factories_SetMatchingSeverity()
        {
            Assert.That(CompilerDiagnostic.Info("CSS1001", "m").Severity,
                Is.EqualTo(DiagnosticSeverity.Info));
            Assert.That(CompilerDiagnostic.Warning("CSS1001", "m").Severity,
                Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(CompilerDiagnostic.Error("CSS1001", "m").Severity,
                Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(CompilerDiagnostic.Fatal("CSS1001", "m").Severity,
                Is.EqualTo(DiagnosticSeverity.Fatal));
        }

        [TestCase(DiagnosticSeverity.Info, false)]
        [TestCase(DiagnosticSeverity.Warning, false)]
        [TestCase(DiagnosticSeverity.Error, true)]
        [TestCase(DiagnosticSeverity.Fatal, true)]
        public void IsErrorOrWorse_MatchesSeverityOrder(DiagnosticSeverity severity, bool expected)
        {
            var diagnostic = new CompilerDiagnostic("CSS1001", severity, "m");

            Assert.That(diagnostic.IsErrorOrWorse, Is.EqualTo(expected));
        }

        [Test]
        public void ToString_WithLocation_StartsWithSourcePosition()
        {
            var diagnostic = CompilerDiagnostic.Error(
                DiagnosticCodes.Css.InvalidValue,
                "Invalid value for width.",
                Location);

            Assert.That(
                diagnostic.ToString(),
                Is.EqualTo("Assets/UI/theme.css(9,5): error CSS1002: Invalid value for width."));
        }

        [Test]
        public void ToString_WithoutLocation_OmitsSourcePosition()
        {
            var diagnostic = CompilerDiagnostic.Warning(
                DiagnosticCodes.Css.UnknownProperty,
                "Unknown property.");

            Assert.That(diagnostic.ToString(), Is.EqualTo("warning CSS1001: Unknown property."));
        }

        [Test]
        public void ToString_WithSuggestion_AppendsIt()
        {
            var diagnostic = CompilerDiagnostic.Warning(
                DiagnosticCodes.Css.UnknownProperty,
                "Unknown property.",
                SourceLocation.None,
                "Did you mean background-color?");

            Assert.That(
                diagnostic.ToString(),
                Is.EqualTo("warning CSS1001: Unknown property. (Did you mean background-color?)"));
        }
    }
}
