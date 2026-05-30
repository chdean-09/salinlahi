using UnityEngine;

namespace Salinlahi.Runtime.Gameplay
{
    public class ProtagonistManager : MonoBehaviour
    {
        public static ProtagonistManager Instance { get; private set; }

        [SerializeField] private GameObject _protagonistPrefab;
        [SerializeField] private float _walkInDuration = 1.5f;

        private const float MinVisibleWorldHeight = 1.5f;

        public Transform ProtagonistTransform { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public Vector3 CalculateProtagonistPosition()
        {
            PlayerBase playerBase = FindFirstObjectByType<PlayerBase>();
            if (playerBase != null)
            {
                Bounds bounds = GetBaseBounds(playerBase);
                return new Vector3(bounds.center.x, bounds.min.y - 0.45f, 0f);
            }

            return new Vector3(0f, -8.35f, 0f);
        }

        private static Bounds GetBaseBounds(PlayerBase playerBase)
        {
            Collider2D collider = playerBase.GetComponent<Collider2D>();
            if (collider != null)
                return collider.bounds;

            SpriteRenderer renderer = playerBase.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite != null)
                return renderer.bounds;

            return new Bounds(playerBase.transform.position, Vector3.one);
        }

        public void EnsureProtagonist(Vector3 targetPosition, bool spawnBelowScreen = true)
        {
            if (ProtagonistTransform != null) return;

            Vector3 startPosition = spawnBelowScreen ? targetPosition + Vector3.down * 5f : targetPosition;

            if (_protagonistPrefab == null)
            {
                DebugLogger.LogError("[ProtagonistManager] _protagonistPrefab not assigned. Place [Manager] ProtagonistManager.prefab in the active scene.");
                return;
            }

            GameObject protagonist = Instantiate(_protagonistPrefab, startPosition, Quaternion.identity);
            ProtagonistTransform = protagonist.transform;
            ValidateProtagonistVisibility(ProtagonistTransform);
        }

        public void WalkInProtagonist(Vector3 targetPosition)
        {
            if (ProtagonistTransform == null) return;
            ValidateProtagonistVisibility(ProtagonistTransform);
            StartCoroutine(WalkInCoroutine(targetPosition));
        }

        private System.Collections.IEnumerator WalkInCoroutine(Vector3 targetPosition)
        {
            Vector3 startPosition = ProtagonistTransform.position;
            float elapsed = 0f;

            while (elapsed < _walkInDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _walkInDuration;
                t = t * (2 - t);

                ProtagonistTransform.position = Vector3.Lerp(startPosition, targetPosition, t);
                yield return null;
            }

            ProtagonistTransform.position = targetPosition;
        }

        private static void ValidateProtagonistVisibility(Transform protagonist)
        {
            SpriteRenderer renderer = protagonist.GetComponentInChildren<SpriteRenderer>();
            if (renderer == null)
            {
                DebugLogger.LogWarning("[ProtagonistManager] Protagonist has no SpriteRenderer.");
                return;
            }

            renderer.enabled = true;
            renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, RenderOrder.Protagonist);
            Color color = renderer.color;
            color.a = Mathf.Max(color.a, 1f);
            renderer.color = color;
            protagonist.gameObject.SetActive(true);

            NormalizeProtagonistRenderer(protagonist, renderer);
        }

        private static void NormalizeProtagonistRenderer(Transform protagonist, SpriteRenderer renderer)
        {
            if (protagonist == null || renderer == null)
                return;

            renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, RenderOrder.Protagonist);

            if (renderer.sprite == null)
            {
                DebugLogger.LogWarning("[ProtagonistManager] Protagonist SpriteRenderer has no sprite assigned.");
                return;
            }

            float spriteWorldHeight = renderer.sprite.bounds.size.y;
            if (spriteWorldHeight <= 0f)
                return;

            float currentHeight = spriteWorldHeight * Mathf.Abs(protagonist.localScale.y);
            if (currentHeight <= Mathf.Epsilon || currentHeight >= MinVisibleWorldHeight)
                return;

            float scaleMultiplier = MinVisibleWorldHeight / currentHeight;
            protagonist.localScale = new Vector3(
                protagonist.localScale.x * scaleMultiplier,
                protagonist.localScale.y * scaleMultiplier,
                protagonist.localScale.z);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
