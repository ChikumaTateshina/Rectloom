#nullable enable

using System;
using System.Collections.Generic;

namespace Rectloom.Core.Ir
{
    /// <summary>
    /// Builds the identities that let an update compile recognise a generated object.
    /// </summary>
    /// <remarks>
    /// An explicit HTML <c>id</c> is always preferred, because it survives the markup being
    /// reordered. Without one, the identity is a structural path such as
    /// <c>root/div[0]/button[2]</c>, which is stable as long as the surrounding structure is.
    /// <para>
    /// The path is kept readable rather than hashed. It ends up in metadata that a developer reads
    /// when working out why an update did not match what they expected.
    /// </para>
    /// </remarks>
    public static class StableId
    {
        /// <summary>Identity of the document root when it declares no <c>id</c>.</summary>
        public const string RootId = "root";

        /// <summary>Separator between the segments of a structural path.</summary>
        public const char Separator = '/';

        /// <summary>
        /// Builds a structural identity for a node.
        /// </summary>
        /// <param name="parentId">Identity of the parent node.</param>
        /// <param name="tagName">Tag name of the element, or a kind name for an anonymous node.</param>
        /// <param name="siblingIndex">Zero-based position among the parent's IR children.</param>
        /// <returns>The structural identity.</returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="parentId"/> or <paramref name="tagName"/> is null, empty or whitespace.
        /// </exception>
        public static string Structural(string parentId, string tagName, int siblingIndex)
        {
            if (string.IsNullOrWhiteSpace(parentId))
            {
                throw new ArgumentException("Parent id must not be empty.", nameof(parentId));
            }

            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("Tag name must not be empty.", nameof(tagName));
            }

            return parentId + Separator + tagName + "[" + siblingIndex.ToString() + "]";
        }

        /// <summary>
        /// Tracks which identities a document has already used.
        /// </summary>
        /// <remarks>
        /// A duplicate <c>id</c> is reported by the parser, but the IR still has to produce unique
        /// identities or an update compile would match two objects to one node. The second element
        /// falls back to its structural path, and if even that collides a numeric suffix is added.
        /// </remarks>
        public sealed class Allocator
        {
            private readonly HashSet<string> _used = new HashSet<string>(StringComparer.Ordinal);

            /// <summary>Number of identities allocated so far.</summary>
            public int Count => _used.Count;

            /// <summary>
            /// Allocates an identity, preferring an explicit id.
            /// </summary>
            /// <param name="explicitId">
            /// The element's <c>id</c>, or null when it has none.
            /// </param>
            /// <param name="fallback">Structural path to use when the explicit id is unavailable.</param>
            /// <returns>An identity no other node in this document uses.</returns>
            /// <exception cref="ArgumentException"><paramref name="fallback"/> is null, empty or whitespace.</exception>
            public string Allocate(string? explicitId, string fallback)
            {
                if (string.IsNullOrWhiteSpace(fallback))
                {
                    throw new ArgumentException("Fallback id must not be empty.", nameof(fallback));
                }

                if (!string.IsNullOrWhiteSpace(explicitId) && _used.Add(explicitId!))
                {
                    return explicitId!;
                }

                if (_used.Add(fallback))
                {
                    return fallback;
                }

                // Only reachable when an explicit id collides with a structural path. Rare, but it
                // must still yield something unique.
                for (int suffix = 2; ; suffix++)
                {
                    string candidate = fallback + "#" + suffix.ToString();

                    if (_used.Add(candidate))
                    {
                        return candidate;
                    }
                }
            }

            /// <summary>
            /// Gets a value indicating whether an identity has been allocated.
            /// </summary>
            /// <param name="id">The identity to test.</param>
            /// <returns><see langword="true"/> when it is in use.</returns>
            public bool IsUsed(string id) => _used.Contains(id);
        }
    }
}
