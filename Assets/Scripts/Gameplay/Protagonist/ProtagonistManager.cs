using UnityEngine;

namespace Salinlahi.Runtime.Gameplay
{
    public class ProtagonistManager : MonoBehaviour
    {
        public static ProtagonistManager Instance { get; private set; }

        [SerializeField] private GameObject _protagonistPrefab;
        [SerializeField] private float _walkInDuration = 1.5f;

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
            if (ProtagonistTransform != null) return;

            if (_protagonistPrefab == null)
            {
                Debug.LogWarning("[ProtagonistManager] No protagonist prefab assigned!");
                return;
            }

            Vector3 startPosition = targetPosition + Vector3.down * 5f; // Below screen
            GameObject protagonist = Instantiate(_protagonistPrefab, startPosition, Quaternion.identity);
            ProtagonistTransform = protagonist.transform;
        }

        public void WalkInProtagonist(Vector3 targetPosition)
        {
            if (ProtagonistTransform == null) return;
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
                // Ease-out curve: t * (2 - t)
                t = t * (2 - t);

                ProtagonistTransform.position = Vector3.Lerp(startPosition, targetPosition, t);
                yield return null;
            }

            ProtagonistTransform.position = targetPosition;
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
