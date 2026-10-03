using UnityEngine;

namespace Salinlahi.Runtime.Gameplay
{
    public class ProtagonistAttackController : MonoBehaviour
    {
        [SerializeField] private ProtagonistSlashVfx _slashVfxPrefab;
        [SerializeField] private int _poolSize = 3;

        private ProtagonistSlashVfx[] _slashPool;
        private int _poolIndex;

        private void Awake()
        {
            InitializePool();
        }

        private void OnEnable()
        {
            EventBus.OnSingleAttackHit += HandleSingleAttack;
        }

        private void OnDisable()
        {
            EventBus.OnSingleAttackHit -= HandleSingleAttack;
        }

        private void InitializePool()
        {
            if (_slashVfxPrefab == null)
            {
                DebugLogger.LogWarning("[ProtagonistAttackController] _slashVfxPrefab not assigned on ProtagonistManager prefab.");
                return;
            }

            _slashPool = new ProtagonistSlashVfx[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                ProtagonistSlashVfx slash = Instantiate(_slashVfxPrefab, transform);
                slash.gameObject.SetActive(false);
                _slashPool[i] = slash;
            }
        }

        private void HandleSingleAttack(Enemy target)
        {
            if (ProtagonistManager.Instance?.ProtagonistTransform == null)
                return;

            Vector3 protagonistPos = ProtagonistManager.Instance.ProtagonistTransform.position;

            // Trigger protagonist draw animation FIRST (before any VFX checks)
            Animator protagonistAnimator = ProtagonistManager.Instance?.ProtagonistTransform?.GetComponent<Animator>();
            if (protagonistAnimator != null)
                protagonistAnimator.SetTrigger("Draw");

            // VFX check comes AFTER animation
            if (target == null)
                return;
            if (_slashPool == null || _slashPool.Length == 0)
                return;

            Vector3 targetPos = target.transform.position;

            // Get pooled slash VFX
            ProtagonistSlashVfx slash = GetPooledSlash();
            slash.gameObject.SetActive(true);
            slash.Play(protagonistPos, targetPos);
        }

        private ProtagonistSlashVfx GetPooledSlash()
        {
            ProtagonistSlashVfx slash = _slashPool[_poolIndex];
            slash.ResetVisual();
            _poolIndex = (_poolIndex + 1) % _poolSize;
            return slash;
        }
    }
}
