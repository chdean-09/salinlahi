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
