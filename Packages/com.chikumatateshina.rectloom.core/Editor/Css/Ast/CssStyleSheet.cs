#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Css.Ast
{
    /// <summary>
    /// One <c>@import</c> found in a stylesheet.
    /// </summary>
    /// <remarks>
    /// The path is kept as written. Turning it into an asset path needs the importing file's
    /// directory, which belongs to the resolver rather than to the parser.
    /// </remarks>
    public sealed class CssImport
    {
        /// <summary>
        /// Creates an import.
        /// </summary>
        /// <param name="path">Path as written, with quotes and any <c>url()</c> wrapper removed.</param>
        /// <param name="source">Position of the at-rule in the source file.</param>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
        public CssImport(string path, SourceLocation source)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Source = source;
        }

        /// <summary>Imported path, relative to the directory of the importing stylesheet.</summary>
        public string Path { get; }

        /// <summary>Position of the at-rule in the source file.</summary>
        public SourceLocation Source { get; }

        /// <inheritdoc />
        public override string ToString() => "@import \"" + Path + "\"";
    }

    /// <summary>
    /// A parsed stylesheet.
    /// </summary>
    /// <remarks>
    /// The stylesheet is a syntax tree: selectors are parsed but nothing is matched, and declaration
    /// values are still raw text. Matching happens in the cascade, once every stylesheet is loaded.
    /// </remarks>
    public sealed class CssStyleSheet
    {
        /// <summary>
        /// Creates a stylesheet.
        /// </summary>
        /// <param name="filePath">Asset path of the source.</param>
        /// <param name="rules">Rules in source order.</param>
        /// <param name="imports">Imports in source order.</param>
        /// <exception cref="ArgumentException"><paramref name="filePath"/> is null, empty or whitespace.</exception>
        /// <exception cref="ArgumentNullException">A collection argument is null.</exception>
        public CssStyleSheet(
            string filePath,
            IReadOnlyList<CssRule> rules,
            IReadOnlyList<CssImport> imports)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must not be empty.", nameof(filePath));
            }

            FilePath = filePath;
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Imports = imports ?? throw new ArgumentNullException(nameof(imports));
        }

        /// <summary>Asset path this stylesheet was parsed from.</summary>
        public string FilePath { get; }

        /// <summary>Rules in source order.</summary>
        public IReadOnlyList<CssRule> Rules { get; }

        /// <summary>
        /// Imports in source order. An imported stylesheet cascades beneath the importing one, so the
        /// resolver inserts its rules before the importing sheet's own rules.
        /// </summary>
        public IReadOnlyList<CssImport> Imports { get; }

        /// <inheritdoc />
        public override string ToString() => FilePath + " (" + Rules.Count + " rules)";
    }
}
