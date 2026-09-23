#nullable enable

using System;

namespace Rectloom.Core.Css.Selectors
{
    /// <summary>
    /// How strongly a selector claims an element.
    /// </summary>
    /// <remarks>
    /// The weights are fixed by the language specification: an id counts 100, a class 10 and an
    /// element 1, and <see cref="Value"/> is their sum.
    /// <para>
    /// Summing into one number differs from the browser rule, which compares the three counts in
    /// order. The two agree until a selector carries eleven or more classes, at which point the
    /// class total here would outrank an id. The specification fixes this formula, so it is kept and
    /// the individual counts stay available for diagnostics.
    /// </para>
    /// </remarks>
    public readonly struct CssSpecificity : IComparable<CssSpecificity>, IEquatable<CssSpecificity>
    {
        /// <summary>Weight of one id in a selector.</summary>
        public const int IdWeight = 100;

        /// <summary>Weight of one class in a selector.</summary>
        public const int ClassWeight = 10;

        /// <summary>Weight of one element name in a selector.</summary>
        public const int ElementWeight = 1;

        /// <summary>The specificity of the universal selector.</summary>
        public static readonly CssSpecificity Zero = default;

        /// <summary>
        /// Creates a specificity.
        /// </summary>
        /// <param name="ids">Number of ids in the selector.</param>
        /// <param name="classes">Number of classes in the selector.</param>
        /// <param name="elements">Number of element names in the selector.</param>
        public CssSpecificity(int ids, int classes, int elements)
        {
            Ids = ids;
            Classes = classes;
            Elements = elements;
        }

        /// <summary>Number of ids in the selector.</summary>
        public int Ids { get; }

        /// <summary>Number of classes in the selector.</summary>
        public int Classes { get; }

        /// <summary>Number of element names in the selector.</summary>
        public int Elements { get; }

        /// <summary>The weighted total used to compare two selectors.</summary>
        public int Value => (Ids * IdWeight) + (Classes * ClassWeight) + (Elements * ElementWeight);

        /// <summary>Adds two specificities, as when combining the parts of a selector.</summary>
        /// <param name="left">First specificity.</param>
        /// <param name="right">Second specificity.</param>
        /// <returns>The component-wise sum.</returns>
        public static CssSpecificity operator +(CssSpecificity left, CssSpecificity right)
        {
            return new CssSpecificity(
                left.Ids + right.Ids,
                left.Classes + right.Classes,
                left.Elements + right.Elements);
        }

        /// <inheritdoc />
        public int CompareTo(CssSpecificity other) => Value.CompareTo(other.Value);

        /// <inheritdoc />
        public bool Equals(CssSpecificity other)
        {
            return Ids == other.Ids && Classes == other.Classes && Elements == other.Elements;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is CssSpecificity other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((Ids * 397) ^ Classes) * 397 ^ Elements;
            }
        }

        /// <summary>Compares two specificities for equality.</summary>
        public static bool operator ==(CssSpecificity left, CssSpecificity right) => left.Equals(right);

        /// <summary>Compares two specificities for inequality.</summary>
        public static bool operator !=(CssSpecificity left, CssSpecificity right) => !left.Equals(right);

        /// <summary>Returns whether the left specificity is weaker.</summary>
        public static bool operator <(CssSpecificity left, CssSpecificity right) => left.Value < right.Value;

        /// <summary>Returns whether the left specificity is stronger.</summary>
        public static bool operator >(CssSpecificity left, CssSpecificity right) => left.Value > right.Value;

        /// <summary>Returns whether the left specificity is weaker or equal.</summary>
        public static bool operator <=(CssSpecificity left, CssSpecificity right) => left.Value <= right.Value;

        /// <summary>Returns whether the left specificity is stronger or equal.</summary>
        public static bool operator >=(CssSpecificity left, CssSpecificity right) => left.Value >= right.Value;

        /// <summary>Returns the counts and the weighted total.</summary>
        public override string ToString() => $"({Ids},{Classes},{Elements})={Value}";
    }
}
