using System.Collections.Generic;
using UnityEngine;

public class CharacterListPopulator : MonoBehaviour
{
    [Tooltip("Legacy source, used only while SaveManager is not on the revised path.")]
    [SerializeField] private CharacterRegistrySO _registry;
    [SerializeField] private CharacterListRow _rowPrefab;
    [SerializeField] private Transform _content;
    [SerializeField] private TracingDojoController _controller;

    private void Start()
    {
        foreach (Transform child in _content) Destroy(child.gameObject);

        foreach (BaybayinCharacterSO character in ResolveSelectableCharacters())
        {
            var row = Instantiate(_rowPrefab, _content);
            row.Bind(character, _controller.SelectCharacter);
        }
    }

    /// <summary>
    /// On the revised path the dojo may only offer symbols the player has actually been taught.
    /// Until SALIN-172 authors a campaign asset SaveManager stays in Legacy mode, where the
    /// registry remains the source so the dojo keeps working.
    /// </summary>
    private IReadOnlyList<BaybayinCharacterSO> ResolveSelectableCharacters()
    {
        if (SaveManager.Instance != null &&
            SaveManager.Instance.Mode == SaveManagerMode.RevisedReady &&
            SaveManager.Instance.Campaign != null)
        {
            return BuildSelectableList(
                SaveManager.Instance.Campaign,
                SaveManager.Instance.LearningState.IntroducedSymbolIds);
        }

        return _registry != null ? _registry.All : new List<BaybayinCharacterSO>();
    }

    public static IReadOnlyList<BaybayinCharacterSO> BuildSelectableList(
        CampaignConfigSO campaign, IReadOnlyCollection<string> introducedSymbolIds)
    {
        var selectable = new List<BaybayinCharacterSO>();
        if (campaign?.symbols == null || introducedSymbolIds == null)
            return selectable;

        var introduced = new HashSet<string>(introducedSymbolIds, System.StringComparer.Ordinal);
        for (int i = 0; i < campaign.symbols.Count; i++)
        {
            BaybayinCharacterSO symbol = campaign.symbols[i];
            if (symbol != null && introduced.Contains(symbol.stableId))
                selectable.Add(symbol);
        }

        return selectable;
    }
}
