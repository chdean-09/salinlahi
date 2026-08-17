using UnityEditor;

public static class CampaignOutcomeFaultMenu
{
    [MenuItem("Salinlahi/Debug/SALIN-174/Fail Next Save Promotion")]
    private static void FailNextSavePromotion()
    {
        CampaignSaveFileStorage.EditorFailNextAt = StorageFaultPoint.PromoteTemporary;
    }

    [MenuItem("Salinlahi/Debug/SALIN-174/Clear Injected Save Fault")]
    private static void ClearInjectedSaveFault()
    {
        CampaignSaveFileStorage.EditorFailNextAt = StorageFaultPoint.None;
    }
}
