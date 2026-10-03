using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    public sealed class ReleaseQaLifecycleTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator Shrine_DamageRecovery_ExpiresAfterOneSecond()
        {
            _root = new GameObject("QA shrine", typeof(HeartSystem), typeof(PlayerBase));
            yield return null;
            var hearts = _root.GetComponent<HeartSystem>();
            int damageEvents = 0;
            void Record(int amount) => damageEvents += amount;
            EventBus.OnBaseDamageApplied += Record;
            try
            {
                EventBus.RaiseBaseHit(1);
                EventBus.RaiseBaseHit(1);
                Assert.AreEqual(2, hearts.GetCurrentHearts());
                Assert.AreEqual(1, damageEvents);
                yield return new WaitForSeconds(1.05f);
                EventBus.RaiseBaseHit(1);
                Assert.AreEqual(1, hearts.GetCurrentHearts());
                Assert.AreEqual(2, damageEvents);
            }
            finally { EventBus.OnBaseDamageApplied -= Record; }
        }

        [UnityTest]
        public IEnumerator MechanicEntryGrace_HoldsThenResumesMovement()
        {
            _root = new GameObject("QA mover", typeof(BoxCollider2D), typeof(EnemyMover));
            var mover = _root.GetComponent<EnemyMover>();
            mover.SetSpeed(1f);
            mover.GiveEntryGrace(0.1f);
            yield return new WaitForSeconds(0.05f);
            Assert.AreEqual(0f, _root.transform.position.y, 0.001f);
            yield return new WaitForSeconds(0.15f);
            Assert.Less(_root.transform.position.y, 0f);
        }

        [UnityTest]
        public IEnumerator MechanicEntryGrace_StartsAtVisibleEntryRatherThanOffScreenSpawn()
        {
            _root = new GameObject("QA entering mover", typeof(BoxCollider2D), typeof(EnemyMover));
            _root.transform.position = Vector3.up;
            var mover = _root.GetComponent<EnemyMover>();
            mover.SetSpeed(1f);
            mover.GiveEntryGrace(0.2f, 0.9f);
            yield return new WaitForSeconds(0.16f);
            float entryY = _root.transform.position.y;
            Assert.Less(entryY, 1f, "The off-screen approach must keep moving.");
            yield return new WaitForSeconds(0.06f);
            Assert.AreEqual(entryY, _root.transform.position.y, 0.001f);
            yield return new WaitForSeconds(0.25f);
            Assert.Less(_root.transform.position.y, entryY);
        }

        [UnityTest]
        public IEnumerator MechanicBanner_HideOwner_HidesTextAndBackground()
        {
            _root = new GameObject("QA canvas", typeof(Canvas));
            var ownerObject = new GameObject("Lifetime banner", typeof(RectTransform), typeof(CanvasGroup));
            ownerObject.transform.SetParent(_root.transform, false);
            var textObject = new GameObject("Ability text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(ownerObject.transform, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = "Two paths carry different symbols.";
            CombatNotificationBanner.Configure(text, 0.39f, ownerObject.GetComponent<CanvasGroup>());
            yield return null;
            Assert.IsTrue(text.isActiveAndEnabled);
            ownerObject.SetActive(false);
            yield return null;
            Assert.IsFalse(text.isActiveAndEnabled);
            Assert.IsFalse(ownerObject.GetComponent<CombatNotificationBanner>().isActiveAndEnabled);
        }

        [UnityTest]
        public IEnumerator Pronunciation_SkipFadesBeforePlayingTheNextCue()
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var singleton = typeof(Singleton<AudioManager>).GetProperty("Instance");
            object previous = singleton.GetValue(null);
            singleton.SetValue(null, null);
            _root = new GameObject("QA audio");
            _root.SetActive(false);
            var bgm = _root.AddComponent<AudioSource>();
            var sfx = _root.AddComponent<AudioSource>();
            var audio = _root.AddComponent<AudioManager>();
            var clip = AudioClip.Create("QA voice", 22050, 1, 44100, false);
            float[] samples = new float[22050];
            for (int i = 0; i < samples.Length; i++) samples[i] = 0.1f;
            clip.SetData(samples, 0);
            try
            {
                void Set(string name, object value) => typeof(AudioManager).GetField(name, flags).SetValue(audio, value);
                Set("_bgmSource", bgm);
                Set("_sfxSource", sfx);
                Set("_homeScreenBgmClip", clip);
                Set("_gameplayBgmClip", clip);
                Set("_enablePronunciationModulation", false);
                Set("_duckBgmDuringPronunciation", false);
                _root.SetActive(true);
                sfx.volume = 0.8f;
                var voice = (AudioSource)typeof(AudioManager).GetField("_pronunciationSfxSource", flags).GetValue(audio);
                voice.volume = 0.8f;
                audio.PlayPronunciation(clip);
                yield return null;
                audio.FadeOutPronunciation();
                yield return new WaitForSecondsRealtime(0.025f);
                float releasingVolume = voice.volume;
                Assert.Less(releasingVolume, 0.8f);
                Assert.IsTrue(voice.isPlaying);
                audio.PlayPronunciation(clip);
                Assert.AreEqual(releasingVolume, voice.volume,
                    "A new cue must not abruptly cancel the previous cue's release.");
                yield return new WaitForSecondsRealtime(0.15f);
                Assert.AreEqual(0.8f, voice.volume, 0.001f);
                Assert.IsTrue(voice.isPlaying, "The queued cue plays after the release finishes.");
            }
            finally
            {
                Object.DestroyImmediate(_root);
                _root = null;
                Object.DestroyImmediate(clip);
                singleton.SetValue(null, previous);
            }
        }

        [UnityTest]
        public IEnumerator BlockedBadge_ShowsLock_AndPoolResetClearsIt()
        {
            _root = new GameObject("QA glyph", typeof(SpriteRenderer), typeof(EnemyGlyphBadge));
            var badge = _root.GetComponent<EnemyGlyphBadge>();
            badge.SetResolutionBlocked(true);
            Transform padlock = _root.transform.Find("ResolutionLock");
            Assert.IsNotNull(padlock);
            Assert.IsTrue(padlock.gameObject.activeSelf);
            badge.ResetForPool();
            yield return null;
            Assert.IsFalse(padlock.gameObject.activeSelf);
        }
    }
}
