using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Plays a tutorial intro template that loops while waiting for the player to tap to proceed.
/// Supports two playback paths:
///   1. VideoClip via UnityEngine.Video.VideoPlayer (preferred when assigned).
///   2. Imported GIF texture/sliced frames via RawImage.
///   3. AnimationClip via Animator (used as a placeholder/fallback).
/// </summary>
public sealed class TutorialIntroPlayer : MonoBehaviour
{
    public enum PlaybackMode { None, Video, Gif, Animation }

    private static readonly Vector2 DefaultGifSurfaceSize = new(420f, 420f);
    private static readonly Vector2 DefaultGifSurfaceAnchoredPosition = new(0f, 560f);

    [Header("Surfaces")]
    [SerializeField] private RawImage _videoSurface;
    [SerializeField] private RawImage _gifSurface;
    [SerializeField] private Image _animationSurface;
    [SerializeField] private Animator _animator;

    [Header("Video")]
    [SerializeField] private VideoPlayer _videoPlayer;
    [SerializeField] private RenderTexture _videoRenderTexture;

    [Header("Interaction")]
    [SerializeField] private Button _tapCatcher;
    [SerializeField] private TMP_Text _tapToProceedLabel;
    [SerializeField] private GameObject _root;

    [Header("GIF Layout")]
    [SerializeField] private Vector2 _gifSurfaceSize = new(420f, 420f);
    [SerializeField] private Vector2 _gifSurfaceAnchoredPosition = new(0f, 560f);

    [Header("Animation Fallback")]
    [Tooltip("State name on the Animator used when an AnimationClip is provided as a fallback.")]
    [SerializeField] private string _animatorStateName = "TutorialPlaceholder";

    private Action _onDismissed;
    private bool _isPlaying;
    private Coroutine _gifPlaybackRoutine;
    private bool _gifFramesUseFullTexture;

    public bool IsPlaying => _isPlaying;
    public PlaybackMode CurrentMode { get; private set; } = PlaybackMode.None;

