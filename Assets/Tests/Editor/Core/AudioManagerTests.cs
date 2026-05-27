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
