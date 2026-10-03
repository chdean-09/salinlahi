using NUnit.Framework;
using Salinlahi.Debug;
using System.Reflection;

namespace Salinlahi.Tests.Editor.ReleaseProfile
{
    [TestFixture]
    public class ReleaseProfileGuardTests
    {
        // --- Truth-table contract (enforces the release-config guard) ---

        [Test]
        public void DevGuardTruthTableRequiresEditorOrDevDefine()
        {
            // Release build: neither symbol defined -> dev utilities MUST be off.
            Assert.IsFalse(DevBuildGuard.IsDevOnlyEnabledForSymbols(false, false),
                "Release build (no UNITY_EDITOR, no SALINLAHI_DEV) must not enable dev utilities.");
            Assert.IsTrue(DevBuildGuard.IsDevOnlyEnabledForSymbols(true, false),
                "Editor must enable dev utilities.");
            Assert.IsTrue(DevBuildGuard.IsDevOnlyEnabledForSymbols(false, true),
                "SALINLAHI_DEV must enable dev utilities.");
            Assert.IsTrue(DevBuildGuard.IsDevOnlyEnabledForSymbols(true, true));
        }

        // --- Presence-in-editor (proves the guard does not over-exclude) ---

        [Test]
        public void ProgressManagerTesterIsCompiledInEditor()
        {
            // UNITY_EDITOR is always defined in EditMode tests, so the guarded
            // dev utility must be present. The release-config contract is
            // enforced by DevGuardTruthTableRequiresEditorOrDevDefine above.
            System.Type tester = typeof(DevBuildGuard).Assembly.GetType(
                "Salinlahi.Debug.ProgressManagerTester");
            Assert.IsNotNull(tester,
                "ProgressManagerTester must be compiled under UNITY_EDITOR.");
        }

        [Test]
        public void UnlockAllLevelsIsCompiledInEditor()
        {
            MethodInfo method = typeof(ProgressManager).GetMethod(
                "UnlockAllLevels",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(method,
                "ProgressManager.UnlockAllLevels must be compiled under UNITY_EDITOR.");
        }

        [Test]
        public void TestSessionControllerStaticHintIsAlwaysCompiled()
        {
            // The static IntendedCharacterID hint is referenced by production
            // code (CombatResolver, RecognitionManager) and must remain
            // available in every configuration, including release builds.
            PropertyInfo prop = typeof(TestSessionController).GetProperty(
                "IntendedCharacterID",
                BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(prop,
                "TestSessionController.IntendedCharacterID must always be compiled.");
            Assert.AreEqual("", TestSessionController.IntendedCharacterID,
                "IntendedCharacterID must default to empty in normal gameplay.");
        }
    }
}
