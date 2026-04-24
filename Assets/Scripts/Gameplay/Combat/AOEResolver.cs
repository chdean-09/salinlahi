using System.Collections.Generic;
using UnityEngine;

// On each successful recognition, asks ActiveEnemyTracker how many live enemies
// share the drawn character and, if >= 3, defeats every non-boss match in a single
// pass during the same frame.
public class AOEResolver : MonoBehaviour
{
    [SerializeField] private CharacterRegistrySO _registry;

    [Tooltip("Optional full-screen flash prefab spawned at this GameObject's position on AOE.")]
    [SerializeField] private GameObject _aoeFlashVfxPrefab;

    [Tooltip("Minimum matching on-screen enemies required to trigger an AOE mass-defeat.")]
    [SerializeField, Min(1)] private int _aoeThreshold = 3;

    private readonly List<Enemy> _iterationBuffer = new List<Enemy>(16);
    private readonly List<BaybayinCharacterSO> _defeatedBuffer = new List<BaybayinCharacterSO>(16);

    private void OnEnable()
    {
        EventBus.OnCharacterRecognized += OnCharacterRecognized;
    }

    private void OnDisable()
    {
        EventBus.OnCharacterRecognized -= OnCharacterRecognized;
    }

    private void OnCharacterRecognized(string characterID)
    {
        ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
        if (tracker == null || _registry == null) return;

        List<Enemy> matches = tracker.FindAllWithCharacter(characterID);
        if (matches == null || matches.Count < _aoeThreshold) return;

        _iterationBuffer.Clear();
        for (int i = 0; i < matches.Count; i++)
            _iterationBuffer.Add(matches[i]);

        BaybayinCharacterSO charSO = FindCharacter(characterID);

        _defeatedBuffer.Clear();
        for (int i = 0; i < _iterationBuffer.Count; i++)
        {
            Enemy e = _iterationBuffer[i];
            if (e == null) continue;
            if (e.IsBoss) continue;
            if (e.Data == null) continue;

            e.TakeDamage(e.Data.maxHealth);
            _defeatedBuffer.Add(charSO);
        }

        if (_defeatedBuffer.Count == 0) return;

        if (_aoeFlashVfxPrefab != null)
            Instantiate(_aoeFlashVfxPrefab, transform.position, Quaternion.identity);

        EventBus.RaiseAOETriggered(_defeatedBuffer);
    }

    private BaybayinCharacterSO FindCharacter(string characterID)
    {
        if (_registry == null || _registry.All == null) return null;
        for (int i = 0; i < _registry.All.Count; i++)
        {
            BaybayinCharacterSO c = _registry.All[i];
            if (c != null && c.characterID == characterID)
                return c;
        }
        return null;
    }
}