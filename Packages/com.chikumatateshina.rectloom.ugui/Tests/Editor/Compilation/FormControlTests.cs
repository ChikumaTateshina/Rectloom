#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rectloom.Core.Compilation;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Layout;
using Rectloom.Ugui.Compilation;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rectloom.Ugui.Tests.Compilation
{
    /// <summary>
    /// <c>&lt;input type="checkbox"&gt;</c> and <c>&lt;input type="range"&gt;</c> become a working Toggle
    /// and Slider, and an update keeps what the user wired to them.
    /// </summary>
    public sealed class FormControlTests
    {
        private const string Folder = "Assets/RectloomFormControlTest";
        private const string HtmlPath = Folder + "/page.html";
        private const string PrefabPath = Folder + "/page.prefab";

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Directory.CreateDirectory(Folder);
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        private static void Write(string body, string css = "")
        {
            File.WriteAllText(
                HtmlPath,
                "<html><head><style>" + css + "</style></head><body>" + body + "</body></html>",
                new UTF8Encoding(false));

            AssetDatabase.ImportAsset(HtmlPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static CompileResult Compile(CompileMode mode = CompileMode.Create)
        {
            return new UguiHtmlUiCompiler(measurerFactory: () => new FixedMeasurer()).Compile(
                new CompileRequest
                {
                    HtmlAssetPath = HtmlPath,
                    CssAssetPaths = Array.Empty<string>(),
                    OutputType = CompileOutputType.Prefab,
                    OutputPath = PrefabPath,
                    CompileMode = mode,
                    Options = new CompilerOptions { GeneratedAssetFolder = Folder + "/Generated" },
                });
        }

        private static string Describe(CompileResult result)
        {
            return string.Join("\n", result.Diagnostics.Select(d => d.ToString()));
        }

        private static T Find<T>(string name)
            where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, "no prefab was written");

            Transform? found = prefab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            Assert.That(found, Is.Not.Null, "no object named '" + name + "'");

            T? component = found!.GetComponent<T>();
            Assert.That(component, Is.Not.Null, name + " has no " + typeof(T).Name);
            return component!;
        }

        private static void AssertClean(CompileResult result)
        {
            Assert.That(result.Success, Is.True, Describe(result));
            Assert.That(
                result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning),
                Is.Empty,
                Describe(result));
        }

        [Test]
        public void ACheckbox_BecomesAToggleWithACheckmark()
        {
            Write("<input id=\"sound\" type=\"checkbox\" checked>");

            AssertClean(Compile());

            Toggle toggle = Find<Toggle>("sound");
            Assert.That(toggle.isOn, Is.True, "checked");
            Assert.That(toggle.graphic, Is.Not.Null);
            Assert.That(toggle.graphic.name, Is.EqualTo("Checkmark"));
            Assert.That(toggle.targetGraphic, Is.SameAs(toggle.GetComponent<Image>()));
            Assert.That(toggle.interactable, Is.True);

            var rect = (RectTransform)toggle.transform;
            Assert.That(rect.rect.width, Is.EqualTo(20f).Within(0.01f), "the built-in size");
        }

        [Test]
        public void AnUncheckedCheckbox_IsOff()
        {
            Write("<input id=\"sound\" type=\"checkbox\">");

            AssertClean(Compile());

            Assert.That(Find<Toggle>("sound").isOn, Is.False);
        }

        [Test]
        public void AccentColour_PaintsTheCheckmark()
        {
            Write("<input id=\"sound\" type=\"checkbox\" checked>", "body { accent-color: #ff0000; }");

            AssertClean(Compile());

            // The box's 3px corners are inside the mark's inset, so the mark is a flat square.
            Image mark = Find<Image>("Checkmark");
            Assert.That(mark.sprite, Is.Null);
            Assert.That(mark.color.r, Is.GreaterThan(0.9f));
            Assert.That(mark.color.g, Is.LessThan(0.1f));
        }

        [Test]
        public void ARange_BecomesASliderWithItsValues()
        {
            Write("<input id=\"volume\" type=\"range\" min=\"0\" max=\"10\" value=\"7\">");

            AssertClean(Compile());

            Slider slider = Find<Slider>("volume");
            Assert.That(slider.minValue, Is.EqualTo(0f));
            Assert.That(slider.maxValue, Is.EqualTo(10f));
            Assert.That(slider.value, Is.EqualTo(7f));
            Assert.That(slider.wholeNumbers, Is.True);
            Assert.That(slider.fillRect, Is.Not.Null);
            Assert.That(slider.handleRect, Is.Not.Null);
            Assert.That(slider.direction, Is.EqualTo(Slider.Direction.LeftToRight));

            var rect = (RectTransform)slider.transform;
            Assert.That(rect.rect.width, Is.EqualTo(160f).Within(0.01f), "input[type=range] in the built-in stylesheet");
        }

        [Test]
        public void ARangeWithoutAValue_StartsHalfway()
        {
            Write("<input id=\"volume\" type=\"range\">");

            AssertClean(Compile());

            Assert.That(Find<Slider>("volume").value, Is.EqualTo(50f));
        }

        [Test]
        public void AFractionalStep_MovesContinuously()
        {
            Write("<input id=\"volume\" type=\"range\" min=\"0\" max=\"1\" step=\"0.1\" value=\"0.25\">");

            AssertClean(Compile());

            Slider slider = Find<Slider>("volume");
            Assert.That(slider.wholeNumbers, Is.False);
            Assert.That(slider.value, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void Disabled_TurnsInteractionOff()
        {
            Write("<button id=\"go\" disabled>Go</button>"
                + "<input id=\"sound\" type=\"checkbox\" disabled>"
                + "<input id=\"volume\" type=\"range\" disabled>");

            AssertClean(Compile());

            Assert.That(Find<Button>("go").interactable, Is.False);
            Assert.That(Find<Toggle>("sound").interactable, Is.False);
            Assert.That(Find<Slider>("volume").interactable, Is.False);
        }

        [Test]
        public void AnUnsupportedInputType_IsReported()
        {
            Write("<input id=\"name\" type=\"date\">");

            CompileResult result = Compile();

            Assert.That(result.Success, Is.True, Describe(result));
            Assert.That(
                result.Diagnostics.Any(d => d.Code == DiagnosticCodes.Html.UnknownElement
                    && d.Message.Contains("type=\"date\"")),
                Is.True,
                Describe(result));
        }

        [Test]
        public void AnUpdate_KeepsListenersWiredByHand_AndAppliesTheNewSource()
        {
            Write("<input id=\"sound\" type=\"checkbox\"><input id=\"volume\" type=\"range\" value=\"10\">");
            AssertClean(Compile());

            // Wire both controls the way an author would in the Inspector.
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                Transform sound = contents.GetComponentsInChildren<Transform>(true).First(t => t.name == "sound");
                Transform volume = contents.GetComponentsInChildren<Transform>(true).First(t => t.name == "volume");

                UnityEventTools.AddStringPersistentListener(
                    sound.GetComponent<Toggle>().onValueChanged, sound.gameObject.SendMessage, "OnSound");
                UnityEventTools.AddStringPersistentListener(
                    volume.GetComponent<Slider>().onValueChanged, volume.gameObject.SendMessage, "OnVolume");

                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            Write("<input id=\"sound\" type=\"checkbox\" checked><input id=\"volume\" type=\"range\" value=\"80\">");
            AssertClean(Compile(CompileMode.Update));

            Toggle toggle = Find<Toggle>("sound");
            Slider slider = Find<Slider>("volume");

            Assert.That(toggle.isOn, Is.True, "the new source");
            Assert.That(slider.value, Is.EqualTo(80f), "the new source");
            Assert.That(toggle.onValueChanged.GetPersistentEventCount(), Is.EqualTo(1), "the user's wiring");
            Assert.That(slider.onValueChanged.GetPersistentEventCount(), Is.EqualTo(1), "the user's wiring");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(
                prefab.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Checkmark"),
                Is.EqualTo(1),
                "the checkmark was reused, not duplicated");
            Assert.That(
                prefab.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Handle"),
                Is.EqualTo(1),
                "the handle was reused, not duplicated");
        }

        [Test]
        public void RadioButtonsWithOneName_ShareAToggleGroup()
        {
            Write("<input id=\"small\" type=\"radio\" name=\"size\">"
                + "<input id=\"large\" type=\"radio\" name=\"size\" checked>"
                + "<input id=\"other\" type=\"radio\" name=\"colour\">");

            AssertClean(Compile());

            Toggle small = Find<Toggle>("small");
            Toggle large = Find<Toggle>("large");
            Toggle other = Find<Toggle>("other");

            Assert.That(small.group, Is.Not.Null);
            Assert.That(large.group, Is.SameAs(small.group));
            Assert.That(other.group, Is.Not.SameAs(small.group));
            Assert.That(large.isOn, Is.True);
            Assert.That(small.isOn, Is.False);
            Assert.That(small.group.allowSwitchOff, Is.False, "one is chosen, so one stays chosen");
            Assert.That(other.group.allowSwitchOff, Is.True, "nothing is chosen yet, as HTML allows");

            var rect = (RectTransform)small.transform;
            Assert.That(rect.rect.width, Is.EqualTo(20f).Within(0.01f));
        }

        [Test]
        public void ATextInput_BecomesAnInputFieldWithItsAttributes()
        {
            Write("<input id=\"name\" type=\"text\" value=\"Alice\" placeholder=\"Your name\" maxlength=\"12\">"
                + "<input id=\"secret\" type=\"password\">"
                + "<input id=\"count\" type=\"number\" readonly>");

            AssertClean(Compile());

            TMP_InputField name = Find<TMP_InputField>("name");
            Assert.That(name.text, Is.EqualTo("Alice"));
            Assert.That(name.characterLimit, Is.EqualTo(12));
            Assert.That(name.lineType, Is.EqualTo(TMP_InputField.LineType.SingleLine));
            Assert.That(name.textComponent, Is.Not.Null);
            Assert.That(name.textViewport, Is.Not.Null);
            Assert.That(name.placeholder, Is.Not.Null);
            Assert.That(((TMP_Text)name.placeholder).text, Is.EqualTo("Your name"));
            Assert.That(name.readOnly, Is.False);

            Assert.That(Find<TMP_InputField>("secret").contentType, Is.EqualTo(TMP_InputField.ContentType.Password));
            Assert.That(Find<TMP_InputField>("count").contentType, Is.EqualTo(TMP_InputField.ContentType.DecimalNumber));
            Assert.That(Find<TMP_InputField>("count").readOnly, Is.True);

            var rect = (RectTransform)name.transform;
            Assert.That(rect.rect.width, Is.EqualTo(180f).Within(0.01f), "the built-in size");
        }

        [Test]
        public void ATextarea_IsAMultiLineFieldStartingWithItsContent()
        {
            Write("<textarea id=\"note\">first line\nsecond</textarea>");

            AssertClean(Compile());

            TMP_InputField note = Find<TMP_InputField>("note");
            Assert.That(note.lineType, Is.EqualTo(TMP_InputField.LineType.MultiLineNewline));
            Assert.That(note.text, Does.StartWith("first line"));
            Assert.That(note.GetComponentsInChildren<TMP_Text>(true).Count(t => t.name == "Label"), Is.Zero,
                "the content is the field's value, not a label beside it");
        }

        [Test]
        public void AnUpdate_KeepsOneTextArea()
        {
            Write("<input id=\"name\" type=\"text\" value=\"a\">");
            AssertClean(Compile());
            Write("<input id=\"name\" type=\"text\" value=\"b\">");
            AssertClean(Compile(CompileMode.Update));

            TMP_InputField name = Find<TMP_InputField>("name");
            Assert.That(name.text, Is.EqualTo("b"));
            Assert.That(name.transform.Cast<Transform>().Count(t => t.name == "Text Area"), Is.EqualTo(1));
        }

        private sealed class FixedMeasurer : ITextMeasurer
        {
            public TextMeasurement Measure(string text, Core.Css.Computed.TextStyle style, float availableWidth)
            {
                return string.IsNullOrEmpty(text)
                    ? TextMeasurement.Empty
                    : new TextMeasurement(text.Length * style.FontSize * 0.5f, style.FontSize, 1);
            }
        }
    }
}
