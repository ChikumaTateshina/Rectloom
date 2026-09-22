#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Diagnostics;
using UnityEngine;

namespace Rectloom.Core.Compilation
{
    /// <summary>
    /// The outcome of one compile pass.
    /// </summary>
    /// <remarks>
    /// A result is always returned, including for failures: authoring mistakes are reported through
    /// <see cref="Diagnostics"/> rather than by throwing.
    /// </remarks>
    public sealed class CompileResult
    {
        private CompileResult(
            bool success,
            GameObject? rootObject,
            IReadOnlyList<CompilerDiagnostic> diagnostics,
            CompileStatistics statistics)
        {
            Success = success;
            RootObject = rootObject;
            Diagnostics = diagnostics;
            Statistics = statistics;
        }

        /// <summary>
        /// Gets a value indicating whether the pass committed its output without errors.
        /// </summary>
        /// <remarks>
        /// False whenever any diagnostic is <see cref="DiagnosticSeverity.Error"/> or
        /// <see cref="DiagnosticSeverity.Fatal"/>. Warnings do not make a pass unsuccessful.
        /// </remarks>
        public bool Success { get; }

        /// <summary>
        /// Root of the generated hierarchy, or <see langword="null"/> when nothing was committed.
        /// </summary>
        /// <remarks>
        /// For <see cref="CompileOutputType.Prefab"/> this is the prefab asset root; for
        /// <see cref="CompileOutputType.SceneObject"/> it is the object in the open scene.
        /// A validate-only pass always leaves this <see langword="null"/>.
        /// </remarks>
        public GameObject? RootObject { get; }

        /// <summary>All diagnostics produced by the pass, in the order they were reported.</summary>
        public IReadOnlyList<CompilerDiagnostic> Diagnostics { get; }

        /// <summary>Counters and timings for the pass.</summary>
        public CompileStatistics Statistics { get; }

        /// <summary>
        /// Creates a result whose success is derived from <paramref name="diagnostics"/>.
        /// </summary>
        /// <param name="rootObject">Root of the generated hierarchy, or null when nothing was committed.</param>
        /// <param name="diagnostics">Diagnostics produced by the pass.</param>
        /// <param name="statistics">Counters and timings, or null to use empty statistics.</param>
        /// <returns>The created result.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="diagnostics"/> is null.</exception>
        public static CompileResult Create(
            GameObject? rootObject,
            IReadOnlyList<CompilerDiagnostic> diagnostics,
            CompileStatistics? statistics = null)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            bool success = true;

            foreach (CompilerDiagnostic diagnostic in diagnostics)
            {
                if (diagnostic.IsErrorOrWorse)
                {
                    success = false;
                    break;
                }
            }

            return new CompileResult(
                success,
                rootObject,
                diagnostics,
                statistics ?? new CompileStatistics());
        }

        /// <summary>
        /// Creates a failed result that committed no output.
        /// </summary>
        /// <param name="diagnostics">
        /// Diagnostics produced by the pass. Must contain at least one error or fatal diagnostic
        /// explaining the failure.
        /// </param>
        /// <param name="statistics">Counters and timings, or null to use empty statistics.</param>
        /// <returns>The created result.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="diagnostics"/> is null.</exception>
        public static CompileResult Failed(
            IReadOnlyList<CompilerDiagnostic> diagnostics,
            CompileStatistics? statistics = null)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            return new CompileResult(
                false,
                null,
                diagnostics,
                statistics ?? new CompileStatistics());
        }
    }
}
