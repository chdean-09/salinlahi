using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    [TestFixture]
    public class CutscenePlayerPanelImageTests
    {
        private GameObject _root;
        private Texture2D _texture;
        private Sprite _sprite;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
            if (_sprite != null)
                Object.DestroyImmediate(_sprite);
            if (_texture != null)
                Object.DestroyImmediate(_texture);

            _root = null;
            _sprite = null;
            _texture = null;
        }

        [Test]
        public void ApplyPanelSprite_WithNullSprite_DisablesTheImageInsteadOfShowingWhite()
        {
            CutscenePlayer player = CreatePlayer(out Image panelImage);
            panelImage.enabled = true;

            ApplyPanelSprite(player, null);

            Assert.IsFalse(
                panelImage.enabled,
                "A panel with no art must not leave the Image enabled: an Image with a null sprite "
                + "draws its default white texture across the whole screen.");
        }

        [Test]
        public void ApplyPanelSprite_WithArtAfterAnEmptyPanel_ReEnablesTheImage()
        {
            CutscenePlayer player = CreatePlayer(out Image panelImage);

            ApplyPanelSprite(player, null);
            Assert.IsFalse(panelImage.enabled, "setup: the empty panel disables the Image.");

            ApplyPanelSprite(player, CreateSprite());

            Assert.IsTrue(
                panelImage.enabled,
                "The next panel that does have art must bring the Image back; otherwise one empty "
                + "panel blanks every panel after it.");
            Assert.AreSame(_sprite, panelImage.sprite);
        }

        [Test]
        public void ApplyPanelSprite_WithNoPanelImageWired_DoesNotThrow()
        {
            CutscenePlayer player = CreatePlayer(out Image panelImage);
            Object.DestroyImmediate(panelImage);
            SetPrivateField(player, "_panelImage", null);

            Assert.DoesNotThrow(() => ApplyPanelSprite(player, null));
        }

        private CutscenePlayer CreatePlayer(out Image panelImage)
        {
            _root = new GameObject("CutscenePlayer_PanelImage_Test", typeof(RectTransform));
            _root.SetActive(false);

            var panelGo = new GameObject("PanelImage", typeof(RectTransform));
            panelGo.transform.SetParent(_root.transform, false);
            panelImage = panelGo.AddComponent<Image>();

            CutscenePlayer player = _root.AddComponent<CutscenePlayer>();
            SetPrivateField(player, "_panelImage", panelImage);
            return player;
        }

        private Sprite CreateSprite()
        {
            _texture = new Texture2D(2, 2);
            _texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            _texture.Apply();
            _sprite = Sprite.Create(_texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            return _sprite;
        }

        private static void ApplyPanelSprite(CutscenePlayer player, Sprite sprite)
        {
            MethodInfo method = typeof(CutscenePlayer).GetMethod(
                "ApplyPanelSprite",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Missing method 'ApplyPanelSprite' on CutscenePlayer.");
            method.Invoke(player, new object[] { sprite });
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
