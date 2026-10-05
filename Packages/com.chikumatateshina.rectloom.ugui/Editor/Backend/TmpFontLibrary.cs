#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Ir;
using TMPro;
using UnityEditor;

namespace Rectloom.Ugui.Backend
{
    /// <summary>
    /// Finds the TextMeshPro font asset a <c>font-family</c> asks for.
    /// </summary>
    /// <remarks>
    /// CSS names a family, Unity needs an asset. The project is searched once per compile and every
    /// font asset is indexed under both its own name and the family name baked into it, so that
    /// <c>font-family: "Noto Sans JP"</c> finds an asset called <c>NotoSansJP-Regular SDF</c>.
    /// <para>
    /// This matters beyond typography. TextMeshPro's default font has no CJK glyphs, so a document in
    /// Japanese renders as a wall of boxes until a font that has them is chosen; being able to name one
    /// in CSS is what makes that possible without editing the generated objects by hand.
    /// </para>
    /// </remarks>
    public sealed class TmpFontLibrary
    {
        // Families that name a category rather than a face. There is no asset to look for, and the
        // default font is the honest answer, so they resolve to it without a diagnostic.
        private static readonly HashSet<string> GenericFamilies = new HashSet<string>(StringComparer.Ordinal)
        {
            "serif", "sansserif", "monospace", "cursive", "fantasy",
            "systemui", "uiserif", "uisansserif", "uimonospace", "uirounded",
            "mathematical", "emoji", "fangsong", "inherit", "initial", "unset",
        };

        private readonly Dictionary<string, TMP_FontAsset?> _byFamily =
            new Dictionary<string, TMP_FontAsset?>(StringComparer.Ordinal);

        private readonly HashSet<string> _reported = new HashSet<string>(StringComparer.Ordinal);
        private readonly IDiagnosticSink? _diagnostics;
        private readonly TMP_FontAsset? _fallback;

        private List<Entry>? _index;

        /// <summary>
        /// Creates a library.
        /// </summary>
        /// <param name="fallback">
        /// Font used when a style names no family, or none of the families it names can be found. Null
        /// means TextMeshPro's own default.
        /// </param>
        /// <param name="diagnostics">
        /// Sink for a family that could not be found, or null to resolve silently.
        /// </param>
        /// <param name="emojiFont">Font explicitly used for emoji runs, independent of body font fallback.</param>
        /// <param name="emojiSprites">Color sprites generated from that font, or null for monochrome font runs.</param>
        public TmpFontLibrary(TMP_FontAsset? fallback = null, IDiagnosticSink? diagnostics = null,
            TMP_FontAsset? emojiFont = null, IReadOnlyDictionary<uint, TMP_SpriteAsset>? emojiSprites = null)
        {
            _fallback = fallback;
            EmojiFont = emojiFont;
            EmojiSprites = emojiSprites;
            _diagnostics = diagnostics;
        }

        /// <summary>The font used when nothing more specific is found.</summary>
        public TMP_FontAsset? Fallback => _fallback;

        /// <summary>Font used directly for emoji runs.</summary>
        public TMP_FontAsset? EmojiFont { get; }
        /// <summary>Color sprites generated from the emoji font, indexed by Unicode scalar.</summary>
        public IReadOnlyDictionary<uint, TMP_SpriteAsset>? EmojiSprites { get; }

        /// <summary>Finds an installed named font without applying a style.</summary>
        public TMP_FontAsset? FindFamily(string family) => Find(Normalise(family));

        /// <summary>
        /// Resolves the font a text style should render with.
        /// </summary>
        /// <param name="style">Resolved text style, whose family list is walked in order.</param>
        /// <param name="source">Position to report an unresolved family against.</param>
        /// <returns>
        /// The font asset to use, or <see langword="null"/> to leave TextMeshPro's default in place.
        /// </returns>
        public TMP_FontAsset? Resolve(UiTextStyle? style, SourceLocation source = default)
        {
            if (style == null || style.FontFamily.Count == 0)
            {
                return _fallback;
            }

            bool anyNamed = false;

            foreach (string family in style.FontFamily)
            {
                string key = Normalise(family);

                if (key.Length == 0 || GenericFamilies.Contains(key))
                {
                    // A generic family ends the list as far as this compiler is concerned: everything
                    // after it would be less preferred than the default it resolves to.
                    return _fallback;
                }

                anyNamed = true;
                TMP_FontAsset? found = Find(key);

                if (found != null)
                {
                    return found;
                }
            }

            if (anyNamed)
            {
                ReportUnresolved(style.FontFamily, source);
            }

            return _fallback;
        }

