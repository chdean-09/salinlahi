using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Core
{
    /// <summary>
    /// The duck envelope itself, which the EditMode tests cannot reach: it is a coroutine
    /// driven by unscaled time, so it needs real frames to run.
    ///
    /// This matters more than a normal audio nicety. The pronunciation clip is the game's
    /// phonological-loop mechanism, and after the loudness pass it still sits only ~2.5 dB
    /// over the music bed -- ducking is what actually makes the syllable read. A duck that
    /// never recovers is silent in the worst way: the music simply stays quiet for the rest
    /// of the session and nothing errors.
    /// </summary>
    [TestFixture]
    public sealed class AudioDuckingPlayModeTests
    {
        private GameObject _go;
        private AudioManager _audio;
        private AudioSource _bgm;
        private AudioSource _sfx;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("AudioManager_DuckTest");
            _audio = _go.AddComponent<AudioManager>();
            _bgm = _go.AddComponent<AudioSource>();
            _sfx = _go.AddComponent<AudioSource>();

            Set("_bgmSource", _bgm);
            Set("_sfxSource", _sfx);
            Set("_duckBgmDuringPronunciation", true);
            Set("_pronunciationDuckLevel", 0.3f);
            Set("_pronunciationDuckFadeOutSeconds", 0.05f);
            Set("_pronunciationDuckHoldSeconds", 0.05f);
            Set("_pronunciationDuckFadeInSeconds", 0.05f);

            Invoke("Awake");
            _audio.SetMasterVolume(1f);
            _audio.SetBgmVolume(1f);
            Invoke("ApplyVolumes");
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (_go != null) Object.DestroyImmediate(_go);

            FieldInfo instance = typeof(Singleton<AudioManager>).GetField(
                "<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            instance?.SetValue(null, null);
        }

        [UnityTest]
        public IEnumerator Duck_DipsTheMusicThenBringsItBack()
        {
            Assert.AreEqual(1f, _bgm.volume, 0.001f, "precondition: music at full level");

            Invoke("DuckBgmForPronunciation", 0.05f);

            // Partway through the dip the music must be measurably down.
            yield return new WaitForSecondsRealtime(0.06f);
            Assert.Less(_bgm.volume, 0.9f,
                "The music should be ducked while the syllable is playing.");

            // ... and fully restored once the envelope completes.
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(1f, _bgm.volume, 0.01f,
                "The music must come back up; a duck that never releases leaves the game quiet "
                + "for the rest of the session with no error to notice.");
        }

        [UnityTest]
        public IEnumerator Duck_RecoversEvenWhileTheGameIsPaused()
        {
            // Learning cards pause the game, and that is exactly when syllables play.
            Time.timeScale = 0f;

            Invoke("DuckBgmForPronunciation", 0.05f);
            yield return new WaitForSecondsRealtime(0.06f);
            Assert.Less(_bgm.volume, 0.9f, "Should still duck behind a paused screen.");

            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(1f, _bgm.volume, 0.01f,
                "The envelope runs on unscaled time, so timeScale = 0 must not strand the duck.");
        }

        [UnityTest]
        public IEnumerator RetriggeringDuck_HoldsOneDipInsteadOfStacking()
        {
            Invoke("DuckBgmForPronunciation", 0.05f);
            yield return new WaitForSecondsRealtime(0.06f);
            float firstDip = _bgm.volume;

            Invoke("DuckBgmForPronunciation", 0.05f);
            yield return new WaitForSecondsRealtime(0.06f);

            Assert.GreaterOrEqual(_bgm.volume, firstDip - 0.05f,
                "A rapid run of cards must hold one continuous dip, not step the music further "
                + "down with each syllable.");

            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(1f, _bgm.volume, 0.01f, "and still recover afterwards");
        }

        [UnityTest]
        public IEnumerator PlayPronunciation_DucksTheMusic_EndToEnd()
        {
            // The whole point of the feature, exercised through the real entry point rather
            // than by poking the duck directly: everything that plays a syllable funnels
            // through PlayPronunciation, so if this link is wrong the feature is dead no
            // matter how well the envelope behaves.
            AudioClip clip = AudioClip.Create("pronunciation-test", 22050, 1, 22050, false);
            clip.SetData(new float[22050], 0);

            Assert.AreEqual(1f, _bgm.volume, 0.001f, "precondition: music at full level");

            _audio.PlayPronunciation(clip);

            yield return new WaitForSecondsRealtime(0.06f);
            Assert.Less(_bgm.volume, 0.9f,
                "Playing a syllable must duck the music -- it sits only ~2.5 dB over the bed "
                + "and is the phonological-loop mechanism the learning model depends on.");

            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(1f, _bgm.volume, 0.01f, "and the music must come back afterwards");

            Object.DestroyImmediate(clip);
        }

        private void Set(string field, object value)
        {
            FieldInfo f = typeof(AudioManager).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"Missing field '{field}' on AudioManager.");
            f.SetValue(_audio, value);
        }

        private void Invoke(string method, params object[] args)
        {
            MethodInfo m = typeof(AudioManager).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(m, $"Missing method '{method}' on AudioManager.");
            m.Invoke(_audio, args);
        }
    }
}
