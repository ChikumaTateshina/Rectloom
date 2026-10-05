#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Rectloom.Core.Metadata
{
    /// <summary>
    /// What one compile pass produced, recorded so the next pass can update it instead of
    /// regenerating it.
    /// </summary>
    /// <remarks>
    /// This is an Editor-only asset rather than a component on the generated root. The language
    /// specification allows either, and keeping it out of the hierarchy is what lets a build contain
    /// no Rectloom code at all: the generated prefab is plain uGUI with nothing of the compiler
    /// left in it.
    /// <para>
    /// The asset is written next to its output, so deleting the prefab and its metadata together is
    /// the obvious thing to do, and a compile that cannot find metadata says so rather than
    /// guessing.
    /// </para>
    /// </remarks>
    public sealed class RectloomDocumentMetadata : ScriptableObject
    {
        /// <summary>
        /// Layout version of this asset.
        /// </summary>
        /// <remarks>
        /// Incremented whenever the stored fields change meaning. An update compile refuses to read
        /// a version it does not know, because misreading ownership is how user data gets deleted.
        /// </remarks>
        public const int CurrentSchemaVersion = 1;

        /// <summary>File extension used for a metadata asset.</summary>
        public const string AssetExtension = ".rectloom.asset";

        [SerializeField] private bool _worldSpaceCanvas;
        [SerializeField] private float _worldUnitsPerPixel;
        [SerializeField] private bool _renderDocumentBackground;

        /// <summary>Canvas mode recorded for this output.</summary>
        public bool WorldSpaceCanvas { get => _worldSpaceCanvas; set => _worldSpaceCanvas = value; }
        /// <summary>World units per pixel recorded for this output.</summary>
        public float WorldUnitsPerPixel { get => _worldUnitsPerPixel; set => _worldUnitsPerPixel = value; }
        /// <summary>Whether the document root background was rendered.</summary>
        public bool RenderDocumentBackground { get => _renderDocumentBackground; set => _renderDocumentBackground = value; }

        [SerializeField] private int _schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string _compilerVersion = RectloomVersion.Current;
        [SerializeField] private string _sourceHtmlGuid = string.Empty;
        [SerializeField] private string _sourceHtmlPath = string.Empty;
        [SerializeField] private List<string> _sourceCssGuids = new List<string>();
        [SerializeField] private List<string> _sourceCssPaths = new List<string>();
        [SerializeField] private string _sourceHash = string.Empty;
        [SerializeField] private string _rootGlobalObjectId = string.Empty;
        [SerializeField] private List<GeneratedNodeMetadata> _nodes = new List<GeneratedNodeMetadata>();

        /// <summary>Layout version this asset was written with.</summary>
        public int SchemaVersion
        {
            get => _schemaVersion;
            set => _schemaVersion = value;
        }

        /// <summary>Compiler version that produced the output.</summary>
        public string CompilerVersion
        {
            get => _compilerVersion;
            set => _compilerVersion = value ?? string.Empty;
        }

        /// <summary>GUID of the HTML source.</summary>
        public string SourceHtmlGuid
        {
            get => _sourceHtmlGuid;
            set => _sourceHtmlGuid = value ?? string.Empty;
        }

        /// <summary>Asset path of the HTML source, for display.</summary>
        public string SourceHtmlPath
        {
            get => _sourceHtmlPath;
            set => _sourceHtmlPath = value ?? string.Empty;
        }

        /// <summary>GUIDs of the stylesheets, in cascade order.</summary>
        public List<string> SourceCssGuids => _sourceCssGuids;

        /// <summary>Asset paths of the stylesheets, in cascade order, for display.</summary>
        public List<string> SourceCssPaths => _sourceCssPaths;

        /// <summary>
        /// Hash of the source text the output was generated from.
        /// </summary>
        /// <remarks>
        /// Lets a caller tell "the sources have not changed" from "the sources changed but produced
        /// the same layout", which the node list alone cannot answer.
        /// </remarks>
        public string SourceHash
        {
            get => _sourceHash;
            set => _sourceHash = value ?? string.Empty;
        }

        /// <summary>
        /// Global object id of the generated root.
        /// </summary>
        /// <remarks>
        /// Scene output has no asset path to look up, so this is how an update finds the object it
        /// generated last time. Prefab output is found by its own path and does not rely on it.
        /// </remarks>
        public string RootGlobalObjectId
        {
            get => _rootGlobalObjectId;
            set => _rootGlobalObjectId = value ?? string.Empty;
        }

        /// <summary>One entry per generated object, in document order.</summary>
        public List<GeneratedNodeMetadata> Nodes => _nodes;

        /// <summary>
        /// Gets a value indicating whether this asset was written by a schema this compiler reads.
        /// </summary>
        public bool IsSchemaSupported => _schemaVersion == CurrentSchemaVersion;

        /// <summary>
        /// Finds the entry for a stable ID.
        /// </summary>
        /// <param name="stableId">Identity to look up.</param>
        /// <param name="node">The entry when found.</param>
        /// <returns><see langword="true"/> when the previous pass generated that node.</returns>
        public bool TryGetNode(string stableId, out GeneratedNodeMetadata node)
        {
            foreach (GeneratedNodeMetadata candidate in _nodes)
            {
                if (string.Equals(candidate.StableId, stableId, StringComparison.Ordinal))
                {
                    node = candidate;
                    return true;
                }
            }

            node = null!;
            return false;
        }

        /// <summary>
        /// Replaces the recorded node list.
        /// </summary>
        /// <param name="nodes">The entries to store, in document order.</param>
        public void SetNodes(IEnumerable<GeneratedNodeMetadata> nodes)
        {
            _nodes.Clear();

            if (nodes == null)
            {
                return;
            }

            _nodes.AddRange(nodes);
        }

        /// <summary>
        /// Hashes the source text a compile read.
        /// </summary>
        /// <param name="sources">Source contents, in the order they were loaded.</param>
        /// <returns>A lower-case hexadecimal SHA-256 digest.</returns>
        /// <remarks>
        /// The sources are separated by a byte that cannot appear in text, so that moving a line
        /// from one file to the next changes the hash.
        /// </remarks>
        public static string ComputeSourceHash(IEnumerable<string> sources)
        {
            var builder = new StringBuilder();

            if (sources != null)
            {
                foreach (string source in sources)
                {
                    builder.Append(source ?? string.Empty);
                    builder.Append('\u0000');
                }
            }

            using (var sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                var text = new StringBuilder(digest.Length * 2);

                foreach (byte value in digest)
                {
                    text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                }

                return text.ToString();
            }
        }
    }
}
