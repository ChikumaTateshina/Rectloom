#nullable enable

namespace Rectloom.Core.Diagnostics
{
    /// <summary>
    /// Severity of a <see cref="CompilerDiagnostic"/>, ordered from least to most severe.
    /// </summary>
    /// <remarks>
    /// The numeric order is part of the public contract: callers may compare severities to
    /// implement thresholds such as "fail the compile at <see cref="Error"/> or above".
    /// </remarks>
    public enum DiagnosticSeverity
    {
        /// <summary>Informational message. Does not affect the compile result.</summary>
        Info = 0,

        /// <summary>
        /// The compiler recovered from a problem and produced output that may not match intent,
        /// for example an unknown element compiled as a generic container.
        /// </summary>
        Warning = 1,

        /// <summary>
        /// A part of the source could not be compiled. Compilation continues where possible so that
        /// the author sees as many problems as possible in one pass, but the result is not successful.
        /// </summary>
        Error = 2,

        /// <summary>
        /// Compilation cannot continue safely and no output is committed, for example when the source
        /// root cannot be parsed or a prefab transaction fails.
        /// </summary>
        Fatal = 3,
    }
}
