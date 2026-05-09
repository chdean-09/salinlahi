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
            // Load the production Bootstrap scene first so all manager
            // singletons (GameManager, EnemyPool, ActiveEnemyTracker, etc.)
            // come up. Bootstrap auto-transitions to MainMenu — bypass that
            // by loading Gameplay directly after Bootstrap's Awake/Start.
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null; yield return null;

            SceneManager.LoadScene("Gameplay", LoadSceneMode.Single);
            yield return null; yield return null;

            // Wire the level config — point WaveManager at a LevelConfig
            // that references BossConfig_ElInquisidor.
            LevelConfigSO level = Resources.Load<LevelConfigSO>("Test/Level5_ElInquisidor_TestRig");
            Assume.That(level, Is.Not.Null,
                "Test rig level not found. Author Resources/Test/Level5_ElInquisidor_TestRig.asset.");

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

            // Drive the encounter: 3 phases, 1 char each, with 2 intermissions.
            for (int phaseIdx = 0; phaseIdx < boss.Config.phases.Count; phaseIdx++)
            {
                // Wait until this phase becomes targetable.
                float t = 0f;
                while ((!boss.IsTargetable || boss.CurrentPhaseIndex != phaseIdx) && t < 5f)
                {
                    yield return null;
                    t += Time.deltaTime;
                }
                Assert.IsTrue(boss.IsTargetable, $"Phase {phaseIdx} did not become targetable within 5s.");

                // Draw each required character once.
                System.Collections.Generic.IReadOnlyList<BaybayinCharacterSO> required = boss.RequiredCharacters;
                for (int i = 0; i < required.Count; i++)
                {
                    if (required[i] == null) continue;
                    boss.TryRouteDraw(required[i].characterID);
                    yield return null;
                }
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
