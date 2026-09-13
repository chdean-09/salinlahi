using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.UI
{
    /// <summary>
    /// SALIN-253 (AC-4) — the cross-scene "open Level Select on era N" handoff.
    ///
    /// Level Select is a separate scene, so "Enter Next Era" cannot call ShowEra directly: the
    /// screen it wants to steer does not exist yet when the button is pressed.
    /// <see cref="EraCompletionScreenUI.PendingEraIndex"/> is the narrowest thing that survives
    /// SceneLoader.LoadLevelSelect() — a static is not MonoBehaviour state — without touching
    /// the save schema, which has already moved twice this sprint.
    ///
    /// THE CONSUME-ONCE CONTRACT IS THE WHOLE RISK. A pending index that is set and never
    /// cleared would pin Level Select to Era 2 for the rest of the session: the player would
    /// press Back from a level, land on Ugnayan again, and nothing — no compile error, no
    /// validator warning, no other test — would report it. That is what the second test pins.
    /// </summary>
    [TestFixture]
    public sealed class LevelSelectPendingEraTests
    {
        private readonly List<Object> _created = new();
        private GameObject _levelSelectObject;

        [SetUp]
        public void SetUp() =>
            EraCompletionScreenUI.PendingEraIndex = EraCompletionScreenUI.NoPendingEra;

        [TearDown]
        public void TearDown()
        {
            // A leaked pending index would leak into every later fixture in the run, which is
            // the session-scoped version of the very defect this file guards.
            EraCompletionScreenUI.PendingEraIndex = EraCompletionScreenUI.NoPendingEra;

            if (_levelSelectObject != null)
            {
                Object.DestroyImmediate(_levelSelectObject);
                _levelSelectObject = null;
            }

            foreach (Object asset in _created)
                if (asset != null)
                    Object.DestroyImmediate(asset);
            _created.Clear();
        }

        [Test]
        public void Start_OpensOnThePendingEraIndex()
        {
            LevelSelectUI levelSelect = CreateLevelSelect(
                Era("Ugat", 1), Era("Ugnayan", 2), Era("Pamana", 3));
            EraCompletionScreenUI.PendingEraIndex = 1;

            InvokePrivate(levelSelect, "Start");

            Assert.AreEqual(
                1,
                CurrentEraIndex(levelSelect),
                "Enter Next Era after Ugat must land the player on Ugnayan (AC-4). Without the "
                + "handoff, Start() always opens on index 0 and the button appears to do "
                + "nothing but return to Ugat.");
        }

        [Test]
        public void Start_WithNoPendingEra_StillOpensOnTheFirstEra()
        {
            LevelSelectUI levelSelect = CreateLevelSelect(
                Era("Ugat", 1), Era("Ugnayan", 2), Era("Pamana", 3));

            InvokePrivate(levelSelect, "Start");

            Assert.AreEqual(
                0,
                CurrentEraIndex(levelSelect),
                "Entering Level Select from the main menu is unchanged. If this ever failed, "
                + "the previous test would prove nothing — the screen would be landing on "
                + "era 1 for an unrelated reason.");
        }

        [Test]
        public void PendingEraIndex_IsConsumedOnce_AndResetsToMinusOne()
        {
            EraCompletionScreenUI.PendingEraIndex = 2;

            Assert.AreEqual(2, EraCompletionScreenUI.ConsumePendingEraIndex());
            Assert.AreEqual(
                EraCompletionScreenUI.NoPendingEra,
                EraCompletionScreenUI.ConsumePendingEraIndex(),
                "The second read must report nothing pending. A value that survives its first "
                + "read pins Level Select to that era for the rest of the session.");
            Assert.AreEqual(
                EraCompletionScreenUI.NoPendingEra,
                EraCompletionScreenUI.PendingEraIndex,
                "Consuming must also clear the property itself, not just the returned value.");
        }

        [Test]
        public void Start_WithAnOutOfRangePendingEra_FallsBackToTheFirstEra()
        {
            LevelSelectUI levelSelect = CreateLevelSelect(Era("Ugat", 1), Era("Ugnayan", 2));
            EraCompletionScreenUI.PendingEraIndex = 7;

            InvokePrivate(levelSelect, "Start");

            Assert.AreEqual(
                0,
                CurrentEraIndex(levelSelect),
                "EraBoundary.IndexOfEra returns -1 for an unknown era and the campaign can be "
                + "shorter than a stale request. Neither may throw or clamp the player onto "
                + "the last era; both mean 'do not steer the screen'.");
        }

        // ----- helpers -------------------------------------------------------------------

        private LevelSelectUI CreateLevelSelect(params EraConfigSO[] eras)
        {
            _levelSelectObject = new GameObject("LevelSelectUI_Test");
            LevelSelectUI levelSelect = _levelSelectObject.AddComponent<LevelSelectUI>();
            SetPrivateField(levelSelect, "_eras", new List<EraConfigSO>(eras));
            return levelSelect;
        }

        private EraConfigSO Era(string eraName, int order)
        {
            EraConfigSO era = Track(ScriptableObject.CreateInstance<EraConfigSO>());
            era.eraName = eraName;
            era.order = order;
            era.levels = new List<LevelConfigSO>();
            return era;
        }

        private static int CurrentEraIndex(LevelSelectUI levelSelect)
        {
            FieldInfo field = typeof(LevelSelectUI).GetField(
                "_currentEraIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "LevelSelectUI no longer tracks '_currentEraIndex'.");
            return (int)field.GetValue(levelSelect);
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing serialized field '" + name + "'.");
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = null;
            for (System.Type type = target.GetType(); type != null && method == null; type = type.BaseType)
            {
                method = type.GetMethod(
                    methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            Assert.IsNotNull(method, "Missing method '" + methodName + "'.");
            method.Invoke(target, null);
        }

        private T Track<T>(T asset) where T : Object
        {
            _created.Add(asset);
            return asset;
        }
    }
}
