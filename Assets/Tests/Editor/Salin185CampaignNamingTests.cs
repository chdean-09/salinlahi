using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class Salin185CampaignNamingTests
{
    [Test]
    public void ActiveLevelAssetsUseApprovedCampaignNames()
    {
        var expectedCampaigns = new Dictionary<int, string>
        {
            { 1, "Ugat" }, { 2, "Ugat" }, { 3, "Ugat" }, { 4, "Ugat" }, { 5, "Ugat" },
            { 6, "Ugnayan" }, { 7, "Ugnayan" }, { 8, "Ugnayan" }, { 9, "Ugnayan" }, { 10, "Ugnayan" },
            { 11, "Pamana" }, { 12, "Pamana" }, { 13, "Pamana" }, { 14, "Pamana" }, { 15, "Pamana" }
        };

        var foundLevels = new HashSet<int>();
        string[] guids = AssetDatabase.FindAssets("t:LevelConfigSO", new[] { "Assets/ScriptableObjects/Levels" });
        Assert.AreEqual(expectedCampaigns.Count, guids.Length,
            "Active level content must contain exactly Levels 1-15. Paglimot mastery encounters require dependency-provided assets and must not be added as normal levels.");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            LevelConfigSO level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(path);
            Assert.IsNotNull(level, path);
            Assert.IsTrue(expectedCampaigns.ContainsKey(level.levelNumber),
                $"{path}: unexpected levelNumber {level.levelNumber}. Active story levels must remain within 1-15; Paglimot is dependency-provided mastery content.");
            Assert.IsFalse(foundLevels.Contains(level.levelNumber),
                $"{path}: duplicate levelNumber {level.levelNumber}.");

            foundLevels.Add(level.levelNumber);
            Assert.AreEqual(expectedCampaigns[level.levelNumber], level.chapterName, path);
            Assert.That(level.levelName, Does.Not.StartWith("Chapter "), path);
        }

        CollectionAssert.AreEquivalent(expectedCampaigns.Keys, foundLevels);
    }

    [Test]
    public void PaglimotIsAbsentUntilMasteryDependenciesExist()
    {
        string[] guids = AssetDatabase.FindAssets("t:LevelConfigSO", new[] { "Assets/ScriptableObjects/Levels" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            LevelConfigSO level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(path);

            Assert.IsNotNull(level, path);
            Assert.AreNotEqual("Paglimot", level.chapterName,
                $"{path}: do not create placeholder Paglimot levels before the mastery-content dependencies are present.");
        }
    }

    [Test]
    public void ActiveCampaignDisplayUsesUgat()
    {
        const string path = "Assets/ScriptableObjects/Themes/Era_01.asset";
        EraConfigSO campaign = AssetDatabase.LoadAssetAtPath<EraConfigSO>(path);

        Assert.IsNotNull(campaign, path);
        Assert.AreEqual("Ugat", campaign.eraName, path);
    }
}
