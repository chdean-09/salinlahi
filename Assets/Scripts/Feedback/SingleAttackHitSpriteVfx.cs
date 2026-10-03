using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class SingleAttackHitSpriteVfx : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Sprite[] _frames;
    [SerializeField, Min(1f)] private float _framesPerSecond = 24f;

    private float _elapsed;
    private bool _isPlaying;
    public SpriteRenderer SpriteRenderer => _spriteRenderer;

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
            return;
        }

        _spriteRenderer.sprite = _frames[frameIndex];
    }

    public void Play()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_spriteRenderer == null || _frames == null || _frames.Length == 0)
            return;

        _elapsed = 0f;
        _isPlaying = true;
        _spriteRenderer.sprite = _frames[0];
    }

    public void ResetVisual()
    {
        _isPlaying = false;
        _elapsed = 0f;

        if (_spriteRenderer != null && _frames != null && _frames.Length > 0)
            _spriteRenderer.sprite = _frames[0];
    }
}
