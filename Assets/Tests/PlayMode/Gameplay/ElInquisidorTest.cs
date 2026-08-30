using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    [TestFixture]
    public class ElInquisidorTest
    {
        private int _onLevelCompleteCount;

        [SetUp]
        public void SetUp()
        {
            _onLevelCompleteCount = 0;
            EventBus.OnLevelComplete += OnLevelCompleteHandler;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.OnLevelComplete -= OnLevelCompleteHandler;
        }

        private void OnLevelCompleteHandler() => _onLevelCompleteCount++;

        [UnityTest]
        public IEnumerator ElInquisidor_IntroThreePhasesTwoIntermissionsOutro_RaisesOnLevelComplete()
        {
            // Check the rig asset BEFORE loading any scene. An Inconclusive
            // exit after loading Bootstrap/Gameplay leaks the scenes and their
            // DontDestroyOnLoad singletons into every later fixture — the rest
            // of the PlayMode suite assumes a bare test scene.
            LevelConfigSO level = Resources.Load<LevelConfigSO>("Test/Level5_ElInquisidor_TestRig");
            Assume.That(level, Is.Not.Null,
                "Test rig level not found. Author Resources/Test/Level5_ElInquisidor_TestRig.asset.");

            // NOTE for when the rig asset exists: this test still leaks the
            // Gameplay scene and Bootstrap singletons after it finishes. Author
            // an explicit teardown (unload scenes, destroy leaked singletons)
            // before relying on any test that runs after this fixture.

            // Load the production Bootstrap scene first so all manager
            // singletons (GameManager, EnemyPool, ActiveEnemyTracker, etc.)
            // come up. Bootstrap auto-transitions to MainMenu — bypass that
            // by loading Gameplay directly after Bootstrap's Awake/Start.
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null; yield return null;

            // Bootstrap's auto-transition already queued SceneLoader.LoadMainMenu.
            // The loader lives in DontDestroyOnLoad, so left alone its async load
            // completes DURING A LATER TEST and single-mode-swaps that test's
            // scene out from under it. Kill it before its pre-load fade reaches
            // LoadSceneAsync — this test drives scene loads directly.
            if (SceneLoader.Instance != null)
                Object.DestroyImmediate(SceneLoader.Instance.gameObject);

            SceneManager.LoadScene("Gameplay", LoadSceneMode.Single);
            yield return null; yield return null;

            GameManager.Instance.SetLevel(level);

            // Kick off the run.
            WaveManager wm = Object.FindFirstObjectByType<WaveManager>();
            Assume.That(wm, Is.Not.Null, "WaveManager not present in Gameplay scene.");
            wm.StartLevel(level);

            // Wait for the boss to spawn and Intro to elapse.
            float waited = 0f;
            while (GameManager.Instance.CurrentBoss == null && waited < 5f)
            {
                yield return null;
                waited += Time.deltaTime;
            }
            Assert.IsNotNull(GameManager.Instance.CurrentBoss, "Boss did not spawn within 5s.");
            BossController boss = GameManager.Instance.CurrentBoss;

            // Drive the encounter: iterate each phase and draw the required number
            // of correct glyphs. In the new model the boss samples random glyphs from
            // LevelConfigSO.allowedCharacters one at a time via CurrentExpectedCharacterID.
            for (int phaseIdx = 0; phaseIdx < boss.Config.phases.Count; phaseIdx++)
            {
                // Wait until this phase becomes targetable.
                float t = 0f;
                while ((!boss.IsTargetable || boss.CurrentPhaseIndex != phaseIdx) && t < 15f)
                {
                    yield return null;
                    t += Time.deltaTime;
                }
                Assert.IsTrue(boss.IsTargetable, $"Phase {phaseIdx} did not become targetable within 15s.");

                int requiredCount = boss.RequiredCharactersForCurrentPhase;
                for (int draw = 0; draw < requiredCount; draw++)
                {
                    // Wait a frame for the boss to sample the next expected character.
                    yield return null;
                    string expected = boss.CurrentExpectedCharacterID;
                    if (expected == null) continue;
                    boss.TryRouteDraw(expected);
                }

                // Allow a frame for the Damaged coroutine to process.
                yield return null;
            }

            // Wait for OnLevelComplete with a generous timeout.
            float endWait = 0f;
            while (_onLevelCompleteCount == 0 && endWait < 30f)
            {
                yield return null;
                endWait += Time.deltaTime;
            }

            Assert.AreEqual(1, _onLevelCompleteCount, "OnLevelComplete must fire exactly once.");
        }
    }
}
