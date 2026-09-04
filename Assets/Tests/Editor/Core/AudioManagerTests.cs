using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Core
{
    [TestFixture]
    public class AudioManagerTests
    {
        private GameObject _managerGo;
        private AudioManager _audioManager;
        private AudioSource _bgmSource;
        private AudioSource _sfxSource;
        private AudioClip _baseHitClip;

        [SetUp]
        public void SetUp()
        {
            _managerGo = new GameObject("AudioManager_Test");
            _audioManager = _managerGo.AddComponent<AudioManager>();
            _bgmSource = _managerGo.AddComponent<AudioSource>();
            _sfxSource = _managerGo.AddComponent<AudioSource>();
            _baseHitClip = AudioClip.Create("base-hit-test", 4410, 1, 44100, false);

            SetPrivateField(_audioManager, "_bgmSource", _bgmSource);
            SetPrivateField(_audioManager, "_sfxSource", _sfxSource);
            SetPrivateField(_audioManager, "_baseHitClips", new[] { _baseHitClip });
            SetPrivateField(_audioManager, "_baseHitPitchMin", 0.9f);
            SetPrivateField(_audioManager, "_baseHitPitchMax", 1.1f);

            InvokePrivateMethod(_audioManager, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            if (_baseHitClip != null)
                Object.DestroyImmediate(_baseHitClip);

            if (_managerGo != null)
                Object.DestroyImmediate(_managerGo);

            FieldInfo instanceField = typeof(Singleton<AudioManager>).GetField(
                "<Instance>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);
            instanceField?.SetValue(null, null);
        }

        [Test]
        public void PlayBaseHitSound_DoesNotMutatePrimarySfxSourcePitch()
        {
            _sfxSource.pitch = 1.35f;

            InvokePrivateMethod(_audioManager, "PlayBaseHitSound", 1);

            Assert.AreEqual(1.35f, _sfxSource.pitch, 0.0001f,
                "Base-hit playback must not alter the primary SFX source pitch used by chain/AOE audio.");
        }

        [TestCase("MainMenu", "Home")]
        [TestCase("Gameplay", "Gameplay")]
        [TestCase("Level_01_Tutorial", "Gameplay")]
        [TestCase("Bootstrap", "None")]
        public void ResolveContext_MapsSceneNamesAsExpected(string sceneName, string expectedContextName)
        {
            MethodInfo method = typeof(AudioManager).GetMethod("ResolveContext", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "ResolveContext should exist for scene->BGM context routing.");

            object context = method.Invoke(null, new object[] { sceneName });
            Assert.IsNotNull(context);
            Assert.AreEqual(expectedContextName, context.ToString());
        }

        [Test]
        public void ApplyContextBgmForScene_MainMenuSetsHomeClipOnBgmSource()
        {
            AudioClip homeClip = AudioClip.Create("home-clip-test", 4410, 1, 44100, false);
            try
            {
                SetPrivateField(_audioManager, "_homeScreenBgmClip", homeClip);

                MethodInfo method = typeof(AudioManager).GetMethod("ApplyContextBgmForScene", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(method, "ApplyContextBgmForScene should exist.");
                method.Invoke(_audioManager, new object[] { "MainMenu" });

                Assert.AreEqual(homeClip, _bgmSource.clip, "MainMenu context should route to the home-screen BGM clip.");
                Assert.IsTrue(_bgmSource.loop, "Context BGM should always loop.");
            }
            finally
            {
                Object.DestroyImmediate(homeClip);
            }
        }

        // ---- Pronunciation ducking (BGM dips under a syllable) ----------------------------
        //
        // The duck is a multiplier kept separate from _bgmScale on purpose: _bgmScale belongs
        // to the fade/crossfade system and is reset to 1 at several points, so folding the two
        // together would let a scene crossfade cancel a duck mid-syllable or strand the music
        // quiet. These tests pin that separation, because nothing else would catch it -- a
        // stuck duck is silent, literally.

        [Test]
        public void ApplyVolumes_AppliesTheDuckMultiplierToBgm()
        {
            _audioManager.SetMasterVolume(1f);
            _audioManager.SetBgmVolume(1f);

            SetPrivateField(_audioManager, "_bgmDuck", 0.35f);
            InvokePrivateMethod(_audioManager, "ApplyVolumes");

            Assert.AreEqual(0.35f, _bgmSource.volume, 0.0001f,
                "A duck must scale the BGM source; the syllable is inaudible otherwise.");
        }

        [Test]
        public void Duck_ComposesWithBgmScale_RatherThanReplacingIt()
        {
            _audioManager.SetMasterVolume(1f);
            _audioManager.SetBgmVolume(0.5f);

            SetPrivateField(_audioManager, "_bgmScale", 0.5f);
            SetPrivateField(_audioManager, "_bgmDuck", 0.4f);
            InvokePrivateMethod(_audioManager, "ApplyVolumes");

            Assert.AreEqual(0.5f * 0.5f * 0.4f, _bgmSource.volume, 0.0001f,
                "Duck, fade scale and the player's slider must all multiply; if any one wins "
                + "outright, either the fade jumps or the duck is lost.");
        }

        [Test]
        public void VolumeSliderChangeDuringADuck_KeepsTheDuck()
        {
            SetPrivateField(_audioManager, "_bgmDuck", 0.35f);

            _audioManager.SetBgmVolume(0.8f);

            Assert.AreEqual(0.8f * 0.35f, _bgmSource.volume, 0.0001f,
                "Moving the BGM slider mid-syllable must not undo the duck.");
        }

        [Test]
        public void CancelBgmDuck_RestoresFullBgmLevel()
        {
            _audioManager.SetMasterVolume(1f);
            _audioManager.SetBgmVolume(1f);
            SetPrivateField(_audioManager, "_bgmDuck", 0.2f);
            InvokePrivateMethod(_audioManager, "ApplyVolumes");

            InvokePrivateMethod(_audioManager, "CancelBgmDuck");

            Assert.AreEqual(1f, _bgmSource.volume, 0.0001f,
                "A duck interrupted by a scene change must not leave the music held down.");
        }

        [Test]
        public void SceneLoad_ClearsAnInFlightDuck()
        {
            _audioManager.SetMasterVolume(1f);
            _audioManager.SetBgmVolume(1f);
            SetPrivateField(_audioManager, "_bgmDuck", 0.25f);

            InvokePrivateMethod(_audioManager, "CancelBgmDuck");

            Assert.AreEqual(
                1f,
                (float)typeof(AudioManager)
                    .GetField("_bgmDuck", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_audioManager),
                0.0001f,
                "Leaving a level mid-syllable must reset the duck for the next scene.");
        }

        [Test]
        public void DuckingDisabled_LeavesBgmUntouched()
        {
            SetPrivateField(_audioManager, "_duckBgmDuringPronunciation", false);
            _audioManager.SetMasterVolume(1f);
            _audioManager.SetBgmVolume(1f);
            InvokePrivateMethod(_audioManager, "ApplyVolumes");
            float before = _bgmSource.volume;

            InvokePrivateMethod(_audioManager, "DuckBgmForPronunciation", 0.5f);

            Assert.AreEqual(before, _bgmSource.volume, 0.0001f,
                "The feature must be switchable off from the inspector without side effects.");
        }

        // --- Silence trimming -------------------------------------------------
        // The shipped UI clips carry long dead tails (the exit/back clip is 0.39s of sound over an
        // 8.04s file). PlayOneShot holds a voice for the whole clip, so the tail kept a voice alive
        // across the scene load that a back press triggers.

        [Test]
        public void TrimSilence_RemovesTheDeadTailThatHeldTheVoiceOpen()
        {
            const int frequency = 44100;
            AudioClip source = MakeClip("tone-then-dead-air", frequency, audibleSeconds: 0.1f, totalSeconds: 1f);
            try
            {
                AudioClip trimmed = InvokeTrim(source);

                Assert.AreNotSame(source, trimmed, "A clip with a dead tail should be rebuilt, not passed through.");
                Assert.Less(trimmed.length, 0.2f,
                    "The 0.9s of silence after the tone must not survive into the played clip.");
            }
            finally { Object.DestroyImmediate(source); }
        }

        [Test]
        public void TrimSilence_KeepsAPadSoADecayingTailIsNotCutToAClick()
        {
            const int frequency = 44100;
            AudioClip source = MakeClip("tone-then-dead-air", frequency, audibleSeconds: 0.1f, totalSeconds: 1f);
            try
            {
                SetPrivateField(_audioManager, "_trailingSilencePadSeconds", 0.03f);

                AudioClip trimmed = InvokeTrim(source);

                Assert.Greater(trimmed.length, 0.1f,
                    "Cutting exactly on the last audible sample would end the clip on a step.");
            }
            finally { Object.DestroyImmediate(source); }
        }

        [Test]
        public void TrimSilence_LeavesAClipWithoutSilenceUntouched()
        {
            const int frequency = 44100;
            AudioClip source = MakeClip("all-tone", frequency, audibleSeconds: 0.5f, totalSeconds: 0.5f);
            try
            {
                Assert.AreSame(source, InvokeTrim(source),
                    "A clip with nothing to trim should be returned as-is rather than copied.");
            }
            finally { Object.DestroyImmediate(source); }
        }

        [Test]
        public void PlayBGM_RestartsATrackThatWasFadedOutAndStopped()
        {
            AudioClip track = AudioClip.Create("bgm-restart-test", 4410, 1, 44100, false);
            try
            {
                _audioManager.PlayBGM(track);
                _bgmSource.Stop();
                Assert.IsFalse(_bgmSource.isPlaying);

                _audioManager.PlayBGM(track);

                Assert.AreSame(track, _bgmSource.clip);
                Assert.IsTrue(_bgmSource.isPlaying,
                    "Stop() leaves .clip assigned, so guarding on the clip alone made the same track "
                    + "unrestartable for the rest of the session.");
            }
            finally { Object.DestroyImmediate(track); }
        }

        private static AudioClip MakeClip(string name, int frequency, float audibleSeconds, float totalSeconds)
        {
            int totalSamples = Mathf.RoundToInt(totalSeconds * frequency);
            int audibleSamples = Mathf.RoundToInt(audibleSeconds * frequency);
            // Alternating full-amplitude samples rather than a sine: a sine starts and ends on a
            // zero crossing, so its first and last samples sit under the silence threshold and the
            // "nothing to trim" case would trim a sample after all.
            float[] data = new float[totalSamples];
            for (int i = 0; i < audibleSamples; i++)
                data[i] = (i % 2 == 0) ? 0.5f : -0.5f;

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, frequency, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip InvokeTrim(AudioClip source)
        {
            MethodInfo method = typeof(AudioManager).GetMethod(
                "TrimLeadingSilence",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(AudioClip), typeof(float), typeof(float) },
                null);
            Assert.IsNotNull(method, "TrimLeadingSilence(clip, threshold, maxLeadingTrim) should exist.");
            return (AudioClip)method.Invoke(_audioManager, new object[] { source, 0.0025f, 0.12f });
        }

        // --- Recognition feedback & outcome stingers --------------------------

        [Test]
        public void WrongGlyphCue_ListensToDrawingFailed_NotRecognitionResolved()
        {
            InvokePrivateMethod(_audioManager, "OnEnable");
            try
            {
                Assert.IsTrue(SubscribesTo("OnDrawingFailed"),
                    "The failure cue must be bound to the commit-path event.");
                Assert.IsFalse(SubscribesTo("OnRecognitionResolved"),
                    "PreviewRecognize raises OnRecognitionResolved continuously while the player "
                    + "is still drawing, so binding the error tone there would fire it on every "
                    + "preview frame.");
                Assert.IsTrue(SubscribesTo("OnCharacterRecognized"),
                    "The success cue must be bound.");
            }
            finally { InvokePrivateMethod(_audioManager, "OnDisable"); }
        }

        [Test]
        public void VictorySting_PlaysOnItsOwnSourceAndDucksTheBgm()
        {
            AudioClip sting = MakeClip("victory", 44100, audibleSeconds: 0.5f, totalSeconds: 0.5f);
            try
            {
                SetPrivateField(_audioManager, "_victoryStingClip", sting);
                SetPrivateField(_audioManager, "_duckBgmDuringSting", true);

                InvokePrivateMethod(_audioManager, "PlayVictorySting");

                AudioSource stingSource = (AudioSource)GetPrivateField(_audioManager, "_stingSfxSource");
                Assert.IsNotNull(stingSource, "Stingers need a dedicated source.");
                Assert.AreNotSame(_sfxSource, stingSource,
                    "Sharing the SFX source would make the sting unstoppable without cutting other SFX.");
                Assert.IsNotNull(stingSource.clip);
                Assert.IsNotNull(GetPrivateField(_audioManager, "_bgmDuckRoutine"),
                    "Both outcome screens leave the gameplay BGM looping, so the sting must duck it.");
            }
            finally { Object.DestroyImmediate(sting); }
        }

        [Test]
        public void SceneLoad_StopsAnInFlightSting()
        {
            AudioClip sting = MakeClip("victory", 44100, audibleSeconds: 0.5f, totalSeconds: 0.5f);
            try
            {
                SetPrivateField(_audioManager, "_victoryStingClip", sting);
                InvokePrivateMethod(_audioManager, "PlayVictorySting");

                InvokePrivateMethod(_audioManager, "StopSting");

                AudioSource stingSource = (AudioSource)GetPrivateField(_audioManager, "_stingSfxSource");
                Assert.IsFalse(stingSource.isPlaying,
                    "The victory sting runs ~12s and the player can leave the screen in two; "
                    + "without this it plays on into the next scene.");
            }
            finally { Object.DestroyImmediate(sting); }
        }

        [Test]
        public void StingVolume_TracksTheSfxSlider()
        {
            InvokePrivateMethod(_audioManager, "EnsureStingSfxSource");
            SetPrivateField(_audioManager, "_stingVolume", 1f);
            _audioManager.SetMasterVolume(1f);
            _audioManager.SetSfxVolume(0.5f);

            AudioSource stingSource = (AudioSource)GetPrivateField(_audioManager, "_stingSfxSource");
            Assert.AreEqual(0.5f, stingSource.volume, 0.0001f,
                "A sting that ignored the SFX slider would be the one sound a player cannot turn down.");
        }

        // --- Reward & threat cues ---------------------------------------------

        [Test]
        public void EnemyDeaths_AreCappedPerBurst()
        {
            AudioClip death = MakeClip("death", 44100, audibleSeconds: 0.2f, totalSeconds: 0.2f);
            try
            {
                SetPrivateField(_audioManager, "_enemyDefeatedClip", death);
                SetPrivateField(_audioManager, "_maxEnemyDeathsPerBurst", 3);
                SetPrivateField(_audioManager, "_enemyDeathBurstWindow", 10f);

                for (int i = 0; i < 12; i++)
                    InvokePrivateMethod(_audioManager, "PlayEnemyDefeatedSfx", (BaybayinCharacterSO)null);

                Assert.AreEqual(3, (int)GetPrivateField(_audioManager, "_enemyDeathsThisBurst"),
                    "A mass clear raises one OnEnemyDefeated per enemy in a single frame; "
                    + "uncapped that is a burst of noise rather than a set of kills.");
            }
            finally { Object.DestroyImmediate(death); }
        }

        [Test]
        public void EnemyDeaths_StartAFreshBurstAfterTheWindow()
        {
            AudioClip death = MakeClip("death", 44100, audibleSeconds: 0.2f, totalSeconds: 0.2f);
            try
            {
                SetPrivateField(_audioManager, "_enemyDefeatedClip", death);
                SetPrivateField(_audioManager, "_maxEnemyDeathsPerBurst", 2);
                SetPrivateField(_audioManager, "_enemyDeathBurstWindow", 0.14f);

                for (int i = 0; i < 5; i++)
                    InvokePrivateMethod(_audioManager, "PlayEnemyDefeatedSfx", (BaybayinCharacterSO)null);

                // Pretend the window elapsed rather than stalling the test on wall-clock time.
                SetPrivateField(_audioManager, "_enemyDeathBurstStartedAt", -1f);
                InvokePrivateMethod(_audioManager, "PlayEnemyDefeatedSfx", (BaybayinCharacterSO)null);

                Assert.AreEqual(1, (int)GetPrivateField(_audioManager, "_enemyDeathsThisBurst"),
                    "The cap must throttle a burst, not permanently silence enemy deaths.");
            }
            finally { Object.DestroyImmediate(death); }
        }

        [Test]
        public void EnemyDeath_DoesNotRepitchTheBaseHitSource()
        {
            AudioClip death = MakeClip("death", 44100, audibleSeconds: 0.2f, totalSeconds: 0.2f);
            try
            {
                SetPrivateField(_audioManager, "_enemyDefeatedClip", death);
                InvokePrivateMethod(_audioManager, "EnsureBaseHitSfxSource");
                AudioSource baseHit = (AudioSource)GetPrivateField(_audioManager, "_baseHitSfxSource");
                baseHit.pitch = 1.23f;

                InvokePrivateMethod(_audioManager, "PlayEnemyDefeatedSfx", (BaybayinCharacterSO)null);

                Assert.AreEqual(1.23f, baseHit.pitch, 0.0001f,
                    "Pitch belongs to the source, so a death must not re-pitch a base hit in flight.");
            }
            finally { Object.DestroyImmediate(death); }
        }

        [Test]
        public void LockedLevel_StaysSilentRatherThanPlayingTheAffirmativeClick()
        {
            SetPrivateField(_audioManager, "_levelLockedClip", null);
            _sfxSource.pitch = 1f;

            Assert.DoesNotThrow(() => _audioManager.PlayLevelLockedDenied(),
                "With no denial clip assigned the refused press must fall silent, never fall back "
                + "to the affirmative click that made a rejection sound like an acceptance.");
        }

        private static bool SubscribesTo(string eventName)
        {
            FieldInfo field = typeof(EventBus).GetField(
                eventName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"EventBus.{eventName} should exist.");
            if (field.GetValue(null) is not System.Delegate handler) return false;
            foreach (System.Delegate d in handler.GetInvocationList())
                if (d.Target is AudioManager) return true;
            return false;
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            return field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, args);
        }
    }
}
