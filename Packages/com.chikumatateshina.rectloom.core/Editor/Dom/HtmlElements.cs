#nullable enable

using System;
using System.Collections.Generic;

namespace Rectloom.Core.Dom
{
    /// <summary>
    /// Tag names the compiler knows about, and the HTML parsing rules that depend on them.
    /// </summary>
    /// <remarks>
    /// The parser consults the rules that are questions of syntax: whether a tag can have children,
    /// whether its content is raw text, and whether it belongs to the document's head rather than to
    /// its layout. Whether a tag is <em>supported</em> is a compilation question, answered when
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

        /// <summary>The style tag, whose content becomes an author stylesheet.</summary>
        public const string Style = "style";

        /// <summary>The link tag, which may reference an external stylesheet.</summary>
        public const string Link = "link";

        // The HTML void elements, listed in full rather than only the supported ones, so that a
        // document using an unsupported void element still produces a correctly shaped tree.
        private static readonly HashSet<string> VoidElements = new HashSet<string>(StringComparer.Ordinal)
        {
            "area", "base", "br", "col", "embed", "hr", "img", "input",
            "link", "meta", "param", "source", "track", "wbr",
        };

        // Elements whose content is text rather than markup. A '<' inside one of these is data, so
        // tokenizing it as a tag would turn a stylesheet or a script into a tree of nonsense.
        private static readonly HashSet<string> RawTextElements = new HashSet<string>(StringComparer.Ordinal)
        {
            Style, "script", "textarea", "title",
        };

        // Elements that describe the document rather than lay it out. They are dropped along with
        // their content, which is what keeps a stylesheet or a page title from being rendered as a
        // label. A <style> is read for its CSS before being dropped.
        private static readonly HashSet<string> MetadataElements = new HashSet<string>(StringComparer.Ordinal)
        {
            Style, Link, "script", "title", "meta", "base", "noscript", "template",
        };

        // Elements whose content is not HTML, or whose behaviour a static UI hierarchy cannot host.
        // They are dropped with a diagnostic: compiling the inside of an <svg> as a box tree would
        // produce a hierarchy that looks nothing like the drawing it describes.
        private static readonly HashSet<string> UnrenderableElements = new HashSet<string>(StringComparer.Ordinal)
        {
            "svg", "math", "canvas", "video", "audio", "iframe", "object", "embed", "applet",
        };

        private static readonly HashSet<string> HeadingElements = new HashSet<string>(StringComparer.Ordinal)
        {
            "h1", "h2", "h3", "h4", "h5", "h6",
        };

        // Tags that compile to a box the compiler is content with. A sectioning or grouping element is
        // here because a plain container is genuinely all it means for layout; an element whose own
        // behaviour would be lost, such as a form control or a table, is deliberately absent so that
        // the loss is reported.
        private static readonly HashSet<string> SupportedElements = new HashSet<string>(StringComparer.Ordinal)
        {
            Body, Div, Span, Paragraph, Button, Image, LineBreak,
            "h1", "h2", "h3", "h4", "h5", "h6",

            // Sectioning and grouping, all of which are boxes and nothing more.
            "article", "section", "header", "footer", "nav", "main", "aside",
            "figure", "figcaption", "blockquote", "pre", "hr", "address", "hgroup",

            // Lists. The marker is not drawn, which the user-agent stylesheet makes explicit by
            // laying them out as plain blocks.
            "ul", "ol", "li", "dl", "dt", "dd",

            // Text-level semantics, which only affect the inherited text style.
            "a", "strong", "b", "em", "i", "u", "s", "strike", "small", "big",
            "code", "kbd", "samp", "var", "mark", "cite", "q", "abbr", "time",
            "label", "sub", "sup", "ins", "del", "bdi", "bdo", "ruby", "rt", "rp",
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
        /// Gets a value indicating whether a tag's content is text rather than markup.
        /// </summary>
        /// <param name="tagName">Lower-cased tag name.</param>
        /// <returns><see langword="true"/> for style, script, textarea and title.</returns>
        /// <remarks>
        /// The tokenizer reads such content verbatim up to the matching end tag, without decoding
        /// character references, because a stylesheet or a script means every character literally.
        /// </remarks>
        public static bool IsRawText(string tagName)
        {
            return RawTextElements.Contains(tagName);
        }

        /// <summary>
        /// Gets a value indicating whether a tag describes the document rather than laying it out.
        /// </summary>
        /// <param name="tagName">Lower-cased tag name.</param>
        /// <returns><see langword="true"/> for head-level elements such as style, title and meta.</returns>
        /// <remarks>
        /// Such an element is dropped together with its content, so that a page title or an embedded
        /// stylesheet never turns into a visible label.
        /// </remarks>
        public static bool IsMetadata(string tagName)
        {
            return MetadataElements.Contains(tagName);
        }

        /// <summary>
        /// Gets a value indicating whether a tag's content cannot be compiled into a box tree.
        /// </summary>
        /// <param name="tagName">Lower-cased tag name.</param>
        /// <returns><see langword="true"/> for svg, video, iframe and their kind.</returns>
        /// <remarks>
        /// The element and its subtree are dropped and reported once, rather than each descendant
        /// being reported as an unknown element on its own.
        /// </remarks>
        public static bool IsUnrenderable(string tagName)
        {
            return UnrenderableElements.Contains(tagName);
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
