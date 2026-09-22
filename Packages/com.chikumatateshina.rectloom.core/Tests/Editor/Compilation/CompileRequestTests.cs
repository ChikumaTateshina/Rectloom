#nullable enable

using NUnit.Framework;
using Rectloom.Core.Compilation;
using UnityEngine;

namespace Rectloom.Core.Tests.Compilation
{
    public sealed class CompileRequestTests
    {
        [Test]
        public void Defaults_MatchNormalUsage()
        {
            var request = new CompileRequest();

            Assert.That(request.CompileMode, Is.EqualTo(CompileMode.Update));
            Assert.That(request.LayoutMode, Is.EqualTo(LayoutMode.Bake));
            Assert.That(request.OutputType, Is.EqualTo(CompileOutputType.Prefab));
        }

        [Test]
        public void CssAssetPaths_DefaultsToEmptyAndNeverBecomesNull()
        {
            var request = new CompileRequest();
            Assert.That(request.CssAssetPaths, Is.Empty);

            request.CssAssetPaths = new[] { "Assets/UI/theme.css" };
            Assert.That(request.CssAssetPaths, Is.EqualTo(new[] { "Assets/UI/theme.css" }));

            request.CssAssetPaths = null!;
            Assert.That(request.CssAssetPaths, Is.Not.Null);
            Assert.That(request.CssAssetPaths, Is.Empty);
        }

        [Test]
        public void Options_DefaultsToDefaultOptionsAndNeverBecomesNull()
        {
            var request = new CompileRequest();
            Assert.That(request.Options, Is.Not.Null);
            Assert.That(request.Options.ReferenceResolution,
                Is.EqualTo(CompilerOptions.DefaultReferenceResolution));

            request.Options = null!;
            Assert.That(request.Options, Is.Not.Null);
        }
    }

    public sealed class CompilerOptionsTests
    {
        [Test]
        public void Defaults_PreserveUserDataAndUseFullHdReference()
        {
            var options = new CompilerOptions();

            Assert.That(options.ReferenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(options.PreserveModifiedGeneratedObjects, Is.True);
            Assert.That(options.UseDefaultStyleSheet, Is.True);
            Assert.That(options.AllowRawImageFallback, Is.True);
            Assert.That(options.StrictMode, Is.False);
        }

        [Test]
        public void Clone_CopiesValuesAndIsIndependent()
        {
            var original = new CompilerOptions
            {
                ReferenceResolution = new Vector2(1280f, 720f),
                StrictMode = true,
                UseDefaultStyleSheet = false,
                PreserveModifiedGeneratedObjects = false,
                AllowRawImageFallback = false,
            };

            CompilerOptions copy = original.Clone();
            original.StrictMode = false;
            original.ReferenceResolution = new Vector2(1f, 1f);

            Assert.That(copy.ReferenceResolution, Is.EqualTo(new Vector2(1280f, 720f)));
            Assert.That(copy.StrictMode, Is.True);
            Assert.That(copy.UseDefaultStyleSheet, Is.False);
            Assert.That(copy.PreserveModifiedGeneratedObjects, Is.False);
            Assert.That(copy.AllowRawImageFallback, Is.False);
        }
    }
}
