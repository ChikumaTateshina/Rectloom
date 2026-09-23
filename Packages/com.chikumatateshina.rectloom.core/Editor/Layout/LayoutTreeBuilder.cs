#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Css.Computed;
using Rectloom.Core.Dom;

namespace Rectloom.Core.Layout
{
    /// <summary>
    /// Turns a styled document into a layout tree.
    /// </summary>
    /// <remarks>
    /// Two things happen here that the solver would otherwise have to deal with repeatedly:
    /// elements hidden with <c>display: none</c> are dropped along with their subtree, and text is
    /// collapsed and split into boxes so that no box mixes its own text with element children.
    /// </remarks>
    public static class LayoutTreeBuilder
    {
        /// <summary>
        /// Builds the layout tree of a document.
        /// </summary>
        /// <param name="document">The parsed document.</param>
        /// <param name="styles">Computed styles for the document's elements.</param>
        /// <returns>
        /// The root box, or <see langword="null"/> when the document has no root element or the root
        /// is hidden.
        /// </returns>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public static LayoutBox? Build(DomDocument document, ComputedStyleTree styles)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (styles == null)
            {
                throw new ArgumentNullException(nameof(styles));
            }

            DomElement? root = document.DocumentElement;

            return root == null ? null : BuildElement(root, styles);
        }

        private static LayoutBox? BuildElement(DomElement element, ComputedStyleTree styles)
        {
            if (!styles.TryGetStyle(element, out ComputedStyle style) || !style.IsRendered)
            {
                return null;
            }

            var box = new LayoutBox(element, style);
            var childBoxes = new List<LayoutBox>();
            var textRuns = new List<(string Text, Diagnostics.SourceLocation Source)>();

            foreach (DomNode node in element.Children)
            {
                switch (node)
                {
                    case DomText text:
                    {
                        string collapsed = TextCollapse.Collapse(text.Text, style.Text.WhiteSpace);

                        if (collapsed.Length > 0 && !IsOnlySeparator(collapsed))
                        {
                            textRuns.Add((collapsed, text.Source));
                        }

                        break;
                    }

                    case DomElement child:
                    {
                        LayoutBox? childBox = BuildElement(child, styles);

                        if (childBox != null)
                        {
                            // Text seen so far belongs before this element, so it is committed now
                            // and keeps its position in the flow.
                            FlushTextRuns(style, textRuns, childBoxes);
                            childBoxes.Add(childBox);
                        }

                        break;
                    }
                }
            }

            if (childBoxes.Count == 0)
            {
                // Nothing but text: the element itself renders it, which is what lets a paragraph
                // compile straight to one text object instead of an empty box wrapping one. An
                // element with no text at all keeps a null content, so it is not treated as a text
                // box with an empty string.
                string text = JoinRuns(textRuns);
                box.TextContent = text.Length == 0 ? null : text;
                return box;
            }

            FlushTextRuns(style, textRuns, childBoxes);

            foreach (LayoutBox child in childBoxes)
            {
                box.AddChild(child);
            }

            return box;
        }

        private static void FlushTextRuns(
            ComputedStyle style,
            List<(string Text, Diagnostics.SourceLocation Source)> runs,
            List<LayoutBox> target)
        {
            if (runs.Count == 0)
            {
                return;
            }

            target.Add(LayoutBox.CreateAnonymousText(style, JoinRuns(runs), runs[0].Source));
            runs.Clear();
        }

        private static string JoinRuns(List<(string Text, Diagnostics.SourceLocation Source)> runs)
        {
            if (runs.Count == 0)
            {
                return string.Empty;
            }

            if (runs.Count == 1)
            {
                return runs[0].Text;
            }

            var parts = new string[runs.Count];

            for (int index = 0; index < runs.Count; index++)
            {
                parts[index] = runs[index].Text;
            }

            return string.Join(" ", parts);
        }

        private static bool IsOnlySeparator(string collapsed)
        {
            // A run of pure indentation collapses to a single space; keeping it would add a stray
            // label between two blocks.
            return collapsed.Length == 1 && collapsed[0] == ' ';
        }
    }
}
