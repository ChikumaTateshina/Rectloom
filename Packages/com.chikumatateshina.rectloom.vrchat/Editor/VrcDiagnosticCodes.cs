#nullable enable

namespace Rectloom.VRChat
{
    /// <summary>
    /// Diagnostic codes for the VRChat adapter.
    /// </summary>
    /// <remarks>
    /// The core reserves the <c>VRC</c> prefix but defines none of these codes itself, because the
    /// core must not know anything about VRChat. They live here instead, which keeps the one-way
    /// dependency intact while still giving the numbers a single home.
    /// </remarks>
    public static class VrcDiagnosticCodes
    {
        /// <summary>A canvas setup that will not work the way the author expects in a world.</summary>
        public const string UnsupportedConfiguration = "VRC1001";

        /// <summary>The installed VRChat SDK is outside the range this adapter was tested against.</summary>
        public const string SdkVersionUntested = "VRC1002";

        /// <summary>A <c>vrc-</c> property value could not be read.</summary>
        public const string InvalidPropertyValue = "VRC1003";

        /// <summary>A <c>vrc-</c> property is not one this adapter knows.</summary>
        public const string UnknownProperty = "VRC1004";

        /// <summary>The VRChat SDK is not installed, so SDK-specific work was skipped.</summary>
        public const string SdkNotInstalled = "VRC1005";
    }
}
