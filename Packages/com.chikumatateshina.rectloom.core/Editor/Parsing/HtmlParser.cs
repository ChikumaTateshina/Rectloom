#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Dom;

namespace Rectloom.Core.Parsing
{
    /// <summary>
    /// Builds a <see cref="DomDocument"/> from HTML source.
    /// </summary>
    /// <remarks>
    /// This parser covers the subset of HTML the compiler accepts rather than the HTML5 tree
    /// construction algorithm; see ADR-0002. It recovers from malformed input instead of failing:
    /// a mismatched end tag closes the elements it can and reports a diagnostic, and an element left
    /// open at the end of the file is closed implicitly.
    /// <para>
    /// The body element is the document root. When the source has no body tag, one is synthesised at
    /// the position of the first content, so every document has the same shape downstream.
    /// </para>
    /// <para>
    /// Head-level elements never reach the tree. A <c>style</c> element is collected as a stylesheet
    /// and a <c>title</c> is dropped, because leaving either in the tree would lay its text out as a
    /// label.
    /// </para>
    /// </remarks>
    public sealed class HtmlParser : IHtmlParser
    {
        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public DomDocument Parse(string filePath, string source, IDiagnosticSink diagnostics)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            IReadOnlyList<HtmlToken> tokens = new HtmlTokenizer(filePath, source, diagnostics).Tokenize();
            var builder = new TreeBuilder(filePath, diagnostics);
            return builder.Build(tokens);
        }

        /// <summary>
        /// Assembles tokens into a document, tracking which elements are still open.
        /// </summary>
        private sealed class TreeBuilder
        {
            private const string RelAttributeName = "rel";
            private const string HrefAttributeName = "href";
            private const string StyleSheetRel = "stylesheet";

            private readonly DomDocument _document;
            private readonly IDiagnosticSink _diagnostics;
            private readonly List<DomElement> _openElements = new List<DomElement>();
            private readonly List<Suppressed> _suppressed = new List<Suppressed>();

            private DomElement? _root;
            private DomElement? _syntheticRoot;

            internal TreeBuilder(string filePath, IDiagnosticSink diagnostics)
            {
                _document = new DomDocument(filePath);
                _diagnostics = diagnostics;
            }

            internal DomDocument Build(IReadOnlyList<HtmlToken> tokens)
            {
                foreach (HtmlToken token in tokens)
                {
                    if (_suppressed.Count > 0)
                    {
                        HandleSuppressed(token);
                        continue;
                    }

                    switch (token.Kind)
                    {
                        case HtmlTokenKind.StartTag:
                            HandleStartTag(token);
                            break;
                        case HtmlTokenKind.EndTag:
                            HandleEndTag(token);
                            break;
                        default:
                            HandleText(token);
                            break;
                    }
                }

                CloseSuppressed(0);
                ReportElementsLeftOpen();
                ReportDuplicateIds();
                return _document;
            }

            private DomNode CurrentParent
            {
                get
                {
                    if (_openElements.Count > 0)
                    {
                        return _openElements[_openElements.Count - 1];
                    }

                    // Content after the root was closed still belongs to the root, never beside it.
                    return (DomNode?)_root ?? _document;
                }
            }

            private void HandleStartTag(HtmlToken token)
            {
                // html and head carry no layout, so they are unwrapped: their children stay, the tag goes.
                if (HtmlElements.IsDocumentWrapper(token.Name))
                {
                    return;
                }

                if (HtmlElements.IsMetadata(token.Name))
                {
                    HandleMetadataStartTag(token);
                    return;
                }

                if (HtmlElements.IsUnrenderable(token.Name))
                {
                    _diagnostics.Warning(
                        DiagnosticCodes.Html.UnknownElement,
                        "<" + token.Name + "> and its content cannot be compiled into UI objects, so "
                            + "the whole element was dropped.",
                        token.Source,
                        "Export it as an image and use <img>, or build it with a component.");

                    Suppress(token, collectStyleSheet: false);
                    return;
                }

                bool isRootTag = string.Equals(token.Name, HtmlElements.Body, StringComparison.Ordinal)
                    && _root == null
                    && _openElements.Count == 0;

                var element = new DomElement(token.Name, token.Source);
                AddAttributes(element, token);

                if (isRootTag)
                {
                    _root = element;
                    _document.AppendChild(element);
                    _openElements.Add(element);
                    return;
                }

                EnsureRoot(token.Source);
                CurrentParent.AppendChild(element);

                if (!token.SelfClosing && !HtmlElements.IsVoid(token.Name))
                {
                    _openElements.Add(element);
                }
            }

