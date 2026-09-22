#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rectloom.Core.Compilation;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Tests.Compilation
{
    public sealed class CompileResultTests
    {
        [Test]
        public void Create_WithNoDiagnostics_IsSuccessful()
        {
            CompileResult result = CompileResult.Create(null, Array.Empty<CompilerDiagnostic>());

            Assert.That(result.Success, Is.True);
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.Statistics, Is.Not.Null);
        }

        [Test]
        public void Create_WithWarningsOnly_IsStillSuccessful()
        {
            var diagnostics = new[]
            {
                CompilerDiagnostic.Info(DiagnosticCodes.Css.UnknownProperty, "info"),
                CompilerDiagnostic.Warning(DiagnosticCodes.Html.UnknownElement, "warning"),
            };

            CompileResult result = CompileResult.Create(null, diagnostics);

            Assert.That(result.Success, Is.True);
        }

        [TestCase(DiagnosticSeverity.Error)]
        [TestCase(DiagnosticSeverity.Fatal)]
        public void Create_WithErrorOrFatal_IsNotSuccessful(DiagnosticSeverity severity)
        {
            var diagnostics = new[]
            {
                CompilerDiagnostic.Warning(DiagnosticCodes.Html.UnknownElement, "warning"),
                new CompilerDiagnostic(DiagnosticCodes.Css.InvalidValue, severity, "bad"),
            };

            CompileResult result = CompileResult.Create(null, diagnostics);

            Assert.That(result.Success, Is.False);
        }

        [Test]
        public void Create_KeepsDiagnosticsAndStatistics()
        {
            var statistics = new CompileStatistics { NodeCount = 7, CreatedObjectCount = 3 };
            IReadOnlyList<CompilerDiagnostic> diagnostics = new[]
            {
                CompilerDiagnostic.Warning(DiagnosticCodes.Html.UnknownElement, "warning"),
            };

            CompileResult result = CompileResult.Create(null, diagnostics, statistics);

            Assert.That(result.Diagnostics, Is.SameAs(diagnostics));
            Assert.That(result.Statistics.NodeCount, Is.EqualTo(7));
            Assert.That(result.Statistics.CreatedObjectCount, Is.EqualTo(3));
        }

        [Test]
        public void Failed_HasNoRootObjectAndIsNotSuccessful()
        {
            var diagnostics = new[]
            {
                CompilerDiagnostic.Fatal(DiagnosticCodes.Unity.TransactionRollback, "rolled back"),
            };

            CompileResult result = CompileResult.Failed(diagnostics);

            Assert.That(result.Success, Is.False);
            Assert.That(result.RootObject, Is.Null);
        }

        [Test]
        public void Factories_WithNullDiagnostics_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => CompileResult.Create(null, null!));
            Assert.Throws<ArgumentNullException>(() => CompileResult.Failed(null!));
        }
    }
}
