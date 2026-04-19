#if UNITY_EDITOR
using UnityEngine;

public class TestDialogueTrigger : MonoBehaviour
{
    [SerializeField] private DialogueController _controller;
    [SerializeField] private DialogueSO _testDialogue;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.D) && _controller != null)
            _controller.Play(_testDialogue);
    }
}
#endif