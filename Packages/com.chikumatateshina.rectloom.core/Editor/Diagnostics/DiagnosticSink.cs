#nullable enable

using System;
using System.Collections.Generic;

namespace Rectloom.Core.Diagnostics
{
    /// <summary>
    /// Collects diagnostics in the order they are reported.
    /// </summary>
    /// <remarks>
    /// This is the sink used by a compile pass. Report order is preserved so that output stays
    /// deterministic for the same source and compiler version, which golden tests rely on.
    /// Instances are not thread safe; the compiler runs a single pass on the main Editor thread.
    /// </remarks>
    public sealed class DiagnosticSink : IDiagnosticSink
    {
        private readonly List<CompilerDiagnostic> _diagnostics = new List<CompilerDiagnostic>();

        /// <summary>Diagnostics recorded so far, in report order.</summary>
        public IReadOnlyList<CompilerDiagnostic> Diagnostics => _diagnostics;

        /// <summary>Number of diagnostics recorded so far.</summary>
        public int Count => _diagnostics.Count;

        /// <summary>
        /// Highest severity recorded so far, or <see cref="DiagnosticSeverity.Info"/> when empty.
        /// </summary>
        public DiagnosticSeverity MaxSeverity { get; private set; } = DiagnosticSeverity.Info;

        /// <summary>
        /// Gets a value indicating whether any <see cref="DiagnosticSeverity.Error"/> or
        /// <see cref="DiagnosticSeverity.Fatal"/> diagnostic was recorded.
        /// </summary>
        /// <remarks>
        /// A compile is reported as successful only when this is <see langword="false"/>.
        /// </remarks>
        public bool HasErrors => MaxSeverity >= DiagnosticSeverity.Error;

        /// <summary>
        /// Gets a value indicating whether any <see cref="DiagnosticSeverity.Fatal"/> diagnostic was
        /// recorded, meaning the pass must not commit output.
        /// </summary>
        public bool HasFatal => MaxSeverity >= DiagnosticSeverity.Fatal;

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException"><paramref name="diagnostic"/> is null.</exception>
        public void Report(CompilerDiagnostic diagnostic)
        {
            if (diagnostic == null)
            {
                throw new ArgumentNullException(nameof(diagnostic));
            }

            _diagnostics.Add(diagnostic);

            if (diagnostic.Severity > MaxSeverity)
            {
                MaxSeverity = diagnostic.Severity;
            }
        }

        /// <summary>
        /// Counts the recorded diagnostics with the given severity.
        /// </summary>
        /// <param name="severity">Severity to count.</param>
        /// <returns>The number of matching diagnostics.</returns>
        public int CountOf(DiagnosticSeverity severity)
        {
            int count = 0;

            foreach (CompilerDiagnostic diagnostic in _diagnostics)
            {
                if (diagnostic.Severity == severity)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Returns a snapshot of the recorded diagnostics that is unaffected by later reports.
        /// </summary>
        /// <returns>A copy of the recorded diagnostics, in report order.</returns>
        public IReadOnlyList<CompilerDiagnostic> ToArray()
        {
            return _diagnostics.ToArray();
        }

        /// <summary>
        /// Removes all recorded diagnostics and resets <see cref="MaxSeverity"/>.
        /// </summary>
        public void Clear()
        {
            _diagnostics.Clear();
            MaxSeverity = DiagnosticSeverity.Info;
        }
    }
}
