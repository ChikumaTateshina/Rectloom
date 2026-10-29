#nullable enable
using System;
using System.Text;
using System.Collections.Generic;
using TMPro;

namespace Rectloom.Ugui.Backend
{
    // Emits only controlled TMP tags. Authored '<' characters remain literal, never markup.
    internal static class EmojiText
    {
        internal static bool IsEmojiAt(string text, int index)
        {
            uint code = char.IsHighSurrogate(text[index]) && index + 1 < text.Length
                && char.IsLowSurrogate(text[index + 1]) ? (uint)char.ConvertToUtf32(text, index) : text[index];
            return (code >= 0x1F000 && code <= 0x1FAFF) || (code >= 0x2600 && code <= 0x27BF)
                || code == 0xFE0F || code == 0x200D || code == 0x20E3
                || (index + 1 < text.Length && (text[index + 1] == '\uFE0F' || text[index + 1] == '\u20E3'));
        }

        // Resolving an emoji font costs a project-wide search, and generating one copies a font into
        // the project. Neither is worth doing for a document with no emoji in it, and the diagnostics
        // that explain a missing emoji font would otherwise appear on every compile.
        internal static bool ContainsEmoji(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            for (int index = 0; index < text!.Length; index++)
            {
                if (IsEmojiAt(text, index))
                {
                    return true;
                }
            }

            return false;
        }

        internal static string Format(string content, TMP_FontAsset? emoji)
            => FormatColor(content, emoji, null);

        /// <summary>
        /// Writes text for TextMeshPro, with each emoji that has a sprite replaced by a sprite tag.
        /// </summary>
        /// <remarks>
        /// No font tag is ever written. TextMeshPro resolves <c>&lt;font="name"&gt;</c> through Resources,
        /// which a VRChat world bundle does not expose, so the tag itself was drawn as text in the world
        /// while the Editor showed the emoji correctly. Sprites are referenced directly by the text
        /// component and survive the bundle. A character with no sprite is written as it is, and the text
        /// font and its fallbacks draw it, or show a missing glyph; neither is ever markup on screen.
        /// </remarks>
        internal static string FormatColor(string content, TMP_FontAsset? emoji,
            IReadOnlyDictionary<uint, TMP_SpriteAsset>? sprites)
        {
            if (sprites == null || sprites.Count == 0) return content;
            var output = new StringBuilder(content.Length);
            for (int index = 0; index < content.Length; index++)
            {
                uint code = char.IsHighSurrogate(content[index]) && index + 1 < content.Length && char.IsLowSurrogate(content[index + 1])
                    ? (uint)char.ConvertToUtf32(content, index) : content[index];
                if (sprites.TryGetValue(code, out TMP_SpriteAsset sprite))
                {
                    // A monochrome glyph takes the text colour, as the font outline would have; a colour
                    // glyph keeps its own palette.
                    output.Append("<sprite name=\"").Append(code.ToString("X"))
                        .Append(ColorEmojiSprites.IsMonochrome(sprite, code) ? "\" tint=1>" : "\" tint=0 color=#FFFFFFFF>");
                    if (code > 0xFFFF) index++;
                    if (index + 1 < content.Length && content[index + 1] == '️') index++;
                    continue;
                }
                // Joiners and selectors only combine glyphs, and sprites never combine.
                if (code == 0x200D || code == 0xFE0F || code == 0x20E3) continue;
                char character = content[index];
                if (character == '<') output.Append("<noparse><</noparse>");
                else output.Append(character);
                if (char.IsHighSurrogate(character) && index + 1 < content.Length
                    && char.IsLowSurrogate(content[index + 1])) output.Append(content[++index]);
            }
            return output.ToString();
        }
    }
}
