using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class EnemyAbilityArtWiringTests
    {
        private const string ArtRoot = "Assets/Art/VFX/EnemyAbilities";

        [Test]
        public void BakodData_UsesBarrierBreakArtAsActiveLayerAndExitSequence()
        {
            EnemyDataSO data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
                "Assets/ScriptableObjects/Enemies/EnemyData_Bakod.asset");

            Assert.IsNotNull(data);
            EnemyAbilityVisualDefinition definition = FindVisual(data, "BakodBarrier");
            Assert.IsNotNull(definition, "Bakod's barrier layer must be wired on its data asset.");
            AssertSpriteFrame(definition.activeSprite, "BakodBarrier", "cracking-barrier-break-1652", 1);
            AssertSpriteSequence(definition.exitFrames, "BakodBarrier", "cracking-barrier-break-1652", 1, 8);
            Assert.AreEqual(EnemyAbilityVisualAnchor.EnemyBody, definition.anchor);
            Assert.AreSame(definition.activeSprite, definition.exitFrames[0]);

            EnemyAbilityVisualDefinition blockedBadge = FindVisual(data, "BakodBlockedTarget");
            Assert.IsNotNull(blockedBadge,
                "Bakod's barrier icon should replace the blocked badge's gray-only placeholder.");
            Assert.AreEqual(EnemyAbilityVisualAnchor.GlyphBadge, blockedBadge.anchor);
            AssertSpriteFrame(blockedBadge.activeSprite, "BakodBarrier", "cracking-barrier-break-1652", 1);
            AssertSpriteSequence(blockedBadge.exitFrames, "BakodBarrier", "cracking-barrier-break-1652", 1, 8);
        }

        [Test]
        public void MantsaData_UsesSplashArtToStainAndClearGlyphBadge()
        {
            EnemyDataSO data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
                "Assets/ScriptableObjects/Enemies/EnemyData_Mantsa.asset");

            Assert.IsNotNull(data);
            EnemyAbilityVisualDefinition definition = FindVisual(data, "MantsaStain");
            Assert.IsNotNull(definition, "Mantsa's stain layer must be wired on its data asset.");
            AssertSpriteFrame(definition.activeSprite, "MantsaStain", "splash-paint-clear-1622", 4);
            Assert.AreEqual(EnemyAbilityVisualAnchor.GlyphBadge, definition.anchor);
            Assert.Greater(definition.activeOpacity, 0f);
            Assert.Less(definition.activeOpacity, 1f,
                "Ink should partially obscure the true glyph instead of replacing it.");

            SerializedObject serializedData = new SerializedObject(data);
            Assert.IsNotNull(serializedData.FindProperty(
                "abilityVisuals.Array.data[0].activationFrames"),
                "Mantsa's splash-on animation should be available as activation frames.");
            AssertSpriteSequence(definition.activationFrames, "MantsaStain", "splash-paint-clear-1622", 1, 4);
            Assert.AreSame(definition.activeSprite, definition.activationFrames[3]);
            AssertSpriteSequence(definition.exitFrames, "MantsaStain", "splash-paint-clear-1622", 5, 3);
        }

        [Test]
        public void AboData_UsesAshArtForTheActiveClueFirstSlot()
        {
            EnemyDataSO data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
                "Assets/ScriptableObjects/Enemies/EnemyData_AbongSimula.asset");

            Assert.IsNotNull(data);
            SerializedObject serializedData = new SerializedObject(data);
            SerializedProperty hudVisuals = serializedData.FindProperty("hudAbilityVisuals");
            Assert.IsNotNull(hudVisuals,
                "Enemy data should support HUD-targeted ability visuals independently of body layers.");
            Assert.AreEqual(1, hudVisuals.arraySize);
            EnemyHudAbilityVisualDefinition definition = FindHudVisual(data, EnemyHudAbilityVisualId.AshClueFirstSlot);
            Assert.IsNotNull(definition);
            AssertSpriteFrame(definition.activeSprite, "AboClueCover", "ash-settle-cover-1609", 4);
            AssertSpriteSequence(definition.activationFrames, "AboClueCover", "ash-settle-cover-1609", 1, 4);
            AssertSpriteSequence(definition.exitFrames, "AboClueCover", "ash-settle-cover-1609", 5, 4);
        }

        [Test]
        public void WalangAwaData_UsesFrameZeroArmorAndAllEightBreakFrames()
        {
            EnemyDataSO data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
                "Assets/ScriptableObjects/Enemies/EnemyData_Walang-Awa.asset");

            Assert.IsNotNull(data);
            EnemyAbilityVisualDefinition definition = FindVisual(data, "Armor");
            Assert.IsNotNull(definition);
            Assert.AreEqual(EnemyAbilityVisualAnchor.EnemyBody, definition.anchor);
            Assert.AreEqual(EnemyAbilityVisualFrameMode.SingleFrame, definition.frameMode);
            Assert.AreEqual(0, definition.singleFrameIndex);
            Assert.AreEqual(0.45f, definition.activeOpacity, 0.001f);
            Assert.AreEqual(0.85f, definition.exitOpacity, 0.001f);
            AssertSpriteFrame(definition.activeSprite, "WalangAwaArmor", "armor-break-layer-1711", 1);
            AssertSpriteSequence(definition.exitFrames, "WalangAwaArmor", "armor-break-layer-1711", 1, 8);
        }

        [Test]
        public void TakipData_UsesCoverArtWithAuthoredCloseAndOpenFrameOrder()
        {
            const string dataPath = "Assets/ScriptableObjects/Enemies/EnemyData_Takip.asset";
            const string frameFolder = "Assets/Art/VFX/EnemyAbilities/TakipGlyphCover";
            EnemyDataSO data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(dataPath);

            Assert.IsNotNull(data);
            EnemyAbilityVisualDefinition definition = FindVisual(data, "GlyphCover");
            Assert.IsNotNull(definition, "Takip's cover must be wired on its data asset.");
            Assert.AreEqual(EnemyAbilityVisualAnchor.GlyphBadge, definition.anchor);
            Assert.AreEqual(EnemyAbilityVisualFrameMode.FullLoop, definition.frameMode);
            Assert.AreEqual(1f, definition.activeOpacity, 0.001f);
            Assert.AreEqual(1f, definition.activationOpacity, 0.001f);
            Assert.AreEqual(1f, definition.exitOpacity, 0.001f);
            Assert.AreEqual(8f, definition.activationFramesPerSecond, 0.001f);
            Assert.AreEqual(8f, definition.exitFramesPerSecond, 0.001f);
            Assert.AreEqual(4, definition.activationFrames.Length);
            Assert.AreEqual(4, definition.exitFrames.Length);
            Assert.AreSame(definition.activeSprite, definition.exitFrames[0]);

            for (int i = 0; i < 8; i++)
            {
                string path = $"{frameFolder}/cover-reveal-glyph-2110-{i + 1:00}.png";
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, $"Frame {i + 1:00} must be imported by Unity.");
                var textureSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(textureSettings);
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode);
                Assert.IsTrue(importer.alphaIsTransparency);
                Assert.IsFalse(importer.mipmapEnabled);
                Assert.AreEqual(FilterMode.Point, importer.filterMode);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
                Assert.AreEqual(96f, importer.spritePixelsPerUnit, 0.001f);
                Assert.AreEqual(SpriteAlignment.Center, (SpriteAlignment)textureSettings.spriteAlignment);
                Assert.AreEqual(new Vector2(0.5f, 0.5f), textureSettings.spritePivot);
            }

            Assert.AreEqual($"{frameFolder}/cover-reveal-glyph-2110-01.png",
                AssetDatabase.GetAssetPath(definition.activeSprite));
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual($"{frameFolder}/cover-reveal-glyph-2110-{i + 5:00}.png",
                    AssetDatabase.GetAssetPath(definition.activationFrames[i]),
                    $"Closing frame {i + 5:00} must keep the authored order.");
                Assert.AreEqual($"{frameFolder}/cover-reveal-glyph-2110-{i + 1:00}.png",
                    AssetDatabase.GetAssetPath(definition.exitFrames[i]),
                    $"Opening frame {i + 1:00} must keep the authored order.");
            }
        }

        private static EnemyAbilityVisualDefinition FindVisual(EnemyDataSO data, string id)
        {
            if (data.abilityVisuals == null)
                return null;

            for (int i = 0; i < data.abilityVisuals.Length; i++)
            {
                EnemyAbilityVisualDefinition definition = data.abilityVisuals[i];
                if (definition != null && definition.id.ToString() == id)
                    return definition;
            }

            return null;
        }

        private static EnemyHudAbilityVisualDefinition FindHudVisual(EnemyDataSO data, EnemyHudAbilityVisualId id)
        {
            if (data.hudAbilityVisuals == null)
                return null;

            for (int i = 0; i < data.hudAbilityVisuals.Length; i++)
            {
                EnemyHudAbilityVisualDefinition definition = data.hudAbilityVisuals[i];
                if (definition != null && definition.id == id)
                    return definition;
            }

            return null;
        }

        private static void AssertSpriteSequence(
            Sprite[] sprites,
            string folder,
            string fileStem,
            int firstFrame,
            int frameCount)
        {
            Assert.IsNotNull(sprites);
            Assert.AreEqual(frameCount, sprites.Length);
            for (int i = 0; i < frameCount; i++)
                AssertSpriteFrame(sprites[i], folder, fileStem, firstFrame + i);
        }

        private static void AssertSpriteFrame(Sprite sprite, string folder, string fileStem, int frame)
        {
            string path = $"{ArtRoot}/{folder}/{fileStem}-{frame:00}.png";
            Assert.IsNotNull(sprite, $"Missing serialized sprite reference for {path}.");
            Assert.AreEqual(path, AssetDatabase.GetAssetPath(sprite),
                "The ability frame should point to the intended individual PNG, not just any sprite.");

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.IsNotNull(importer, $"Unity must import {path} as a texture sprite.");
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
            Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode);
            Assert.IsTrue(importer.alphaIsTransparency);
            Assert.IsFalse(importer.mipmapEnabled);
            Assert.AreEqual(FilterMode.Point, importer.filterMode);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
            Assert.AreEqual(96f, importer.spritePixelsPerUnit, 0.001f);
            Assert.AreEqual(SpriteAlignment.Center, (SpriteAlignment)textureSettings.spriteAlignment);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), textureSettings.spritePivot);
        }
    }
}
