#nullable enable

using System;
using System.Collections.Generic;

namespace Rectloom.Core.Dom
{
    /// <summary>
    /// Tag names the compiler knows about, and the HTML parsing rules that depend on them.
    /// </summary>
    /// <remarks>
    /// The parser only consults <see cref="IsVoid"/>, because whether a tag can have children is a
    /// syntax question. Whether a tag is <em>supported</em> is a compilation question, answered when
    /// elements are mapped to IR nodes, so an unknown tag still parses into a normal element.
    /// </remarks>
    public static class HtmlElements
    {
        /// <summary>The body tag, which becomes the compiled root.</summary>
        public const string Body = "body";

        /// <summary>The div tag.</summary>
        public const string Div = "div";

        /// <summary>The span tag.</summary>
        public const string Span = "span";

        /// <summary>The p tag.</summary>
        public const string Paragraph = "p";

        /// <summary>The button tag.</summary>
        public const string Button = "button";

        /// <summary>The img tag.</summary>
        public const string Image = "img";

        /// <summary>The br tag.</summary>
        public const string LineBreak = "br";

        /// <summary>The html tag, accepted and unwrapped.</summary>
        public const string Html = "html";

        /// <summary>The head tag, accepted and unwrapped.</summary>
        public const string Head = "head";

        // The HTML void elements, listed in full rather than only the supported ones, so that a
        // document using an unsupported void element still produces a correctly shaped tree.
        private static readonly HashSet<string> VoidElements = new HashSet<string>(StringComparer.Ordinal)
        {
            "area", "base", "br", "col", "embed", "hr", "img", "input",
            "link", "meta", "param", "source", "track", "wbr",
        };

        private static readonly HashSet<string> HeadingElements = new HashSet<string>(StringComparer.Ordinal)
        {
            "h1", "h2", "h3", "h4", "h5", "h6",
        };

        private static readonly HashSet<string> SupportedElements = new HashSet<string>(StringComparer.Ordinal)
        {
            Body, Div, Span, Paragraph, Button, Image, LineBreak,
            "h1", "h2", "h3", "h4", "h5", "h6",
        };

        /// <summary>
        /// Gets a value indicating whether a tag can never have children or an end tag.
        /// </summary>
        /// <param name="tagName">Lower-cased tag name.</param>
        /// <returns><see langword="true"/> for void elements such as img and br.</returns>
        public static bool IsVoid(string tagName)
        {
            return VoidElements.Contains(tagName);
        }

        /// <summary>
        /// Gets a value indicating whether a tag is one of h1 to h6.
        /// </summary>
        /// <param name="tagName">Lower-cased tag name.</param>
        /// <returns><see langword="true"/> for heading tags.</returns>
        public static bool IsHeading(string tagName)
        {
            return HeadingElements.Contains(tagName);
        }

        /// <summary>
        /// Gets the heading level of <paramref name="tagName"/>.
        /// </summary>
        /// <param name="tagName">Lower-cased tag name.</param>
        /// <returns>1 to 6 for h1 to h6, or 0 when the tag is not a heading.</returns>
        public static int GetHeadingLevel(string tagName)
        {
            return IsHeading(tagName) ? tagName[1] - 48 : 0;
        }

        /// <summary>
        /// Gets a value indicating whether the compiler maps this tag to a specific kind of UI node.
        /// </summary>
        /// <param name="tagName">Lower-cased tag name.</param>
        /// <returns><see langword="true"/> for the supported element set.</returns>
        /// <remarks>
        /// An unsupported tag is not an error: it compiles to a generic container and reports
        /// <c>HTML1003</c>, and fails only when strict mode is on.
        /// </remarks>
        public static bool IsSupported(string tagName)
        {
            return SupportedElements.Contains(tagName);
        }

        /// <summary>
        /// Gets a value indicating whether a tag is a document wrapper that carries no layout of its own.
        /// </summary>
        /// <param name="tagName">Lower-cased tag name.</param>
        /// <returns><see langword="true"/> for html and head.</returns>
        public static bool IsDocumentWrapper(string tagName)
        {
            return string.Equals(tagName, Html, StringComparison.Ordinal)
                || string.Equals(tagName, Head, StringComparison.Ordinal);
        }
    }
}
