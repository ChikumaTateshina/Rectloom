#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Dom
{
    /// <summary>
    /// Root of a parsed HTML document.
    /// </summary>
    /// <remarks>
    /// The document is a container above the root element, so that a source file with stray text or
    /// several top-level elements still produces a single tree.
    /// </remarks>
    public sealed class DomDocument : DomNode
    {
        private Dictionary<string, DomElement>? _elementsById;

        /// <summary>
        /// Creates an empty document.
        /// </summary>
        /// <param name="filePath">Asset path of the HTML source.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="filePath"/> is null, empty or whitespace.
        /// </exception>
        public DomDocument(string filePath)
            : base(SourceLocation.FileStart(filePath))
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must not be empty.", nameof(filePath));
            }

            FilePath = filePath;
        }

        /// <summary>Asset path of the HTML source this document was parsed from.</summary>
        public string FilePath { get; }

        /// <summary>
        /// The document's root element, normally the body element, or <see langword="null"/> when the
        /// source contained no element at all.
        /// </summary>
        public DomElement? DocumentElement
        {
            get
            {
                foreach (DomNode child in Children)
                {
                    if (child is DomElement element)
                    {
                        return element;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Walks every element in the document, in document order.
        /// </summary>
        /// <returns>All elements in the document.</returns>
        public IEnumerable<DomElement> Elements()
        {
            foreach (DomNode node in DescendantsAndSelf())
            {
                if (node is DomElement element)
                {
                    yield return element;
                }
            }
        }

        /// <summary>
        /// Looks up an element by its id attribute.
        /// </summary>
        /// <param name="id">The id to find, compared case sensitively.</param>
        /// <param name="element">The first element with that id, in document order.</param>
        /// <returns><see langword="true"/> when an element with that id exists.</returns>
        /// <remarks>
        /// When a document repeats an id the parser reports <c>HTML1002</c>; this lookup then returns
        /// the first occurrence, which is also the one that keeps the id as its stable ID.
        /// The index is built on first use, and the tree does not change after parsing.
        /// </remarks>
        public bool TryGetElementById(string id, out DomElement element)
        {
            if (_elementsById == null)
            {
                _elementsById = BuildIdIndex();
            }

            return _elementsById.TryGetValue(id, out element!);
        }

        private Dictionary<string, DomElement> BuildIdIndex()
        {
            var index = new Dictionary<string, DomElement>(StringComparer.Ordinal);

            foreach (DomElement element in Elements())
            {
                if (element.Id != null && !index.ContainsKey(element.Id))
                {
                    index.Add(element.Id, element);
                }
            }

            return index;
        }
    }
}
