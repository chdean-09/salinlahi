using UnityEngine;

public class ChallengeInputRouter : MonoBehaviour
{
    private ChallengeFlowController _controller;

    public void Bind(ChallengeFlowController controller)
    {
        _controller = controller;
    }

    public void Unbind(ChallengeFlowController controller)
    {
        if (_controller == controller)
            _controller = null;
    }

    private void OnEnable()
    {
        EventBus.OnRecognitionResolved += HandleRecognitionResolved;
        EventBus.OnDrawingFailed += HandleDrawingFailed;
    }

    private void OnDisable()
    {
        EventBus.OnRecognitionResolved -= HandleRecognitionResolved;
        EventBus.OnDrawingFailed -= HandleDrawingFailed;
    }

    private void HandleRecognitionResolved(RecognitionResult result, bool passedThreshold, float threshold)
    {
        if (!ChallengeRuntimeState.IsActive || _controller == null || !passedThreshold)
            return;
        _controller.SubmitTrace(result.characterID);
    }

    private void HandleDrawingFailed()
    {
        if (ChallengeRuntimeState.IsActive && _controller != null)
            _controller.SubmitTrace("NONE");
    }
}