            /// <summary>
            /// Handles a head-level element: a stylesheet is collected, everything else is dropped.
            /// </summary>
            private void HandleMetadataStartTag(HtmlToken token)
            {
                if (string.Equals(token.Name, HtmlElements.Link, StringComparison.Ordinal))
                {
                    TryCollectLinkedStyleSheet(token);
                    return;
                }

                bool isStyle = string.Equals(token.Name, HtmlElements.Style, StringComparison.Ordinal);
                Suppress(token, collectStyleSheet: isStyle);
            }

            private void TryCollectLinkedStyleSheet(HtmlToken token)
            {
                string? rel = null;
                string? href = null;

                foreach (DomAttribute attribute in token.Attributes)
                {
                    if (string.Equals(attribute.Name, RelAttributeName, StringComparison.Ordinal))
                    {
                        rel = attribute.Value;
                    }
                    else if (string.Equals(attribute.Name, HrefAttributeName, StringComparison.Ordinal))
                    {
                        href = attribute.Value;
                    }
                }

                bool isStyleSheet = rel != null
                    && rel.IndexOf(StyleSheetRel, StringComparison.OrdinalIgnoreCase) >= 0;

                if (!isStyleSheet || string.IsNullOrWhiteSpace(href))
                {
                    return;
                }

                _document.AddStyleSheet(DomStyleSheet.Linked(href!, token.Source));
            }

            /// <summary>
            /// Starts dropping a subtree, unless the element cannot contain one.
            /// </summary>
            private void Suppress(HtmlToken token, bool collectStyleSheet)
            {
                if (token.SelfClosing || HtmlElements.IsVoid(token.Name))
                {
                    return;
                }

                _suppressed.Add(new Suppressed(
                    token.Name,
                    token.Source,
                    collectStyleSheet ? new StringBuilder() : null));
            }

            /// <summary>
            /// Consumes a token inside a dropped subtree.
            /// </summary>
            /// <remarks>
            /// Nested start tags are tracked so that an end tag closes the element it belongs to. A
            /// <c>style</c> body arrives as one raw-text token, so the collected text is the CSS
            /// exactly as authored.
            /// </remarks>
            private void HandleSuppressed(HtmlToken token)
            {
                switch (token.Kind)
                {
                    case HtmlTokenKind.StartTag:
                        if (!token.SelfClosing && !HtmlElements.IsVoid(token.Name))
                        {
                            _suppressed.Add(new Suppressed(token.Name, token.Source, null));
                        }

                        return;

                    case HtmlTokenKind.EndTag:
                        for (int index = _suppressed.Count - 1; index >= 0; index--)
                        {
                            if (string.Equals(_suppressed[index].Name, token.Name, StringComparison.Ordinal))
                            {
                                CloseSuppressed(index);
                                return;
                            }
                        }

                        return;

                    default:
                        StringBuilder? text = _suppressed[_suppressed.Count - 1].Text;
                        text?.Append(token.Text);
                        return;
                }
            }

            /// <summary>
            /// Closes every dropped element from <paramref name="depth"/> upwards, collecting any CSS.
            /// </summary>
            private void CloseSuppressed(int depth)
            {
                for (int index = _suppressed.Count - 1; index >= depth; index--)
                {
                    Suppressed frame = _suppressed[index];
                    _suppressed.RemoveAt(index);

                    if (frame.Text == null || frame.Text.Length == 0)
                    {
                        continue;
                    }

                    _document.AddStyleSheet(DomStyleSheet.Embedded(frame.Text.ToString(), frame.Source));
                }
            }

