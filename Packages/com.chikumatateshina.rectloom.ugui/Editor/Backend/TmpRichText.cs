#nullable enable

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Rectloom.Core.Ir;
using TMPro;
using UnityEngine;

namespace Rectloom.Ugui.Backend
{
    /// <summary>
    /// Writes styled runs of text as TextMeshPro rich text.
    /// </summary>
    /// <remarks>
    /// Only tags this class writes ever reach the text component: what the author typed is escaped, so
    /// a literal <c>&lt;b&gt;</c> in the source stays literal. Each run is written relative to the
    /// component's own style, so a run in that style gets no tags at all.
    /// <para>
    /// A run in another font family is written in the component's font. A font tag is resolved through
    /// Resources, which a VRChat world bundle cannot read, and the tag would be drawn as text there.
    /// </para>
    /// </remarks>
    internal static class TmpRichText
    {
        /// <summary>Prefix of the link ids written for runs that are links, followed by their index.</summary>
        internal const string LinkIdPrefix = "rectloom-link-";

        /// <summary>
        /// Builds the text a component should display.
        /// </summary>
        /// <param name="content">Plain text, used when there are no runs.</param>
        /// <param name="spans">The runs, or null.</param>
        /// <param name="style">The component's own style.</param>
        /// <param name="emojiSprites">Emoji sprites, or null.</param>
        /// <param name="links">Receives the target of each link run, in the order of their link ids.</param>
        /// <returns>The text, and whether it contains markup.</returns>
        internal static string Build(
            string content,
            IReadOnlyList<UiTextSpan>? spans,
            UiTextStyle style,
            IReadOnlyDictionary<uint, TMP_SpriteAsset>? emojiSprites,
            List<string>? links,
            out bool rich)
        {
            bool sprites = emojiSprites != null && emojiSprites.Count > 0;

            if (spans == null || spans.Count == 0)
            {
                rich = sprites;
                return EmojiText.FormatColor(content, null, emojiSprites);
            }

            rich = true;
            var output = new StringBuilder(content.Length * 2);

            foreach (UiTextSpan span in spans)
            {
                var close = new Stack<string>();

                if (span.Href != null)
                {
                    int index = links?.Count ?? 0;
                    links?.Add(span.Href);
                    Open(output, close, "<link=\"" + LinkIdPrefix + index.ToString(CultureInfo.InvariantCulture) + "\">", "</link>");
                }

                UiTextStyle own = span.Style;

                if (own.Bold && !style.Bold)
                {
                    Open(output, close, "<b>", "</b>");
                }

                if (own.Italic && !style.Italic)
                {
                    Open(output, close, "<i>", "</i>");
                }

                if (own.Underline && !style.Underline)
                {
                    Open(output, close, "<u>", "</u>");
                }

                if (own.LineThrough && !style.LineThrough)
                {
                    Open(output, close, "<s>", "</s>");
                }

                if (own.Color != style.Color)
                {
                    Open(output, close, "<color=#" + ColorUtility.ToHtmlStringRGBA(own.Color) + ">", "</color>");
                }

                if (!Mathf.Approximately(own.FontSize, style.FontSize) && own.FontSize > 0f)
                {
                    Open(output, close, "<size=" + own.FontSize.ToString("0.##", CultureInfo.InvariantCulture) + ">", "</size>");
                }

                output.Append(sprites ? EmojiText.FormatColor(span.Text, null, emojiSprites) : Escape(span.Text));

                while (close.Count > 0)
                {
                    output.Append(close.Pop());
                }
            }

            return output.ToString();
        }

        private static void Open(StringBuilder output, Stack<string> close, string open, string end)
        {
            output.Append(open);
            close.Push(end);
        }

        private static string Escape(string text)
        {
            return text.IndexOf('<') < 0 ? text : text.Replace("<", "<noparse><</noparse>");
        }
    }
}
