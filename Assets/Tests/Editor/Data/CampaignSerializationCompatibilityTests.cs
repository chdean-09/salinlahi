using NUnit.Framework;
using UnityEditor;

namespace Salinlahi.Tests.Editor.Data
{
    public class CampaignSerializationCompatibilityTests
    {
        [Test]
        public void ExistingEraAsset_RetainsLegacyPresentationAndInitializesNewCollections()
        {
            EraConfigSO era = AssetDatabase.LoadAssetAtPath<EraConfigSO>(
                "Assets/ScriptableObjects/Themes/Era_01.asset");

            Assert.IsNotNull(era);
            Assert.IsNotEmpty(era.eraName);
            Assert.IsNotNull(era.backgroundSprite);
            Assert.IsNotNull(era.bannerSprite);
            Assert.IsNotNull(era.levels);
        }

        [Test]
        public void ExistingLevelAsset_RetainsLegacyFieldsAndInitializesRevisedCollections()
        {
            LevelConfigSO level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                "Assets/ScriptableObjects/Levels/Level1_Config.asset");

            Assert.IsNotNull(level);
            Assert.AreEqual("Level 1", level.levelName);
            Assert.AreEqual(1, level.levelNumber);
            Assert.IsNotNull(level.waves);
            Assert.IsNotNull(level.allowedCharacters);
            Assert.IsNotNull(level.allowedEnemyTypes);
            Assert.IsNotNull(level.focusWords);
            Assert.IsNotNull(level.cumulativeSymbolPool);
            Assert.IsNotNull(level.learningRequirements);
            Assert.IsNotNull(level.practiceRequirements);
            Assert.IsNotNull(level.rewardIds);
            Assert.IsNotNull(level.masteryRequirements);
            Assert.IsNotNull(level.defenseRules);
            Assert.IsNotNull(level.contextMedia);
            Assert.IsNotNull(level.finalRestorationValue);
        }

        [Test]
        public void ExistingCharacterAsset_RetainsLegacyIdentityAndInitializesRevisedCollections()
        {
            BaybayinCharacterSO character = AssetDatabase.LoadAssetAtPath<BaybayinCharacterSO>(
                "Assets/ScriptableObjects/Characters/Char_DA.asset");

            Assert.IsNotNull(character);
            Assert.AreEqual("DA", character.characterID);
            Assert.AreEqual("da", character.syllable);
            Assert.IsNotNull(character.displaySprite);
            Assert.IsNotNull(character.pronunciationClip);
            Assert.AreEqual("DA_template_01", character.templateFileName);
            Assert.IsNotNull(character.legacyAliases);
            Assert.IsNotNull(character.spokenValues);
        }

        [Test]
        public void ExistingLevelAsset_ReconcileWavesToRosterStillRuns()
        {
            LevelConfigSO level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                "Assets/ScriptableObjects/Levels/Level1_Config.asset");

            Assert.IsNotNull(level);
            Assert.DoesNotThrow(level.ReconcileWavesToRoster);
        }
    }
}
