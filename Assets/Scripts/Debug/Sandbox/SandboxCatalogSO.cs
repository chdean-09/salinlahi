#if UNITY_EDITOR || SALINLAHI_SANDBOX
using System.Collections.Generic;
using UnityEngine;

namespace Salinlahi.Debug.Sandbox
{
    [CreateAssetMenu(
        fileName = "SandboxCatalog",
        menuName = "Salinlahi/Debug/Sandbox Catalog")]
    public class SandboxCatalogSO : ScriptableObject
    {
        [SerializeField] private List<EnemyDataSO> _enemyTypes = new();
        [SerializeField] private List<BaybayinCharacterSO> _characters = new();

        public IReadOnlyList<EnemyDataSO> EnemyTypes => _enemyTypes;
        public IReadOnlyList<BaybayinCharacterSO> Characters => _characters;
    }
}
#endif
