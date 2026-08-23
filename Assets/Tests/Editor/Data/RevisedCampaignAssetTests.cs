using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// SALIN-198: the revised campaign asset must exist, resolve Level 1
    /// (INA/AMA) cleanly, and be reproducible by an idempotent bootstrap.
    /// Media references on Level 1 and the era story/memory references are
    /// deferred to SALIN-199/200 (stacked above) and are the only issue codes
    /// tolerated inside the Level-1 scope; everything else in that scope must
    /// validate clean. Levels 2-15 content remains on SALIN-172/204/205.
    /// </summary>
    [TestFixture]
    public sealed class RevisedCampaignAssetTests
    {
        private static CampaignConfigSO LoadCampaign()
        {
            RevisedCampaignBootstrap.Run();
            Level1NarrativeBootstrap.Run();
            return AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(
                RevisedCampaignBootstrap.CampaignAssetPath);
        }

        private static bool IsLevelOneScope(string path)
        {
            if (path.StartsWith("campaign.revised-v1.manifest")
                || path.StartsWith("campaign.revised-v1.tuning")
                || path.StartsWith("campaign.revised-v1.learningTuning")
                || path.StartsWith("campaign.revised-v1.symbols"))
                return true;

            if (!path.StartsWith("campaign.revised-v1.eras[0]"))
                return false;

            // Era-1 level entries other than level 1 belong to SALIN-204.
            int levelsIndex = path.IndexOf(".levels[", System.StringComparison.Ordinal);
            return levelsIndex < 0 || path.Contains(".levels[0]");
        }

        private static bool IsDeferredMediaIssue(ContentValidationIssue issue)
        {
            // After SALIN-200, the only deferred Level-1 media are the context
            // image and narration audio — SALIN-199's manifest scope. Dialogue,
            // cutscene, and era story/memory references must all resolve.
            return issue.Code == ContentValidationCode.RequiredMediaMissing
                && (issue.Path.Contains(".media") || issue.Path.Contains(".contextMedia"));
        }

        [Test]
        public void Bootstrap_ProducesACampaignAssetAtTheStablePath()
        {
            CampaignConfigSO campaign = LoadCampaign();

            Assert.IsNotNull(campaign,
                $"Expected the bootstrap to produce {RevisedCampaignBootstrap.CampaignAssetPath}.");
            Assert.IsTrue(campaign.manifest != null && campaign.manifest.IsRevisedV1,
                "The campaign manifest must be the revised-v1 identity.");
        }

        [Test]
        public void LevelOneScope_ValidatesCleanExceptDeferredMedia()
        {
            CampaignConfigSO campaign = LoadCampaign();
            Assert.IsNotNull(campaign);

            IReadOnlyList<ContentValidationIssue> issues = CampaignConfigValidator.Validate(campaign);
            var violations = issues
                .Where(issue => IsLevelOneScope(issue.Path) && !IsDeferredMediaIssue(issue))
                .Select(issue => $"{issue.Code} @ {issue.Path}: {issue.Message}")
                .ToList();

            Assert.IsEmpty(violations,
                "Level-1-scoped validation must be clean apart from media deferred to SALIN-199/200:\n"
                + string.Join("\n", violations));
        }

        [Test]
        public void LevelOne_ResolvesInaAndAmaWithApprovedDecompositions()
        {
            CampaignConfigSO campaign = LoadCampaign();
            Assert.IsNotNull(campaign);

            Assert.IsTrue(campaign.TryGetLevel("level.ugat.01", out LevelConfigSO level),
                "level.ugat.01 must resolve uniquely.");
            Assert.AreEqual(2, level.focusWords.Count);

            FocusWordDefinition ina = level.focusWords[0];
            Assert.AreEqual("level.ugat.01.focus.01", ina.stableId);
            Assert.AreEqual("INA", ina.latinSpelling);
            Assert.IsFalse(string.IsNullOrWhiteSpace(ina.meaning));
            CollectionAssert.AreEqual(
                new[] { "symbol.ei", "symbol.na" },
                ina.decomposition.Select(reference => reference.symbol.stableId).ToArray());

            FocusWordDefinition ama = level.focusWords[1];
            Assert.AreEqual("level.ugat.01.focus.02", ama.stableId);
            Assert.AreEqual("AMA", ama.latinSpelling);
            Assert.IsFalse(string.IsNullOrWhiteSpace(ama.meaning));
            CollectionAssert.AreEqual(
                new[] { "symbol.a", "symbol.ma" },
                ama.decomposition.Select(reference => reference.symbol.stableId).ToArray());

            foreach (SymbolValueReference reference in ina.decomposition.Concat(ama.decomposition))
            {
                Assert.IsTrue(
                    campaign.TryGetSpokenValue(reference.symbol.stableId, reference.spokenValueId, out _),
                    $"Decomposition value {reference.spokenValueId} must resolve on {reference.symbol.stableId}.");
            }

            Assert.AreEqual(4, level.cumulativeSymbolPool.Count,
                "Level 1 introduces exactly EI, NA, A, MA.");
            Assert.IsTrue(level.activeClueCombatEnabled);
            Assert.AreEqual(ClueChannels.Glyph | ClueChannels.LatinText, level.clueChannels,
                "Level 1 must declare a readable visual channel while badge art is missing.");
            Assert.IsTrue(
                ClueChannelResolver.HasReadableVisual(
                    ClueChannelResolver.Resolve(level.clueChannels, level.audioVisualFallback)),
                "Level 1's resolved clue must be readable without audio.");
            Assert.IsNotNull(level.challengeSequence,
                "Level 1 must carry its context-challenge sequence.");
            Assert.IsFalse(level.challengePrototypeEnabled,
                "Level 1's challenge must run as the planned phase so the tier policy "
                + "and the evidence sink engage.");
            Assert.AreEqual(1, level.challengePolicy.tier);
            CollectionAssert.IsNotEmpty(level.rewardIds);
        }

        [Test]
        public void SymbolCatalog_MatchesTheRevisedIdentity()
        {
            CampaignConfigSO campaign = LoadCampaign();
            Assert.IsNotNull(campaign);

            Assert.AreEqual(17, campaign.symbols.Count);
            Assert.AreEqual(18, campaign.symbols.Sum(symbol => symbol.spokenValues.Count),
                "Exactly eighteen contextual spoken values across seventeen symbols.");

            Assert.IsTrue(campaign.TryGetSymbol("symbol.dara", out BaybayinCharacterSO dara));
            Assert.IsTrue(dara.TryGetSpokenValue("value.da", out _));
            Assert.IsTrue(dara.TryGetSpokenValue("value.ra", out _));

            foreach (string symbolId in new[] { "symbol.ei", "symbol.na", "symbol.a", "symbol.ma" })
            {
                Assert.IsTrue(campaign.TryGetSymbol(symbolId, out BaybayinCharacterSO symbol));
                Assert.AreEqual("level.ugat.01", symbol.firstIntroductionLevelId,
                    $"{symbolId} is introduced by Level 1.");
            }
        }

        [Test]
        public void LevelOne_NarrativeReferencesResolve()
        {
            CampaignConfigSO campaign = LoadCampaign();
            Assert.IsNotNull(campaign);

            Assert.IsTrue(campaign.TryGetLevel("level.ugat.01", out LevelConfigSO level));
            Assert.IsNotNull(level.introDialogue, "Level 1 must open with its intro dialogue.");
            Assert.IsNotEmpty(level.introDialogue.lines);
            Assert.IsNotNull(level.outroDialogue, "Level 1 must close with its completion dialogue.");

            foreach (FocusWordDefinition focus in level.focusWords)
            {
                Assert.IsNotNull(focus.media?.dialogue,
                    $"{focus.stableId} needs its explanation dialogue.");
                Assert.IsNotEmpty(focus.media.dialogue.lines);
                Assert.IsNotNull(focus.media.cutscene,
                    $"{focus.stableId} needs its memory cutscene reference.");
            }

            Assert.IsNotNull(level.contextMedia?.dialogue);
            Assert.IsNotNull(level.contextMedia?.cutscene);

            Assert.IsTrue(campaign.TryGetEra("era.ugat", out EraConfigSO era));
            Assert.IsNotNull(era.storyReference, "The Ugat era needs its story reference.");
            Assert.IsNotNull(era.memoryReference, "The Ugat era needs its memory cutscene.");
            Assert.IsNotEmpty(era.memoryReference.panels);
        }

        [Test]
        public void Bootstrap_IsIdempotent()
        {
            CampaignConfigSO first = LoadCampaign();
            Assert.IsNotNull(first);
            int firstIssueCount = CampaignConfigValidator.Validate(first).Count;

            RevisedCampaignBootstrap.Run();
            CampaignConfigSO second = AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(
                RevisedCampaignBootstrap.CampaignAssetPath);

            Assert.AreSame(first, second, "Re-running must update in place, never recreate.");
            Assert.AreEqual(firstIssueCount, CampaignConfigValidator.Validate(second).Count,
                "A second run must not change the validation outcome.");
        }

        [Test]
        public void Bootstrap_AbortsWithoutWritingWhenALevelConfigIsMissing()
        {
            const string levelOnePath = "Assets/ScriptableObjects/Levels/Level1_Config.asset";
            const string stagedPath = "Assets/ScriptableObjects/Levels/Level1_Config_Staged.asset";

            Assert.IsNotNull(LoadCampaign());
            var levelTwo = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                "Assets/ScriptableObjects/Levels/Level2_Config.asset");
            Assert.IsNotNull(levelTwo);

            string moveError = AssetDatabase.MoveAsset(levelOnePath, stagedPath);
            Assert.IsTrue(string.IsNullOrEmpty(moveError),
                $"Could not stage the missing-level case: {moveError}");

            try
            {
                LogAssert.Expect(LogType.Error,
                    "RevisedCampaignBootstrap: missing Level1_Config asset. "
                    + "Aborting; no assets were modified.");

                Assert.DoesNotThrow(() => RevisedCampaignBootstrap.Run(),
                    "A missing level config must abort cleanly instead of throwing mid-write.");

                Assert.AreEqual("level.ugat.02", levelTwo.stableId,
                    "An aborted run must not shift level identities.");
                Assert.AreEqual(2, levelTwo.levelNumber);
                CollectionAssert.IsEmpty(levelTwo.focusWords,
                    "An aborted run must not author Level 1's focus words into Level 2.");
            }
            finally
            {
                AssetDatabase.MoveAsset(stagedPath, levelOnePath);
                RevisedCampaignBootstrap.Run();
            }
        }
    }
}
