#nullable enable

using System;
using Rectloom.Core.Assets;

#if RECTLOOM_VECTOR_GRAPHICS
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Unity.VectorGraphics;
using UnityEditor;
using UnityEngine;
#endif

namespace Rectloom.Ugui.Backend
{
    /// <summary>
    /// Renders SVG through Unity's Vector Graphics package.
    /// </summary>
    /// <remarks>
    /// The package is used as a renderer only, and only while compiling. What comes out is a PNG, so the
    /// generated UI is an ordinary <c>Image</c> with an ordinary sprite: nothing from the package is
    /// referenced by the output, a project without it can still open what was generated, and a VRChat
    /// world needs no component that is not already allowed.
    /// <para>
    /// The package is optional. Without it this class still compiles, reports that it is unavailable, and
    /// says what to install, which is what lets a document carrying an SVG degrade to a warning rather
    /// than make the package a requirement for everyone.
    /// </para>
    /// </remarks>
    public sealed class SvgRasterizer : IVectorImageRasterizer
    {
        /// <summary>Name of the package that provides rendering.</summary>
        public const string PackageName = "com.unity.vectorgraphics";

        /// <summary>The shared instance.</summary>
        public static readonly SvgRasterizer Instance = new SvgRasterizer();

        private SvgRasterizer()
        {
        }

        /// <inheritdoc />
        public string UnavailableHint =>
            "Install Unity's Vector Graphics package: Window > Package Manager > + > Add package by name > "
            + PackageName + ". It is only used while compiling; the generated UI does not depend on it.";

#if RECTLOOM_VECTOR_GRAPHICS
        private const string ShaderFolder = "Packages/com.unity.vectorgraphics/Runtime/Shaders/";

        /// <summary>Samples per pixel, which is what smooths the edges of a rendered shape.</summary>
        private const int AntiAliasing = 4;

        /// <summary>Resolution of the texture gradients are baked into.</summary>
        private const uint GradientResolution = 128;

        /// <inheritdoc />
        public bool IsAvailable => true;

        /// <inheritdoc />
        public bool TryRasterize(byte[] svg, int maxWidth, int maxHeight, out byte[] png, out string error)
        {
            png = Array.Empty<byte>();

            if (svg == null || svg.Length == 0)
            {
                error = "the SVG is empty";
                return false;
            }

            Mesh? mesh = null;
            Texture2D? atlas = null;
            Texture2D? texture = null;
            var materials = new List<Material>();

            try
            {
                SVGParser.SceneInfo scene;

                using (var reader = new StringReader(WithExplicitSize(Encoding.UTF8.GetString(svg))))
                {
                    scene = SVGParser.ImportSVG(reader, ViewportOptions.PreserveViewport);
                }

                Rect viewport = scene.SceneViewport;
                bool hasViewport = viewport.width > 0f && viewport.height > 0f;

                // How finely curves are cut depends on how large they will be drawn: a deviation that is
                // invisible at icon size is a visible facet on a logo a metre across.
                float unitsPerPixel = hasViewport
                    ? Mathf.Max(viewport.width / Mathf.Max(1, maxWidth), viewport.height / Mathf.Max(1, maxHeight))
                    : 1f;

                var options = new VectorUtils.TessellationOptions
                {
                    StepDistance = 1000f,
                    MaxCordDeviation = Mathf.Max(0.01f, unitsPerPixel * 0.25f),
                    MaxTanAngleDeviation = 0.1f,
                    SamplingStepSize = 0.01f,
                };

                List<VectorUtils.Geometry> geometry = VectorUtils.TessellateScene(scene.Scene, options);

                if (geometry == null || geometry.Count == 0)
                {
                    error = "it contains nothing the renderer can draw";
                    return false;
                }

                // Gradients and textures are baked into an atlas the shader samples; a document made of
                // flat fills has none.
                VectorUtils.TextureAtlas? baked = VectorUtils.GenerateAtlasAndFillUVs(geometry, GradientResolution);
                atlas = baked?.Texture;

                // Drawn as a mesh rather than through the package's sprite path. Building a vector sprite
                // overrides the sprite's geometry, which Unity only allows while importing an asset or in
                // play mode, and a compile is neither.
                mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                VectorUtils.FillMesh(mesh, geometry, 1f);
                mesh.RecalculateBounds();

                Rect bounds = hasViewport ? viewport : BoundsOf(mesh);

                if (bounds.width <= 0f || bounds.height <= 0f)
                {
                    error = "it has no size";
                    return false;
                }

                // Fitted inside the box at the image's own aspect ratio, so a wide logo in a square box
                // comes out wide rather than stretched.
                float fit = Mathf.Min(maxWidth / bounds.width, maxHeight / bounds.height);
                int width = Mathf.Max(1, Mathf.RoundToInt(bounds.width * fit));
                int height = Mathf.Max(1, Mathf.RoundToInt(bounds.height * fit));

                Material? draw = CreateMaterial(atlas != null ? "VectorGradient.shader" : "Vector.shader", materials);
                Material? demultiply = CreateMaterial("VectorDemultiply.shader", materials);
                Material? expand = CreateMaterial("VectorExpandEdges.shader", materials);
                Material? blend = CreateMaterial("VectorBlendMax.shader", materials);

                if (draw == null || demultiply == null || expand == null || blend == null)
                {
                    error = "the Vector Graphics package's shaders could not be loaded";
                    return false;
                }

                texture = Render(mesh, atlas, bounds, width, height, draw, demultiply, expand, blend);
                png = texture.EncodeToPNG();
                error = string.Empty;
                return png.Length > 0;
            }
            catch (Exception exception)
            {
                // A malformed document surfaces as whatever the parser happened to throw. It is the
                // document's problem either way, and it must not become the compile's.
                error = "it could not be parsed (" + exception.Message + ")";
                return false;
            }
            finally
            {
                Destroy(texture);
                Destroy(mesh);
                Destroy(atlas);

                foreach (Material material in materials)
                {
                    Destroy(material);
                }
            }
        }

