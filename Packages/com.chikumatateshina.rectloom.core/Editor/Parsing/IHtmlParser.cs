#nullable enable

using Rectloom.Core.Diagnostics;
using Rectloom.Core.Dom;

namespace Rectloom.Core.Parsing
{
    /// <summary>
    /// Turns HTML source text into the compiler's internal DOM.
    /// </summary>
    /// <remarks>
    /// The pipeline depends on this interface rather than on a concrete parser, so that the parser
    /// can be replaced without touching the DOM or anything downstream of it.
    /// <para>
    /// Implementations report malformed source as diagnostics and always return a document. Parsing
    /// is a syntax step only: it does not decide which elements the compiler supports, which is why
    /// an unrecognised tag still becomes an ordinary <see cref="DomElement"/>.
    /// </para>
    /// </remarks>
    public interface IHtmlParser
    {
        /// <summary>
        /// Parses one HTML source file.
        /// </summary>
        /// <param name="filePath">Asset path of the source, used in diagnostics.</param>
        /// <param name="source">The file contents.</param>
        /// <param name="diagnostics">Sink for parse diagnostics.</param>
        /// <returns>
        /// The parsed document. Never <see langword="null"/>, even for empty or malformed input; in
        /// that case <see cref="DomDocument.DocumentElement"/> may be <see langword="null"/>.
        /// </returns>
        DomDocument Parse(string filePath, string source, IDiagnosticSink diagnostics);
    }
}
