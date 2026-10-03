using UnityEngine;

namespace Salinlahi.Runtime.Gameplay
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class ProtagonistSlashVfx : MonoBehaviour
    {
        [SerializeField] private Sprite[] _slashFrames;
        [SerializeField] private float _framesPerSecond = 12f;

        private SpriteRenderer _renderer;
        private int _currentFrame;
        private float _frameTimer;
        private bool _isPlaying;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
        }

        public void Play(Vector3 startPosition, Vector3 targetPosition)
        {
            transform.position = startPosition;
            
            // Rotate to face the target
            Vector3 direction = (targetPosition - startPosition).normalized;
            if (direction != Vector3.zero)
            {
                transform.up = direction;
            }

            _currentFrame = 0;
            _frameTimer = 0f;
            _isPlaying = true;
            
            if (_renderer != null)
            {
                _renderer.enabled = true;
                if (_slashFrames.Length > 0)
                {
                    _renderer.sprite = _slashFrames[0];
                }
            }
        }

        private void Update()
        {
            if (!_isPlaying || _slashFrames.Length == 0) return;

            _frameTimer += Time.deltaTime;
            float frameDuration = 1f / _framesPerSecond;

            if (_frameTimer >= frameDuration)
            {
                _frameTimer -= frameDuration;
                _currentFrame++;

                if (_currentFrame >= _slashFrames.Length)
                {
                    _isPlaying = false;
                    if (_renderer != null)
                    {
                        _renderer.enabled = false;
                    }
                    gameObject.SetActive(false);
                }
                else
                {
                    _renderer.sprite = _slashFrames[_currentFrame];
                }
            }
        }

        public void ResetVisual()
        {
            _isPlaying = false;
            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
            _currentFrame = 0;
        }
    }
}
