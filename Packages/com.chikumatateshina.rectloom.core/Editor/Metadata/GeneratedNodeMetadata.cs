#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Ir;
using UnityEngine;

namespace Rectloom.Core.Metadata
{
    /// <summary>
    /// What the compiler generated for one IR node, and therefore what it owns.
    /// </summary>
    /// <remarks>
    /// An update compile reads this to answer two questions: which object belongs to which node,
    /// and which parts of that object the compiler may overwrite. Anything not listed here is
    /// user-owned and must survive.
    /// </remarks>
    [Serializable]
    public sealed class GeneratedNodeMetadata
    {
        [SerializeField] private string _stableId = string.Empty;
        [SerializeField] private string _transformPath = string.Empty;
        [SerializeField] private string _sourcePath = string.Empty;
        [SerializeField] private string _sourceTag = string.Empty;
        [SerializeField] private UiNodeKind _kind;
        [SerializeField] private string _globalObjectId = string.Empty;
        [SerializeField] private List<string> _managedComponentTypes = new List<string>();
        [SerializeField] private List<string> _managedProperties = new List<string>();

        /// <summary>
        /// Identity of the IR node this object was generated for.
        /// </summary>
        public string StableId
        {
            get => _stableId;
            set => _stableId = value ?? string.Empty;
        }

        /// <summary>
        /// Path of object names from the generated root to this object, for example
        /// <c>panel/apply/Label</c>, or an empty string for the root itself.
        /// </summary>
        /// <remarks>
        /// This is how an update finds the existing object. A path works identically for a prefab
        /// loaded into a temporary scene and for an object already in one, which object ids do not.
        /// An object name is compiler-owned, so renaming one is not user data that has to survive.
        /// </remarks>
        public string TransformPath
        {
            get => _transformPath;
            set => _transformPath = value ?? string.Empty;
        }

        /// <summary>
        /// Source location this node came from, as <c>file(line,column)</c>.
        /// </summary>
        /// <remarks>
        /// Recorded for the developer reading metadata to work out why an update matched what it
        /// did. The compiler does not match on it.
        /// </remarks>
        public string SourcePath
        {
            get => _sourcePath;
            set => _sourcePath = value ?? string.Empty;
        }

        /// <summary>Tag name of the element, or an empty string for a generated object.</summary>
        public string SourceTag
        {
            get => _sourceTag;
            set => _sourceTag = value ?? string.Empty;
        }

        /// <summary>Kind of UI object this node compiled to.</summary>
        public UiNodeKind Kind
        {
            get => _kind;
            set => _kind = value;
        }

        /// <summary>
        /// Unity global object id of the generated object, when one could be captured.
        /// </summary>
        /// <remarks>
        /// Used to find the root of scene output, which has no asset path to look up. Child objects
        /// are found by <see cref="TransformPath"/> instead.
        /// </remarks>
        public string GlobalObjectId
        {
            get => _globalObjectId;
            set => _globalObjectId = value ?? string.Empty;
        }

        /// <summary>
        /// Full names of the component types the compiler added to this object.
        /// </summary>
        /// <remarks>
        /// A component on the object that is not in this list was added by someone else and is
        /// never removed or rewritten.
        /// </remarks>
        public List<string> ManagedComponentTypes => _managedComponentTypes;

        /// <summary>
        /// Serialized property paths the compiler writes, as <c>Type.property</c>.
        /// </summary>
        /// <remarks>
        /// Version 1.0 updates by re-applying the same values rather than diffing properties, so
        /// this list is recorded for the developer and for a later version that does diff. It is
        /// what makes the metadata say, in writing, that <c>Button.m_OnClick</c> is not ours.
        /// </remarks>
        public List<string> ManagedProperties => _managedProperties;

        /// <summary>
        /// Gets a value indicating whether a component type is one the compiler added.
        /// </summary>
        /// <param name="typeFullName">Full name of the component type.</param>
        /// <returns><see langword="true"/> when the compiler added it.</returns>
        public bool Manages(string typeFullName)
        {
            foreach (string managed in _managedComponentTypes)
            {
                if (string.Equals(managed, typeFullName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return _stableId + " -> " + (_transformPath.Length == 0 ? "<root>" : _transformPath);
        }
    }
}
