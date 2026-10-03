#nullable enable

using Rectloom.Core.Ir;
using UnityEngine;

namespace Rectloom.Ugui.Backend
{
    /// <summary>
    /// Writes solved rectangles onto <see cref="RectTransform"/>s.
    /// </summary>
    /// <remarks>
    /// This is the only place the layout engine's coordinate system is converted. Layout works
    /// top-left with y growing down; Unity anchors from the bottom-left with y growing up. Every
    /// generated object is anchored to its parent's top-left corner so that a baked position is a
    /// direct translation of the layout result, with the y sign flipped.
    /// <para>
    /// Anchors are deliberately collapsed to a point rather than stretched. In bake mode the
    /// compiler owns the exact rectangle, and a stretched anchor would make the result depend on how
    /// the parent is later resized.
    /// </para>
    /// </remarks>
    public static class RectTransformBaker
    {
        /// <summary>The top-left corner, as an anchor and pivot.</summary>
        public static readonly Vector2 TopLeft = new Vector2(0f, 1f);

        /// <summary>
        /// Bakes a node's rectangle onto a transform.
        /// </summary>
        /// <param name="transform">Transform to write to.</param>
        /// <param name="rect">The node's solved rectangle.</param>
        /// <param name="parentContentOffset">
        /// Top-left of the parent's content box, relative to the parent's own corner. A child
        /// position is relative to that content box, so this is what moves it inside the parent's
        /// padding.
        /// </param>
        public static void Bake(RectTransform transform, UiRect rect, Vector2 parentContentOffset)
        {
            transform.anchorMin = TopLeft;
            transform.anchorMax = TopLeft;
            transform.pivot = TopLeft;
            transform.sizeDelta = new Vector2(rect.Width, rect.Height);
            transform.anchoredPosition = new Vector2(
                parentContentOffset.x + rect.X,
                -(parentContentOffset.y + rect.Y));
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Bakes the root rectangle, which has no parent box to sit inside.
        /// </summary>
        /// <param name="transform">Transform to write to.</param>
        /// <param name="size">Size of the root, in logical pixels.</param>
        public static void BakeRoot(RectTransform transform, Vector2 size)
        {
            transform.anchorMin = TopLeft;
            transform.anchorMax = TopLeft;
            transform.pivot = TopLeft;
            transform.sizeDelta = size;
            transform.anchoredPosition = Vector2.zero;
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Stretches a transform to fill its parent's content box.
        /// </summary>
        /// <param name="transform">Transform to write to.</param>
        /// <param name="parent">Rectangle of the parent node.</param>
        /// <remarks>
        /// Used for the label a button or a painted box generates. A stretched label keeps the
        /// parent's padding if someone later resizes the parent by hand, which is the one place
        /// where following the parent is more useful than a fixed rectangle.
        /// </remarks>
        public static void StretchToContent(RectTransform transform, UiRect parent)
        {
            float left = parent.ContentX;
            float top = parent.ContentY;
            float right = parent.Width - parent.ContentX - parent.ContentWidth;
            float bottom = parent.Height - parent.ContentY - parent.ContentHeight;

            transform.anchorMin = Vector2.zero;
            transform.anchorMax = Vector2.one;
            transform.pivot = new Vector2(0.5f, 0.5f);
            transform.offsetMin = new Vector2(left, bottom);
            transform.offsetMax = new Vector2(-right, -top);
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
        }
    }
}
