#nullable enable

using UnityEngine;

namespace Rectloom.Ugui.Tests.Extensions
{
    /// <summary>
    /// A component with one field of each supported kind, so the binder can be exercised without
    /// depending on the shape of a real Unity component.
    /// </summary>
    /// <remarks>
    /// This lives in a runtime assembly rather than beside the tests that use it, because Unity
    /// refuses to attach a MonoBehaviour from an Editor-only assembly. That is the same limit the
    /// generic binder works under: it can only ever attach a component that could exist at runtime.
    /// </remarks>
    public sealed class BinderProbe : MonoBehaviour
    {
        /// <summary>Alignment of the probe, used to test enum binding.</summary>
        public enum Mode
        {
            /// <summary>The initial value.</summary>
            Idle = 0,

            /// <summary>A second value.</summary>
            Primary = 1,

            /// <summary>A third value.</summary>
            Secondary = 2,
        }

        [SerializeField] private bool _flag;
        [SerializeField] private int _count;
        [SerializeField] private float _speed;
        [SerializeField] private string _label = string.Empty;
        [SerializeField] private Mode _mode = Mode.Idle;
        [SerializeField] private Vector2 _offset;
        [SerializeField] private Vector3 _position;
        [SerializeField] private Vector4 _channels;
        [SerializeField] private Color _tint = Color.black;
        [SerializeField] private Rect _area;
        [SerializeField] private Texture? _texture;

        /// <summary>A boolean field.</summary>
        public bool Flag => _flag;

        /// <summary>An integer field.</summary>
        public int Count => _count;

        /// <summary>A floating point field.</summary>
        public float Speed => _speed;

        /// <summary>A string field.</summary>
        public string Label => _label;

        /// <summary>An enum field.</summary>
        public Mode SelectedMode => _mode;

        /// <summary>A two component vector field.</summary>
        public Vector2 Offset => _offset;

        /// <summary>A three component vector field.</summary>
        public Vector3 Position => _position;

        /// <summary>A four component vector field.</summary>
        public Vector4 Channels => _channels;

        /// <summary>A colour field.</summary>
        public Color Tint => _tint;

        /// <summary>A rectangle field.</summary>
        public Rect Area => _area;

        /// <summary>An object reference field.</summary>
        public Texture? Texture => _texture;
    }
}
