using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    /// <summary>
    /// SALIN-182 criterion 4: the Tier 5 shield blocks the next Scroll heart loss.
    ///
    /// Play Mode on purpose. HeartSystem sets its heart count in Awake and ComboManager subscribes
    /// in OnEnable, and Edit Mode runs neither on a runtime-created GameObject — the sibling Edit
    /// Mode fixture works only because it invokes those callbacks by hand, which is fine for pure
    /// grant bookkeeping but would not exercise the real heart pipeline this criterion is about.
    ///
    /// Ordering note: HeartSystem.Start raises OnHeartsChanged, which resets the combo streak. The
    /// hearts are therefore created and settled *before* the streak is built, or the setup would
    /// silently destroy the streak it depends on.
    ///
    /// Focus Mode is switched off for these levels, and that is load-bearing rather than tidiness.
    /// Reaching the threshold with it enabled raises OnFocusModeActivated, which every EnemyMover
    /// subscribes to in order to halve its speed. ComboManager.OnDisable unsubscribes but never
    /// raises OnFocusModeDeactivated, so destroying this fixture's manager mid-timer left the whole
    /// player in slow motion and made an unrelated wall-clock boss-animation test fail three runs
    /// out of three. Turning the flag off also exercises the intended decoupling: tier powers do
    /// not depend on Focus Mode being enabled.
    /// </summary>
    [TestFixture]
    public class ComboShieldHeartLossTests
    {
        private const int Threshold = 3;
        private const int MaxHearts = 3;

        private readonly List<Object> _toDestroy = new();

        [TearDown]
        public void TearDown()
        {
            // Belt and braces: a latched Focus Mode would slow every enemy in every later test.
            EventBus.RaiseFocusModeDeactivated();

            ClearSingletonInstance<ComboManager>();
            ClearSingletonInstance<GameManager>();

            for (int i = _toDestroy.Count - 1; i >= 0; i--)
                if (_toDestroy[i] != null)
                    Object.DestroyImmediate(_toDestroy[i]);
            _toDestroy.Clear();
        }

        [UnityTest]
        public IEnumerator AShield_BlocksTheNextHeartLossEntirely()
        {
            HeartSystem hearts = CreateHeartSystem();
            ComboManager combo = CreateComboManager(tier: 5);
            yield return null;

            BuildStreak(Threshold);
            Assert.That(combo.ShieldCharges, Is.EqualTo(1), "Setup: a shield was earned.");

            hearts.LoseHeart(1);

            Assert.That(hearts.GetCurrentHearts(), Is.EqualTo(MaxHearts),
                "The shield must block the heart loss itself, not merely its side effects.");
            Assert.That(combo.ShieldCharges, Is.EqualTo(0), "The charge is spent.");
        }

        // Because no heart is actually lost, OnHeartsChanged never fires, so the streak survives.
        // That falls out of blocking at the source rather than being special-cased, and it is the
        // behaviour a player would expect from a blocked hit.
        [UnityTest]
        public IEnumerator ABlockedHeartLoss_LeavesTheComboStreakIntact()
        {
            CreateHeartSystem();
            ComboManager combo = CreateComboManager(tier: 5);
            yield return null;

            BuildStreak(Threshold);
            int streakBefore = combo.CurrentStreak;

            FindHeartSystem().LoseHeart(1);

            Assert.That(combo.CurrentStreak, Is.EqualTo(streakBefore),
                "A blocked hit should not silently cost the player their streak.");
        }

        [UnityTest]
        public IEnumerator OnceSpent_TheShieldNoLongerBlocks()
        {
            HeartSystem hearts = CreateHeartSystem();
            ComboManager combo = CreateComboManager(tier: 5);
            yield return null;

            BuildStreak(Threshold);
            hearts.LoseHeart(1);
            Assert.That(combo.ShieldCharges, Is.EqualTo(0), "Setup: the charge was spent.");

            hearts.LoseHeart(1);

            Assert.That(hearts.GetCurrentHearts(), Is.EqualTo(MaxHearts - 1),
                "The second hit must land: the shield is one charge, not a mode.");
        }

        [UnityTest]
        public IEnumerator WithoutAShield_HeartsBehaveExactlyAsBefore()
        {
            HeartSystem hearts = CreateHeartSystem();
            CreateComboManager(tier: 1);
            yield return null;

            BuildStreak(Threshold);
            hearts.LoseHeart(1);

            Assert.That(hearts.GetCurrentHearts(), Is.EqualTo(MaxHearts - 1),
                "Tier 1 grants no shield, so this path must be untouched by SALIN-182.");
        }

        private static void BuildStreak(int hits)
        {
            for (int i = 0; i < hits; i++)
                EventBus.RaiseEnemyTargeted(null);
        }

        private HeartSystem CreateHeartSystem()
        {
            var go = new GameObject("HeartSystem_Shield_Test");
            go.SetActive(false);
            HeartSystem hearts = go.AddComponent<HeartSystem>();
            SetPrivateField(hearts, "_maxHearts", MaxHearts);
            go.SetActive(true);
            _toDestroy.Add(go);
            return hearts;
        }

        private HeartSystem FindHeartSystem()
        {
            return Object.FindAnyObjectByType<HeartSystem>(FindObjectsInactive.Include);
        }

        private ComboManager CreateComboManager(int tier)
        {
            var levelObject = new GameObject("GameManager_Shield_Test");
            levelObject.SetActive(false);
            GameManager manager = levelObject.AddComponent<GameManager>();
            var level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.challengePolicy.tier = tier;
            level.focusModeEnabled = false;   // see the fixture summary -- this is not tidiness
            typeof(GameManager).GetProperty("CurrentLevel")?
                .GetSetMethod(true)?.Invoke(manager, new object[] { level });
            SetSingletonInstance(manager);
            _toDestroy.Add(levelObject);
            _toDestroy.Add(level);

            var config = ScriptableObject.CreateInstance<GameConfigSO>();
            config.focusModeThreshold = Threshold;
            config.focusModeDuration = 2f;
            _toDestroy.Add(config);

            var comboObject = new GameObject("ComboManager_Shield_Test");
            comboObject.SetActive(false);
            ComboManager combo = comboObject.AddComponent<ComboManager>();
            SetPrivateField(combo, "_config", config);
            comboObject.SetActive(true);
            SetSingletonInstance(combo);
            _toDestroy.Add(comboObject);

            Assert.That(combo.ShieldCharges, Is.EqualTo(0), "Setup: no shield before the streak.");
            return combo;
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            target.GetType()
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(target, value);
        }

        private static void SetSingletonInstance<T>(T instance) where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetProperty("Instance")?
                .GetSetMethod(true)?.Invoke(null, new object[] { instance });
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetProperty("Instance")?
                .GetSetMethod(true)?.Invoke(null, new object[] { null });
        }
    }
}
