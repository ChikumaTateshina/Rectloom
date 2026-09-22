#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Tests.Diagnostics
{
    public sealed class DiagnosticSinkTests
    {
        [Test]
        public void NewSink_IsEmptyAndClean()
        {
            var sink = new DiagnosticSink();

            Assert.That(sink.Count, Is.Zero);
            Assert.That(sink.Diagnostics, Is.Empty);
            Assert.That(sink.HasErrors, Is.False);
            Assert.That(sink.HasFatal, Is.False);
            Assert.That(sink.MaxSeverity, Is.EqualTo(DiagnosticSeverity.Info));
        }

        [Test]
        public void Report_PreservesOrder()
        {
            var sink = new DiagnosticSink();

            sink.Warning("CSS1001", "first");
            sink.Error("CSS1002", "second");
            sink.Info("CSS1003", "third");

            Assert.That(
                sink.Diagnostics.Select(d => d.Message),
                Is.EqualTo(new[] { "first", "second", "third" }));
        }

        [Test]
        public void MaxSeverity_TracksHighestReported()
        {
            var sink = new DiagnosticSink();

            sink.Info("CSS1001", "info");
            Assert.That(sink.MaxSeverity, Is.EqualTo(DiagnosticSeverity.Info));

            sink.Error("CSS1002", "error");
            Assert.That(sink.MaxSeverity, Is.EqualTo(DiagnosticSeverity.Error));

            sink.Warning("CSS1003", "warning");
            Assert.That(sink.MaxSeverity, Is.EqualTo(DiagnosticSeverity.Error), "must not decrease");
        }

        [Test]
        public void HasErrors_IsTrueForErrorAndFatal()
        {
            var errorSink = new DiagnosticSink();
            errorSink.Error("CSS1002", "error");

            var fatalSink = new DiagnosticSink();
            fatalSink.Fatal("INTERNAL9001", "fatal");

            Assert.That(errorSink.HasErrors, Is.True);
            Assert.That(errorSink.HasFatal, Is.False);
            Assert.That(fatalSink.HasErrors, Is.True);
            Assert.That(fatalSink.HasFatal, Is.True);
        }

        [Test]
        public void HasErrors_IsFalseForWarningsOnly()
        {
            var sink = new DiagnosticSink();

            sink.Warning("HTML1003", "unknown element");

            Assert.That(sink.HasErrors, Is.False);
        }

        [Test]
        public void CountOf_CountsMatchingSeverityOnly()
        {
            var sink = new DiagnosticSink();

            sink.Warning("CSS1001", "a");
            sink.Warning("CSS1001", "b");
            sink.Error("CSS1002", "c");

            Assert.That(sink.CountOf(DiagnosticSeverity.Warning), Is.EqualTo(2));
            Assert.That(sink.CountOf(DiagnosticSeverity.Error), Is.EqualTo(1));
            Assert.That(sink.CountOf(DiagnosticSeverity.Fatal), Is.Zero);
        }

        [Test]
        public void ToArray_SnapshotIsUnaffectedByLaterReports()
        {
            var sink = new DiagnosticSink();
            sink.Warning("CSS1001", "a");

            IReadOnlyList<CompilerDiagnostic> snapshot = sink.ToArray();
            sink.Warning("CSS1001", "b");

            Assert.That(snapshot.Count, Is.EqualTo(1));
            Assert.That(sink.Count, Is.EqualTo(2));
        }

        [Test]
        public void Clear_ResetsCountAndSeverity()
        {
            var sink = new DiagnosticSink();
            sink.Fatal("INTERNAL9001", "fatal");

            sink.Clear();

            Assert.That(sink.Count, Is.Zero);
            Assert.That(sink.HasErrors, Is.False);
            Assert.That(sink.MaxSeverity, Is.EqualTo(DiagnosticSeverity.Info));
        }

        [Test]
        public void Report_WithNullDiagnostic_Throws()
        {
            var sink = new DiagnosticSink();

            Assert.Throws<ArgumentNullException>(() => sink.Report(null!));
        }

        [Test]
        public void ReportExtension_WithNullSink_Throws()
        {
            IDiagnosticSink? sink = null;

            Assert.Throws<ArgumentNullException>(
                () => sink!.Report("CSS1001", DiagnosticSeverity.Info, "m"));
        }

        [Test]
        public void ReportExtension_ForwardsCodeSeverityMessageAndLocation()
        {
            var sink = new DiagnosticSink();
            var location = new SourceLocation("Assets/UI/page.html", 4, 11);

            sink.Error(DiagnosticCodes.Html.UnexpectedClosingTag, "stray </div>", location, "remove it");

            CompilerDiagnostic diagnostic = sink.Diagnostics[0];
            Assert.That(diagnostic.Code, Is.EqualTo("HTML1001"));
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(diagnostic.Message, Is.EqualTo("stray </div>"));
            Assert.That(diagnostic.Location, Is.EqualTo(location));
            Assert.That(diagnostic.Suggestion, Is.EqualTo("remove it"));
        }
    }
}
