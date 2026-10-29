#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Ir;
using Rectloom.Core.Metadata;
using UnityEngine;
using UnityEngine.UI;

namespace Rectloom.Ugui.Backend
{
    /// <summary>
    /// <c>overflow: scroll</c> and <c>overflow: auto</c>, as a ScrollRect.
    /// </summary>
    /// <remarks>
    /// The box itself is the viewport: it already clips with a RectMask2D. Its children move into a
    /// content object as large as everything they cover, and the ScrollRect drags that object. A
    /// ScrollRect is driven by the pointer alone, so it works in a VRChat world without any script.
    /// </remarks>
    public sealed partial class UguiBackend
    {
        /// <summary>Name of the object a scrolling box keeps its children in.</summary>
        public const string ScrollContentName = "Content";

        private static readonly string[] ScrollRectProperties =
        {
            "m_Content", "m_Horizontal", "m_Vertical", "m_MovementType", "m_Viewport", "m_ScrollSensitivity",
        };

        private sealed partial class Pass
        {
            /// <summary>
            /// Creates the content object a scrolling box's children go into, sized to cover them.
            /// </summary>
            private GameObject ApplyScrollContent(
                UiNode node,
                GameObject target,
                string transformPath,
                HashSet<string> siblingNames,
                List<string> expected)
            {
                GameObject content = ApplyPart(node, target, transformPath, "/@scroll-content", ScrollContentName,
                    siblingNames, expected, out GeneratedNodeMetadata entry);

                // Anchored where the box's own top-left is, so the children bake exactly as they would
                // directly under the box.
                RectTransform rect = GetOrAdd<RectTransform>(content, entry, RectTransformProperties);
                rect.anchorMin = rect.anchorMax = rect.pivot = RectTransformBaker.TopLeft;
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Extent(node);
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;

                // The scroll rect was added with the box's own components, before this object existed.
                ScrollRect? scroll = target.GetComponent<ScrollRect>();

                if (scroll != null)
                {
                    scroll.content = rect;
                }

                FinishPart(content, entry);
                return content;
            }

            /// <summary>
            /// Adds the ScrollRect, and the clear graphic it needs to be dragged.
            /// </summary>
            private void ApplyScrollRect(UiNode node, GameObject target, GeneratedNodeMetadata entry)
            {
                if (!node.Visual.Scrolls || node.Children.Count == 0)
                {
                    return;
                }

                Vector2 extent = Extent(node);

                // A drag has to land on something, and the gaps between children are nothing.
                if (target.GetComponent<Graphic>() == null)
                {
                    ApplyPaint(node, target, entry, forceGraphic: true);
                }

                ScrollRect scroll = GetOrAdd<ScrollRect>(target, entry, ScrollRectProperties);
                scroll.viewport = (RectTransform)target.transform;
                scroll.horizontal = node.Visual.ScrollsX && extent.x > node.Rect.Width + 0.5f;
                scroll.vertical = node.Visual.ScrollsY && extent.y > node.Rect.Height + 0.5f;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.inertia = true;
                scroll.scrollSensitivity = 30f;
            }

            /// <summary>
            /// The size of everything inside a box, measured from its top-left corner, and never less
            /// than the box itself.
            /// </summary>
            private static Vector2 Extent(UiNode node)
            {
                UiRect rect = node.Rect;
                float right = rect.Width;
                float bottom = rect.Height;
                float paddingRight = rect.Width - rect.ContentX - rect.ContentWidth;
                float paddingBottom = rect.Height - rect.ContentY - rect.ContentHeight;

                foreach (UiNode child in node.Children)
                {
                    right = Mathf.Max(right, rect.ContentX + child.Rect.X + child.Rect.Width + paddingRight);
                    bottom = Mathf.Max(bottom, rect.ContentY + child.Rect.Y + child.Rect.Height + paddingBottom);
                }

                return new Vector2(right, bottom);
            }
        }
    }
}
