using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CampaignSaveNoticePanel : MonoBehaviour
{
    [SerializeField] private GameObject _overlayRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private TMP_Text _retryText;

    public bool HasRequiredReferences => _overlayRoot != null && _titleText != null &&
        _bodyText != null && _confirmButton != null;

    private void Awake()
    {
        if (_confirmButton != null)
            _confirmButton.onClick.AddListener(HandleConfirmation);
        Hide();
    }

    public void Present(CampaignSaveNotice notice)
    {
        if (!HasRequiredReferences || notice == null || notice.kind == CampaignSaveNoticeKind.None)
        {
            Hide();
            return;
        }

        _titleText.text = TitleFor(notice);
        _bodyText.text = BodyFor(notice);
        if (_retryText != null)
            _retryText.text = CampaignSaveNoticeCopy.ConfirmLabel(notice.kind);
        _overlayRoot.SetActive(true);
        _confirmButton.interactable = true;
    }

    public void Hide()
    {
        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);
    }

    private void HandleConfirmation()
    {
        if (SaveManager.Instance == null)
            return;
        _confirmButton.interactable = false;
        if (SaveManager.Instance.PendingNotice.kind == CampaignSaveNoticeKind.Blocking)
        {
            SaveManager.Instance.RetryInitialization();
            SaveManager.Instance.RefreshPendingNotice();
            _confirmButton.interactable = true;
            return;
        }
        if (SaveManager.Instance.Repository != null && SaveManager.Instance.Repository.TryAcknowledgePendingNotice())
        {
            SaveManager.Instance.RefreshPendingNotice();
            Hide();
        }
        else
            _confirmButton.interactable = true;
    }

    // SALIN-272: the strings themselves live in CampaignSaveNoticeCopy, behind a
    // NOT PRODUCT-APPROVED banner, so content can be rewritten without touching flow.
    private static string TitleFor(CampaignSaveNotice notice) =>
        CampaignSaveNoticeCopy.Title(notice.kind);

    private static string BodyFor(CampaignSaveNotice notice) =>
        CampaignSaveNoticeCopy.Body(notice.kind, notice.reasonCode);
}
