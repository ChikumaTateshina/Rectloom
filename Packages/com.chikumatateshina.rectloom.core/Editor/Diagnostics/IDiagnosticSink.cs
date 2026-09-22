#nullable enable

using System;

namespace Rectloom.Core.Diagnostics
{
    /// <summary>
    /// Receives diagnostics produced by a compiler stage.
    /// </summary>
    /// <remarks>
    /// Stages depend on this interface rather than on <see cref="DiagnosticSink"/> so that callers
    /// can filter, forward or count diagnostics without the stage knowing. Implementations must not
    /// throw from <see cref="Report"/>: a failing sink would abort an otherwise recoverable compile.
    /// </remarks>
    public interface IDiagnosticSink
    {
        /// <summary>
        /// Records a diagnostic.
        /// </summary>
        /// <param name="diagnostic">The diagnostic to record.</param>
        void Report(CompilerDiagnostic diagnostic);
    }

    /// <summary>
    /// Convenience overloads for reporting diagnostics to an <see cref="IDiagnosticSink"/>.
    /// </summary>
    public static class DiagnosticSinkExtensions
    {
        /// <summary>Reports a diagnostic built from its parts.</summary>
        /// <param name="sink">Sink to report to.</param>
        /// <param name="code">Stable diagnostic code.</param>
        /// <param name="severity">Severity of the problem.</param>
        /// <param name="message">Human readable description.</param>
        /// <param name="location">Position in the source.</param>
        /// <param name="suggestion">Optional fix hint.</param>
        /// <exception cref="ArgumentNullException"><paramref name="sink"/> is null.</exception>
        public static void Report(
            this IDiagnosticSink sink,
            string code,
            DiagnosticSeverity severity,
            string message,
            SourceLocation location = default,
            string? suggestion = null)
        {
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            sink.Report(new CompilerDiagnostic(code, severity, message, location, suggestion));
        }

        /// <summary>Reports an <see cref="DiagnosticSeverity.Info"/> diagnostic.</summary>
        /// <param name="sink">Sink to report to.</param>
        /// <param name="code">Stable diagnostic code.</param>
        /// <param name="message">Human readable description.</param>
        /// <param name="location">Position in the source.</param>
        /// <param name="suggestion">Optional fix hint.</param>
        public static void Info(
            this IDiagnosticSink sink,
            string code,
            string message,
            SourceLocation location = default,
            string? suggestion = null)
        {
            sink.Report(code, DiagnosticSeverity.Info, message, location, suggestion);
        }

        /// <summary>Reports a <see cref="DiagnosticSeverity.Warning"/> diagnostic.</summary>
        /// <param name="sink">Sink to report to.</param>
        /// <param name="code">Stable diagnostic code.</param>
        /// <param name="message">Human readable description.</param>
        /// <param name="location">Position in the source.</param>
        /// <param name="suggestion">Optional fix hint.</param>
        public static void Warning(
            this IDiagnosticSink sink,
            string code,
            string message,
            SourceLocation location = default,
            string? suggestion = null)
        {
            sink.Report(code, DiagnosticSeverity.Warning, message, location, suggestion);
        }

        /// <summary>Reports an <see cref="DiagnosticSeverity.Error"/> diagnostic.</summary>
        /// <param name="sink">Sink to report to.</param>
        /// <param name="code">Stable diagnostic code.</param>
        /// <param name="message">Human readable description.</param>
        /// <param name="location">Position in the source.</param>
        /// <param name="suggestion">Optional fix hint.</param>
        public static void Error(
            this IDiagnosticSink sink,
            string code,
            string message,
            SourceLocation location = default,
            string? suggestion = null)
        {
            sink.Report(code, DiagnosticSeverity.Error, message, location, suggestion);
        }

        /// <summary>Reports a <see cref="DiagnosticSeverity.Fatal"/> diagnostic.</summary>
        /// <param name="sink">Sink to report to.</param>
        /// <param name="code">Stable diagnostic code.</param>
        /// <param name="message">Human readable description.</param>
        /// <param name="location">Position in the source.</param>
        /// <param name="suggestion">Optional fix hint.</param>
        public static void Fatal(
            this IDiagnosticSink sink,
            string code,
            string message,
            SourceLocation location = default,
            string? suggestion = null)
        {
            sink.Report(code, DiagnosticSeverity.Fatal, message, location, suggestion);
        }
    }
}