        /// <summary>
        /// Draws the mesh into a texture, in three passes.
        /// </summary>
        /// <remarks>
        /// The shapes come out premultiplied, so they are converted back to straight alpha, which is what
        /// a PNG and an <c>Image</c> expect. The result is then laid over a copy of itself with its edges
        /// grown outwards: without that, the transparent pixels around a shape are black, and bilinear
        /// filtering blends that black into the edge as a dark fringe.
        /// </remarks>
        private static Texture2D Render(
            Mesh mesh,
            Texture2D? atlas,
            Rect bounds,
            int width,
            int height,
            Material draw,
            Material demultiply,
            Material expand,
            Material blend)
        {
            RenderTexture previous = RenderTexture.active;

            var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 0)
            {
                msaaSamples = AntiAliasing,
                sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
            };

            RenderTexture source = RenderTexture.GetTemporary(descriptor);
            descriptor.msaaSamples = 1;
            RenderTexture straight = RenderTexture.GetTemporary(descriptor);
            RenderTexture expanded = RenderTexture.GetTemporary(descriptor);

            try
            {
                RenderTexture.active = source;
                GL.Clear(true, true, Color.clear);
                Draw(mesh, atlas, bounds, draw);

                RenderTexture.active = straight;
                GL.Clear(true, true, Color.clear);
                Graphics.Blit(source, straight, demultiply);

                RenderTexture.active = expanded;
                GL.Clear(false, true, Color.clear);
                Graphics.Blit(straight, expanded, expand);
                Graphics.Blit(straight, expanded, blend);

                RenderTexture.active = expanded;

                var copy = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };

                copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                copy.Apply();
                return copy;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(source);
                RenderTexture.ReleaseTemporary(straight);
                RenderTexture.ReleaseTemporary(expanded);
            }
        }

        /// <summary>
        /// Draws the mesh so that <paramref name="bounds"/> fills the target.
        /// </summary>
        /// <remarks>
        /// SVG measures down from the top-left and the target measures up from the bottom-left, so y is
        /// flipped here, once, where the two meet.
        /// </remarks>
        private static void Draw(Mesh mesh, Texture2D? atlas, Rect bounds, Material material)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Color[] colors = mesh.colors;
            var uvs = new List<Vector2>();
            var settings = new List<Vector2>();
            mesh.GetUVs(0, uvs);
            mesh.GetUVs(2, settings);

            material.SetTexture("_MainTex", atlas);
            material.SetPass(0);

            GL.PushMatrix();
            GL.LoadOrtho();
            GL.Color(Color.white);
            GL.Begin(GL.TRIANGLES);

            foreach (int index in triangles)
            {
                if (uvs.Count > index)
                {
                    GL.TexCoord2(uvs[index].x, uvs[index].y);
                }

                if (settings.Count > index)
                {
                    GL.MultiTexCoord2(2, settings[index].x, settings[index].y);
                }

                if (colors.Length > index)
                {
                    GL.Color(colors[index]);
                }

                Vector3 vertex = vertices[index];

                GL.Vertex3(
                    (vertex.x - bounds.xMin) / bounds.width,
                    1f - ((vertex.y - bounds.yMin) / bounds.height),
                    0f);
            }

            GL.End();
            GL.PopMatrix();

            material.SetTexture("_MainTex", null);
        }

        private static readonly Regex RootTag = new Regex(@"<svg\b[^>]*>", RegexOptions.IgnoreCase);

        private static readonly Regex ViewBox = new Regex(
            @"\bviewBox\s*=\s*[""']\s*([-+0-9.eE]+)[\s,]+([-+0-9.eE]+)[\s,]+([-+0-9.eE]+)[\s,]+([-+0-9.eE]+)\s*[""']",
            RegexOptions.IgnoreCase);

        private static readonly Regex SizeAttribute = new Regex(
            @"\s(width|height)\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase);

        /// <summary>
        /// Gives the root element an absolute width and height when it only has a view box.
        /// </summary>
        /// <remarks>
        /// An exported logo usually carries a <c>viewBox</c> and no size, or a size of <c>100%</c>, because
        /// it is meant to fill whatever it is put in. The parser takes the document's frame from its width
        /// and height, so without them it has no frame at all and the image is cropped to its shapes, or
        /// has no size. The view box is the frame the author drew in, so it is written in as the size.
        /// </remarks>
        private static string WithExplicitSize(string svg)
        {
            Match root = RootTag.Match(svg);

            if (!root.Success)
            {
                return svg;
            }

            Match box = ViewBox.Match(root.Value);

            if (!box.Success
                || !TryParse(box.Groups[3].Value, out float width)
                || !TryParse(box.Groups[4].Value, out float height)
                || width <= 0f
                || height <= 0f)
            {
                return svg;
            }

            bool hasWidth = false;
            bool hasHeight = false;

            foreach (Match attribute in SizeAttribute.Matches(root.Value))
            {
                // A percentage is relative to a container this image does not have.
                bool absolute = attribute.Groups[2].Value.IndexOf('%') < 0
                    && attribute.Groups[2].Value.Trim().Length > 0;

                if (string.Equals(attribute.Groups[1].Value, "width", StringComparison.OrdinalIgnoreCase))
                {
                    hasWidth = absolute;
                }
                else
                {
                    hasHeight = absolute;
                }
            }

            if (hasWidth && hasHeight)
            {
                return svg;
            }

            string tag = SizeAttribute.Replace(root.Value, string.Empty);
            string size = " width=\"" + width.ToString("R", CultureInfo.InvariantCulture)
                + "\" height=\"" + height.ToString("R", CultureInfo.InvariantCulture) + "\"";

            tag = tag.Insert("<svg".Length, size);
            return svg.Substring(0, root.Index) + tag + svg.Substring(root.Index + root.Length);
        }

        private static bool TryParse(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static Rect BoundsOf(Mesh mesh)
        {
            Bounds bounds = mesh.bounds;
            return new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        }

        /// <summary>
        /// Loads one of the package's shaders by path.
        /// </summary>
        /// <remarks>
        /// By path rather than by name, because <c>Shader.Find</c> does not see a package's shaders until
        /// something has referenced them, which on a fresh project nothing has.
        /// </remarks>
        private static Material? CreateMaterial(string shaderFile, List<Material> owned)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + shaderFile);

            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            owned.Add(material);
            return material;
        }

        private static void Destroy(UnityEngine.Object? target)
        {
            if (target != null)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
#else
        /// <inheritdoc />
        public bool IsAvailable => false;

        /// <inheritdoc />
        public bool TryRasterize(byte[] svg, int maxWidth, int maxHeight, out byte[] png, out string error)
        {
            png = Array.Empty<byte>();
            error = "the Vector Graphics package is not installed";
            return false;
        }
#endif
    }
}
