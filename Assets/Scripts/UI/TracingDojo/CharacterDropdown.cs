using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterDropdown : MonoBehaviour
{
    [SerializeField] private TMP_Text _headerLabel;
    [SerializeField] private Button _headerButton;
    [SerializeField] private GameObject _panel;
    [SerializeField] private GameObject _backdrop;
    [SerializeField] private Button _backdropButton;
    [SerializeField] private string _placeholderText = "Select \u25BE";

    private void Awake()
    {
        _headerButton.onClick.AddListener(Toggle);
        _backdropButton.onClick.AddListener(Close);
        _headerLabel.text = _placeholderText;
        SetOpen(false);
    }

    public void SetCurrentCharacter(BaybayinCharacterSO character)
    {
        _headerLabel.text = character != null
            ? $"{character.characterID} \u25BE"
            : _placeholderText;
    }

    public void Toggle() => SetOpen(!_panel.activeSelf);

    public void Close() => SetOpen(false);

    private void SetOpen(bool open)
    {
        _panel.SetActive(open);
        _backdrop.SetActive(open);
    }
}
