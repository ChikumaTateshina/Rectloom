#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Metadata;
using UnityEngine;

namespace Rectloom.Ugui.Backend
{
    /// <summary>
    /// What one backend pass did.
    /// </summary>
    /// <remarks>
    /// The node records are what gets written to metadata, so the next pass knows which object
    /// belongs to which node and which parts of it the compiler owns.
    /// </remarks>
    public sealed class BackendResult
    {
        /// <summary>
        /// Creates a result.
        /// </summary>
        /// <param name="root">Root of the generated hierarchy.</param>
        /// <param name="nodes">One record per generated object, in document order.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public BackendResult(
            GameObject root,
            IReadOnlyList<GeneratedNodeMetadata> nodes,
            IReadOnlyDictionary<string, GameObject> objects)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            Objects = objects ?? throw new ArgumentNullException(nameof(objects));
        }

        /// <summary>Root of the generated hierarchy.</summary>
        public GameObject Root { get; }

        /// <summary>One record per generated object, in document order.</summary>
        public IReadOnlyList<GeneratedNodeMetadata> Nodes { get; }

        /// <summary>
        /// The generated objects by stable ID.
        /// </summary>
        /// <remarks>
        /// Used by the extension pipeline, which has to reach the object a node produced while that
        /// object is still editable, before prefab output is committed.
        /// </remarks>
        public IReadOnlyDictionary<string, GameObject> Objects { get; }

        /// <summary>Objects created by this pass.</summary>
        public int CreatedCount { get; internal set; }

        /// <summary>Objects that already existed and were updated in place.</summary>
        public int UpdatedCount { get; internal set; }

        /// <summary>Generated objects deleted because the source no longer mentions them.</summary>
        public int RemovedCount { get; internal set; }

        /// <summary>
        /// Generated objects the source no longer mentions that were kept because someone had
        /// worked on them.
        /// </summary>
        public int PreservedCount { get; internal set; }
    }
}