            private void HandleEndTag(HtmlToken token)
            {
                if (HtmlElements.IsDocumentWrapper(token.Name) || HtmlElements.IsMetadata(token.Name))
                {
                    return;
                }

                if (HtmlElements.IsVoid(token.Name))
                {
                    _diagnostics.Warning(
                        DiagnosticCodes.Html.UnexpectedClosingTag,
                        "<" + token.Name + "> never has an end tag.",
                        token.Source,
                        "Remove </" + token.Name + ">.");
                    return;
                }

                int match = FindLastOpen(token.Name);

                if (match < 0)
                {
                    _diagnostics.Warning(
                        DiagnosticCodes.Html.UnexpectedClosingTag,
                        "</" + token.Name + "> does not close any open element.",
                        token.Source,
                        "Remove it, or add a matching <" + token.Name + ">.");
                    return;
                }

                // Everything opened after the matched element is closed implicitly, as browsers do.
                for (int index = _openElements.Count - 1; index > match; index--)
                {
                    DomElement unclosed = _openElements[index];

                    _diagnostics.Warning(
                        DiagnosticCodes.Html.UnclosedElement,
                        "<" + unclosed.TagName + "> is closed implicitly by </" + token.Name + ">.",
                        unclosed.Source,
                        "Add </" + unclosed.TagName + "> before </" + token.Name + ">.");

                    _openElements.RemoveAt(index);
                }

                _openElements.RemoveAt(match);
            }

            private void HandleText(HtmlToken token)
            {
                bool isIndentationBeforeRoot = _root == null && string.IsNullOrWhiteSpace(token.Text);

                if (isIndentationBeforeRoot || token.Text.Length == 0)
                {
                    return;
                }

                EnsureRoot(token.Source);
                CurrentParent.AppendChild(new DomText(token.Text, token.Source));
            }

            private void EnsureRoot(SourceLocation location)
            {
                if (_root != null)
                {
                    return;
                }

                // A fragment without a body tag still needs one root, so downstream stages see the
                // same shape whether or not the author wrote <body>.
                _root = new DomElement(HtmlElements.Body, location);
                _syntheticRoot = _root;
                _document.AppendChild(_root);
                _openElements.Add(_root);
            }

            private void AddAttributes(DomElement element, HtmlToken token)
            {
                foreach (DomAttribute attribute in token.Attributes)
                {
                    element.TryAddAttribute(attribute);
                }
            }

            private int FindLastOpen(string tagName)
            {
                for (int index = _openElements.Count - 1; index >= 0; index--)
                {
                    if (string.Equals(_openElements[index].TagName, tagName, StringComparison.Ordinal))
                    {
                        return index;
                    }
                }

                return -1;
            }

            private void ReportElementsLeftOpen()
            {
                for (int index = _openElements.Count - 1; index >= 0; index--)
                {
                    DomElement element = _openElements[index];

                    // The synthesised root was never written by the author, so there is no missing
                    // end tag to report.
                    if (ReferenceEquals(element, _syntheticRoot))
                    {
                        continue;
                    }

                    _diagnostics.Warning(
                        DiagnosticCodes.Html.UnclosedElement,
                        "<" + element.TagName + "> is not closed before the end of the file.",
                        element.Source,
                        "Add </" + element.TagName + ">.");
                }

                _openElements.Clear();
            }

            private void ReportDuplicateIds()
            {
                var seen = new Dictionary<string, DomElement>(StringComparer.Ordinal);

                foreach (DomElement element in _document.Elements())
                {
                    if (element.Id == null)
                    {
                        continue;
                    }

                    if (seen.TryGetValue(element.Id, out DomElement first))
                    {
                        _diagnostics.Error(
                            DiagnosticCodes.Html.DuplicateId,
                            "id '" + element.Id + "' is already used by <" + first.TagName + "> at "
                                + first.Source + ". This element falls back to a generated stable ID, "
                                + "so an update compile cannot reliably match it.",
                            element.Source,
                            "Give each element a unique id.");
                        continue;
                    }

                    seen.Add(element.Id, element);
                }
            }

            /// <summary>
            /// One element of a subtree that is being dropped rather than built.
            /// </summary>
            private readonly struct Suppressed
            {
                internal Suppressed(string name, SourceLocation source, StringBuilder? text)
                {
                    Name = name;
                    Source = source;
                    Text = text;
                }

                /// <summary>Tag name, used to match the end tag that closes this element.</summary>
                internal string Name { get; }

                /// <summary>Where the start tag was written.</summary>
                internal SourceLocation Source { get; }

                /// <summary>
                /// Collected text, for a <c>style</c> element, or <see langword="null"/> when the
                /// content is simply discarded.
                /// </summary>
                internal StringBuilder? Text { get; }
            }
        }
    }
}
