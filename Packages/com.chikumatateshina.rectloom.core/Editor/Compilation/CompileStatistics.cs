#nullable enable

namespace Rectloom.Core.Compilation
{
    /// <summary>
    /// Counters and timings gathered during one compile pass.
    /// </summary>
    /// <remarks>
    /// Statistics are reporting only: they never influence generated output. Timings are wall-clock
    /// milliseconds per pipeline stage and are the numbers the performance benchmarks assert on.
    /// </remarks>
    public sealed class CompileStatistics
    {
        /// <summary>Number of DOM elements parsed from the HTML source.</summary>
        public int ElementCount { get; set; }

        /// <summary>Number of IR nodes produced for the document.</summary>
        public int NodeCount { get; set; }

        /// <summary>Number of generated objects created by this pass.</summary>
        public int CreatedObjectCount { get; set; }

        /// <summary>Number of existing generated objects updated in place by this pass.</summary>
        public int UpdatedObjectCount { get; set; }

        /// <summary>Number of generated objects deleted because they left the source.</summary>
        public int RemovedObjectCount { get; set; }

        /// <summary>
        /// Number of generated objects that left the source but were kept because they carried user
        /// modifications.
        /// </summary>
        public int PreservedObjectCount { get; set; }

        /// <summary>Milliseconds spent loading sources and building the DOM.</summary>
        public double ParseMilliseconds { get; set; }

        /// <summary>Milliseconds spent matching selectors and resolving the cascade.</summary>
        public double StyleMilliseconds { get; set; }

        /// <summary>Milliseconds spent solving layout.</summary>
        public double LayoutMilliseconds { get; set; }

        /// <summary>Milliseconds spent turning the IR into Unity objects.</summary>
        public double BackendMilliseconds { get; set; }

        /// <summary>Milliseconds spent in the whole pass, including work outside the stages above.</summary>
        public double TotalMilliseconds { get; set; }
    }
}
