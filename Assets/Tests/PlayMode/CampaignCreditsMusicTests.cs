#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CampaignCreditsMusicTests
{
    private GameObject _host;
    private AudioManager _audio;
    private AudioSource _source;
    private AudioClip _previous;
    private float _timeScale;

    [SetUp]
    public void SetUp()
    {
        _timeScale = Time.timeScale;
        Time.timeScale = 0f;
        _host = new GameObject("CreditsAudioTest");
        _host.SetActive(false);
        _audio = _host.AddComponent<AudioManager>();
        _source = _host.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        AudioSource sfx = _host.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        SetField("_bgmSource", _source);
        SetField("_sfxSource", sfx);
        _host.SetActive(true);
        SetField("_masterVolume", 1f);
        SetField("_bgmVolume", 0.4f);
        _previous = AudioClip.Create("previous-music", 44100, 1, 44100, false);
        _audio.FadeInBGM(_previous, 0f, 0.43f);
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = _timeScale;
        Object.DestroyImmediate(_host);
        Object.DestroyImmediate(_previous);
    }

    [UnityTest]
    public IEnumerator EndingScreen_FadesInAndScrollsAutomaticallyWhileGameplayIsStopped()
    {
        GameObject screenHost = new GameObject("CreditsTransitionTest", typeof(RectTransform));
        try
        {
            CampaignEndingScreenUI screen = screenHost.AddComponent<CampaignEndingScreenUI>();
            screen.Present();
            CanvasGroup group = screenHost.GetComponent<CanvasGroup>();
            Assert.AreEqual(0f, group.alpha);
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.AreEqual(1f, group.alpha, 0.001f);
            RectTransform content = screenHost.transform.Find(
                "EndingBackdrop/SafeArea/EndingScroll/CreditsViewport/Credits").GetComponent<RectTransform>();
            float before = content.anchoredPosition.y;
            yield return new WaitForSecondsRealtime(1f);
            Assert.Greater(content.anchoredPosition.y, before + 10f);
            Assert.AreEqual(1, screenHost.GetComponentsInChildren<UnityEngine.UI.Button>().Length);
        }
        finally
        {
            Object.DestroyImmediate(screenHost);
        }
    }

    [UnityTest]
    public IEnumerator CreditsMusic_IsQuietAndDoesNotRestartWhenReplayed()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/Credits/credits-ambient");
        Assert.IsNotNull(clip);
        Assert.AreEqual(64f, clip.length, 0.1f);
        _audio.PlayCreditsBgm(clip);
        yield return new WaitForSecondsRealtime(3.6f);
        Assert.AreSame(clip, _source.clip);
        Assert.IsTrue(_source.loop);
        Assert.AreEqual(0.4f * 0.65f, _source.volume, 0.001f);
        float position = _source.time;
        _audio.PlayCreditsBgm(clip);
        Assert.AreEqual(position, _source.time, 0.1f);
    }

    [UnityTest]
    public IEnumerator LeavingCredits_RestoresPreviousTrackAndItsAuthoredLevel()
    {
        _audio.PlayCreditsBgm(Resources.Load<AudioClip>("Audio/Credits/credits-ambient"));
        yield return new WaitForSecondsRealtime(3.6f);
        _audio.StopCreditsBgm();
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.AreSame(_previous, _source.clip);
        Assert.AreEqual(0.4f * 0.43f, _source.volume, 0.001f);
        _audio.StopCreditsBgm();
        Assert.AreSame(_previous, _source.clip);
    }

    [UnityTest]
    public IEnumerator MutedMusic_StaysSilentThroughoutCreditsFade()
    {
        SetField("_bgmVolume", 0f);
        _audio.PlayCreditsBgm(Resources.Load<AudioClip>("Audio/Credits/credits-ambient"));
        yield return new WaitForSecondsRealtime(3.6f);
        Assert.AreEqual(0f, _source.volume);
        Assert.AreEqual(0f, _audio.BgmVolume);
    }

    [UnityTest]
    public IEnumerator LeavingCredits_DoesNotResumeMusicThatWasAlreadyStopped()
    {
        _audio.StopBGM();
        _audio.PlayCreditsBgm(Resources.Load<AudioClip>("Audio/Credits/credits-ambient"));
        yield return new WaitForSecondsRealtime(3.6f);
        _audio.StopCreditsBgm();
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.IsFalse(_source.isPlaying);
    }

    [UnityTest]
    public IEnumerator EndingScreen_StartsCreditsMusicAndRestoresItWhenClosed()
    {
        GameObject screenHost = new GameObject("CreditsScreenAudioTest", typeof(RectTransform));
        try
        {
            screenHost.AddComponent<CampaignEndingScreenUI>().Present();
            yield return new WaitForSecondsRealtime(3.6f);
            Assert.AreSame(Resources.Load<AudioClip>("Audio/Credits/credits-ambient"), _source.clip);
            screenHost.SetActive(false);
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.AreSame(_previous, _source.clip);
        }
        finally
        {
            Object.DestroyImmediate(screenHost);
        }
    }

    [UnityTest]
    public IEnumerator VictoryMusicAlreadyDucked_DoesNotJumpLouderBeforeCredits()
    {
        SetField("_bgmDuck", 0.1f);
        typeof(AudioManager).GetMethod("ApplyVolumes", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_audio, null);
        float outgoing = _source.volume;
        _audio.PlayCreditsBgm(Resources.Load<AudioClip>("Audio/Credits/credits-ambient"));
        Assert.LessOrEqual(_source.volume, outgoing + 0.001f);
        yield return new WaitForSecondsRealtime(3.6f);
        Assert.AreEqual(0.4f * 0.65f, _source.volume, 0.001f);
    }

    private void SetField(string name, object value) =>
        typeof(AudioManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_audio, value);
}
#endif
