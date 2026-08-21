using System.Collections.Generic;
using UnityEngine;

public enum ContentRequirementKind
{
    Instruction,
    Practice,
    Assessment,
    Mastery,
}

[System.Serializable]
public sealed class ContentRequirement
{
    public ContentRequirementKind kind;
    public SymbolValueReference symbolValue = new();
    [Min(1)] public int requiredSuccesses = 1;
}

[System.Serializable]
public sealed class DefenseRules
{
    [Min(1)] public int shrineHearts = 3;
    public bool focusModeEnabled = true;
    public bool multiKillChainEnabled = true;
}

[System.Serializable]
public sealed class ContentMediaReferences
{
    public Sprite contextImage;
    public AudioClip narrationClip;
    public DialogueSO dialogue;
    public CutsceneSO cutscene;
}

[System.Serializable]
public sealed class FocusWordDefinition
{
    public string stableId;
    public string latinSpelling;
    public string displayLabel;

    [Tooltip("Approved plain-language meaning of the whole word. Required. Authored by SALIN-172 " +
             "against the SALIN-167/SALIN-188 matrix; the Meaning mastery dimension matches on this.")]
    public string meaning;

    public List<SymbolValueReference> decomposition = new();
    public ContentMediaReferences media = new();
}
