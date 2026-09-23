#nullable enable

using System;

namespace Rectloom.Core.Css.Values
{
    /// <summary>
    /// Four lengths, one per box edge, as used by <c>margin</c> and <c>padding</c>.
    /// </summary>
    /// <remarks>
    /// Edges are stored separately rather than as a single value, because the shorthand and the
    /// longhand properties can be mixed in one cascade and each edge resolves on its own.
    /// </remarks>
    public readonly struct EdgeSizes : IEquatable<EdgeSizes>
    {
        /// <summary>All four edges set to zero pixels.</summary>
        public static readonly EdgeSizes Zero = new EdgeSizes(
            CssLength.Zero,
            CssLength.Zero,
            CssLength.Zero,
            CssLength.Zero);

        /// <summary>
        /// Creates edge sizes.
        /// </summary>
        /// <param name="top">Top edge.</param>
        /// <param name="right">Right edge.</param>
        /// <param name="bottom">Bottom edge.</param>
        /// <param name="left">Left edge.</param>
        public EdgeSizes(CssLength top, CssLength right, CssLength bottom, CssLength left)
        {
            Top = top;
            Right = right;
            Bottom = bottom;
            Left = left;
        }

        /// <summary>Top edge.</summary>
        public CssLength Top { get; }

        /// <summary>Right edge.</summary>
        public CssLength Right { get; }

        /// <summary>Bottom edge.</summary>
        public CssLength Bottom { get; }

        /// <summary>Left edge.</summary>
        public CssLength Left { get; }

        /// <summary>Creates edge sizes with the same length on all four edges.</summary>
        /// <param name="all">Length for every edge.</param>
        /// <returns>The created edge sizes.</returns>
        public static EdgeSizes All(CssLength all) => new EdgeSizes(all, all, all, all);

        /// <summary>Returns a copy with a different top edge.</summary>
        /// <param name="top">The new top edge.</param>
        /// <returns>The updated edge sizes.</returns>
        public EdgeSizes WithTop(CssLength top) => new EdgeSizes(top, Right, Bottom, Left);

        /// <summary>Returns a copy with a different right edge.</summary>
        /// <param name="right">The new right edge.</param>
        /// <returns>The updated edge sizes.</returns>
        public EdgeSizes WithRight(CssLength right) => new EdgeSizes(Top, right, Bottom, Left);

        /// <summary>Returns a copy with a different bottom edge.</summary>
        /// <param name="bottom">The new bottom edge.</param>
        /// <returns>The updated edge sizes.</returns>
        public EdgeSizes WithBottom(CssLength bottom) => new EdgeSizes(Top, Right, bottom, Left);

        /// <summary>Returns a copy with a different left edge.</summary>
        /// <param name="left">The new left edge.</param>
        /// <returns>The updated edge sizes.</returns>
        public EdgeSizes WithLeft(CssLength left) => new EdgeSizes(Top, Right, Bottom, left);

        /// <summary>
        /// Resolves the horizontal edges against a containing block width.
        /// </summary>
        /// <param name="basis">Width of the containing block's content box.</param>
        /// <returns>Left plus right, in logical pixels, treating auto as zero.</returns>
        /// <remarks>
        /// Percentage margins and padding resolve against the containing block's <em>width</em> on
        /// every edge in CSS, including the vertical ones. See <see cref="ResolveVertical"/>.
        /// </remarks>
        public float ResolveHorizontal(float basis)
        {
            return Left.Resolve(basis, 0f) + Right.Resolve(basis, 0f);
        }

        /// <summary>
        /// Resolves the vertical edges against a containing block width.
        /// </summary>
        /// <param name="basis">Width of the containing block's content box.</param>
        /// <returns>Top plus bottom, in logical pixels, treating auto as zero.</returns>
        public float ResolveVertical(float basis)
        {
            return Top.Resolve(basis, 0f) + Bottom.Resolve(basis, 0f);
        }

        /// <inheritdoc />
        public bool Equals(EdgeSizes other)
        {
            return Top.Equals(other.Top)
                && Right.Equals(other.Right)
                && Bottom.Equals(other.Bottom)
                && Left.Equals(other.Left);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is EdgeSizes other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Top.GetHashCode();
                hash = (hash * 397) ^ Right.GetHashCode();
                hash = (hash * 397) ^ Bottom.GetHashCode();
                hash = (hash * 397) ^ Left.GetHashCode();
                return hash;
            }
        }

        /// <summary>Compares two edge sizes for equality.</summary>
        public static bool operator ==(EdgeSizes left, EdgeSizes right) => left.Equals(right);

        /// <summary>Compares two edge sizes for inequality.</summary>
        public static bool operator !=(EdgeSizes left, EdgeSizes right) => !left.Equals(right);

        /// <summary>Returns the four edges in CSS shorthand order.</summary>
        public override string ToString() => $"{Top} {Right} {Bottom} {Left}";
    }
}
