using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CampaignOutcomeSaveFailurePanel : MonoBehaviour
{
    [SerializeField] private GameObject _overlayRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private Button _retryButton;
    [SerializeField] private Button _mainMenuButton;

    private Func<CampaignOutcomeCommitResult> _retryAction;
    private Action _acceptedAction;
    private Action _mainMenuAction;
    private bool _busy;
    private bool _acceptedCallbackInvoked;
    private bool _retryListenerBound;
    private bool _mainMenuListenerBound;
    private bool _isPresented;

    public bool HasRequiredReferences => _overlayRoot != null &&
        _titleText != null && _bodyText != null &&
        _retryButton != null && _mainMenuButton != null;

    private void Awake()
    {
        BindListeners();
        if (!_isPresented)
            Hide();
    }

    public void Present(
        CampaignOutcomeCommitResult result,
        Func<CampaignOutcomeCommitResult> retryAction,
        Action acceptedAction,
        Action mainMenuAction)
    {
        if (Application.isPlaying && !HasRequiredReferences)
            BuildFallbackUi();

        if (!HasRequiredReferences || result == null)
        {
            Hide();
            return;
        }

        BindListeners();
        _retryAction = retryAction;
        _acceptedAction = acceptedAction;
        _mainMenuAction = mainMenuAction;
        _acceptedCallbackInvoked = false;
        _busy = false;
        _isPresented = true;
        Render(result);
        _overlayRoot.SetActive(true);
        SetButtonsInteractable(true);
    }

    public void Hide()
    {
        _isPresented = false;
        _busy = false;
        SetButtonsInteractable(true);
        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);
    }

    private void BindListeners()
    {
        if (_retryButton != null && !_retryListenerBound)
        {
            _retryButton.onClick.AddListener(HandleRetry);
            _retryListenerBound = true;
        }

        if (_mainMenuButton != null && !_mainMenuListenerBound)
        {
            _mainMenuButton.onClick.AddListener(HandleMainMenu);
            _mainMenuListenerBound = true;
        }
    }

    private void HandleRetry()
    {
        if (_busy || _retryAction == null)
            return;

        _busy = true;
        SetButtonsInteractable(false);
        CampaignOutcomeCommitResult result;
        try
        {
            result = _retryAction();
        }
        catch
        {
            result = CampaignOutcomeCommitResult.Rejected(
                null, CampaignSaveFailureCode.IoFailure, "retry-failed");
        }

        if (result != null && result.IsAccepted)
        {
            Hide();
            if (!_acceptedCallbackInvoked)
            {
                _acceptedCallbackInvoked = true;
                _acceptedAction?.Invoke();
            }
            return;
        }

        _busy = false;
        if (result != null)
            Render(result);
        SetButtonsInteractable(true);
    }

    private void HandleMainMenu()
    {
        if (_busy)
            return;
        _mainMenuAction?.Invoke();
    }

    private void Render(CampaignOutcomeCommitResult result)
    {
        bool pending = result.Status == CampaignOutcomeCommitStatus.PendingRetry;
        _titleText.text = pending
            ? "Your progress is waiting to be saved"
            : "This completion could not be preserved";
        _bodyText.text = pending
            ? "Salinlahi could not save this level completion. Try again now, or return to the Main Menu. Your completion will remain pending and will be retried the next time the game starts."
            : "Salinlahi could not create a valid pending completion. You can try again now, but if you return to the Main Menu you may need to replay this level.";
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (_retryButton != null)
            _retryButton.interactable = interactable;
        if (_mainMenuButton != null)
            _mainMenuButton.interactable = interactable;
    }

    private void BuildFallbackUi()
    {
        if (HasRequiredReferences)
            return;

        _overlayRoot = gameObject;
        Image overlayImage = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 190f / 255f);
        overlayImage.raycastTarget = true;
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        GameObject card = new GameObject("MessageCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(820f, 680f);
        card.GetComponent<Image>().color = new Color32(45, 32, 25, 255);
        card.GetComponent<Image>().raycastTarget = true;

        _titleText = CreateText(card.transform, "TitleText", "Your progress is waiting to be saved", 45f, 120f, 48f);
        _bodyText = CreateText(card.transform, "BodyText", string.Empty, 185f, 280f, 32f);
        _retryButton = CreateButton(card.transform, "RetryButton", "Retry", 145f);
        _mainMenuButton = CreateButton(card.transform, "MainMenuButton", "Main Menu", 25f);
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        string text,
        float top,
        float height,
        float fontSize)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(50f, -top - height);
        rect.offsetMax = new Vector2(-50f, -top);
        return label;
    }

    private static Button CreateButton(Transform parent, string name, string labelText, float y)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(520f, 110f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color32(209, 168, 82, 255);
        image.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        TMP_Text label = CreateText(buttonObject.transform, "Label", labelText, 0f, 110f, 32f);
        RectTransform labelRect = ((Component)label).GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label.color = Color.black;
        return button;
    }
}
