using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Plays a tutorial intro template that loops while waiting for the player to tap to proceed.
/// Supports two playback paths:
///   1. VideoClip via UnityEngine.Video.VideoPlayer (preferred when assigned).
///   2. AnimationClip via Animator (used as a placeholder/fallback).
/// </summary>
public sealed class TutorialIntroPlayer : MonoBehaviour
{
    public enum PlaybackMode { None, Video, Animation }

    [Header("Surfaces")]
    [SerializeField] private RawImage _videoSurface;
    [SerializeField] private Image _animationSurface;
    [SerializeField] private Animator _animator;

    [Header("Video")]
    [SerializeField] private VideoPlayer _videoPlayer;
    [SerializeField] private RenderTexture _videoRenderTexture;

    [Header("Interaction")]
    [SerializeField] private Button _tapCatcher;
    [SerializeField] private TMP_Text _tapToProceedLabel;
    [SerializeField] private GameObject _root;

    [Header("Animation Fallback")]
    [Tooltip("State name on the Animator used when an AnimationClip is provided as a fallback.")]
    [SerializeField] private string _animatorStateName = "TutorialPlaceholder";

    private Action _onDismissed;
    private bool _isPlaying;

    public bool IsPlaying => _isPlaying;
    public PlaybackMode CurrentMode { get; private set; } = PlaybackMode.None;

    private void Awake()
    {
        if (_root != null) _root.SetActive(false);
        // The tap-catcher is a SIBLING of _root, so toggling _root never disables it.
        // Manage it explicitly so it can't stay active full-screen (blocking drawing input
        // via IsScreenPositionOverUI) after the video is dismissed.
        SetTapCatcherActive(false);
    }

    private void SetTapCatcherActive(bool active)
    {
        if (_tapCatcher != null)
            _tapCatcher.gameObject.SetActive(active);
    }

    private void OnEnable()
    {
        if (_tapCatcher != null)
            _tapCatcher.onClick.AddListener(OnTapped);
    }

    private void OnDisable()
    {
        if (_tapCatcher != null)
            _tapCatcher.onClick.RemoveListener(OnTapped);
        if (_videoPlayer != null)
        {
            _videoPlayer.loopPointReached -= OnVideoLoopPoint;
            _videoPlayer.errorReceived -= OnVideoError;
        }
    }

    /// <summary>
    /// Pure helper: choose Video when a clip is assigned, else Animation when a clip is assigned, else None.
    /// Public/static so it can be exercised by EditMode tests.
    /// </summary>
    public static PlaybackMode SelectMode(VideoClip videoClip, AnimationClip animationClip)
        => SelectMode(videoClip != null, animationClip != null);

    public static PlaybackMode SelectMode(bool hasVideoClip, bool hasAnimationClip)
    {
        if (hasVideoClip) return PlaybackMode.Video;
        if (hasAnimationClip) return PlaybackMode.Animation;
        return PlaybackMode.None;
    }

    public IEnumerator Play(OnboardingVideoTemplate template, Action onDismissed = null)
    {
        _onDismissed = onDismissed;
        ConfigureLabel(template.tapToProceedText);

        CurrentMode = SelectMode(template.videoClip, template.animationClip);
        if (CurrentMode == PlaybackMode.None)
        {
            DebugLogger.LogWarning("TutorialIntroPlayer.Play: No video or animation clip assigned. Showing tap catcher only.");
        }

        ShowSurfaces(CurrentMode);
        if (_root != null) _root.SetActive(true);
        SetTapCatcherActive(true);
        _isPlaying = true;

        switch (CurrentMode)
        {
            case PlaybackMode.Video:
                yield return StartVideo(template.videoClip);
                break;
            case PlaybackMode.Animation:
                StartAnimation(template.animationClip);
                break;
        }

        yield return new WaitWhile(() => _isPlaying);
    }

    public void Dismiss()
    {
        if (!_isPlaying) return;
        _isPlaying = false;
        StopAllPlayback();
        if (_root != null) _root.SetActive(false);
        SetTapCatcherActive(false);
        Action callback = _onDismissed;
        _onDismissed = null;
        callback?.Invoke();
    }

    private void OnTapped() => Dismiss();

    private void ConfigureLabel(string text)
    {
        if (_tapToProceedLabel == null) return;
        _tapToProceedLabel.text = string.IsNullOrEmpty(text) ? "Tap anywhere to continue" : text;
    }

    private void ShowSurfaces(PlaybackMode mode)
    {
        if (_videoSurface != null) _videoSurface.gameObject.SetActive(mode == PlaybackMode.Video);
        if (_animationSurface != null) _animationSurface.gameObject.SetActive(mode == PlaybackMode.Animation);
    }

    private IEnumerator StartVideo(VideoClip clip)
    {
        if (_videoPlayer == null)
        {
            DebugLogger.LogError("TutorialIntroPlayer: VideoPlayer reference missing.");
            yield break;
        }
        _videoPlayer.clip = clip;
        _videoPlayer.isLooping = true;
        if (_videoRenderTexture != null && _videoSurface != null)
            _videoSurface.texture = _videoRenderTexture;
        _videoPlayer.errorReceived += OnVideoError;
        _videoPlayer.loopPointReached += OnVideoLoopPoint;
        _videoPlayer.Prepare();
        while (!_videoPlayer.isPrepared)
            yield return null;
        _videoPlayer.Play();
    }

    private void StartAnimation(AnimationClip clip)
    {
        if (_animator == null)
        {
            DebugLogger.LogError("TutorialIntroPlayer: Animator reference missing for animation fallback.");
            return;
        }
        if (!string.IsNullOrEmpty(_animatorStateName))
            _animator.Play(_animatorStateName, 0, 0f);
    }

    private void StopAllPlayback()
    {
        if (_videoPlayer != null && _videoPlayer.isPlaying)
            _videoPlayer.Stop();
    }

    private void OnVideoLoopPoint(VideoPlayer source) { /* looping is built-in; hook here if needed */ }

    private void OnVideoError(VideoPlayer source, string message)
    {
        DebugLogger.LogError($"TutorialIntroPlayer: VideoPlayer error: {message}");
    }
}
