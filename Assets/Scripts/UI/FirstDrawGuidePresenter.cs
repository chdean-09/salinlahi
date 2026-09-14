using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One low-opacity trace guide, the first time each glyph is the needed symbol.
///
/// <para>
/// <b>It never blocks.</b> No EnterDialoguePause, no TutorialRuntimeState.SetDrawingInputLocked,
/// no Time.timeScale, and the image disables its own raycasts — the player keeps drawing straight
/// through it. Iligaw's lesson is the only thing on Level 1 permitted to gate, which is also why
/// E/I — Iligaw's own syllable — is not in this presenter's step list: its beat-8 draw teaches it.
/// </para>
///
/// <para>
/// <b>Trigger.</b> The brief this was authored against named <c>EventBus.OnActiveClueChanged</c>
/// with a <c>BaybayinCharacterSO</c> payload — that event does not exist. The real hook is the
/// instance event <see cref="ActiveClueDirector.OnActiveClueChanged"/> (<c>Action&lt;Enemy, Enemy&gt;</c>,
/// previous/next), and the needed character is read off the <c>next</c> Enemy. Because
/// <see cref="ActiveClueDirector.Instance"/> may not exist yet when this component enables, binding
/// mirrors <c>ActiveCluePresenter.SubscribeToDirector</c>: re-resolve the instance, unsubscribe from
/// any previously-subscribed director before subscribing to a new one, and seed from
/// <c>CurrentClue</c> once bound. Unlike <c>ActiveCluePresenter</c> — which gets a second bind
/// opportunity from <c>ApplyLevel</c> — this component has no equivalent external call, so it also
/// retries the bind every <see cref="Update"/> until it succeeds, then stops.
/// </para>
/// </summary>
public sealed class FirstDrawGuidePresenter : MonoBehaviour
{
    [Tooltip("The image the guide sprite is drawn into. Raycast target is forced off at Awake.")]
    [SerializeField] private Image _guideImage;

    [Tooltip("Steps carrying each glyph's guide sprite, matched by targetCharacter.characterID.")]
    [SerializeField] private Level1TutorialStepSO[] _steps;

    [Tooltip("Seconds the guide stays up if the player has not drawn the glyph correctly.")]
    [SerializeField] private float _timeoutSeconds = 8f;

    [Range(0f, 1f)]
    [SerializeField] private float _guideAlpha = 0.35f;

    private readonly HashSet<string> _shown = new();
    private Coroutine _routine;
    private ActiveClueDirector _subscribedDirector;

    private void Awake()
    {
        if (_guideImage != null)
        {
            _guideImage.raycastTarget = false;
            _guideImage.enabled = false;
        }
    }

    private void OnEnable() => SubscribeToDirector();

    private void Update()
    {
        // ActiveClueDirector is created by LevelFlowController, which can run after this
        // component's OnEnable. Retrying here is the only bind opportunity this presenter has —
        // ActiveCluePresenter gets a second one from ApplyLevel, which nothing calls on this
        // component — and it costs nothing once bound because SubscribeToDirector short-circuits.
        if (_subscribedDirector == null)
            SubscribeToDirector();
    }

    private void OnDisable()
    {
        if (_subscribedDirector != null)
            _subscribedDirector.OnActiveClueChanged -= HandleActiveClueChanged;
        _subscribedDirector = null;

        Hide();
    }

    private void SubscribeToDirector()
    {
        ActiveClueDirector director = ActiveClueDirector.Instance;
        if (director == null || _subscribedDirector == director)
            return;

        if (_subscribedDirector != null)
            _subscribedDirector.OnActiveClueChanged -= HandleActiveClueChanged;

        _subscribedDirector = director;
        _subscribedDirector.OnActiveClueChanged += HandleActiveClueChanged;

        HandleActiveClueChanged(null, _subscribedDirector.CurrentClue);
    }

    private void HandleActiveClueChanged(Enemy previous, Enemy next)
    {
        string id = next != null && next.Character != null ? next.Character.characterID : null;
        if (!FirstDrawGuideMemory.ShouldShowFor(id, _shown))
            return;

        Level1TutorialStepSO step = FindStep(id);
        if (step == null || step.guideSprite == null)
            return;

        _shown.Add(id.Trim().ToLowerInvariant());

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShowUntilDrawnOrTimeout(step, id));
    }

    private IEnumerator ShowUntilDrawnOrTimeout(Level1TutorialStepSO step, string expectedID)
    {
        _guideImage.sprite = step.guideSprite;
        _guideImage.color = new Color(1f, 1f, 1f, _guideAlpha);
        _guideImage.enabled = true;

        bool drawn = false;
        Action<RecognitionResult, bool, float> handler = (result, passed, _) =>
        {
            if (passed && string.Equals(result.characterID, expectedID,
                    StringComparison.OrdinalIgnoreCase))
                drawn = true;
        };
        EventBus.OnRecognitionResolved += handler;

        try
        {
            float elapsed = 0f;
            while (!drawn && elapsed < _timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        finally
        {
            EventBus.OnRecognitionResolved -= handler;
            Hide();
        }
    }

    private Level1TutorialStepSO FindStep(string characterID)
    {
        if (_steps == null) return null;
        for (int i = 0; i < _steps.Length; i++)
        {
            Level1TutorialStepSO step = _steps[i];
            if (step?.targetCharacter == null) continue;
            if (string.Equals(step.targetCharacter.characterID, characterID,
                    StringComparison.OrdinalIgnoreCase))
                return step;
        }
        return null;
    }

    private void Hide()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        if (_guideImage != null) _guideImage.enabled = false;
    }
}
