#nullable enable

using System;

namespace Rectloom.Core.Diagnostics
{
    /// <summary>
    /// A single message produced by the compiler, anchored to a position in the HTML or CSS source.
    /// </summary>
    /// <remarks>
    /// Diagnostics are the only supported way to report problems. Compiler stages never log directly
    /// to the Unity console and never throw for authoring mistakes; they convert the problem into a
    /// diagnostic and let the caller decide how to present it.
    /// Instances are immutable, so a diagnostic can be shared across sinks and result objects.
    /// </remarks>
    public sealed class CompilerDiagnostic
    {
        /// <summary>
        /// Creates a diagnostic.
        /// </summary>
        /// <param name="code">
        /// Stable diagnostic code such as <c>CSS1002</c>. See <see cref="DiagnosticCodes"/>.
        /// </param>
        /// <param name="severity">Severity of the problem.</param>
        /// <param name="message">Human readable description of the problem.</param>
        /// <param name="location">Position in the source, or <see cref="SourceLocation.None"/>.</param>
        /// <param name="suggestion">Optional hint describing how to fix the problem.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="code"/> or <paramref name="message"/> is null, empty or whitespace.
        /// </exception>
        public CompilerDiagnostic(
            string code,
            DiagnosticSeverity severity,
            string message,
            SourceLocation location = default,
            string? suggestion = null)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("Diagnostic code must not be empty.", nameof(code));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Diagnostic message must not be empty.", nameof(message));
            }

            Code = code;
            Severity = severity;
            Message = message;
            Location = location;
            Suggestion = suggestion;
        }

        /// <summary>Stable diagnostic code, for example <c>CSS1002</c>.</summary>
        public string Code { get; }

        /// <summary>Severity of the problem.</summary>
        public DiagnosticSeverity Severity { get; }

        /// <summary>Human readable description of the problem.</summary>
        public string Message { get; }

        /// <summary>Position in the HTML or CSS source that caused the diagnostic.</summary>
        public SourceLocation Location { get; }

        /// <summary>Optional hint describing how to fix the problem.</summary>
        public string? Suggestion { get; }

        /// <summary>Source file the diagnostic points at, or <see langword="null"/> when unknown.</summary>
        public string? FilePath => Location.FilePath;

        /// <summary>One-based line number in <see cref="FilePath"/>.</summary>
        public int Line => Location.Line;

        /// <summary>One-based column number in <see cref="FilePath"/>.</summary>
        public int Column => Location.Column;

        /// <summary>
        /// Gets a value indicating whether this diagnostic is <see cref="DiagnosticSeverity.Error"/>
        /// or <see cref="DiagnosticSeverity.Fatal"/>.
        /// </summary>
        public bool IsErrorOrWorse => Severity >= DiagnosticSeverity.Error;

        /// <summary>Creates an <see cref="DiagnosticSeverity.Info"/> diagnostic.</summary>
        /// <param name="code">Stable diagnostic code.</param>
        /// <param name="message">Human readable description.</param>
        /// <param name="location">Position in the source.</param>
        /// <param name="suggestion">Optional fix hint.</param>
        /// <returns>The created diagnostic.</returns>
        public static CompilerDiagnostic Info(
            string code,
            string message,
            SourceLocation location = default,
            string? suggestion = null)
        {
            return new CompilerDiagnostic(code, DiagnosticSeverity.Info, message, location, suggestion);
        }

        /// <summary>Creates a <see cref="DiagnosticSeverity.Warning"/> diagnostic.</summary>
        /// <param name="code">Stable diagnostic code.</param>
        /// <param name="message">Human readable description.</param>
        /// <param name="location">Position in the source.</param>
        /// <param name="suggestion">Optional fix hint.</param>
        /// <returns>The created diagnostic.</returns>
        public static CompilerDiagnostic Warning(
            string code,
            string message,
            SourceLocation location = default,
            string? suggestion = null)
        {
            return new CompilerDiagnostic(code, DiagnosticSeverity.Warning, message, location, suggestion);
        }

        /// <summary>Creates an <see cref="DiagnosticSeverity.Error"/> diagnostic.</summary>
        /// <param name="code">Stable diagnostic code.</param>
        /// <param name="message">Human readable description.</param>
        /// <param name="location">Position in the source.</param>
        /// <param name="suggestion">Optional fix hint.</param>
        /// <returns>The created diagnostic.</returns>
        public static CompilerDiagnostic Error(
            string code,
            string message,
            SourceLocation location = default,
            string? suggestion = null)
        {
            return new CompilerDiagnostic(code, DiagnosticSeverity.Error, message, location, suggestion);
        }

        /// <summary>Creates a <see cref="DiagnosticSeverity.Fatal"/> diagnostic.</summary>
        /// <param name="code">Stable diagnostic code.</param>
        /// <param name="message">Human readable description.</param>
        /// <param name="location">Position in the source.</param>
        /// <param name="suggestion">Optional fix hint.</param>
        /// <returns>The created diagnostic.</returns>
        public static CompilerDiagnostic Fatal(
            string code,
            string message,
            SourceLocation location = default,
            string? suggestion = null)
        {
            return new CompilerDiagnostic(code, DiagnosticSeverity.Fatal, message, location, suggestion);
        }

        /// <summary>
        /// Returns a deterministic single-line representation in the form
        /// <c>path(line,column): error CSS1002: message</c>.
        /// </summary>
        /// <remarks>
        /// The file prefix is omitted when the diagnostic has no known source position, and the
        /// suggestion is appended when present. The format is stable so that golden tests can assert
        /// on it directly.
        /// </remarks>
        public override string ToString()
        {
            string severity = Severity.ToString().ToLowerInvariant();
            string head = Location.IsKnown
                ? $"{Location}: {severity} {Code}: {Message}"
                : $"{severity} {Code}: {Message}";

            return string.IsNullOrEmpty(Suggestion) ? head : $"{head} ({Suggestion})";
        }
    }
}
