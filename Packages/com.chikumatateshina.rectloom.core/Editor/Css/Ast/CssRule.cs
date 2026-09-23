#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Css.Selectors;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Css.Ast
{
    /// <summary>
    /// One style rule: a selector list and the declarations it applies.
    /// </summary>
    public sealed class CssRule
    {
        /// <summary>
        /// Creates a rule.
        /// </summary>
        /// <param name="selectors">Selectors this rule applies to. Must not be empty.</param>
        /// <param name="declarations">Declarations in source order.</param>
        /// <param name="sourceOrder">
        /// Position of this rule across all stylesheets, counting from zero. Breaks ties between
        /// declarations of equal specificity, where the later rule wins.
        /// </param>
        /// <param name="source">Position of the selector list in the source file.</param>
        /// <exception cref="ArgumentNullException">A collection argument is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="selectors"/> is empty.</exception>
        public CssRule(
            IReadOnlyList<CssSelector> selectors,
            IReadOnlyList<CssDeclaration> declarations,
            int sourceOrder,
            SourceLocation source)
        {
            if (selectors == null)
            {
                throw new ArgumentNullException(nameof(selectors));
            }

            if (selectors.Count == 0)
            {
                throw new ArgumentException("A rule must have at least one selector.", nameof(selectors));
            }

            Selectors = selectors;
            Declarations = declarations ?? throw new ArgumentNullException(nameof(declarations));
            SourceOrder = sourceOrder;
            Source = source;
        }

        /// <summary>Selectors this rule applies to.</summary>
        public IReadOnlyList<CssSelector> Selectors { get; }

        /// <summary>Declarations in source order.</summary>
        public IReadOnlyList<CssDeclaration> Declarations { get; }

        /// <summary>
        /// Position of this rule across all stylesheets, counting from zero.
        /// </summary>
        public int SourceOrder { get; }

        /// <summary>Position of the selector list in the source file.</summary>
        public SourceLocation Source { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            var texts = new List<string>(Selectors.Count);

            foreach (CssSelector selector in Selectors)
            {
                texts.Add(selector.RawText);
            }

            return string.Join(", ", texts) + " { " + Declarations.Count + " declarations }";
        }
    }
}
