using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CampaignConfigValidationMenu
{
    private const string MenuPath = "Salinlahi/Validation/Validate Revised Campaign";

    [MenuItem(MenuPath)]
    public static void ValidateSelectedCampaign()
    {
        CampaignConfigSO campaign = Selection.activeObject as CampaignConfigSO;
        if (campaign == null)
        {
            EditorUtility.DisplayDialog(
                "Validate Revised Campaign",
                "Select a CampaignConfigSO asset before running revised campaign validation.",
                "OK");
            return;
        }

        IReadOnlyList<ContentValidationIssue> issues = Validate(campaign);
        LogResults(issues);
        int errors = Count(issues, ContentValidationSeverity.Error);
        int warnings = Count(issues, ContentValidationSeverity.Warning);
        EditorUtility.DisplayDialog(
            "Validate Revised Campaign",
            errors == 0
                ? "Validation passed.\nWarnings: " + warnings + "."
                : "Validation failed.\nErrors: " + errors + "\nWarnings: " + warnings + ".",
            "OK");
    }

    public static IReadOnlyList<ContentValidationIssue> Validate(CampaignConfigSO campaign)
    {
        return CampaignConfigValidator.Validate(campaign);
    }

    private static void LogResults(IReadOnlyList<ContentValidationIssue> issues)
    {
        int errors = Count(issues, ContentValidationSeverity.Error);
        int warnings = Count(issues, ContentValidationSeverity.Warning);
        for (int index = 0; index < issues.Count; index++)
        {
            ContentValidationIssue issue = issues[index];
            string message = "[SALIN-170] " + issue.Code + " @ " + issue.Path + ": " + issue.Message;
            if (issue.Severity == ContentValidationSeverity.Warning)
                Debug.LogWarning(message, issue.Context);
            else
                Debug.LogError(message, issue.Context);
        }

        Debug.Log("[SALIN-170] Revised campaign validation: " + errors +
                  " error(s), " + warnings + " warning(s).");
    }

    private static int Count(
        IReadOnlyList<ContentValidationIssue> issues,
        ContentValidationSeverity severity)
    {
        int count = 0;
        for (int index = 0; index < issues.Count; index++)
        {
            if (issues[index].Severity == severity)
                count++;
        }

        return count;
    }
}
