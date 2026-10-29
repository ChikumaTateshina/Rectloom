#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Rectloom.Core.Css.Computed;
using Rectloom.Core.Css.Values;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Dom;

namespace Rectloom.Core.Layout
{
    /// <summary>
    /// Gathers text and text-level elements into one run of styled spans.
    /// </summary>
    /// <remarks>
    /// This is the part of an inline formatting context the compiler reproduces. An inline element is
    /// folded into the run only when all it does is change how its text looks: it paints no box, asks
    /// for no component, and holds nothing but text and further elements of the same kind. Anything
    /// else stays a box of its own, as it always has.
    /// <para>
    /// White space collapses across element boundaries, as in a browser: <c>a &lt;b&gt; b&lt;/b&gt;</c>
    /// has one space, not two, and the run as a whole is trimmed at both ends.
    /// </para>
    /// </remarks>
    internal sealed class InlineContent
    {
        // Elements whose content is not text even when they are display: inline.
        private static readonly HashSet<string> Replaced = new HashSet<string>(StringComparer.Ordinal)
        {
            HtmlElements.Image, HtmlElements.Button, HtmlElements.Input, "select", "textarea", "iframe",
        };

        private readonly List<TextSpan> _spans = new List<TextSpan>();

        /// <summary>Gets a value indicating whether nothing has been added yet.</summary>
        internal bool IsEmpty => _spans.Count == 0;

        /// <summary>Source of the first text added, which is where an anonymous box is reported.</summary>
        internal SourceLocation FirstSource => _spans.Count == 0 ? SourceLocation.None : _spans[0].Source;

        /// <summary>
        /// Gets a value indicating whether an element can be folded into a run of text.
        /// </summary>
        internal static bool CanFold(DomElement element, ComputedStyleTree styles)
        {
            if (!styles.TryGetStyle(element, out ComputedStyle style))
            {
                return false;
            }

            if (!style.IsRendered)
            {
                // A hidden element contributes nothing either way.
                return true;
            }

            if (style.Display != CssDisplay.Inline
                || Replaced.Contains(element.TagName)
                || style.Visual.PaintsAnything
                || style.Position == CssPosition.Absolute)
            {
                return false;
            }

            foreach (DomAttribute attribute in element.Attributes)
            {
                // Something asked for this element as an object, so it has to stay one.
                if (attribute.Name.StartsWith("component", StringComparison.Ordinal)
                    || attribute.Name.StartsWith("vrc-", StringComparison.Ordinal)
                    || attribute.Name.StartsWith("unity-", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            foreach (DomNode child in element.Children)
            {
                if (child is DomElement childElement
                    && !string.Equals(childElement.TagName, HtmlElements.LineBreak, StringComparison.Ordinal)
                    && !CanFold(childElement, styles))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Adds a text node in the given style.</summary>
        internal void AddText(string? raw, TextStyle style, string? href, SourceLocation source)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            bool collapses = style.WhiteSpace != CssWhiteSpace.Pre && style.WhiteSpace != CssWhiteSpace.PreWrap;
            string text = collapses ? CollapseKeepingEnds(raw!) : raw!;

            // A space after a space, or at the very start, is the same collapsible space twice.
            if (collapses && text.Length > 0 && text[0] == ' ' && (EndsWithSpace() || _spans.Count == 0))
            {
                text = text.Substring(1);
            }

            Append(text, style, href, source);
        }

        /// <summary>Adds a line break.</summary>
        internal void AddBreak(TextStyle style, string? href, SourceLocation source)
        {
            TrimTrailingSpace();
            Append("\n", style, href, source);
        }

        /// <summary>Adds an element that <see cref="CanFold"/> accepted, with everything inside it.</summary>
        internal void AddElement(DomElement element, ComputedStyleTree styles, string? href)
        {
            if (!styles.TryGetStyle(element, out ComputedStyle style) || !style.IsRendered)
            {
                return;
            }

            if (string.Equals(element.TagName, "a", StringComparison.Ordinal)
                && element.TryGetAttribute("href", out DomAttribute link)
                && !string.IsNullOrWhiteSpace(link.Value))
            {
                href = link.Value.Trim();
            }

            foreach (DomNode child in element.Children)
            {
                switch (child)
                {
                    case DomText text:
                        AddText(text.Text, style.Text, href, text.Source);
                        break;
                    case DomElement childElement when string.Equals(childElement.TagName, HtmlElements.LineBreak, StringComparison.Ordinal):
                        AddBreak(style.Text, href, childElement.Source);
                        break;
                    case DomElement childElement:
                        AddElement(childElement, styles, href);
                        break;
                }
            }
        }

        /// <summary>
        /// Takes the run gathered so far and starts a new one.
        /// </summary>
        /// <param name="ownStyle">Style of the element the text sits directly in.</param>
        /// <param name="text">The plain text, or empty when there is none.</param>
        /// <returns>
        /// The spans, or null when every span is in <paramref name="ownStyle"/> and none is a link, in
        /// which case the plain text says everything.
        /// </returns>
        internal IReadOnlyList<TextSpan>? Take(TextStyle ownStyle, out string text)
        {
            TrimTrailingSpace();
            _spans.RemoveAll(span => span.Text.Length == 0);

            var taken = new List<TextSpan>(_spans);
            _spans.Clear();

            text = TextSpan.Join(taken);

            foreach (TextSpan span in taken)
            {
                if (!ReferenceEquals(span.Style, ownStyle) || span.Href != null)
                {
                    return taken;
                }
            }

            return null;
        }

        private void Append(string text, TextStyle style, string? href, SourceLocation source)
        {
            if (text.Length == 0)
            {
                return;
            }

            if (_spans.Count > 0)
            {
                TextSpan last = _spans[_spans.Count - 1];

                if (ReferenceEquals(last.Style, style) && string.Equals(last.Href, href, StringComparison.Ordinal))
                {
                    last.Text += text;
                    return;
                }
            }

            _spans.Add(new TextSpan(text, style, href, source));
        }

        private bool EndsWithSpace()
        {
            for (int index = _spans.Count - 1; index >= 0; index--)
            {
                string text = _spans[index].Text;

                if (text.Length > 0)
                {
                    char last = text[text.Length - 1];
                    return last == ' ' || last == '\n';
                }
            }

            return false;
        }

        private void TrimTrailingSpace()
        {
            for (int index = _spans.Count - 1; index >= 0; index--)
            {
                TextSpan span = _spans[index];

                if (span.Text.Length == 0)
                {
                    continue;
                }

                bool collapses = span.Style.WhiteSpace != CssWhiteSpace.Pre && span.Style.WhiteSpace != CssWhiteSpace.PreWrap;

                if (collapses && span.Text[span.Text.Length - 1] == ' ')
                {
                    span.Text = span.Text.Substring(0, span.Text.Length - 1);
                }

                return;
            }
        }

        private static string CollapseKeepingEnds(string text)
        {
            var builder = new StringBuilder(text.Length);
            bool space = false;

            foreach (char character in text)
            {
                if (character == ' ' || character == '\t' || character == '\n' || character == '\r' || character == '\f')
                {
                    space = true;
                    continue;
                }

                if (space)
                {
                    builder.Append(' ');
                    space = false;
                }

                builder.Append(character);
            }

            if (space)
            {
                builder.Append(' ');
            }

            return builder.ToString();
        }
    }
}
