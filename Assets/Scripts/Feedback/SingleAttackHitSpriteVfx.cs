using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class SingleAttackHitSpriteVfx : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Sprite[] _frames;
    [SerializeField, Min(1f)] private float _framesPerSecond = 24f;

    private float _elapsed;
    private bool _isPlaying;
    private int _lastFrameIndex = -1;
    private System.Action<int> _onFrameChanged;
    private System.Action _onCompleted;

    public float PlayDuration
    {
        get
        {
            int frameCount = _frames != null ? _frames.Length : 0;
            if (frameCount <= 0)
                return 0f;

            return frameCount / Mathf.Max(1f, _framesPerSecond);
        }
    }

    private void Awake()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        _elapsed = 0f;
        _isPlaying = false;
        _lastFrameIndex = -1;
        _onFrameChanged = null;
        _onCompleted = null;
    }

    private void Update()
    {
        if (!_isPlaying || _spriteRenderer == null || _frames == null || _frames.Length == 0)
            return;

        _elapsed += Time.deltaTime;
        int frameIndex = Mathf.FloorToInt(_elapsed * Mathf.Max(1f, _framesPerSecond));

        if (frameIndex >= _frames.Length)
        {
            _isPlaying = false;
            _onCompleted?.Invoke();
            _onCompleted = null;
            return;
        }

        if (frameIndex != _lastFrameIndex)
        {
            _lastFrameIndex = frameIndex;
            _spriteRenderer.sprite = _frames[frameIndex];
            _onFrameChanged?.Invoke(frameIndex);
        }
    }

    public void Play(System.Action<int> onFrameChanged = null, System.Action onCompleted = null)
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_spriteRenderer == null || _frames == null || _frames.Length == 0)
            return;

        _onFrameChanged = onFrameChanged;
        _onCompleted = onCompleted;
        _elapsed = 0f;
        _isPlaying = true;
        _lastFrameIndex = 0;
        _spriteRenderer.sprite = _frames[0];
        _onFrameChanged?.Invoke(0);
    }

    public void ResetVisual()
    {
        _isPlaying = false;
        _elapsed = 0f;
        _lastFrameIndex = -1;
        _onFrameChanged = null;
        _onCompleted = null;

        if (_spriteRenderer != null && _frames != null && _frames.Length > 0)
            _spriteRenderer.sprite = _frames[0];
    }
}
