#nullable enable

using System;
using System.Collections.Generic;
using System.Text;

namespace Rectloom.Core.Css.Values
{
    /// <summary>
    /// Custom properties and <c>var()</c> substitution.
    /// </summary>
    /// <remarks>
    /// A custom property is any declaration whose name starts with <c>--</c>. Its value is kept as
    /// written and is never interpreted, because what it means depends entirely on the property it is
    /// later substituted into.
    /// <para>
    /// Substitution happens while the computed style is built, before any value is parsed. Doing it
    /// there, rather than inside each property's parser, is what lets a single <c>var()</c> stand for
    /// a length, a colour or a whole shorthand without the parsers knowing variables exist.
    /// </para>
    /// </remarks>
    public static class CssVariables
    {
        /// <summary>Prefix that marks a declaration as a custom property.</summary>
        public const string CustomPropertyPrefix = "--";

        /// <summary>
        /// Largest number of substitution rounds attempted before giving up.
        /// </summary>
        /// <remarks>
        /// A custom property may expand to a value that uses another one, so substitution repeats.
        /// The limit is what stops <c>--a: var(--b)</c> paired with <c>--b: var(--a)</c> from looping
        /// forever; such a pair is reported as unresolved rather than detected as a cycle, because the
        /// author's fix is the same either way.
        /// </remarks>
        public const int MaxSubstitutionRounds = 16;

        /// <summary>
        /// Gets a value indicating whether a property name declares a custom property.
        /// </summary>
        /// <param name="property">Property name as written.</param>
        /// <returns><see langword="true"/> for a name starting with <c>--</c>.</returns>
        public static bool IsCustomProperty(string? property)
        {
            return property != null
                && property.Length > CustomPropertyPrefix.Length
                && property.StartsWith(CustomPropertyPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Gets a value indicating whether a value mentions <c>var(</c> at all.
        /// </summary>
        /// <param name="value">Declaration value as written.</param>
        /// <returns><see langword="true"/> when substitution has something to do.</returns>
        /// <remarks>
        /// Checked before substituting so that the overwhelmingly common case, a value with no
        /// variables in it, costs one scan rather than a round of parsing.
        /// </remarks>
        public static bool ContainsVar(string? value)
        {
            return value != null && value.IndexOf("var(", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Substitutes every <c>var()</c> reference in a value.
        /// </summary>
        /// <param name="value">Declaration value as written.</param>
        /// <param name="customProperties">
        /// Custom properties in scope, keyed by name including the leading <c>--</c>.
        /// </param>
        /// <param name="result">The substituted value when substitution succeeds.</param>
        /// <param name="unresolved">
        /// Name of the first custom property that is neither declared nor given a fallback, when
        /// substitution fails.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when every reference was replaced. A value with no <c>var()</c> in
        /// it always succeeds and is returned unchanged.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="value"/> or <paramref name="customProperties"/> is null.
        /// </exception>
        public static bool TrySubstitute(
            string value,
            IReadOnlyDictionary<string, string> customProperties,
            out string result,
            out string? unresolved)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (customProperties == null)
            {
                throw new ArgumentNullException(nameof(customProperties));
            }

            result = value;
            unresolved = null;

            for (int round = 0; round < MaxSubstitutionRounds; round++)
            {
                if (!ContainsVar(result))
                {
                    return true;
                }

                if (!SubstituteOnce(result, customProperties, out string next, out unresolved))
                {
                    return false;
                }

                if (string.Equals(next, result, StringComparison.Ordinal))
                {
                    // Nothing changed although a reference is still present, so repeating cannot help.
                    unresolved = FindFirstReference(result);
                    return false;
                }

                result = next;
            }

            unresolved = FindFirstReference(result);
            return false;
        }

        private static bool SubstituteOnce(
            string value,
            IReadOnlyDictionary<string, string> customProperties,
            out string result,
            out string? unresolved)
        {
            var builder = new StringBuilder(value.Length);
            int index = 0;
            unresolved = null;

            while (index < value.Length)
            {
                int start = IndexOfVar(value, index);

                if (start < 0)
                {
                    builder.Append(value, index, value.Length - index);
                    break;
                }

                builder.Append(value, index, start - index);

                int open = start + "var".Length;
                int close = FindMatchingParenthesis(value, open);

                if (close < 0)
                {
                    // An unterminated var() cannot be read as anything else, so the whole value fails.
                    result = value;
                    unresolved = "var(";
                    return false;
                }

                string inner = value.Substring(open + 1, close - open - 1);
                SplitReference(inner, out string name, out string? fallback);

                if (customProperties.TryGetValue(name, out string substituted))
                {
                    builder.Append(substituted);
                }
                else if (fallback != null)
                {
                    builder.Append(fallback);
                }
                else
                {
                    result = value;
                    unresolved = name;
                    return false;
                }

                index = close + 1;
            }

            result = builder.ToString();
            return true;
        }

        private static int IndexOfVar(string value, int from)
        {
            for (int index = from; index + 3 < value.Length; index++)
            {
                if ((value[index] == 'v' || value[index] == 'V')
                    && (value[index + 1] == 'a' || value[index + 1] == 'A')
                    && (value[index + 2] == 'r' || value[index + 2] == 'R')
                    && value[index + 3] == '(')
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// Finds the parenthesis that closes the one at <paramref name="open"/>, ignoring quoted text.
        /// </summary>
        private static int FindMatchingParenthesis(string value, int open)
        {
            if (open >= value.Length || value[open] != '(')
            {
                return -1;
            }

            int depth = 0;

            for (int index = open; index < value.Length; index++)
            {
                char current = value[index];

                if (current == '"' || current == '\'')
                {
                    index = SkipQuoted(value, index);
                    continue;
                }

                if (current == '(')
                {
                    depth++;
                }
                else if (current == ')')
                {
                    depth--;

                    if (depth == 0)
                    {
                        return index;
                    }
                }
            }

            return -1;
        }

        private static int SkipQuoted(string value, int start)
        {
            char quote = value[start];

            for (int index = start + 1; index < value.Length; index++)
            {
                if (value[index] == quote)
                {
                    return index;
                }
            }

            return value.Length - 1;
        }

        /// <summary>
        /// Splits the inside of a <c>var()</c> into its name and its optional fallback.
        /// </summary>
        /// <remarks>
        /// Only the first comma separates the two. Everything after it is the fallback, commas
        /// included, so that <c>var(--font, "A", sans-serif)</c> keeps its whole font list.
        /// </remarks>
        private static void SplitReference(string inner, out string name, out string? fallback)
        {
            int depth = 0;

            for (int index = 0; index < inner.Length; index++)
            {
                char current = inner[index];

                if (current == '"' || current == '\'')
                {
                    index = SkipQuoted(inner, index);
                    continue;
                }

                if (current == '(')
                {
                    depth++;
                }
                else if (current == ')')
                {
                    depth--;
                }
                else if (current == ',' && depth == 0)
                {
                    name = inner.Substring(0, index).Trim();
                    fallback = inner.Substring(index + 1).Trim();
                    return;
                }
            }

            name = inner.Trim();
            fallback = null;
        }

        private static string FindFirstReference(string value)
        {
            int start = IndexOfVar(value, 0);

            if (start < 0)
            {
                return "var(";
            }

            int close = FindMatchingParenthesis(value, start + "var".Length);

            if (close < 0)
            {
                return "var(";
            }

            SplitReference(value.Substring(start + 4, close - start - 4), out string name, out _);
            return name;
        }
    }
}
