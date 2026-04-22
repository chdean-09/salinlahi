using UnityEngine;

public class CharacterListPopulator : MonoBehaviour
{
    [SerializeField] private CharacterRegistrySO _registry;
    [SerializeField] private CharacterListRow _rowPrefab;
    [SerializeField] private Transform _content;
    [SerializeField] private TracingDojoController _controller;

    private void Start()
    {
        foreach (Transform child in _content) Destroy(child.gameObject);

        foreach (var character in _registry.All)
        {
            var row = Instantiate(_rowPrefab, _content);
            row.Bind(character, _controller.SelectCharacter);
        }
    }
}
