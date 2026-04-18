using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    [Header("Heart Display")]
    [SerializeField] private Image[] _heartIcons;
    [SerializeField] private Sprite _heartFull;
    [SerializeField] private Sprite _heartEmpty;
    [SerializeField] private float _heartShakeDuration = 0.3f;
    [SerializeField] private float _heartShakeMagnitude = 5f;

    [Header("Wave Display")]
    [SerializeField] private TMP_Text _waveText;
    [SerializeField] private float _waveSlideDistance = 60f;
    [SerializeField] private float _waveSlideDuration = 0.35f;

    [Header("Combo Display")]
    [SerializeField] private TMP_Text _streakText;
    [SerializeField] private float _comboPopScale = 1.4f;
    [SerializeField] private float _comboPopDuration = 0.15f;

    [Header("Focus Mode")]
    [SerializeField] private GameObject _focusModeIndicator;
    [SerializeField] private CanvasGroup _focusModeCanvasGroup;
    [SerializeField] private float _focusFadeDuration = 0.2f;

    [Header("Pause")]
    [SerializeField] private Button _pauseButton;

    [Header("Drawing Feedback")]
    [SerializeField] private CanvasGroup _rejectFlash;
    [SerializeField] private GameObject _rejectXMark;
    [SerializeField] private CanvasGroup _successFlash;
    [SerializeField] private float _rejectDuration = 0.5f;
    [SerializeField] private float _successDuration = 0.3f;

    private int _lastHeartCount;
    private Vector3 _streakBaseScale;
    private Vector3 _waveBasePos;

    private void Awake()
    {
        if (_streakText != null)
            _streakBaseScale = _streakText.transform.localScale;
        if (_waveText != null)
            _waveBasePos = _waveText.transform.localPosition;

        // Start with feedback hidden
        if (_rejectFlash != null) _rejectFlash.alpha = 0f;
        if (_rejectXMark != null) _rejectXMark.SetActive(false);
        if (_successFlash != null) _successFlash.alpha = 0f;
    }

    private void OnEnable()
    {
        EventBus.OnHeartsChanged += UpdateHearts;
        EventBus.OnWaveStarted += UpdateWave;
        EventBus.OnComboChanged += UpdateStreakDisplay;
        EventBus.OnFocusModeActivated += ShowFocusIndicator;
        EventBus.OnFocusModeDeactivated += HideFocusIndicator;
        EventBus.OnDrawingFailed += ShowRejectFeedback;
        EventBus.OnEnemyDefeated += ShowSuccessFeedback;

        HeartSystem heartSystem = FindFirstObjectByType<HeartSystem>();
        if (heartSystem != null)
            UpdateHearts(heartSystem.GetCurrentHearts());

        if (_pauseButton != null)
            _pauseButton.onClick.AddListener(OnPausePressed);
    }

    private void OnDisable()
    {
        EventBus.OnHeartsChanged -= UpdateHearts;
        EventBus.OnWaveStarted -= UpdateWave;
        EventBus.OnComboChanged -= UpdateStreakDisplay;
        EventBus.OnFocusModeActivated -= ShowFocusIndicator;
        EventBus.OnFocusModeDeactivated -= HideFocusIndicator;
        EventBus.OnDrawingFailed -= ShowRejectFeedback;
        EventBus.OnEnemyDefeated -= ShowSuccessFeedback;

        if (_pauseButton != null)
            _pauseButton.onClick.RemoveListener(OnPausePressed);
    }

    // ── Hearts ──────────────────────────────────────────────────────

    private void UpdateHearts(int current)
    {
        bool lost = current < _lastHeartCount;
        _lastHeartCount = current;

        for (int i = 0; i < _heartIcons.Length; i++)
        {
            if (_heartIcons[i] == null) continue;

            bool filled = i < current;
            // Assign sprite only if both sprites are configured; fail visibly in Editor, silently in builds.
            if (_heartFull != null && _heartEmpty != null)
                _heartIcons[i].sprite = filled ? _heartFull : _heartEmpty;
            else
                _heartIcons[i].color = filled ? Color.red : new Color(1f, 1f, 1f, 0.25f);

            if (lost && i == current)
                StartCoroutine(HeartLossAnimation(_heartIcons[i]));
        }
    }

    private IEnumerator HeartLossAnimation(Image heart)
    {
        if (heart == null) yield break;
        Vector3 originalPos = heart.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < _heartShakeDuration)
        {
            if (heart == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            float x = Random.Range(-1f, 1f) * _heartShakeMagnitude;
            float y = Random.Range(-1f, 1f) * _heartShakeMagnitude;
            heart.transform.localPosition = originalPos + new Vector3(x, y, 0f);
            yield return null;
        }

        if (heart != null)
            heart.transform.localPosition = originalPos;
    }

    // ── Wave ────────────────────────────────────────────────────────

    private void UpdateWave(int waveIndex)
    {
        if (_waveText == null) return;
        _waveText.text = $"Wave {waveIndex + 1}";
        StartCoroutine(WaveSlideAnimation());
    }

    private IEnumerator WaveSlideAnimation()
    {
        Vector3 startPos = _waveBasePos + Vector3.right * _waveSlideDistance;
        float elapsed = 0f;

        while (elapsed < _waveSlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / _waveSlideDuration;
            // Ease out quad
            t = 1f - (1f - t) * (1f - t);
            _waveText.transform.localPosition = Vector3.Lerp(startPos, _waveBasePos, t);
            yield return null;
        }

        _waveText.transform.localPosition = _waveBasePos;
    }

    // ── Combo ───────────────────────────────────────────────────────

    private void UpdateStreakDisplay(int streak)
    {
        if (_streakText == null) return;

        if (streak <= 0)
        {
            _streakText.gameObject.SetActive(false);
            return;
        }

        _streakText.gameObject.SetActive(true);
        _streakText.text = $"x{streak}";
        StartCoroutine(ComboPopAnimation());
    }

    private IEnumerator ComboPopAnimation()
    {
        Vector3 targetScale = _streakBaseScale * _comboPopScale;
        float half = _comboPopDuration / 2f;

        // Scale up
        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            _streakText.transform.localScale = Vector3.Lerp(_streakBaseScale, targetScale, elapsed / half);
            yield return null;
        }

        // Scale back down
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            _streakText.transform.localScale = Vector3.Lerp(targetScale, _streakBaseScale, elapsed / half);
            yield return null;
        }

        _streakText.transform.localScale = _streakBaseScale;
    }

    // ── Focus Mode ──────────────────────────────────────────────────

    private void ShowFocusIndicator()
    {
        if (_focusModeIndicator != null)
            _focusModeIndicator.SetActive(true);

        if (_focusModeCanvasGroup != null)
            StartCoroutine(FadeCanvasGroup(_focusModeCanvasGroup, 0f, 1f, _focusFadeDuration));
    }

    private void HideFocusIndicator()
    {
        if (_focusModeCanvasGroup != null)
            StartCoroutine(FadeCanvasGroupThenDisable(_focusModeCanvasGroup, _focusModeIndicator, _focusFadeDuration));
        else if (_focusModeIndicator != null)
            _focusModeIndicator.SetActive(false);
    }

    // ── Pause ───────────────────────────────────────────────────────

    private void OnPausePressed()
    {
        GameManager.Instance.PauseGame();
    }

    // ── Drawing Feedback ────────────────────────────────────────────

    private void ShowRejectFeedback()
    {
        StartCoroutine(FlashFeedback(_rejectFlash, _rejectXMark, _rejectDuration));
    }

    private void ShowSuccessFeedback(BaybayinCharacterSO _)
    {
        StartCoroutine(FlashFeedback(_successFlash, null, _successDuration));
    }

    private IEnumerator FlashFeedback(CanvasGroup flash, GameObject mark, float duration)
    {
        if (flash == null) yield break;

        flash.alpha = 1f;
        if (mark != null) mark.SetActive(true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            flash.alpha = 1f - (elapsed / duration);
            yield return null;
        }

        flash.alpha = 0f;
        if (mark != null) mark.SetActive(false);
    }

    // ── Utilities ───────────────────────────────────────────────────

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        float elapsed = 0f;
        group.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        group.alpha = to;
    }

    private IEnumerator FadeCanvasGroupThenDisable(CanvasGroup group, GameObject target, float duration)
    {
        yield return FadeCanvasGroup(group, 1f, 0f, duration);
        if (target != null) target.SetActive(false);
    }
}
