#nullable enable

using System;
using System.Globalization;

namespace Rectloom.Core.Css.Values
{
    /// <summary>
    /// The units a length can be written in.
    /// </summary>
    /// <remarks>
    /// Version 1.0 supports <c>auto</c>, <c>px</c> and <c>%</c> only. Font-relative units such as
    /// <c>em</c> are deliberately absent: they would make a length depend on the resolved font of an
    /// ancestor, which the baked layout solver does not model.
    /// </remarks>
    public enum CssLengthUnit
    {
        /// <summary>The value is resolved from content or from the containing block.</summary>
        Auto = 0,

        /// <summary>A logical pixel, matching the canvas reference resolution.</summary>
        Pixel = 1,

        /// <summary>A percentage of the containing block's corresponding content size.</summary>
        Percent = 2,
    }

    /// <summary>
    /// A CSS length such as <c>200px</c>, <c>50%</c> or <c>auto</c>.
    /// </summary>
    /// <remarks>
    /// A length keeps its unit rather than being resolved during parsing, because a percentage
    /// cannot be resolved until the containing block's size is known during layout.
    /// </remarks>
    public readonly struct CssLength : IEquatable<CssLength>
    {
        /// <summary>The <c>auto</c> length.</summary>
        public static readonly CssLength Auto = default;

        /// <summary>Zero pixels.</summary>
        public static readonly CssLength Zero = new CssLength(CssLengthUnit.Pixel, 0f);

        /// <summary>
        /// Creates a length.
        /// </summary>
        /// <param name="unit">The unit.</param>
        /// <param name="value">The value. Ignored when <paramref name="unit"/> is auto.</param>
        public CssLength(CssLengthUnit unit, float value)
        {
            Unit = unit;
            Value = unit == CssLengthUnit.Auto ? 0f : value;
        }

        /// <summary>Unit of this length.</summary>
        public CssLengthUnit Unit { get; }

        /// <summary>
        /// Numeric value in <see cref="Unit"/>. A percentage is stored as written, so <c>50%</c> is
        /// <c>50</c>, not <c>0.5</c>.
        /// </summary>
        public float Value { get; }

        /// <summary>Gets a value indicating whether this length is <c>auto</c>.</summary>
        public bool IsAuto => Unit == CssLengthUnit.Auto;

        /// <summary>Gets a value indicating whether this length is an absolute pixel value.</summary>
        public bool IsAbsolute => Unit == CssLengthUnit.Pixel;

        /// <summary>Gets a value indicating whether this length is a percentage.</summary>
        public bool IsPercent => Unit == CssLengthUnit.Percent;

        /// <summary>Creates a pixel length.</summary>
        /// <param name="value">Value in logical pixels.</param>
        /// <returns>The created length.</returns>
        public static CssLength Pixels(float value) => new CssLength(CssLengthUnit.Pixel, value);

        /// <summary>Creates a percentage length.</summary>
        /// <param name="value">Percentage, where 50 means 50%.</param>
        /// <returns>The created length.</returns>
        public static CssLength Percent(float value) => new CssLength(CssLengthUnit.Percent, value);

        /// <summary>
        /// Parses a CSS length.
        /// </summary>
        /// <param name="text">Raw value such as <c>200px</c>, <c>50%</c>, <c>0</c> or <c>auto</c>.</param>
        /// <param name="length">The parsed length when parsing succeeds.</param>
        /// <returns><see langword="true"/> when <paramref name="text"/> is a supported length.</returns>
        /// <remarks>
        /// A bare number is only accepted when it is zero, because CSS requires a unit everywhere
        /// else and silently treating <c>width: 200</c> as pixels would hide a real mistake.
        /// </remarks>
        public static bool TryParse(string? text, out CssLength length)
        {
            length = Auto;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string value = text!.Trim();

            if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
            {
                length = Auto;
                return true;
            }

            if (value.EndsWith("%", StringComparison.Ordinal))
            {
                if (TryParseNumber(value.Substring(0, value.Length - 1), out float percent))
                {
                    length = Percent(percent);
                    return true;
                }

                return false;
            }

            if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseNumber(value.Substring(0, value.Length - 2), out float pixels))
                {
                    length = Pixels(pixels);
                    return true;
                }

                return false;
            }

            if (TryParseNumber(value, out float unitless) && unitless == 0f)
            {
                length = Zero;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves this length against a containing block size.
        /// </summary>
        /// <param name="basis">Size of the containing block along the relevant axis.</param>
        /// <param name="fallback">Value to return when this length is <c>auto</c>.</param>
        /// <returns>The resolved size in logical pixels.</returns>
        public float Resolve(float basis, float fallback)
        {
            switch (Unit)
            {
                case CssLengthUnit.Pixel:
                    return Value;
                case CssLengthUnit.Percent:
                    return basis * Value / 100f;
                default:
                    return fallback;
            }
        }

        /// <inheritdoc />
        public bool Equals(CssLength other)
        {
            return Unit == other.Unit && Value.Equals(other.Value);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is CssLength other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Unit * 397) ^ Value.GetHashCode();
            }
        }

        /// <summary>Compares two lengths for equality.</summary>
        public static bool operator ==(CssLength left, CssLength right) => left.Equals(right);

        /// <summary>Compares two lengths for inequality.</summary>
        public static bool operator !=(CssLength left, CssLength right) => !left.Equals(right);

        /// <summary>Returns the length as it would be written in CSS.</summary>
        public override string ToString()
        {
            switch (Unit)
            {
                case CssLengthUnit.Pixel:
                    return Value.ToString("0.###", CultureInfo.InvariantCulture) + "px";
                case CssLengthUnit.Percent:
                    return Value.ToString("0.###", CultureInfo.InvariantCulture) + "%";
                default:
                    return "auto";
            }
        }

        private static bool TryParseNumber(string text, out float value)
        {
            return float.TryParse(
                text.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }
    }
}
