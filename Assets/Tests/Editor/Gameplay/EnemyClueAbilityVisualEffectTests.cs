using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public sealed class EnemyClueAbilityVisualEffectTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] is GameObject gameObject
                    && gameObject != null
                    && gameObject.GetComponent<ActiveCluePresenter>() != null)
                {
                    UnityEngine.Object.DestroyImmediate(gameObject);
                }
            }

            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                    UnityEngine.Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void Ngatngat_AdvancesOnlyWhileActiveAndCapsAtHeavyStage()
        {
            ActiveCluePresenter presenter = CreatePresenter(out _, out _);

            InvokeRequired(presenter, "SetNgatngatVisualActive", false);
            InvokeRequired(presenter, "RecordNgatngatRestoration");
            Assert.AreEqual(0, GetPrivateField<int>(presenter, "_ngatngatDamageStage"),
                "Restoration without a live Ngatngat must not advance its damage stage.");

            InvokeRequired(presenter, "SetNgatngatVisualActive", true);
            InvokeRequired(presenter, "RecordNgatngatRestoration");
            Assert.AreEqual(1, GetPrivateField<int>(presenter, "_ngatngatDamageStage"));
            InvokeRequired(presenter, "RecordNgatngatRestoration");
            Assert.AreEqual(2, GetPrivateField<int>(presenter, "_ngatngatDamageStage"));
            InvokeRequired(presenter, "RecordNgatngatRestoration");
            Assert.AreEqual(2, GetPrivateField<int>(presenter, "_ngatngatDamageStage"),
                "Further successful restorations must not exceed the heavily eaten stage.");

            InvokeRequired(presenter, "SetNgatngatVisualActive", false);
            Assert.AreEqual(0, GetPrivateField<int>(presenter, "_ngatngatDamageStage"),
                "Removing the last live Ngatngat must restore the normal stage.");
        }

        [Test]
        public void Ngatngat_DamagesRenderedLatinVerticesProgressivelyAndRestoresThem()
        {
            ActiveCluePresenter presenter = CreatePresenter(out RectTransform rail, out _);
            TextMeshProUGUI label = CreateLabel(rail, "KAYA");
            object slot = CreateRailSlot(presenter, label, "KAYA", null);
            AddRailSlot(presenter, slot);
            label.ForceMeshUpdate(true, true);
            Color32[][] original = CaptureVertexColors(label);

            InvokeRequired(presenter, "SetNgatngatVisualActive", true);
            InvokeRequired(presenter, "RecordNgatngatRestoration");
            Color32[][] partial = CaptureVertexColors(label);
            Assert.AreEqual("KAYA", label.text,
                "The support text remains intact; only generated vertex visibility may change.");
            Assert.IsTrue(HasMoreTransparentVertices(original, partial),
                "Stage one should show a partial bite in the rendered Latin text.");

            InvokeRequired(presenter, "RecordNgatngatRestoration");
            Color32[][] heavy = CaptureVertexColors(label);
            Assert.IsTrue(HasMoreTransparentVertices(partial, heavy),
                "Stage two must show more damage than stage one.");

            InvokeRequired(presenter, "SetNgatngatVisualActive", false);
            Assert.AreEqual("KAYA", label.text);
            AssertVertexColorsEqual(original, CaptureVertexColors(label),
                "Removing Ngatngat must restore the exact original TMP vertex colours.");
        }

        [Test]
        public void Uhaw_CaptureCompletesReturnsToItsSlotAndCanResetMidAnimation()
        {
            ActiveCluePresenter presenter = CreatePresenter(out RectTransform rail, out _);
            Image glyph = CreateGlyph(rail);
            Enemy uhaw = CreateTargetEnemy();
            glyph.rectTransform.anchoredPosition = new Vector2(-220f, 0f);
            uhaw.transform.position = new Vector3(3f, 0f, 0f);
            object slot = CreateRailSlot(presenter, null, null, glyph);
            AddRailSlot(presenter, slot);
            ConfigureRestoredSlot(presenter, slot);
            IList captures = GetPrivateField<IList>(presenter, "_uhawGlyphCaptures");

            Assert.IsTrue((bool)InvokeRequired(presenter, "BeginUhawGlyphCapture", slot, uhaw),
                "An already restored slot with glyph art should start one capture animation.");
            Assert.IsFalse(glyph.gameObject.activeSelf,
                "The source glyph must hide while its single proxy is in transit.");
            Vector2 sourcePosition = rail.InverseTransformPoint(glyph.rectTransform.position);
            RectTransform proxy = GetPrivateField<RectTransform>(captures[0], "ProxyRect");
            InvokeRequired(presenter, "TickUhawGlyphCaptures", 10f);
            Assert.AreEqual(1, captures.Count);
            Assert.AreEqual("Captured", GetPrivateField<object>(captures[0], "State").ToString());
            Assert.Greater((proxy.anchoredPosition - sourcePosition).sqrMagnitude, 1f,
                "The captured proxy should travel from the slot toward Uhaw.");

            Vector2 capturedPosition = proxy.anchoredPosition;
            uhaw.transform.position += Vector3.right;
            InvokeRequired(presenter, "TickUhawGlyphCaptures", 0.01f);
            Assert.Greater((proxy.anchoredPosition - capturedPosition).sqrMagnitude, 1f,
                "A held character should track Uhaw while it moves.");

            rail.gameObject.SetActive(false);
            InvokeRequired(presenter, "TickUhawGlyphCaptures", 10f);
            Assert.IsFalse(glyph.gameObject.activeSelf,
                "Hiding the rail pauses capture without restoring or duplicating the glyph.");
            Assert.AreEqual(1, captures.Count);
            rail.gameObject.SetActive(true);

            InvokeRequired(presenter, "ReleaseUhawGlyphCaptures", false);
            Assert.AreEqual("Returning", GetPrivateField<object>(captures[0], "State").ToString());
            Vector2 returnPosition = proxy.anchoredPosition;
            InvokeRequired(presenter, "TickUhawGlyphCaptures", 0.1f);
            Assert.Greater((proxy.anchoredPosition - returnPosition).sqrMagnitude, 1f,
                "Release should visibly move the glyph back toward its original slot.");
            InvokeRequired(presenter, "TickUhawGlyphCaptures", 10f);
            Assert.IsTrue(glyph.gameObject.activeSelf,
                "The original slot glyph becomes visible again after the return completes.");
            Assert.AreEqual(0, captures.Count, "A completed return must destroy its proxy exactly once.");

            Assert.IsTrue((bool)InvokeRequired(presenter, "BeginUhawGlyphCapture", slot, uhaw));
            uhaw.gameObject.SetActive(false);
            InvokeRequired(presenter, "TickUhawGlyphCaptures", 10f);
            Assert.IsTrue(glyph.gameObject.activeSelf,
                "Defeating or removing Uhaw must return its held glyph to the original slot.");
            Assert.AreEqual(0, captures.Count);
            uhaw.gameObject.SetActive(true);

            Assert.IsTrue((bool)InvokeRequired(presenter, "BeginUhawGlyphCapture", slot, uhaw));
            InvokeRequired(presenter, "TickUhawGlyphCaptures", 0.05f);
            Assert.IsFalse(glyph.gameObject.activeSelf);
            InvokeRequired(presenter, "ResetUhawGlyphCaptures");
            Assert.IsTrue(glyph.gameObject.activeSelf,
                "An interrupted capture reset must immediately restore the source glyph.");
            Assert.AreEqual(0, captures.Count, "Reset must not leave a duplicate or orphaned proxy.");

            SetPrivateField(uhaw, "_spawnSequence", 7L);
            Assert.IsTrue((bool)InvokeRequired(presenter, "BeginUhawGlyphCapture", slot, uhaw));
            SetPrivateField(uhaw, "_spawnSequence", 8L);
            InvokeRequired(presenter, "TickUhawGlyphCaptures", 0.1f);
            Assert.AreEqual("Returning", GetPrivateField<object>(captures[0], "State").ToString(),
                "Reusing the same enemy shell for a new spawn must release its old captured glyph.");
            InvokeRequired(presenter, "TickUhawGlyphCaptures", 10f);
            Assert.IsTrue(glyph.gameObject.activeSelf);

            rail.gameObject.SetActive(false);
            Assert.IsTrue((bool)InvokeRequired(
                presenter, "CaptureOrQueueUhawGlyph", slot, uhaw),
                "A restoration during a hidden HUD should be queued for the visible rail.");
            Assert.AreSame(slot, GetPrivateField<object>(presenter, "_pendingUhawGlyphSlot"));
            InvokeRequired(presenter, "ResetUhawGlyphCaptures");
            Assert.IsNull(GetPrivateField<object>(presenter, "_pendingUhawGlyphSlot"),
                "Pooling/reset must also discard a queued, not-yet-visible capture.");
        }

        private ActiveCluePresenter CreatePresenter(out RectTransform rail, out Canvas canvas)
        {
            var canvasObject = new GameObject("AbilityVisuals_TestCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _created.Add(canvasObject);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1200f, 700f);

            var cameraObject = new GameObject("AbilityVisuals_TestWorldCamera");
            _created.Add(cameraObject);
            cameraObject.tag = "MainCamera";
            Camera worldCamera = cameraObject.AddComponent<Camera>();
            worldCamera.transform.position = new Vector3(0f, 0f, -10f);
            worldCamera.orthographic = true;
            worldCamera.orthographicSize = 5f;

            var railObject = new GameObject("AbilityVisuals_TestRail", typeof(RectTransform));
            railObject.transform.SetParent(canvasObject.transform, false);
            _created.Add(railObject);
            rail = railObject.GetComponent<RectTransform>();
            rail.sizeDelta = new Vector2(800f, 200f);

            var presenterObject = new GameObject("AbilityVisuals_TestPresenter");
            presenterObject.transform.SetParent(canvasObject.transform, false);
            _created.Add(presenterObject);
            ActiveCluePresenter presenter = presenterObject.AddComponent<ActiveCluePresenter>();
            SetPrivateField(presenter, "_railRoot", railObject);
            return presenter;
        }

        private TextMeshProUGUI CreateLabel(RectTransform parent, string text)
        {
            var labelObject = new GameObject("AbilityVisuals_TestLabel", typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            _created.Add(labelObject);
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = 40f;
            label.text = text;
            label.rectTransform.sizeDelta = new Vector2(160f, 64f);
            label.ForceMeshUpdate(true, true);
            return label;
        }

        private Image CreateGlyph(RectTransform parent)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            _created.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f));
            _created.Add(sprite);
            var glyphObject = new GameObject("AbilityVisuals_TestGlyph",
                typeof(RectTransform), typeof(Image));
            glyphObject.transform.SetParent(parent, false);
            _created.Add(glyphObject);
            Image glyph = glyphObject.GetComponent<Image>();
            glyph.sprite = sprite;
            glyph.rectTransform.sizeDelta = new Vector2(48f, 48f);
            return glyph;
        }

        private Enemy CreateTargetEnemy()
        {
            var targetObject = new GameObject("AbilityVisuals_TestUhaw",
                typeof(BoxCollider2D), typeof(EnemyMover), typeof(Enemy));
            _created.Add(targetObject);
            return targetObject.GetComponent<Enemy>();
        }

        private object CreateRailSlot(
            ActiveCluePresenter presenter, TextMeshProUGUI label, string latinLabel, Image glyph)
        {
            Type slotType = typeof(ActiveCluePresenter).GetNestedType(
                "RailSlot", BindingFlags.NonPublic);
            Assert.IsNotNull(slotType, "The presenter needs a concrete slot object for its rail.");
            object slot = Activator.CreateInstance(slotType, nonPublic: true);
            var frameObject = new GameObject("AbilityVisuals_TestFrame",
                typeof(RectTransform), typeof(Image));
            frameObject.transform.SetParent(glyph != null
                ? glyph.transform.parent
                : label.transform.parent, false);
            _created.Add(frameObject);
            Image frame = frameObject.GetComponent<Image>();
            if (glyph == null)
            {
                var glyphObject = new GameObject("AbilityVisuals_TestEmptyGlyph",
                    typeof(RectTransform), typeof(Image));
                glyphObject.transform.SetParent(frameObject.transform.parent, false);
                _created.Add(glyphObject);
                glyph = glyphObject.GetComponent<Image>();
                glyph.gameObject.SetActive(false);
            }
            SetPrivateField(slot, "Label", label);
            SetPrivateField(slot, "LatinLabel", latinLabel);
            SetPrivateField(slot, "Glyph", glyph);
            SetPrivateField(slot, "Frame", frame);
            SetPrivateField(slot, "Anchor", frame.rectTransform);
            return slot;
        }

        private void ConfigureRestoredSlot(ActiveCluePresenter presenter, object slot)
        {
            var character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.stableId = "test-character";
            _created.Add(character);

            var word = new FocusWordDefinition
            {
                stableId = "test-word",
                decomposition = new List<SymbolValueReference>
                {
                    new SymbolValueReference { symbol = character },
                },
            };
            SetPrivateField(slot, "Word", word);
            SetPrivateField(slot, "DecompositionIndex", 0);
            presenter.RestorationState.Configure(new[] { word });
            presenter.RestorationState.Apply(character.stableId);
        }

        private static void AddRailSlot(ActiveCluePresenter presenter, object slot)
        {
            IList slots = GetPrivateField<IList>(presenter, "_railSlots");
            slots.Add(slot);
        }

        private static object InvokeRequired(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing focused behavior method '{methodName}'.");
            return method.Invoke(target, args);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static Color32[][] CaptureVertexColors(TMP_Text text)
        {
            text.ForceMeshUpdate(true, true);
            var meshInfo = text.textInfo.meshInfo;
            var result = new Color32[meshInfo.Length][];
            for (int i = 0; i < meshInfo.Length; i++)
            {
                result[i] = new Color32[meshInfo[i].colors32.Length];
                Array.Copy(meshInfo[i].colors32, result[i], result[i].Length);
            }
            return result;
        }

        private static bool HasMoreTransparentVertices(Color32[][] previous, Color32[][] current)
        {
            int previousTransparent = CountTransparent(previous);
            int currentTransparent = CountTransparent(current);
            return currentTransparent > previousTransparent;
        }

        private static int CountTransparent(Color32[][] colors)
        {
            int count = 0;
            for (int mesh = 0; mesh < colors.Length; mesh++)
            {
                for (int i = 0; i < colors[mesh].Length; i++)
                {
                    if (colors[mesh][i].a == 0)
                        count++;
                }
            }
            return count;
        }

        private static void AssertVertexColorsEqual(
            Color32[][] expected, Color32[][] actual, string message)
        {
            Assert.AreEqual(expected.Length, actual.Length, message);
            for (int mesh = 0; mesh < expected.Length; mesh++)
            {
                Assert.AreEqual(expected[mesh].Length, actual[mesh].Length, message);
                CollectionAssert.AreEqual(expected[mesh], actual[mesh], message);
            }
        }
    }
}
