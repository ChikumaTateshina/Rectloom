#nullable enable
using System;
using System.Text;
using Rectloom.Core.Metadata;
using TMPro;
using UnityEditor;

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
        {
            if (emoji == null) return content;
            MaterialReferenceManager.AddFontAsset(emoji);
            var output = new StringBuilder(content.Length);
            bool inEmoji = false;
            for (int index = 0; index < content.Length; index++)
            {
                bool selected = IsEmojiAt(content, index);
                if (selected != inEmoji)
                {
                    if (inEmoji) output.Append("</font>");
                    if (selected) output.Append("<font=\"").Append(emoji.name).Append("\">");
                    inEmoji = selected;
                }
                char character = content[index];
                if (character == '<') output.Append("<noparse><</noparse>");
                else output.Append(character);
                if (char.IsHighSurrogate(character) && index + 1 < content.Length
                    && char.IsLowSurrogate(content[index + 1])) output.Append(content[++index]);
            }
            if (inEmoji) output.Append("</font>");
            return output.ToString();
        }

        // TMP resolves font tags via Resources in a player. Registering only in the Editor would
        // look correct there but lose the emoji font after building a world.
        internal static TMP_FontAsset? EnsureResource(TMP_FontAsset? source, string generatedFolder)
        {
            if (source == null) return null;
            string path = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(path)) return source;
            string resourceFolder = TMP_Settings.instance == null ? "Fonts & Materials/"
                : TMP_Settings.defaultFontAssetPath;
            resourceFolder = resourceFolder.Trim('/');
            string resourceSuffix = "/Resources/" + (resourceFolder.Length == 0 ? "" : resourceFolder + "/")
                + source.name + ".asset";
            if (path.EndsWith(resourceSuffix, StringComparison.Ordinal)) return source;
            string name = "RectloomEmoji_" + AssetDatabase.AssetPathToGUID(path);
            string folder = generatedFolder.TrimEnd('/') + "/Resources"
                + (resourceFolder.Length == 0 ? "" : "/" + resourceFolder);
            string destination = folder + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(destination);
            if (existing != null) return existing;
            MetadataStore.EnsureFolder(folder);
            if (!AssetDatabase.CopyAsset(path, destination))
                throw new InvalidOperationException("Cannot make the emoji font available to a player: " + path);
            var copy = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(destination);
            copy.name = name;
            copy.ReadFontAssetDefinition();
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssets();
            return copy;
        }
    }
}
