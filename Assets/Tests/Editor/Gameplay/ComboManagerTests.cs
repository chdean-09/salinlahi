using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class ComboManagerTests
    {
        private GameObject _gameObject;
        private ComboManager _comboManager;
        private GameConfigSO _config;
        private int _lastComboValue;
        private int _focusActivatedCount;
        private int _focusDeactivatedCount;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("ComboManager_Test");
            _config = ScriptableObject.CreateInstance<GameConfigSO>();
            _config.focusModeThreshold = 3;
            _config.focusModeDuration = 2f;
            _config.focusModeSpeedMultiplier = 0.5f;

            _comboManager = _gameObject.AddComponent<ComboManager>();

            var configField = typeof(ComboManager).GetField("_config",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            configField.SetValue(_comboManager, _config);

            _lastComboValue = -1;
            _focusActivatedCount = 0;
            _focusDeactivatedCount = 0;
            EventBus.OnComboChanged += OnComboChanged;
            EventBus.OnFocusModeActivated += OnFocusActivated;
            EventBus.OnFocusModeDeactivated += OnFocusDeactivated;

            _comboManager.GetType().GetMethod("OnEnable",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .Invoke(_comboManager, null);
        }

        [TearDown]
        public void TearDown()
        {
            _comboManager.GetType().GetMethod("OnDisable",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .Invoke(_comboManager, null);

            EventBus.OnComboChanged -= OnComboChanged;
            EventBus.OnFocusModeActivated -= OnFocusActivated;
            EventBus.OnFocusModeDeactivated -= OnFocusDeactivated;

            if (_gameObject != null)
                Object.DestroyImmediate(_gameObject);
            if (_config != null)
                Object.DestroyImmediate(_config);

            var instanceField = typeof(Singleton<ComboManager>).GetField(
                "<Instance>k__BackingField",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            instanceField?.SetValue(null, null);
        }

        private void OnComboChanged(int streak) => _lastComboValue = streak;
        private void OnFocusActivated() => _focusActivatedCount++;
        private void OnFocusDeactivated() => _focusDeactivatedCount++;

        [Test]
        public void CurrentStreak_StartsAtZero()
        {
            Assert.AreEqual(0, _comboManager.CurrentStreak);
        }

        [Test]
        public void HandleEnemyTargeted_IncrementsStreak()
        {
            EventBus.RaiseEnemyTargeted(null);
            Assert.AreEqual(1, _comboManager.CurrentStreak);

            EventBus.RaiseEnemyTargeted(null);
            Assert.AreEqual(2, _comboManager.CurrentStreak);
        }

        [Test]
        public void HandleEnemyTargeted_RaisesComboChanged()
        {
            EventBus.RaiseEnemyTargeted(null);
            Assert.AreEqual(1, _lastComboValue);
        }

        [Test]
        public void HandleMiss_ResetsStreakToZero()
        {
            EventBus.RaiseEnemyTargeted(null);
            Assert.AreEqual(1, _comboManager.CurrentStreak);

            EventBus.RaiseDrawingMissed();
            Assert.AreEqual(0, _comboManager.CurrentStreak);
        }

        [Test]
        public void HandleDrawingFailed_ResetsStreakToZero()
        {
            EventBus.RaiseEnemyTargeted(null);
            EventBus.RaiseDrawingFailed();
            Assert.AreEqual(0, _comboManager.CurrentStreak);
        }

        [Test]
        public void SuppressedHeartLossReset_DoesNotResetStreak()
        {
            EventBus.RaiseEnemyTargeted(null);
            EventBus.RaiseEnemyTargeted(null);
            Assert.AreEqual(2, _comboManager.CurrentStreak);

            _comboManager.SuppressNextHeartLossResets(1);
            EventBus.RaiseHeartsChanged(2);

            Assert.AreEqual(2, _comboManager.CurrentStreak);
        }
    }
}