    public static TutorialIntroPlayer CreateRuntime()
    {
        Canvas canvas = FindTutorialCanvas();
        if (canvas == null)
        {
            GameObject canvasObject = new("TutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
        }

        GameObject playerObject = new("TutorialIntroPlayer", typeof(RectTransform));
        playerObject.transform.SetParent(canvas.transform, false);
        RectTransform playerRect = playerObject.GetComponent<RectTransform>();
        Stretch(playerRect);

        TutorialIntroPlayer player = playerObject.AddComponent<TutorialIntroPlayer>();
        player._root = CreateRoot(playerObject.transform);
        player._videoSurface = CreateVideoSurface(player._root.transform);
        player._gifSurface = CreateGifSurface(player._root.transform);
        player._animationSurface = CreateAnimationSurface(player._root.transform);
        player._tapToProceedLabel = CreateLabel(player._root.transform);
        player._tapCatcher = CreateTapCatcher(playerObject.transform);
        player._videoPlayer = playerObject.AddComponent<VideoPlayer>();
        player._videoPlayer.playOnAwake = false;
        player._videoPlayer.isLooping = true;
        player._videoRenderTexture = new RenderTexture(1080, 1920, 0)
        {
            name = "TutorialIntroRuntimeTexture",
            hideFlags = HideFlags.HideAndDontSave,
        };
        player._videoPlayer.targetTexture = player._videoRenderTexture;
        player._videoSurface.texture = player._videoRenderTexture;
        player._tapCatcher.onClick.AddListener(player.OnTapped);

        player._root.SetActive(false);
        player.SetTapCatcherActive(false);
        return player;
    }

    private static Canvas FindTutorialCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.name == "TutorialCanvas")
                return canvas;
        }

        return null;
    }

    private static GameObject CreateRoot(Transform parent)
    {
        GameObject root = new("IntroOverlayRoot", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);
        Stretch(root.GetComponent<RectTransform>());
        Image background = root.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.82f);
        background.raycastTarget = false;
        return root;
    }

    private static RawImage CreateVideoSurface(Transform parent)
    {
        GameObject surfaceObject = new("VideoSurface", typeof(RectTransform), typeof(RawImage));
        surfaceObject.transform.SetParent(parent, false);
        RectTransform rect = surfaceObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.08f, 0.22f);
        rect.anchorMax = new Vector2(0.92f, 0.78f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        RawImage image = surfaceObject.GetComponent<RawImage>();
        image.color = Color.white;
        image.raycastTarget = false;
        surfaceObject.SetActive(false);
        return image;
    }

    private static RawImage CreateGifSurface(Transform parent)
    {
        GameObject surfaceObject = new("GifSurface", typeof(RectTransform), typeof(RawImage));
        surfaceObject.transform.SetParent(parent, false);
        RectTransform rect = surfaceObject.GetComponent<RectTransform>();
        ConfigureGifSurfaceRect(rect, DefaultGifSurfaceSize, DefaultGifSurfaceAnchoredPosition);

        RawImage image = surfaceObject.GetComponent<RawImage>();
        image.color = Color.white;
        image.raycastTarget = false;
        image.uvRect = new Rect(0f, 0f, 1f, 1f);
        surfaceObject.SetActive(false);
        return image;
    }

    private static Image CreateAnimationSurface(Transform parent)
    {
        GameObject surfaceObject = new("AnimationSurface", typeof(RectTransform), typeof(Image));
        surfaceObject.transform.SetParent(parent, false);
        RectTransform rect = surfaceObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.08f, 0.22f);
        rect.anchorMax = new Vector2(0.92f, 0.78f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = surfaceObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.18f);
        image.raycastTarget = false;
        surfaceObject.SetActive(false);
        return image;
    }

    private static TMP_Text CreateLabel(Transform parent)
    {
        GameObject labelObject = new("TapToProceedLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.12f);
        rect.anchorMax = new Vector2(0.5f, 0.12f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(900f, 120f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 34f;
        label.color = Color.white;
        label.raycastTarget = false;
        TutorialFontProvider.ApplyTo(label);
        return label;
    }

    private static Button CreateTapCatcher(Transform parent)
    {
        GameObject buttonObject = new("IntroTapCatcher", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Stretch(buttonObject.GetComponent<RectTransform>());

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;
        return buttonObject.GetComponent<Button>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void Awake()
    {
        ApplyGifLayout();
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
        StopGifPlayback();
        if (_videoPlayer != null)
        {
            _videoPlayer.loopPointReached -= OnVideoLoopPoint;
            _videoPlayer.errorReceived -= OnVideoError;
        }
    }

    private void OnDestroy()
    {
        if (_videoRenderTexture != null && _videoRenderTexture.hideFlags == HideFlags.HideAndDontSave)
        {
            _videoRenderTexture.Release();
            if (Application.isPlaying)
                Destroy(_videoRenderTexture);
            else
                DestroyImmediate(_videoRenderTexture);
            _videoRenderTexture = null;
        }
    }

    /// <summary>
    /// Pure helper: choose Video when a clip is assigned, else GIF, else Animation, else None.
    /// Public/static so it can be exercised by EditMode tests.
    /// </summary>
    public static PlaybackMode SelectMode(VideoClip videoClip, AnimationClip animationClip)
        => SelectMode(videoClip != null, animationClip != null);

    public static PlaybackMode SelectMode(VideoClip videoClip, Texture2D gifTexture, IReadOnlyList<Sprite> gifFrames, AnimationClip animationClip)
        => SelectMode(videoClip != null, HasGifContent(gifTexture, gifFrames), animationClip != null);

    public static PlaybackMode SelectMode(bool hasVideoClip, bool hasAnimationClip)
        => SelectMode(hasVideoClip, false, hasAnimationClip);

    public static PlaybackMode SelectMode(bool hasVideoClip, bool hasGifTexture, bool hasAnimationClip)
    {
        if (hasVideoClip) return PlaybackMode.Video;
        if (hasGifTexture) return PlaybackMode.Gif;
        if (hasAnimationClip) return PlaybackMode.Animation;
        return PlaybackMode.None;
    }

    public static bool TemplateUsesGif(OnboardingVideoTemplate template)
        => SelectMode(template.videoClip, template.gifTexture, template.gifFrames, template.animationClip) == PlaybackMode.Gif;

    public IEnumerator Play(OnboardingVideoTemplate template, Action onDismissed = null)
    {
        _onDismissed = onDismissed;
        ConfigureLabel(template.tapToProceedText);

        CurrentMode = SelectMode(template.videoClip, template.gifTexture, template.gifFrames, template.animationClip);
        if (CurrentMode == PlaybackMode.None)
        {
            _onDismissed = null;
            yield break;
        }

        ConfigureOverlayRoot(dimBackground: true, showLabel: true);
        ShowSurfaces(CurrentMode);
        if (_root != null) _root.SetActive(true);
        SetTapCatcherActive(true);
        _isPlaying = true;

        switch (CurrentMode)
        {
            case PlaybackMode.Video:
                yield return StartVideo(template.videoClip);
                break;
            case PlaybackMode.Gif:
                StartGif(template.gifTexture, template.gifFrames, template.gifFramesPerSecond);
                break;
            case PlaybackMode.Animation:
                StartAnimation(template.animationClip);
                break;
        }

        yield return new WaitWhile(() => _isPlaying);
    }

    public bool ShowGifHint(OnboardingVideoTemplate template)
    {
        if (!TemplateUsesGif(template))
            return false;

        StopAllPlayback();
        _onDismissed = null;
        CurrentMode = PlaybackMode.Gif;
        ConfigureOverlayRoot(dimBackground: false, showLabel: false);
        ShowSurfaces(PlaybackMode.Gif);
        if (_root != null) _root.SetActive(true);
        SetTapCatcherActive(false);
        _isPlaying = true;
        StartGif(template.gifTexture, template.gifFrames, template.gifFramesPerSecond);
        return true;
    }

    public void HideGifHint()
    {
        if (CurrentMode != PlaybackMode.Gif)
            return;

        _isPlaying = false;
        StopGifPlayback();
        if (_gifSurface != null)
            _gifSurface.gameObject.SetActive(false);
        if (_root != null)
            _root.SetActive(false);
        SetTapCatcherActive(false);
        CurrentMode = PlaybackMode.None;
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
        if (_gifSurface != null)
        {
            if (mode == PlaybackMode.Gif)
                ApplyGifLayout();
            _gifSurface.gameObject.SetActive(mode == PlaybackMode.Gif);
        }
        if (_animationSurface != null) _animationSurface.gameObject.SetActive(mode == PlaybackMode.Animation);
    }

    private void ConfigureOverlayRoot(bool dimBackground, bool showLabel)
    {
        if (_root != null && _root.TryGetComponent(out Image background))
        {
            background.enabled = dimBackground;
            background.color = dimBackground
                ? new Color(0f, 0f, 0f, 0.82f)
                : new Color(0f, 0f, 0f, 0f);
        }

        if (_tapToProceedLabel != null)
            _tapToProceedLabel.gameObject.SetActive(showLabel);
    }

    private void ApplyGifLayout()
    {
        if (_gifSurface == null)
            return;

        RectTransform rect = _gifSurface.transform as RectTransform;
        ConfigureGifSurfaceRect(rect, ResolvePositive(_gifSurfaceSize, DefaultGifSurfaceSize), _gifSurfaceAnchoredPosition);
    }

    internal static void ConfigureGifSurfaceRect(RectTransform rect, Vector2 size, Vector2 anchoredPosition)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = ResolvePositive(size, DefaultGifSurfaceSize);
        rect.anchoredPosition = anchoredPosition;
    }

    private static Vector2 ResolvePositive(Vector2 value, Vector2 fallback)
        => value.x > 0f && value.y > 0f ? value : fallback;

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

    private void StartGif(Texture2D texture, IReadOnlyList<Sprite> frames, float framesPerSecond)
    {
        if (_gifSurface == null)
        {
            DebugLogger.LogError("TutorialIntroPlayer: GIF surface reference missing.");
            return;
        }

        StopGifPlayback();

        if (HasFrames(frames))
        {
            _gifFramesUseFullTexture = FramesUseSeparateTextures(frames);
            ApplyGifFrame(frames[0]);
            _gifPlaybackRoutine = StartCoroutine(AnimateGifFrames(frames, framesPerSecond));
            return;
        }

        _gifFramesUseFullTexture = false;
        _gifSurface.texture = texture;
        _gifSurface.uvRect = new Rect(0f, 0f, 1f, 1f);
    }

    private IEnumerator AnimateGifFrames(IReadOnlyList<Sprite> frames, float framesPerSecond)
    {
        float secondsPerFrame = 1f / Mathf.Max(1f, framesPerSecond);
        WaitForSecondsRealtime delay = new(secondsPerFrame);
        int index = 0;

        while (_isPlaying && CurrentMode == PlaybackMode.Gif)
        {
            if (HasFrames(frames))
            {
                ApplyGifFrame(frames[index]);
                index = (index + 1) % frames.Count;
            }
            yield return delay;
        }
    }

    private void ApplyGifFrame(Sprite frame)
    {
        if (_gifSurface == null || frame == null || frame.texture == null)
            return;

        Texture2D texture = frame.texture;
        _gifSurface.texture = texture;

        if (_gifFramesUseFullTexture)
        {
            _gifSurface.uvRect = new Rect(0f, 0f, 1f, 1f);
            return;
        }

        Rect rect = frame.textureRect;
        _gifSurface.uvRect = new Rect(
            rect.x / texture.width,
            rect.y / texture.height,
            rect.width / texture.width,
            rect.height / texture.height);
    }

    private void StopAllPlayback()
    {
        StopGifPlayback();
        if (_videoPlayer != null && _videoPlayer.isPlaying)
            _videoPlayer.Stop();
    }

    private void StopGifPlayback()
    {
        if (_gifPlaybackRoutine == null)
            return;

        StopCoroutine(_gifPlaybackRoutine);
        _gifPlaybackRoutine = null;
    }

    private static bool HasGifContent(Texture2D texture, IReadOnlyList<Sprite> frames)
        => texture != null || HasFrames(frames);

    private static bool HasFrames(IReadOnlyList<Sprite> frames)
        => frames != null && frames.Count > 0;

    private static bool FramesUseSeparateTextures(IReadOnlyList<Sprite> frames)
    {
        if (!HasFrames(frames))
            return false;

        for (int i = 0; i < frames.Count; i++)
        {
            Sprite current = frames[i];
            Texture2D texture = current != null ? current.texture : null;
            if (texture == null)
                return false;

            for (int j = i + 1; j < frames.Count; j++)
            {
                Sprite other = frames[j];
                if (other != null && ReferenceEquals(texture, other.texture))
                    return false;
            }
        }

        return true;
    }

    private void OnVideoLoopPoint(VideoPlayer source) { /* looping is built-in; hook here if needed */ }

    private void OnVideoError(VideoPlayer source, string message)
    {
        DebugLogger.LogError($"TutorialIntroPlayer: VideoPlayer error: {message}");
    }
}
