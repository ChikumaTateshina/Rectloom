#nullable enable

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Rectloom.Core.Assets
{
    /// <summary>
    /// The decoded contents of a <c>data:</c> URI.
    /// </summary>
    public readonly struct DataUriPayload
    {
        /// <summary>
        /// Creates a payload.
        /// </summary>
        /// <param name="mediaType">Lower-cased media type, for example <c>image/png</c>.</param>
        /// <param name="bytes">The decoded bytes.</param>
        /// <param name="fileExtension">File extension to store the bytes under, including the dot.</param>
        public DataUriPayload(string mediaType, byte[] bytes, string fileExtension)
        {
            MediaType = mediaType;
            Bytes = bytes;
            FileExtension = fileExtension;
        }

        /// <summary>Lower-cased media type, for example <c>image/png</c>.</summary>
        public string MediaType { get; }

        /// <summary>The decoded bytes.</summary>
        public byte[] Bytes { get; }

        /// <summary>File extension to store the bytes under, including the leading dot.</summary>
        public string FileExtension { get; }

        /// <summary>
        /// Gets a name that identifies these bytes, derived from their content.
        /// </summary>
        /// <remarks>
        /// Content addressed rather than counted, so that the same image embedded twice becomes one
        /// asset and recompiling the same document produces the same file name. Deterministic output is
        /// a hard requirement, and a counter would break it as soon as two elements were reordered.
        /// </remarks>
        public string ContentName => DataUri.ContentHash(Bytes) + FileExtension;
    }

    /// <summary>
    /// Reads images embedded in markup as <c>data:</c> URIs.
    /// </summary>
    /// <remarks>
    /// An embedded image has no file for Unity to reference, and a prefab cannot point at bytes that
    /// exist only in memory, so the bytes have to become a project asset. Decoding is separated from
    /// writing the asset so that the decoding rules can be tested without a project.
    /// <para>
    /// Only base64 payloads are read. A percent-encoded <c>data:</c> URI is legal CSS but is not a
    /// practical way to carry an image, and accepting it would mean guessing at a text encoding.
    /// </para>
    /// </remarks>
    public static class DataUri
    {
        /// <summary>The scheme that introduces a data URI.</summary>
        public const string Scheme = "data:";

        /// <summary>Marker that says the payload is base64 encoded.</summary>
        public const string Base64Marker = ";base64,";

        private static readonly Dictionary<string, string> ImageExtensions =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "image/png", ".png" },
                { "image/jpeg", ".jpg" },
                { "image/jpg", ".jpg" },
                { "image/gif", ".gif" },
                { "image/bmp", ".bmp" },
                { "image/x-tga", ".tga" },
                { "image/tga", ".tga" },
            };

        /// <summary>
        /// Gets a value indicating whether a reference is a data URI.
        /// </summary>
        /// <param name="value">The reference as written in markup or CSS.</param>
        /// <returns><see langword="true"/> when it starts with <c>data:</c>.</returns>
        public static bool IsDataUri(string? value)
        {
            return value != null
                && value.TrimStart().StartsWith(Scheme, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Decodes a data URI.
        /// </summary>
        /// <param name="value">The URI as written.</param>
        /// <param name="payload">The decoded payload when decoding succeeds.</param>
        /// <param name="error">
        /// Why decoding failed, phrased for a diagnostic message, when it failed.
        /// </param>
        /// <returns><see langword="true"/> when the URI carries an image this compiler can store.</returns>
        public static bool TryDecode(string? value, out DataUriPayload payload, out string error)
        {
            payload = default;

            if (!IsDataUri(value))
            {
                error = "the value is not a data: URI";
                return false;
            }

            string uri = value!.Trim();
            int comma = uri.IndexOf(',');

            if (comma < 0)
            {
                error = "it has no comma separating the media type from the data";
                return false;
            }

            string header = uri.Substring(Scheme.Length, comma - Scheme.Length);
            bool isBase64 = header.EndsWith("base64", StringComparison.OrdinalIgnoreCase)
                && header.IndexOf(';') >= 0;

            if (!isBase64)
            {
                error = "only base64 data: URIs are supported";
                return false;
            }

            string mediaType = header.Substring(0, header.IndexOf(';')).Trim().ToLowerInvariant();

            if (mediaType.Length == 0)
            {
                // A data URI with no media type defaults to text/plain, which is never an image.
                error = "it does not declare an image media type";
                return false;
            }

            if (!ImageExtensions.TryGetValue(mediaType, out string extension))
            {
                // SVG is singled out because it is what this is nearly always about: design tools embed
                // logos and icons as SVG, and Unity has no importer for it without an extra package.
                error = string.Equals(mediaType, "image/svg+xml", StringComparison.Ordinal)
                    ? "it is an SVG, which Unity cannot import as an image"
                    : "'" + mediaType + "' is not an image format Unity can import";

                return false;
            }

            byte[] bytes;

            try
            {
                bytes = Convert.FromBase64String(StripWhitespace(uri.Substring(comma + 1)));
            }
            catch (FormatException)
            {
                error = "the base64 data is malformed";
                return false;
            }

            if (bytes.Length == 0)
            {
                error = "the data is empty";
                return false;
            }

            payload = new DataUriPayload(mediaType, bytes, extension);
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Computes the short content hash used to name a stored payload.
        /// </summary>
        /// <param name="bytes">The decoded bytes.</param>
        /// <returns>Sixteen lower-case hexadecimal characters.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is null.</exception>
        public static string ContentHash(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder(16);

                for (int index = 0; index < 8; index++)
                {
                    builder.Append(hash[index].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        /// <summary>
        /// Removes the whitespace a long base64 payload may be wrapped with.
        /// </summary>
        private static string StripWhitespace(string value)
        {
            var builder = new StringBuilder(value.Length);

            foreach (char character in value)
            {
                if (!char.IsWhiteSpace(character))
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }
    }
}
