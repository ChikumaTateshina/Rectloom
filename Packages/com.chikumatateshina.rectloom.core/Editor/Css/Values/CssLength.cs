#nullable enable

using System;
using System.Globalization;

namespace Rectloom.Core.Css.Values
{
    /// <summary>
    /// The units a length can be written in.
    /// </summary>
    /// <remarks>
    /// Absolute units (<c>in</c>, <c>cm</c>, <c>mm</c>, <c>q</c>, <c>pt</c>, <c>pc</c>) are converted
    /// to <see cref="Pixel"/> while parsing, using the fixed ratios CSS defines, so nothing
    /// downstream has to know they exist.
    /// <para>
    /// Font-relative units survive parsing because the font size of the element is not known until
    /// the cascade has run. They are resolved into <see cref="Pixel"/> while the computed style is
    /// built, which is where CSS resolves them too, so neither layout nor a backend ever sees one.
    /// </para>
    /// </remarks>
    public enum CssLengthUnit
    {
        /// <summary>The value is resolved from content or from the containing block.</summary>
        Auto = 0,

        /// <summary>A logical pixel, matching the canvas reference resolution.</summary>
        Pixel = 1,

        /// <summary>A percentage of the containing block's corresponding content size.</summary>
        Percent = 2,

        /// <summary>A multiple of the element's own font size.</summary>
        Em = 3,

        /// <summary>A multiple of the root element's font size.</summary>
        Rem = 4,
    }

    /// <summary>
    /// A CSS length such as <c>200px</c>, <c>50%</c>, <c>8mm</c>, <c>1.5em</c> or <c>auto</c>.
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

        /// <summary>Logical pixels per CSS inch, which CSS fixes at 96 regardless of the device.</summary>
        public const float PixelsPerInch = 96f;

        // Absolute units, longest suffix first so that a suffix which ends with a shorter one is
        // matched as itself. "rem" has to be tested before "em" for the same reason.
        private static readonly (string Suffix, float PixelsPerUnit)[] AbsoluteUnits =
        {
            ("in", PixelsPerInch),
            ("cm", PixelsPerInch / 2.54f),
            ("mm", PixelsPerInch / 25.4f),
            ("pt", PixelsPerInch / 72f),
            ("pc", PixelsPerInch / 6f),
            ("q", PixelsPerInch / 101.6f),
        };

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

        /// <summary>
        /// Gets a value indicating whether this length still needs a font size to be resolved.
        /// </summary>
        public bool IsFontRelative => Unit == CssLengthUnit.Em || Unit == CssLengthUnit.Rem;

        /// <summary>Creates a pixel length.</summary>
        /// <param name="value">Value in logical pixels.</param>
        /// <returns>The created length.</returns>
        public static CssLength Pixels(float value) => new CssLength(CssLengthUnit.Pixel, value);

        /// <summary>Creates a percentage length.</summary>
        /// <param name="value">Percentage, where 50 means 50%.</param>
        /// <returns>The created length.</returns>
        public static CssLength Percent(float value) => new CssLength(CssLengthUnit.Percent, value);

        /// <summary>Creates a length in <c>em</c>.</summary>
        /// <param name="value">Multiple of the element's own font size.</param>
        /// <returns>The created length.</returns>
        public static CssLength Em(float value) => new CssLength(CssLengthUnit.Em, value);

        /// <summary>Creates a length in <c>rem</c>.</summary>
        /// <param name="value">Multiple of the root element's font size.</param>
        /// <returns>The created length.</returns>
        public static CssLength Rem(float value) => new CssLength(CssLengthUnit.Rem, value);

        /// <summary>
        /// Parses a CSS length.
        /// </summary>
        /// <param name="text">
        /// Raw value such as <c>200px</c>, <c>50%</c>, <c>8mm</c>, <c>1.5em</c>, <c>0</c> or
        /// <c>auto</c>.
        /// </param>
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

            if (TryParseSuffixed(value, "px", out float pixels))
            {
                length = Pixels(pixels);
                return true;
            }

            // Tested before "em", which it ends with.
            if (TryParseSuffixed(value, "rem", out float rem))
            {
                length = Rem(rem);
                return true;
            }

            if (TryParseSuffixed(value, "em", out float em))
            {
                length = Em(em);
                return true;
            }

            foreach ((string suffix, float pixelsPerUnit) in AbsoluteUnits)
            {
                if (TryParseSuffixed(value, suffix, out float absolute))
                {
                    length = Pixels(absolute * pixelsPerUnit);
                    return true;
                }
            }

            if (TryParseNumber(value, out float unitless) && unitless == 0f)
            {
                length = Zero;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves a font-relative length into pixels, leaving every other unit untouched.
        /// </summary>
        /// <param name="fontSize">Font size of the element, in logical pixels, for <c>em</c>.</param>
        /// <param name="rootFontSize">Font size of the root element, for <c>rem</c>.</param>
        /// <returns>
        /// An equivalent length whose <see cref="IsFontRelative"/> is <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// Called while the computed style is built, which is the first point where the element's own
        /// font size is known and the last point before layout, so no later stage has to carry a font
        /// size around just to read a length.
        /// </remarks>
        public CssLength ToAbsolute(float fontSize, float rootFontSize)
        {
            switch (Unit)
            {
                case CssLengthUnit.Em:
                    return Pixels(Value * fontSize);
                case CssLengthUnit.Rem:
                    return Pixels(Value * rootFontSize);
                default:
                    return this;
            }
        }

        /// <summary>
        /// Resolves this length against a containing block size.
        /// </summary>
        /// <param name="basis">Size of the containing block along the relevant axis.</param>
        /// <param name="fallback">Value to return when this length is <c>auto</c>.</param>
        /// <returns>The resolved size in logical pixels.</returns>
        /// <remarks>
        /// A font-relative length resolves to <paramref name="fallback"/>, because resolving it needs
        /// a font size that layout does not have. <see cref="ToAbsolute"/> has already run by then, so
        /// reaching that case means a length skipped the computed-style stage.
        /// </remarks>
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
                    return Number() + "px";
                case CssLengthUnit.Percent:
                    return Number() + "%";
                case CssLengthUnit.Em:
                    return Number() + "em";
                case CssLengthUnit.Rem:
                    return Number() + "rem";
                default:
                    return "auto";
            }
        }

        private string Number() => Value.ToString("0.###", CultureInfo.InvariantCulture);

        private static bool TryParseSuffixed(string value, string suffix, out float number)
        {
            number = 0f;

            if (value.Length <= suffix.Length
                || !value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return TryParseNumber(value.Substring(0, value.Length - suffix.Length), out number);
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
