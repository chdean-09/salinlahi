using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Learning
{
    /// <summary>
    /// SALIN-202: Sound-dimension evidence flows through a defined event —
    /// OnPronunciationRequested with an audible clip records one exposure on the
    /// symbol. Headless EditMode does not run OnEnable on AddComponent (the same
    /// environment gap behind the pre-existing event-driven failures on dev), so
    /// these tests pin the handler contract by direct invocation; the OnEnable
    /// subscription line follows the class's existing OnLevelComplete /
    /// OnWaveStarted pattern and shares its lifecycle guarantees.
    /// </summary>
    [TestFixture]
    public sealed class SoundEvidenceTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            ClearSingletonInstance<ProgressManager>();
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
        }

        private ProgressManager CreateProgressManager()
        {
            GameObject gameObject = new GameObject("ProgressManager");
            _objectsToDestroy.Add(gameObject);
            ProgressManager manager = gameObject.AddComponent<ProgressManager>();
            SetSingletonInstance(manager);
            return manager;
        }

        private BaybayinCharacterSO Character(string stableId, bool withClip)
        {
            var character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = "BA";
            character.stableId = stableId;
            if (withClip)
            {
                // A real recorded clip: AudioClip.Create can return null in batch
                // mode (audio subsystem disabled), which would silently satisfy
                // the no-clip guard and hollow these tests out.
                AudioClip clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Audio/Pronunciation/BA.wav");
                Assert.IsNotNull(clip, "Test setup: the BA pronunciation asset must exist.");
                character.pronunciationClip = clip;
            }

            _objectsToDestroy.Add(character);
            return character;
        }

        private static void InvokePronunciationHandler(
            ProgressManager manager, BaybayinCharacterSO character)
        {
            MethodInfo handler = typeof(ProgressManager).GetMethod(
                "HandlePronunciationRequested", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(handler, "ProgressManager.HandlePronunciationRequested must exist.");
            handler.Invoke(manager, new object[] { character });
        }

        private static LearningEvidenceEntry FindSoundEntry(ProgressManager manager, string contentId)
        {
            LearningEvidenceBatch batch = manager.LevelEvidence.Build();
            return batch.entries.FirstOrDefault(entry =>
                entry.contentId == contentId && entry.dimension == MasteryDimension.Sound);
        }

        [Test]
        public void PronunciationWithClip_RecordsASoundExposure()
        {
            ProgressManager manager = CreateProgressManager();

            InvokePronunciationHandler(manager, Character("symbol.ba", withClip: true));

            LearningEvidenceEntry entry = FindSoundEntry(manager, "symbol.ba");
            Assert.IsNotNull(entry, "Hearing a pronunciation must record Sound evidence.");
            Assert.AreEqual(1, entry.attemptCount);
            Assert.AreEqual(1, entry.successCount);
            Assert.AreEqual(LearningContentKind.Symbol, entry.contentKind);
        }

        [Test]
        public void PronunciationWithoutClip_RecordsNothing()
        {
            ProgressManager manager = CreateProgressManager();

            InvokePronunciationHandler(manager, Character("symbol.ma", withClip: false));

            Assert.IsNull(FindSoundEntry(manager, "symbol.ma"),
                "Nothing audible played, so no Sound exposure may be recorded.");
        }

        [Test]
        public void RepeatedPronunciations_AccumulateExposures()
        {
            ProgressManager manager = CreateProgressManager();
            BaybayinCharacterSO character = Character("symbol.na", withClip: true);

            InvokePronunciationHandler(manager, character);
            InvokePronunciationHandler(manager, character);

            LearningEvidenceEntry entry = FindSoundEntry(manager, "symbol.na");
            Assert.IsNotNull(entry);
            Assert.AreEqual(2, entry.attemptCount);
        }

        private static void SetSingletonInstance<T>(T instance) where T : MonoBehaviour
        {
            PropertyInfo property = typeof(Singleton<T>).GetProperty(
                "Instance", BindingFlags.Static | BindingFlags.Public);
            MethodInfo setter = property?.GetSetMethod(nonPublic: true);
            Assert.IsNotNull(setter);
            setter.Invoke(null, new object[] { instance });
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            PropertyInfo property = typeof(Singleton<T>).GetProperty(
                "Instance", BindingFlags.Static | BindingFlags.Public);
            MethodInfo setter = property?.GetSetMethod(nonPublic: true);
            Assert.IsNotNull(setter);
            setter.Invoke(null, new object[] { null });
        }
    }
}
