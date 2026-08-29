using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// SALIN-182. How the tier power is granted and spent on the existing combo streak.
    ///
    /// Edit Mode, following the sibling ComboManagerTests: the fixture invokes OnEnable/OnDisable
    /// explicitly by reflection, because Edit Mode does not run the MonoBehaviour lifecycle on a
    /// runtime-created GameObject and the EventBus subscriptions live there. The shield's actual
    /// effect on hearts needs a real Awake, so it is asserted in the Play Mode fixture instead.
    /// </summary>
    [TestFixture]
    public class ComboPowerGrantTests
    {
        private const int Threshold = 3;

        private GameObject _comboObject;
        private GameObject _gameManagerObject;
        private ComboManager _combo;
        private GameConfigSO _config;
        private LevelConfigSO _level;

        [SetUp]
        public void SetUp()
        {
            _gameManagerObject = new GameObject("GameManager_ComboPower_Test");
            GameManager gameManager = _gameManagerObject.AddComponent<GameManager>();
            SetSingletonInstance(gameManager);

            _config = ScriptableObject.CreateInstance<GameConfigSO>();
            _config.focusModeThreshold = Threshold;
            _config.focusModeDuration = 2f;

            _level = ScriptableObject.CreateInstance<LevelConfigSO>();
            // Focus Mode off: reaching the threshold with it on raises OnFocusModeActivated, which
            // EnemyMover latches to halve enemy speed, and ComboManager never raises the matching
            // Deactivated on teardown. The Play Mode sibling hit exactly that and took an unrelated
            // wall-clock test down with it. Also exercises the decoupling -- powers are granted
            // regardless of this flag.
            _level.focusModeEnabled = false;
            SetCurrentLevel(gameManager, _level);

            _comboObject = new GameObject("ComboManager_ComboPower_Test");
            _combo = _comboObject.AddComponent<ComboManager>();
            SetPrivateField(_combo, "_config", _config);
            Invoke(_combo, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.RaiseFocusModeDeactivated();
            Invoke(_combo, "OnDisable");
            ClearSingletonInstance<GameManager>();
            if (_comboObject != null) Object.DestroyImmediate(_comboObject);
            if (_gameManagerObject != null) Object.DestroyImmediate(_gameManagerObject);
            if (_config != null) Object.DestroyImmediate(_config);
            if (_level != null) Object.DestroyImmediate(_level);
        }

        [Test]
        public void ReachingTheThreshold_AtTierOne_GrantsNothing()
        {
            _level.challengePolicy.tier = 1;

            BuildStreak(Threshold);

            Assert.That(_combo.ActivePower, Is.EqualTo(ComboPower.None));
            Assert.That(_combo.ShieldCharges, Is.EqualTo(0));
            Assert.That(_combo.PendingRapidShotHits, Is.EqualTo(0));
        }

        [Test]
        public void ReachingTheThreshold_AtTierTwo_GrantsTheConfiguredRapidShotHits()
        {
            _level.challengePolicy.tier = 2;
            _config.rapidShotBonusHits = 1;

            BuildStreak(Threshold);

            Assert.That(_combo.ActivePower, Is.EqualTo(ComboPower.RapidShot));
            Assert.That(_combo.PendingRapidShotHits, Is.EqualTo(1));
        }

        // "One bonus combat hit", not one per recognition: the grant is spent and does not refill
        // while the streak continues past the threshold.
        [Test]
        public void RapidShotHits_AreSpentOnceAndNotRefilledByFurtherHits()
        {
            _level.challengePolicy.tier = 2;
            _config.rapidShotBonusHits = 1;
            BuildStreak(Threshold);

            Assert.That(_combo.TryConsumeRapidShotHit(), Is.True);
            Assert.That(_combo.TryConsumeRapidShotHit(), Is.False, "The grant is one hit, not a tap.");

            BuildStreak(2);
            Assert.That(_combo.TryConsumeRapidShotHit(), Is.False,
                "Continuing the same streak past the threshold must not re-grant the power.");
        }

        [Test]
        public void ReachingTheThreshold_AtTierFive_GrantsExactlyOneShield()
        {
            _level.challengePolicy.tier = 5;

            BuildStreak(Threshold);

            Assert.That(_combo.ActivePower, Is.EqualTo(ComboPower.Shield));
            Assert.That(_combo.ShieldCharges, Is.EqualTo(1));
        }

        // The nonstacking rule. Earning a second streak while a charge is still held must not bank
        // two shields.
        [Test]
        public void ShieldsDoNotStack_AcrossSeparateStreaks()
        {
            _level.challengePolicy.tier = 5;
            BuildStreak(Threshold);
            Assert.That(_combo.ShieldCharges, Is.EqualTo(1), "Setup: first shield earned.");

            EventBus.RaiseDrawingFailed();      // breaks the streak, shield is not streak-scoped
            BuildStreak(Threshold);             // earn the threshold a second time

            Assert.That(_combo.ShieldCharges, Is.EqualTo(1),
                "A second grant while a charge is held must be a no-op, not a second shield.");
        }

        [Test]
        public void AShieldSurvivesAStreakReset_ButRapidShotDoesNot()
        {
            _level.challengePolicy.tier = 5;
            BuildStreak(Threshold);

            EventBus.RaiseDrawingFailed();

            Assert.That(_combo.CurrentStreak, Is.EqualTo(0));
            Assert.That(_combo.ActivePower, Is.EqualTo(ComboPower.None),
                "The streak-scoped power clears with the streak.");
            Assert.That(_combo.ShieldCharges, Is.EqualTo(1),
                "A shield blocks 'the next' heart loss; revoking it on the next miss would mean it "
                + "almost never survived to do its job.");
        }

        // SALIN-141 established that an abandoned attempt leaves no residue. A banked free heart
        // crossing into the next attempt would be exactly that.
        [Test]
        public void EndingTheLevelAttempt_ClearsBankedShields()
        {
            _level.challengePolicy.tier = 5;
            BuildStreak(Threshold);
            Assert.That(_combo.ShieldCharges, Is.EqualTo(1), "Setup: a shield is banked.");

            EventBus.RaiseLevelAttemptAborted();

            Assert.That(_combo.ShieldCharges, Is.EqualTo(0));
            Assert.That(_combo.ActivePower, Is.EqualTo(ComboPower.None));
        }

        [Test]
        public void DisablingComboPowers_LeavesTheStreakBehaviourUntouched()
        {
            _level.challengePolicy.tier = 5;
            _config.comboPowersEnabled = false;

            BuildStreak(Threshold);

            Assert.That(_combo.CurrentStreak, Is.EqualTo(Threshold), "The streak still counts.");
            Assert.That(_combo.ActivePower, Is.EqualTo(ComboPower.None));
            Assert.That(_combo.ShieldCharges, Is.EqualTo(0));
        }

        [Test]
        public void BelowTheThreshold_NoPowerIsGranted()
        {
            _level.challengePolicy.tier = 5;

            BuildStreak(Threshold - 1);

            Assert.That(_combo.ShieldCharges, Is.EqualTo(0));
            Assert.That(_combo.ActivePower, Is.EqualTo(ComboPower.None));
        }

        private void BuildStreak(int hits)
        {
            for (int i = 0; i < hits; i++)
                EventBus.RaiseEnemyTargeted(null);
        }

        private static void SetCurrentLevel(GameManager manager, LevelConfigSO level)
        {
            typeof(GameManager).GetProperty("CurrentLevel")?
                .GetSetMethod(true)?
                .Invoke(manager, new object[] { level });
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            target.GetType()
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(target, value);
        }

        private static void Invoke(object target, string method)
        {
            target.GetType()
                .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(target, null);
        }

        private static void SetSingletonInstance<T>(T instance) where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetProperty("Instance")?
                .GetSetMethod(true)?
                .Invoke(null, new object[] { instance });
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetProperty("Instance")?
                .GetSetMethod(true)?
                .Invoke(null, new object[] { null });
        }
    }
}
