using UnityEngine;

namespace Salinlahi.Runtime.Gameplay
{
    public class ProtagonistManager : MonoBehaviour
    {
        public static ProtagonistManager Instance { get; private set; }

        [SerializeField] private GameObject _protagonistPrefab;
        [SerializeField] private float _walkInDuration = 1.5f;

        private const string ProtagonistPrefabPath = "Assets/Prefabs/Protagonist/Protagonist.prefab";
        private const string ProtagonistSpritePath = "Assets/Art/Characters/Protagonist/sprite_prot_japanese_idle_back-Sheet.png";
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

        public void EnsureProtagonist(Vector3 targetPosition)
        {
            DebugLogger.Log("[ProtagonistManager] EnsureProtagonist called. Current ProtagonistTransform: " + (ProtagonistTransform != null ? ProtagonistTransform.name : "null"));

            if (ProtagonistTransform != null)
            {
                DebugLogger.Log("[ProtagonistManager] Protagonist already exists, skipping creation");
                return;
            }

            Vector3 startPosition = targetPosition + Vector3.down * 5f;
            DebugLogger.Log("[ProtagonistManager] Will create protagonist at start position: " + startPosition + " (target: " + targetPosition + ")");

            if (_protagonistPrefab == null)
            {
#if UNITY_EDITOR
                _protagonistPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ProtagonistPrefabPath);
#endif

                if (_protagonistPrefab == null)
                {
                    DebugLogger.LogWarning("[ProtagonistManager] No protagonist prefab assigned. Creating protagonist from sprite fallback.");
                    ProtagonistTransform = CreateProtagonistFromSprite(targetPosition);
                    return;
                }
            }

            GameObject protagonist = Instantiate(_protagonistPrefab, startPosition, Quaternion.identity);
            ProtagonistTransform = protagonist.transform;
            ValidateProtagonistVisibility(ProtagonistTransform);
            DebugLogger.Log("[ProtagonistManager] Protagonist instantiated at: " + startPosition);
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

        private Transform CreateProtagonistFromSprite(Vector3 targetPosition)
        {
            Vector3 startPosition = targetPosition + Vector3.down * 5f;
            GameObject protagonist = new("Protagonist");
            protagonist.transform.position = startPosition;
            protagonist.transform.localScale = new Vector3(0.3f, 0.3f, 1f);

            SpriteRenderer renderer = protagonist.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadFallbackSprite();
            renderer.sortingOrder = RenderOrder.Protagonist;
            NormalizeProtagonistRenderer(protagonist.transform, renderer);
            LogRendererState("[ProtagonistManager] Fallback protagonist renderer", protagonist.transform, renderer);

            DebugLogger.Log("[ProtagonistManager] Protagonist fallback created at: " + startPosition);
            return protagonist.transform;
        }

        private static Sprite LoadFallbackSprite()
        {
#if UNITY_EDITOR
            DebugLogger.Log("[ProtagonistManager] Attempting to load sprites from: " + ProtagonistSpritePath);
            Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(ProtagonistSpritePath);
            DebugLogger.Log("[ProtagonistManager] Found " + assets.Length + " assets at path");
            foreach (Object asset in assets)
            {
                if (asset is Sprite sprite)
                {
                    DebugLogger.Log("[ProtagonistManager] Found sprite: " + sprite.name);
                    return sprite;
                }
            }
#endif
            DebugLogger.Log("[ProtagonistManager] Attempting to load sprite from Resources");
            return Resources.Load<Sprite>("Characters/Protagonist/sprite_prot_japanese_idle_back-Sheet");
        }

        private static void ValidateProtagonistVisibility(Transform protagonist)
        {
            SpriteRenderer renderer = protagonist.GetComponentInChildren<SpriteRenderer>();
            if (renderer == null)
            {
                DebugLogger.LogWarning("[ProtagonistManager] Protagonist has no SpriteRenderer.");
                return;
            }

            LogRendererState("[ProtagonistManager] Protagonist visibility check", protagonist, renderer);

            renderer.enabled = true;
            renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, RenderOrder.Protagonist);
            Color color = renderer.color;
            color.a = Mathf.Max(color.a, 1f);
            renderer.color = color;
            protagonist.gameObject.SetActive(true);

            NormalizeProtagonistRenderer(protagonist, renderer);
            LogRendererState("[ProtagonistManager] Protagonist visibility normalized", protagonist, renderer);
        }

        private static void NormalizeProtagonistRenderer(Transform protagonist, SpriteRenderer renderer)
        {
            if (renderer.sprite == null)
            {
                DebugLogger.LogWarning("[ProtagonistManager] Protagonist SpriteRenderer has no sprite assigned.");
                return;
            }

            float currentHeight = renderer.sprite.bounds.size.y * protagonist.localScale.y;
            if (currentHeight <= 0f || currentHeight >= MinVisibleWorldHeight)
            {
                return;
            }

            float multiplier = MinVisibleWorldHeight / currentHeight;
            protagonist.localScale = new Vector3(
                protagonist.localScale.x * multiplier,
                protagonist.localScale.y * multiplier,
                protagonist.localScale.z);
        }

        private static void LogRendererState(string prefix, Transform protagonist, SpriteRenderer renderer)
        {
            DebugLogger.Log(prefix + ": " +
                "Sprite=" + (renderer.sprite != null ? renderer.sprite.name : "NULL") +
                ", Enabled=" + renderer.enabled +
                ", Color=" + renderer.color +
                ", SortingOrder=" + renderer.sortingOrder +
                ", SortingLayer=" + renderer.sortingLayerName + " (" + renderer.sortingLayerID + ")" +
                ", Position=" + protagonist.position +
                ", Scale=" + protagonist.localScale +
                ", Active=" + protagonist.gameObject.activeSelf);
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
