using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class LevelConfigResolverTests
    {
        private LevelConfigSO _currentLevel;
        private LevelConfigSO _registryLevel;
        private LevelConfigSO _fallbackLevel;

        [TearDown]
        public void TearDown()
        {
            Destroy(_currentLevel);
            Destroy(_registryLevel);
            Destroy(_fallbackLevel);
            PlayerPrefs.DeleteKey(ProgressManager.SelectedLevelKey);
        }

        [Test]
        public void ResolveSelected_UsesRegistryWhenCurrentLevelIsNull()
        {
            PlayerPrefs.SetInt(ProgressManager.SelectedLevelKey, 1);
            _registryLevel = CreateLevel("Registry Level 1", 1);

            LevelConfigSO resolved = LevelConfigResolver.ResolveSelected(
                null,
                new[] { _registryLevel },
                null);

            Assert.AreSame(_registryLevel, resolved);
        }

        [Test]
        public void ResolveSelected_IgnoresStaleCurrentLevel()
        {
            PlayerPrefs.SetInt(ProgressManager.SelectedLevelKey, 1);
            _currentLevel = CreateLevel("Current Level 2", 2);
            _registryLevel = CreateLevel("Registry Level 1", 1);

            LevelConfigSO resolved = LevelConfigResolver.ResolveSelected(
                _currentLevel,
                new[] { _registryLevel, _currentLevel },
                null);

            Assert.AreSame(_registryLevel, resolved);
        }

        [Test]
        public void Resolve_UsesInspectorFallbackWhenNoRegistryMatchExists()
        {
            _fallbackLevel = CreateLevel("Fallback", 1);

            LevelConfigSO resolved = LevelConfigResolver.Resolve(
                1,
                null,
                null,
                _fallbackLevel);

            Assert.AreSame(_fallbackLevel, resolved);
        }

        private static LevelConfigSO CreateLevel(string name, int levelNumber)
        {
            LevelConfigSO config = ScriptableObject.CreateInstance<LevelConfigSO>();
            config.name = name;
            config.levelNumber = levelNumber;
            return config;
        }

        private static void Destroy(Object target)
        {
            if (target != null)
                Object.DestroyImmediate(target);
        }
    }
}
