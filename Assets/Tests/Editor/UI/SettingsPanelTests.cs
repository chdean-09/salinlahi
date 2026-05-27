using System.Reflection;
using NUnit.Framework;
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
        public void OnEnable_AndOnDisable_TogglesSliderInteractivity()
        {
            InvokePrivateMethod(_panel, "OnEnable");
            Assert.IsTrue(_masterSlider.interactable);
            Assert.IsTrue(_bgmSlider.interactable);
            Assert.IsTrue(_sfxSlider.interactable);

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
        public void OnEnable_StylesCloseButtonLikeMainMenuButton()
        {
            InvokePrivateMethod(_panel, "OnEnable");

            RectTransform rect = _closeButton.GetComponent<RectTransform>();
            Image image = _closeButton.GetComponent<Image>();
            Text label = _closeButton.GetComponentInChildren<Text>(true);

            Assert.GreaterOrEqual(rect.sizeDelta.x, 280f);
            Assert.GreaterOrEqual(rect.sizeDelta.y, 88f);
            Assert.AreEqual(Color.white, image.color);
            Assert.AreEqual(new Color(0.7019608f, 0.5019608f, 0.07450981f, 1f), label.color);
            Assert.IsNotNull(label.GetComponent<Shadow>());
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
