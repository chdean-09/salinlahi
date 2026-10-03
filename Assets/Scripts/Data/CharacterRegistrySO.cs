using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CharacterRegistry",
    menuName = "Salinlahi/Character Registry")]
public class CharacterRegistrySO : ScriptableObject
{
    public List<BaybayinCharacterSO> All = new List<BaybayinCharacterSO>();
}
