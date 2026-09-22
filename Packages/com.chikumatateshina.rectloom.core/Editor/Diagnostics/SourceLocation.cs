#nullable enable

using System;

namespace Rectloom.Core.Diagnostics
{
    /// <summary>
    /// Identifies a position inside an HTML or CSS source file.
    /// </summary>
    /// <remarks>
    /// Every DOM node, CSS declaration and IR node carries a <see cref="SourceLocation"/> so that
    /// diagnostics can point back at the authored source instead of at generated Unity objects.
    /// <see cref="Line"/> and <see cref="Column"/> are one-based, matching the convention used by
    /// editors and by the Unity console.
    /// </remarks>
    public readonly struct SourceLocation : IEquatable<SourceLocation>
    {
        /// <summary>The first valid one-based line or column number.</summary>
        public const int FirstIndex = 1;

        /// <summary>
        /// A location that is not attached to any source file.
        /// </summary>
        /// <remarks>
        /// Used for diagnostics raised by the compiler itself (for example invalid
        /// <c>CompileRequest</c> values) rather than by a position in HTML or CSS.
        /// </remarks>
        public static readonly SourceLocation None = default;

        /// <summary>
        /// Creates a location inside <paramref name="filePath"/>.
        /// </summary>
        /// <param name="filePath">
        /// Asset path of the source file, or <see langword="null"/> when unknown.
        /// </param>
        /// <param name="line">One-based line number. Values below 1 are clamped to 1.</param>
        /// <param name="column">One-based column number. Values below 1 are clamped to 1.</param>
        public SourceLocation(string? filePath, int line, int column)
        {
            FilePath = filePath;
            Line = line < FirstIndex ? FirstIndex : line;
            Column = column < FirstIndex ? FirstIndex : column;
        }

        /// <summary>
        /// Creates a location pointing at the start of <paramref name="filePath"/>.
        /// </summary>
        /// <param name="filePath">Asset path of the source file.</param>
        /// <returns>A location at line 1, column 1 of the file.</returns>
        public static SourceLocation FileStart(string filePath)
        {
            return new SourceLocation(filePath, FirstIndex, FirstIndex);
        }

        /// <summary>Asset path of the source file, or <see langword="null"/> when unknown.</summary>
        public string? FilePath { get; }

        /// <summary>One-based line number, or <c>0</c> when this location is <see cref="None"/>.</summary>
        public int Line { get; }

        /// <summary>One-based column number, or <c>0</c> when this location is <see cref="None"/>.</summary>
        public int Column { get; }

        /// <summary>
        /// Gets a value indicating whether this location points at a known file position.
        /// </summary>
        public bool IsKnown => FilePath != null && Line >= FirstIndex && Column >= FirstIndex;

        /// <inheritdoc />
        public bool Equals(SourceLocation other)
        {
            return Line == other.Line
                && Column == other.Column
                && string.Equals(FilePath, other.FilePath, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is SourceLocation other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = FilePath == null ? 0 : StringComparer.Ordinal.GetHashCode(FilePath);
                hash = (hash * 397) ^ Line;
                hash = (hash * 397) ^ Column;
                return hash;
            }
        }

        /// <summary>Compares two locations for equality.</summary>
        public static bool operator ==(SourceLocation left, SourceLocation right) => left.Equals(right);

        /// <summary>Compares two locations for inequality.</summary>
        public static bool operator !=(SourceLocation left, SourceLocation right) => !left.Equals(right);

        /// <summary>
        /// Returns a deterministic <c>path(line,column)</c> representation, or
        /// <c>&lt;unknown&gt;</c> when the location is not attached to a file.
        /// </summary>
        public override string ToString()
        {
            return IsKnown ? $"{FilePath}({Line},{Column})" : "<unknown>";
        }
    }
}
