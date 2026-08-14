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
            _retryText.text = notice.kind == CampaignSaveNoticeKind.Blocking ? "Retry" : "Continue";
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

    private static string TitleFor(CampaignSaveNotice notice)
    {
        if (notice.kind == CampaignSaveNoticeKind.Migration) return "Your Journey Has Been Updated";
        if (notice.kind == CampaignSaveNoticeKind.Recovery) return "Journey Save Recovered";
        return "Journey Data Cannot Be Opened";
    }

    private static string BodyFor(CampaignSaveNotice notice)
    {
        if (notice.kind == CampaignSaveNoticeKind.Migration)
            return "Your previous journey progress was archived safely. Audio preferences were preserved. The revised journey begins at Ugat Level 1.";
        if (notice.kind == CampaignSaveNoticeKind.Recovery)
            return "No valid journey save could be recovered, so a clean journey was created. Failed local files were retained for diagnostics.";
        if (notice.reasonCode == "UnsupportedSchema" || notice.reasonCode == "unsupported-schema")
            return "This journey was created by a newer version of Salinlahi. Update the game to continue. Progress was not changed.";
        if (notice.reasonCode == "BlockedIo" || notice.reasonCode == "io-failure")
            return "Journey files could not be read. Check device storage and try again. Progress was not changed.";
        return "The revised journey content is incomplete or incompatible. Progress was not changed.";
    }
}
