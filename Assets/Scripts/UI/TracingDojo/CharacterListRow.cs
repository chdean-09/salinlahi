using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterListRow : MonoBehaviour
{
    [SerializeField] private Image _iconImage;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private Button _button;

    public void Bind(BaybayinCharacterSO character, Action<BaybayinCharacterSO> onSelect)
    {
        _iconImage.sprite = character.displaySprite;
        _label.text = character.characterID;
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => onSelect(character));
    }
}
