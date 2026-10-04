using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    [TestFixture]
    public class SettingsPanelTests
    {
        private GameObject _root;
        private SettingsPanel _panel;
        private GameObject _existingBackdrop;
        private Button _closeButton;
        private Slider _masterSlider;
        private Slider _bgmSlider;
        private Slider _sfxSlider;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("SettingsPanel_TestRoot", typeof(RectTransform));
            _panel = _root.AddComponent<SettingsPanel>();

            _existingBackdrop = CreateExistingBackdrop(_root.transform);
            _closeButton = CreateCloseButton(_root.transform);
            _masterSlider = CreateSlider("MasterSlider", _root.transform, out Image masterFill, out Image masterHandle);
            _bgmSlider = CreateSlider("BGMSlider", _root.transform, out Image bgmFill, out Image bgmHandle);
            _sfxSlider = CreateSlider("SFXSlider", _root.transform, out Image sfxFill, out Image sfxHandle);

            // Simulate the bug state: controls exist but are invisible.
            masterFill.color = new Color(1f, 1f, 1f, 0f);
            masterHandle.color = new Color(1f, 1f, 1f, 0f);
            bgmFill.color = new Color(1f, 1f, 1f, 0f);
            bgmHandle.color = new Color(1f, 1f, 1f, 0f);
            sfxFill.color = new Color(1f, 1f, 1f, 0f);
            sfxHandle.color = new Color(1f, 1f, 1f, 0f);

            SetPrivateField(_panel, "_masterSlider", _masterSlider);
            SetPrivateField(_panel, "_bgmSlider", _bgmSlider);
            SetPrivateField(_panel, "_sfxSlider", _sfxSlider);
            SetPrivateField(_panel, "_closeButton", _closeButton);
            SetPrivateField(_panel, "_masterLabel", CreateLabel("MasterLabel"));
            SetPrivateField(_panel, "_bgmLabel", CreateLabel("BGMLabel"));
            SetPrivateField(_panel, "_sfxLabel", CreateLabel("SFXLabel"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void OnEnable_NormalizesInvisibleSliderVisuals()
        {
            InvokePrivateMethod(_panel, "OnEnable");

            Assert.Greater(GetFillImage(_masterSlider).color.a, 0.1f);
            Assert.Greater(GetHandleImage(_masterSlider).color.a, 0.1f);
            Assert.Greater(GetFillImage(_bgmSlider).color.a, 0.1f);
            Assert.Greater(GetHandleImage(_bgmSlider).color.a, 0.1f);
            Assert.Greater(GetFillImage(_sfxSlider).color.a, 0.1f);
            Assert.Greater(GetHandleImage(_sfxSlider).color.a, 0.1f);
        }

        [Test]
        public void OnEnable_ReplacesLowContrastSliderVisuals()
        {
            SetSliderGraphicColors(_masterSlider, Color.black);
            SetSliderGraphicColors(_bgmSlider, Color.black);
            SetSliderGraphicColors(_sfxSlider, Color.black);

            InvokePrivateMethod(_panel, "OnEnable");

            Assert.Greater(RelativeLuminance(GetBackgroundImage(_masterSlider).color), 0.15f);
            Assert.Greater(RelativeLuminance(GetFillImage(_masterSlider).color), 0.15f);
            Assert.Greater(RelativeLuminance(GetHandleImage(_masterSlider).color), 0.6f);
            Assert.Greater(RelativeLuminance(GetBackgroundImage(_bgmSlider).color), 0.15f);
            Assert.Greater(RelativeLuminance(GetFillImage(_bgmSlider).color), 0.15f);
            Assert.Greater(RelativeLuminance(GetHandleImage(_bgmSlider).color), 0.6f);
            Assert.Greater(RelativeLuminance(GetBackgroundImage(_sfxSlider).color), 0.15f);
            Assert.Greater(RelativeLuminance(GetFillImage(_sfxSlider).color), 0.15f);
            Assert.Greater(RelativeLuminance(GetHandleImage(_sfxSlider).color), 0.6f);
        }

        [Test]
        public void OnEnable_WithoutAudioManager_DisablesSliders()
        {
            InvokePrivateMethod(_panel, "OnEnable");
            Assert.IsFalse(_masterSlider.interactable);
            Assert.IsFalse(_bgmSlider.interactable);
            Assert.IsFalse(_sfxSlider.interactable);

            InvokePrivateMethod(_panel, "OnDisable");
            Assert.IsFalse(_masterSlider.interactable);
            Assert.IsFalse(_bgmSlider.interactable);
            Assert.IsFalse(_sfxSlider.interactable);
        }

        [Test]
        public void OnEnable_ReusesExistingBackdropBehindSliderCard()
        {
            InvokePrivateMethod(_panel, "OnEnable");

            Transform modalBackdrop = _root.transform.Find("ModalBackdrop");
            Assert.IsNull(modalBackdrop, "Existing Background should be reused instead of creating a second full-screen backdrop.");
            Assert.AreEqual(0, _existingBackdrop.transform.GetSiblingIndex(), "Backdrop must stay behind the settings card so it cannot block slider input.");

            Transform settingsCard = _root.transform.Find("SettingsCard");
            Assert.IsNotNull(settingsCard);
            Assert.Greater(settingsCard.GetSiblingIndex(), _existingBackdrop.transform.GetSiblingIndex());
            Assert.Greater(GetTopLevelSiblingIndex(_masterSlider.transform, _root.transform), _existingBackdrop.transform.GetSiblingIndex());
            Assert.Greater(GetTopLevelSiblingIndex(_bgmSlider.transform, _root.transform), _existingBackdrop.transform.GetSiblingIndex());
            Assert.Greater(GetTopLevelSiblingIndex(_sfxSlider.transform, _root.transform), _existingBackdrop.transform.GetSiblingIndex());
        }

        [Test]
        public void OnEnable_StylesCloseButtonWithOriginalMenuSkin()
        {
            InvokePrivateMethod(_panel, "OnEnable");

            RectTransform rect = _closeButton.GetComponent<RectTransform>();
            Image image = _closeButton.GetComponent<Image>();
            Text label = _closeButton.GetComponentInChildren<Text>(true);

            Assert.GreaterOrEqual(rect.sizeDelta.x, 280f);
            Assert.GreaterOrEqual(rect.sizeDelta.y, 88f);
            Assert.AreEqual(Color.white, image.color);
            Assert.AreEqual(new Color(0.7019608f, 0.5019608f, 0.07450981f, 1f), label.color);
            Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(UITextScale.Title));
            Assert.That(_closeButton.transition, Is.EqualTo(Selectable.Transition.ColorTint));
            Assert.That(image.sprite.name, Is.EqualTo("ui_button_generic_0"));
            Assert.That(label.GetComponent<Shadow>().enabled, Is.True);
        }

        [Test]
        public void OnEnable_SlidersDoNotReuseButtonArtwork()
        {
            Texture2D texture = new(4, 4);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            try
            {
                _closeButton.GetComponent<Image>().sprite = sprite;
                GetHandleImage(_masterSlider).sprite = sprite;
                InvokePrivateMethod(_panel, "OnEnable");
                Assert.That(GetHandleImage(_masterSlider).sprite, Is.Not.SameAs(sprite));
                Assert.That(GetFillImage(_masterSlider).sprite, Is.Not.SameAs(sprite));
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void OnEnable_ShowsMutedValueAndPronunciationHelpInsideParchment()
        {
            _sfxSlider.value = 0f;
            InvokePrivateMethod(_panel, "OnEnable");
            Transform card = _root.transform.Find("SettingsCard/SettingsScroll");
            Assert.That(card.Find("SFXSliderValue").GetComponent<TMP_Text>().text, Is.EqualTo("Naka-mute"));
            Assert.That(card.Find("SFXSliderHint").GetComponent<TMP_Text>().text,
                Does.Contain("pagbigkas ng mga pantig"));
            foreach (Slider slider in new[] { _masterSlider, _bgmSlider, _sfxSlider })
            {
                RectTransform rect = slider.GetComponent<RectTransform>();
                Assert.That(rect.anchorMin.x, Is.GreaterThanOrEqualTo(ScrollPanelArt.FullSafeArea.xMin));
                // Rect computes xMax by adding width, which can differ from the authored anchor by one float step.
                Assert.That(rect.anchorMax.x, Is.LessThanOrEqualTo(ScrollPanelArt.FullSafeArea.xMax + 0.0001f));
                Assert.That(rect.sizeDelta.y, Is.GreaterThanOrEqualTo(96f));
                Assert.That(slider.GetComponent<Image>().raycastTarget, Is.True);
            }
        }

        [Test]
        public void ChangingSliderValue_KeepsThumbHeightFixed()
        {
            InvokePrivateMethod(_panel, "OnEnable");
            _masterSlider.SetValueWithoutNotify(0.1f);
            float height = _masterSlider.handleRect.rect.height;
            _masterSlider.SetValueWithoutNotify(0.9f);
            Assert.That(_masterSlider.handleRect.rect.height, Is.EqualTo(height).Within(0.01f));
            Assert.That(height, Is.EqualTo(64f).Within(0.01f));
        }

        [Test]
        public void OnEnable_WithAudioManager_EnablesAndSynchronizesSliders()
        {
            GameObject audioObject = new("SettingsAudio_Test", typeof(AudioManager));
            AudioManager audio = audioObject.GetComponent<AudioManager>();
            PropertyInfo instance = typeof(Singleton<AudioManager>).GetProperty("Instance");
            object previous = instance.GetValue(null);
            try
            {
                instance.GetSetMethod(true).Invoke(null, new object[] { audio });
                SetPrivateField(audio, "_masterVolume", 0.3f);
                SetPrivateField(audio, "_bgmVolume", 0.7f);
                SetPrivateField(audio, "_sfxVolume", 0.8f);
                InvokePrivateMethod(_panel, "OnEnable");
                Assert.That(_masterSlider.interactable && _bgmSlider.interactable && _sfxSlider.interactable, Is.True);
                Assert.That(_masterSlider.value, Is.EqualTo(0.3f).Within(0.001f));
                Assert.That(_bgmSlider.value, Is.EqualTo(0.7f).Within(0.001f));
                Assert.That(_sfxSlider.value, Is.EqualTo(0.8f).Within(0.001f));
                InvokePrivateMethod(_panel, "OnDisable");
                Assert.That(_masterSlider.interactable || _bgmSlider.interactable || _sfxSlider.interactable, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(audioObject);
                instance.GetSetMethod(true).Invoke(null, new[] { previous });
            }
        }

        [Test]
        public void OnEnable_CoversDisplayAndKeepsControlsInSafeArea()
        {
            _root.AddComponent<SafeAreaHandler>();
            InvokePrivateMethod(_panel, "OnEnable");
            RectTransform page = _root.GetComponent<RectTransform>();
            Assert.That(page.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(page.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(_root.GetComponent<SafeAreaHandler>().enabled, Is.False);
            Assert.That(_existingBackdrop.GetComponent<Image>().sprite, Is.Null,
                "The full-screen backdrop must not render the scroll artwork.");
            Assert.That(_existingBackdrop.GetComponent<Image>().color.a, Is.GreaterThanOrEqualTo(0.9f));
            RectTransform art = _existingBackdrop.GetComponent<RectTransform>();
            Assert.That(art.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(art.anchorMax, Is.EqualTo(Vector2.one));
            Transform content = _root.transform.Find("SettingsCard");
            Assert.That(content.GetComponent<SafeAreaHandler>(), Is.Not.Null);
            Assert.That(_closeButton.transform.parent, Is.SameAs(content));
            RectTransform scroll = content.Find("SettingsScroll").GetComponent<RectTransform>();
            Assert.That(scroll.anchorMin.y, Is.GreaterThanOrEqualTo(0.15f));
            Assert.That(scroll.anchorMax.y, Is.LessThanOrEqualTo(0.85f));
            Assert.That(scroll.GetComponent<Image>().raycastTarget, Is.False);
        }

        [Test]
        public void OnEnable_UsesReadableSettingsTypography()
        {
            InvokePrivateMethod(_panel, "OnEnable");
            Transform content = _root.transform.Find("SettingsCard/SettingsScroll");
            Assert.That(content.Find("Title").GetComponent<TMP_Text>().fontSizeMax,
                Is.EqualTo(UITextScale.Display));
            Assert.That(content.Find("MasterLabel").GetComponent<TMP_Text>().fontSizeMax,
                Is.EqualTo(UITextScale.Title));
            Assert.That(content.Find("SFXSliderHint").GetComponent<TMP_Text>().fontSizeMin,
                Is.GreaterThanOrEqualTo(UITextScale.Body));
            Assert.That(content.Find("AudioStatus").GetComponent<TMP_Text>().text,
                Is.EqualTo("Awtomatikong nase-save ang mga pagbabago"), "Edit-mode preview has no audio manager.");
        }

        [TestCase(360f, 640f)]
        [TestCase(390f, 844f)]
        [TestCase(430f, 932f)]
        [TestCase(768f, 1024f)]
        public void SettingsLayout_KeepsVisibleControlsSeparatedAcrossPortraitSizes(float width, float height)
        {
            GameObject viewport = new("SettingsViewport_Test", typeof(RectTransform));
            try
            {
                // Match this project's CanvasScaler (1080x1920, match = 0.5).
                float scale = Mathf.Sqrt((width / 1080f) * (height / 1920f));
                viewport.GetComponent<RectTransform>().sizeDelta = new Vector2(width / scale, height / scale);
                _root.transform.SetParent(viewport.transform, false);
                InvokePrivateMethod(_panel, "OnEnable");
                RectTransform scroll = _root.transform.Find("SettingsCard/SettingsScroll") as RectTransform;
                Vector3[] thumb = new Vector3[4];
                Vector3[] label = new Vector3[4];
                _masterSlider.handleRect.GetWorldCorners(thumb);
                scroll.Find("BGMLabel").GetComponent<RectTransform>().GetWorldCorners(label);
                Assert.That(thumb[0].y, Is.GreaterThan(label[1].y), "Master thumb must clear the Music label.");
                _bgmSlider.handleRect.GetWorldCorners(thumb);
                scroll.Find("SFXLabel").GetComponent<RectTransform>().GetWorldCorners(label);
                Assert.That(thumb[0].y, Is.GreaterThan(label[1].y), "Music thumb must clear the Sound effects label.");
                _sfxSlider.handleRect.GetWorldCorners(thumb);
                scroll.Find("AudioStatus").GetComponent<RectTransform>().GetWorldCorners(label);
                Assert.That(thumb[0].y, Is.GreaterThan(label[1].y), "SFX thumb must clear the save message.");
            }
            finally
            {
                _root.transform.SetParent(null, false);
                Object.DestroyImmediate(viewport);
            }
        }

        [Test]
        public void Reopening_DoesNotDuplicateAudioRows()
        {
            InvokePrivateMethod(_panel, "OnEnable");
            int childCount = _root.transform.Find("SettingsCard/SettingsScroll").childCount;
            InvokePrivateMethod(_panel, "OnDisable");
            InvokePrivateMethod(_panel, "OnEnable");
            Assert.That(_root.transform.Find("SettingsCard/SettingsScroll").childCount, Is.EqualTo(childCount));
        }

        private TMP_Text CreateLabel(string name)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(_root.transform, false);
            return go.GetComponent<TMP_Text>();
        }

        private static GameObject CreateExistingBackdrop(Transform parent)
        {
            GameObject backdrop = new("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backdrop.transform.SetParent(parent, false);
            RectTransform rect = backdrop.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            backdrop.GetComponent<Image>().raycastTarget = true;
            return backdrop;
        }

        private static Button CreateCloseButton(Transform parent)
        {
            GameObject buttonGo = new("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(parent, false);
            buttonGo.GetComponent<RectTransform>().sizeDelta = new Vector2(150f, 44f);

            GameObject labelGo = new("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(buttonGo.transform, false);
            Text label = labelGo.GetComponent<Text>();
            label.text = "Back";

            return buttonGo.GetComponent<Button>();
        }

        private static Slider CreateSlider(string name, Transform parent, out Image fillImage, out Image handleImage)
        {
            GameObject sliderGo = new(name);
            sliderGo.transform.SetParent(parent);
            Slider slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.5f;

            GameObject bgGo = new("Background");
            bgGo.transform.SetParent(sliderGo.transform);
            bgGo.AddComponent<RectTransform>();
            Image bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0f);

            GameObject fillAreaGo = new("Fill Area");
            fillAreaGo.transform.SetParent(sliderGo.transform);
            fillAreaGo.AddComponent<RectTransform>();

            GameObject fillGo = new("Fill");
            fillGo.transform.SetParent(fillAreaGo.transform);
            fillGo.AddComponent<RectTransform>();
            fillImage = fillGo.AddComponent<Image>();
            fillImage.color = new Color(1f, 1f, 1f, 0f);

            GameObject handleAreaGo = new("Handle Slide Area");
            handleAreaGo.transform.SetParent(sliderGo.transform);
            handleAreaGo.AddComponent<RectTransform>();

            GameObject handleGo = new("Handle");
            handleGo.transform.SetParent(handleAreaGo.transform);
            handleGo.AddComponent<RectTransform>();
            handleImage = handleGo.AddComponent<Image>();
            handleImage.color = new Color(1f, 1f, 1f, 0f);

            slider.fillRect = fillGo.GetComponent<RectTransform>();
            slider.handleRect = handleGo.GetComponent<RectTransform>();
            slider.targetGraphic = handleImage;

            return slider;
        }

        private static Image GetFillImage(Slider slider)
        {
            return slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
        }

        private static Image GetBackgroundImage(Slider slider)
        {
            return slider.transform.Find("Background")?.GetComponent<Image>();
        }

        private static Image GetHandleImage(Slider slider)
        {
            return slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : null;
        }

        private static void SetSliderGraphicColors(Slider slider, Color color)
        {
            Image background = GetBackgroundImage(slider);
            if (background != null)
                background.color = color;

            Image fill = GetFillImage(slider);
            if (fill != null)
                fill.color = color;

            Image handle = GetHandleImage(slider);
            if (handle != null)
                handle.color = color;
        }

        private static float RelativeLuminance(Color color)
        {
            return 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
        }

        private static int GetTopLevelSiblingIndex(Transform child, Transform root)
        {
            Transform current = child;
            while (current.parent != null && current.parent != root)
                current = current.parent;

            return current.GetSiblingIndex();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, args);
        }
    }
}
