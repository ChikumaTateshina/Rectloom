#nullable enable

using System;
using System.Collections.Generic;
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
            private readonly DomDocument _document;
            private readonly IDiagnosticSink _diagnostics;
            private readonly List<DomElement> _openElements = new List<DomElement>();

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

            private void HandleEndTag(HtmlToken token)
            {
                if (HtmlElements.IsDocumentWrapper(token.Name))
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
        }
    }
}
