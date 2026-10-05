#nullable enable

using NUnit.Framework;
using Rectloom.Core.Assets;
using UnityEditor;
using UnityEngine;

namespace Rectloom.Core.Tests.Assets
{
    /// <summary>
    /// Covers writing an embedded image into the project, which is the one step that needs the asset
    /// database and so cannot be covered by the decoding tests.
    /// </summary>
    /// <remarks>
    /// Worth asserting for real rather than through a double: the reason an embedded image has to
    /// become a file at all is that a prefab can only reference an imported asset, and whether the
    /// written file imports as a <see cref="Sprite"/> depends on importer settings that no test double
    /// would exercise.
    /// </remarks>
    public sealed class ProjectEmbeddedImageStoreTests
    {
        private const string Folder = "Assets/RectloomEmbeddedImageTest";

        private const string PngUri =
            "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z"
            + "8DwHwAFAAH/iZk9HQAAAABJRU5ErkJggg==";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        private static DataUriPayload Decode()
        {
            Assert.That(DataUri.TryDecode(PngUri, out DataUriPayload payload, out string error), Is.True, error);
            return payload;
        }

        [Test]
        public void TryStore_WritesASpriteUnderTheGeneratedFolder()
        {
            DataUriPayload payload = Decode();

            Assert.That(
                ProjectEmbeddedImageStore.Instance.TryStore(payload, Folder, out string path, out string error),
                Is.True,
                error);

            Assert.That(path, Is.EqualTo(Folder + "/" + ProjectEmbeddedImageStore.SubFolder + "/" + payload.ContentName));
            Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(path), Is.Not.Null, "it imported as a sprite");
        }

        [Test]
        public void TryStore_ReusesTheAssetWhenTheSameImageIsStoredTwice()
        {
            DataUriPayload payload = Decode();

            ProjectEmbeddedImageStore.Instance.TryStore(payload, Folder, out string first, out _);
            Object asset = AssetDatabase.LoadMainAssetAtPath(first);

            ProjectEmbeddedImageStore.Instance.TryStore(payload, Folder, out string second, out _);

            Assert.That(second, Is.EqualTo(first), "content addressed, so one file");
            Assert.That(AssetDatabase.LoadMainAssetAtPath(second), Is.SameAs(asset), "it was not rewritten");
        }
    }
}
