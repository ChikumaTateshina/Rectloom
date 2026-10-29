#nullable enable

using System.Collections.Generic;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Ir;
using UnityEngine;
using UnityEngine.UI;

namespace Rectloom.Ugui.Backend
{
    /// <summary>
    /// <c>:hover</c>, <c>:active</c>, <c>:focus</c> and <c>:disabled</c> backgrounds, as a selectable's
    /// colour tint.
    /// </summary>
    public sealed partial class UguiBackend
    {
        private static readonly Color DisabledDim = new Color(0.78f, 0.78f, 0.78f, 0.5f);

        private sealed partial class Pass
        {
            /// <summary>
            /// Gives a control the background each state asks for.
            /// </summary>
            /// <remarks>
            /// uGUI tints one graphic by state. When the box is a flat or rounded fill, the graphic is
            /// painted white and each state's tint is the colour itself, so the colours are exact. A box
            /// with a border has both colours painted into one sprite, which a tint changes together, so
            /// there the tint is the ratio of the state colour to the normal one: exact for the fill, and
            /// unable to brighten it.
            /// </remarks>
            private void ApplyStateColors(UiNode node, Selectable selectable, Image? image)
            {
                IReadOnlyDictionary<UiInteractionState, Color> states = node.Visual.StateBackgrounds;

                if (states.Count == 0 || image == null)
                {
                    return;
                }

                Color normal = node.Visual.BackgroundColor ?? Color.clear;
                Color hover = states.TryGetValue(UiInteractionState.Highlighted, out Color h) ? h : normal;
                Color pressed = states.TryGetValue(UiInteractionState.Pressed, out Color p) ? p : hover;
                Color selected = states.TryGetValue(UiInteractionState.Selected, out Color f) ? f : normal;
                Color disabled = states.TryGetValue(UiInteractionState.Disabled, out Color d) ? d : normal * DisabledDim;

                ColorBlock block = ColorBlock.defaultColorBlock;
                block.colorMultiplier = 1f;
                block.fadeDuration = 0.1f;

                if (!node.Visual.HasBorder)
                {
                    float radius = node.Visual.BorderRadius;
                    Sprite? shape = radius >= 0.5f ? _backend._sprites.GetSprite(radius, 0f, Color.white, Color.white) : null;
                    image.sprite = shape;
                    image.type = shape != null ? Image.Type.Sliced : Image.Type.Simple;
                    image.color = Color.white;

                    block.normalColor = normal;
                    block.highlightedColor = hover;
                    block.pressedColor = pressed;
                    block.selectedColor = selected;
                    block.disabledColor = disabled;
                }
                else
                {
                    bool clamped = false;
                    block.normalColor = Color.white;
                    block.highlightedColor = Ratio(hover, normal, ref clamped);
                    block.pressedColor = Ratio(pressed, normal, ref clamped);
                    block.selectedColor = Ratio(selected, normal, ref clamped);
                    block.disabledColor = Ratio(disabled, normal, ref clamped);

                    if (clamped)
                    {
                        _backend._diagnostics.Warning(
                            DiagnosticCodes.Css.InvalidValue,
                            "A state's background is lighter than the normal one, which a box with a border "
                                + "cannot show: its fill and border are tinted together, and a tint only darkens.",
                            node.Source,
                            "Make the state colours darker than the normal background, or remove the border.");
                    }
                }

                selectable.transition = Selectable.Transition.ColorTint;
                selectable.targetGraphic = image;
                selectable.colors = block;
            }

            private static Color Ratio(Color state, Color normal, ref bool clamped)
            {
                float Channel(float s, float n, ref bool c)
                {
                    if (n <= 0.0001f)
                    {
                        c |= s > 0.0001f;
                        return 1f;
                    }

                    float value = s / n;
                    c |= value > 1.0001f;
                    return Mathf.Min(1f, value);
                }

                return new Color(
                    Channel(state.r, normal.r, ref clamped),
                    Channel(state.g, normal.g, ref clamped),
                    Channel(state.b, normal.b, ref clamped),
                    Channel(state.a, normal.a, ref clamped));
            }
        }
    }
}
