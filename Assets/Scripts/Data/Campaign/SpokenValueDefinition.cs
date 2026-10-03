using UnityEngine;

[System.Serializable]
public sealed class SpokenValueDefinition
{
    public string stableId;
    public string displayValue;
    public AudioClip pronunciationClip;
}

[System.Serializable]
public sealed class SymbolValueReference
{
    public BaybayinCharacterSO symbol;
    public string spokenValueId;
}