        private TMP_FontAsset? Find(string key)
        {
            if (_byFamily.TryGetValue(key, out TMP_FontAsset? cached))
            {
                return cached;
            }

            TMP_FontAsset? found = Search(key);
            _byFamily[key] = found;
            return found;
        }

        /// <summary>
        /// Looks for a font asset whose family or asset name matches, preferring an exact match.
        /// </summary>
        /// <remarks>
        /// A prefix match is accepted as a second choice because font assets are conventionally named
        /// after the family plus a weight and a rendering mode, as in <c>NotoSansJP-Regular SDF</c>.
        /// Requiring an exact match would mean no realistic asset ever matched a CSS family name.
        /// </remarks>
        private TMP_FontAsset? Search(string key)
        {
            TMP_FontAsset? prefixMatch = null;

            foreach (Entry entry in Index)
            {
                if (string.Equals(entry.FamilyKey, key, StringComparison.Ordinal)
                    || string.Equals(entry.NameKey, key, StringComparison.Ordinal))
                {
                    return entry.Font;
                }

                if (prefixMatch == null
                    && (entry.NameKey.StartsWith(key, StringComparison.Ordinal)
                        || entry.FamilyKey.StartsWith(key, StringComparison.Ordinal)))
                {
                    prefixMatch = entry.Font;
                }
            }

            return prefixMatch;
        }

        /// <summary>
        /// Every font asset in the project, indexed on first use.
        /// </summary>
        /// <remarks>
        /// Built once per library because searching the asset database is slow and a document mentions
        /// only a handful of families. A font asset imported during a compile is therefore not picked
        /// up until the next one, which is the right trade: a compile should see one fixed project.
        /// </remarks>
        private List<Entry> Index
        {
            get
            {
                if (_index != null)
                {
                    return _index;
                }

                _index = new List<Entry>();
                string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");

                // Sorted by path so that two assets matching one family always resolve the same way.
                Array.Sort(guids, StringComparer.Ordinal);

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

                    if (font == null)
                    {
                        continue;
                    }

                    _index.Add(new Entry(font, Normalise(font.name), Normalise(font.faceInfo.familyName)));
                }

                _index.Sort(static (left, right) => string.CompareOrdinal(left.NameKey, right.NameKey));
                return _index;
            }
        }

        private void ReportUnresolved(IReadOnlyList<string> families, SourceLocation source)
        {
            if (_diagnostics == null)
            {
                return;
            }

            string names = string.Join(", ", families);

            if (!_reported.Add(names))
            {
                return;
            }

            _diagnostics.Warning(
                DiagnosticCodes.Asset.NotFound,
                "No TextMeshPro font asset matches font-family '" + names + "', so the default font "
                    + "was used.",
                source,
                "Create a font asset from the font with Window > TextMeshPro > Font Asset Creator, and "
                    + "name it after the family.");
        }

        /// <summary>
        /// Reduces a family or asset name to a comparable key.
        /// </summary>
        /// <remarks>
        /// Case, spaces, hyphens and underscores all vary freely between a CSS family name and the
        /// asset created from it, so they are removed rather than compared.
        /// </remarks>
        private static string Normalise(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value!.Length);

            foreach (char character in value)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }

            return builder.ToString();
        }

        private readonly struct Entry
        {
            internal Entry(TMP_FontAsset font, string nameKey, string familyKey)
            {
                Font = font;
                NameKey = nameKey;
                FamilyKey = familyKey;
            }

            internal TMP_FontAsset Font { get; }

            internal string NameKey { get; }

            internal string FamilyKey { get; }
        }
    }
}
