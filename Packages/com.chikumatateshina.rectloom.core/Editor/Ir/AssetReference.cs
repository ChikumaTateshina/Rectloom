#nullable enable

using System;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Ir
{
    /// <summary>
    /// How an asset reference identifies its asset.
    /// </summary>
    public enum AssetReferenceKind
    {
        /// <summary>No asset is referenced.</summary>
        None = 0,

        /// <summary>A project asset path such as <c>Assets/UI/icon.png</c>.</summary>
        AssetPath = 1,

        /// <summary>A Unity asset GUID.</summary>
        Guid = 2,
    }

    /// <summary>
    /// A reference to a project asset, not yet resolved to an object.
    /// </summary>
    /// <remarks>
    /// The IR stays free of Unity object references so that it can be built, compared and written to
    /// metadata without the asset database. Resolving happens in the backend, and a reference that
    /// cannot be resolved becomes a diagnostic rather than a null object.
    /// <para>
    /// A relative path written in HTML or CSS is already resolved against the file that wrote it, so
    /// what is stored here is always a project path.
    /// </para>
    /// </remarks>
    public readonly struct AssetReference : IEquatable<AssetReference>
    {
        /// <summary>A reference to nothing.</summary>
        public static readonly AssetReference None = default;

        private AssetReference(AssetReferenceKind kind, string? value, SourceLocation source)
        {
            Kind = kind;
            Value = value;
            Source = source;
        }

        /// <summary>How this reference identifies its asset.</summary>
        public AssetReferenceKind Kind { get; }

        /// <summary>
        /// The asset path or GUID, or <see langword="null"/> when <see cref="Kind"/> is
        /// <see cref="AssetReferenceKind.None"/>.
        /// </summary>
        public string? Value { get; }

        /// <summary>Position in the source file the reference was written at.</summary>
        public SourceLocation Source { get; }

        /// <summary>Gets a value indicating whether this reference names an asset.</summary>
        public bool HasValue => Kind != AssetReferenceKind.None && !string.IsNullOrEmpty(Value);

        /// <summary>
        /// Creates a reference by project asset path.
        /// </summary>
        /// <param name="assetPath">Project path of the asset.</param>
        /// <param name="source">Where the reference was written.</param>
        /// <returns>The created reference, or <see cref="None"/> when the path is empty.</returns>
        public static AssetReference FromPath(string? assetPath, SourceLocation source)
        {
            return string.IsNullOrWhiteSpace(assetPath)
                ? None
                : new AssetReference(AssetReferenceKind.AssetPath, assetPath!.Trim(), source);
        }

        /// <summary>
        /// Creates a reference by asset GUID.
        /// </summary>
        /// <param name="guid">Unity asset GUID, 32 hexadecimal characters.</param>
        /// <param name="source">Where the reference was written.</param>
        /// <returns>The created reference, or <see cref="None"/> when the GUID is empty.</returns>
        public static AssetReference FromGuid(string? guid, SourceLocation source)
        {
            return string.IsNullOrWhiteSpace(guid)
                ? None
                : new AssetReference(AssetReferenceKind.Guid, guid!.Trim(), source);
        }

        /// <summary>
        /// Gets a value indicating whether a reference string looks like an asset GUID.
        /// </summary>
        /// <param name="value">The reference as written.</param>
        /// <returns><see langword="true"/> for 32 hexadecimal characters.</returns>
        /// <remarks>
        /// GUIDs and paths share one attribute, so they are told apart by shape. A 32 character hex
        /// string is not a plausible file name, which makes the test safe in practice.
        /// </remarks>
        public static bool LooksLikeGuid(string? value)
        {
            if (value == null || value.Length != 32)
            {
                return false;
            }

            foreach (char character in value)
            {
                bool isHex = (character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f')
                    || (character >= 'A' && character <= 'F');

                if (!isHex)
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public bool Equals(AssetReference other)
        {
            return Kind == other.Kind && string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is AssetReference other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ (Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value));
            }
        }

        /// <summary>Compares two references for equality.</summary>
        public static bool operator ==(AssetReference left, AssetReference right) => left.Equals(right);

        /// <summary>Compares two references for inequality.</summary>
        public static bool operator !=(AssetReference left, AssetReference right) => !left.Equals(right);

        /// <inheritdoc />
        public override string ToString()
        {
            return HasValue ? Kind + ":" + Value : "none";
        }
    }
}
