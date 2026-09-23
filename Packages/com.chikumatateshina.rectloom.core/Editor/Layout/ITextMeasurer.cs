#nullable enable

using Rectloom.Core.Css.Computed;

namespace Rectloom.Core.Layout
{
    /// <summary>
    /// The size a run of text needs.
    /// </summary>
    public readonly struct TextMeasurement
    {
        /// <summary>A measurement of nothing.</summary>
        public static readonly TextMeasurement Empty = new TextMeasurement(0f, 0f, 0);

        /// <summary>
        /// Creates a measurement.
        /// </summary>
        /// <param name="width">Width of the widest line, in logical pixels.</param>
        /// <param name="height">Total height of all lines, in logical pixels.</param>
        /// <param name="lineCount">Number of lines the text occupies.</param>
        public TextMeasurement(float width, float height, int lineCount)
        {
            Width = width;
            Height = height;
            LineCount = lineCount;
        }

        /// <summary>Width of the widest line, in logical pixels.</summary>
        public float Width { get; }

        /// <summary>Total height of all lines, in logical pixels.</summary>
        public float Height { get; }

        /// <summary>Number of lines the text occupies.</summary>
        public int LineCount { get; }

        /// <inheritdoc />
        public override string ToString() => $"{Width}x{Height} ({LineCount} lines)";
    }

    /// <summary>
    /// Measures text for the layout solver.
    /// </summary>
    /// <remarks>
    /// Measurement is an interface because the core cannot depend on TextMeshPro: the assembly
    /// dependency only runs the other way, from the uGUI backend to the core. The backend supplies a
    /// measurer backed by the real font, while the core ships a deterministic approximation so that
    /// layout can be tested without Unity or a font asset.
    /// <para>
    /// An implementation must be deterministic. The same text, style and available width have to
    /// produce the same measurement every time, or compiled output stops being reproducible.
    /// </para>
    /// </remarks>
    public interface ITextMeasurer
    {
        /// <summary>
        /// Measures a run of text.
        /// </summary>
        /// <param name="text">The text to measure, with whitespace already collapsed.</param>
        /// <param name="style">Text style to measure with.</param>
        /// <param name="availableWidth">
        /// Width the text may wrap within, or <see cref="float.PositiveInfinity"/> to measure the
        /// text on one line. Wrapping is only applied when <see cref="TextStyle.WrapsText"/> is true.
        /// </param>
        /// <returns>The size the text needs.</returns>
        TextMeasurement Measure(string text, TextStyle style, float availableWidth);
    }
}
