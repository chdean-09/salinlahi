#if UNITY_EDITOR || SALINLAHI_SANDBOX
using System.Collections.Generic;
using UnityEngine;

namespace Salinlahi.Debug.Sandbox
{
    public static class SandboxCharacterSelection
    {
        public static BaybayinCharacterSO ResolveCharacter(
            bool useRandomCharacter,
            IReadOnlyList<BaybayinCharacterSO> characters,
            int selectedCharacterIndex)
        {
            if (characters == null || characters.Count == 0)
                return null;

            if (useRandomCharacter)
            {
                int randomIndex = Random.Range(0, characters.Count);
                return characters[randomIndex];
            }

            int safeIndex = Mathf.Clamp(
                selectedCharacterIndex,
                0,
                characters.Count - 1);
            return characters[safeIndex];
        }
    }
}
#endif
