#nullable enable
using System;
using System.Text;
using System.Collections.Generic;
using Rectloom.Core.Diagnostics;
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
            => FormatColor(content, emoji, null);

        internal static string FormatColor(string content, TMP_FontAsset? emoji,
            IReadOnlyDictionary<uint, TMP_SpriteAsset>? sprites)
        {
            if (emoji == null && (sprites == null || sprites.Count == 0)) return content;
            if (emoji != null) MaterialReferenceManager.AddFontAsset(emoji);
            var output = new StringBuilder(content.Length);
            bool inEmoji = false;
            for (int index = 0; index < content.Length; index++)
            {
                uint code = char.IsHighSurrogate(content[index]) && index + 1 < content.Length && char.IsLowSurrogate(content[index + 1])
                    ? (uint)char.ConvertToUtf32(content, index) : content[index];
                if (sprites != null && sprites.TryGetValue(code, out TMP_SpriteAsset sprite))
                {
                    if (inEmoji) { output.Append("</font>"); inEmoji = false; }
                    MaterialReferenceManager.AddSpriteAsset(sprite);
                    output.Append("<sprite=\"").Append(sprite.name).Append("\" index=0 tint=0 color=#FFFFFFFF>");
                    if (code > 0xFFFF) index++;
                    if (index + 1 < content.Length && content[index + 1] == '\uFE0F') index++;
                    continue;
                }
                bool selected = emoji != null && IsEmojiAt(content, index);
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
        //
        // Nothing here is allowed to throw. An emoji font that cannot be relocated costs the emoji in a
        // build; it must not cost the compile, which would take the whole document down with it,
        // images and all.
        internal static TMP_FontAsset? EnsureResource(
            TMP_FontAsset? source,
            string generatedFolder,
            IDiagnosticSink? diagnostics = null)
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

            // A freshly created asset may not have a GUID yet, and an empty one would give every font
            // the same name.
            string guid = AssetDatabase.AssetPathToGUID(path);
            string name = "RectloomEmoji_" + (string.IsNullOrEmpty(guid) ? source.name : guid);
            string folder = generatedFolder.TrimEnd('/') + "/Resources"
                + (resourceFolder.Length == 0 ? "" : "/" + resourceFolder);
            string destination = folder + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(destination);
            if (existing != null) return existing;

            TMP_FontAsset? copy = null;

            try
            {
                MetadataStore.EnsureFolder(folder);

                if (AssetDatabase.CopyAsset(path, destination))
                {
                    copy = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(destination);
                }
            }
            catch (Exception exception)
            {
                ReportNotRelocated(diagnostics, path, exception.Message);
                return source;
            }

            if (copy == null)
            {
                ReportNotRelocated(diagnostics, path, "it could not be copied to '" + destination + "'");
                return source;
            }

            copy.name = name;
            copy.ReadFontAssetDefinition();
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssets();
            return copy;
        }

        /// <summary>
        /// Reports an emoji font that stays where it is.
        /// </summary>
        /// <remarks>
        /// The font still renders in the Editor, because the font tag resolves against the assets loaded
        /// in the session. A built player resolves it through Resources instead, so the warning is about
        /// the build rather than about what is on screen now.
        /// </remarks>
        private static void ReportNotRelocated(IDiagnosticSink? diagnostics, string path, string reason)
        {
            diagnostics?.Warning(
                DiagnosticCodes.Asset.NotFound,
                "The emoji font at '" + path + "' was not copied into a Resources folder because "
                    + reason + ". Emoji render in the Editor but will be missing from a build.",
                SourceLocation.None,
                "Move the font asset into a Resources folder yourself, or set the Emoji TMP Font to one "
                    + "that already lives in one.");
        }
    }
}
