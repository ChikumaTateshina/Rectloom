#nullable enable

using System;
using System.Collections.Generic;

namespace Rectloom.Core.Css.Parsing
{
    /// <summary>
    /// Turns a path written in a stylesheet into a project asset path.
    /// </summary>
    /// <remarks>
    /// A relative path is resolved against the directory of the file that wrote it, which is what
    /// makes a stylesheet movable together with the images it refers to.
    /// <para>
    /// Paths are normalised to forward slashes with <c>.</c> and <c>..</c> segments removed, so that
    /// the same file always produces the same key. That matters for cycle detection, which compares
    /// paths rather than file handles.
    /// </para>
    /// </remarks>
    public static class CssPathResolver
    {
        private static readonly char[] Separators = { '/', '\\' };

        /// <summary>
        /// Resolves a path written inside a source file.
        /// </summary>
        /// <param name="sourceFilePath">Asset path of the file the reference was written in.</param>
        /// <param name="reference">The path as written, for example <c>./common.css</c>.</param>
        /// <returns>
        /// The normalised asset path, or <see langword="null"/> when <paramref name="reference"/> is
        /// empty or climbs above the project root.
        /// </returns>
        /// <remarks>
        /// A reference that already starts at a project root such as <c>Assets/</c> or
        /// <c>Packages/</c> is used as-is. Anything else is relative to
        /// <paramref name="sourceFilePath"/>.
        /// </remarks>
        public static string? Resolve(string? sourceFilePath, string? reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return null;
            }

            string value = reference!.Trim().Replace('\\', '/');

            if (IsProjectRooted(value))
            {
                return Normalise(value);
            }

            string directory = GetDirectory(sourceFilePath);

            return Normalise(directory.Length == 0 ? value : directory + "/" + value);
        }

        /// <summary>
        /// Gets the directory part of an asset path.
        /// </summary>
        /// <param name="filePath">An asset path.</param>
        /// <returns>The directory, without a trailing slash, or an empty string when there is none.</returns>
        public static string GetDirectory(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return string.Empty;
            }

            string value = filePath!.Replace('\\', '/');
            int lastSlash = value.LastIndexOf('/');

            return lastSlash < 0 ? string.Empty : value.Substring(0, lastSlash);
        }

        /// <summary>
        /// Gets a value indicating whether a path starts at a project root.
        /// </summary>
        /// <param name="path">The path to test.</param>
        /// <returns><see langword="true"/> for paths under <c>Assets/</c> or <c>Packages/</c>.</returns>
        public static bool IsProjectRooted(string path)
        {
            return path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Normalises separators and removes <c>.</c> and <c>..</c> segments.
        /// </summary>
        /// <param name="path">The path to normalise.</param>
        /// <returns>
        /// The normalised path, or <see langword="null"/> when it climbs above its own root.
        /// </returns>
        public static string? Normalise(string path)
        {
            string[] segments = path.Replace('\\', '/').Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>(segments.Length);

            foreach (string segment in segments)
            {
                if (segment == ".")
                {
                    continue;
                }

                if (segment == "..")
                {
                    if (result.Count == 0)
                    {
                        // Climbing above the root would leave the project; there is nothing to open.
                        return null;
                    }

                    result.RemoveAt(result.Count - 1);
                    continue;
                }

                result.Add(segment);
            }

            return result.Count == 0 ? null : string.Join("/", result);
        }
    }
}
