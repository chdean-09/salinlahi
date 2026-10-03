using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Headless entry point for the revised-campaign validator.
///
/// <see cref="CampaignConfigValidationMenu"/> requires an Editor <c>Selection</c>, so it cannot be
/// driven from batch mode. Every ticket that needed a validator baseline has so far written its own
/// throwaway <c>-executeMethod</c> harness; this is that harness, committed once.
///
/// Usage:
/// <code>
/// Unity -batchmode -quit -nographics -projectPath &lt;project&gt; \
///       -executeMethod CampaignConfigValidationBatch.Run \
///       -salinValidationOut &lt;report.txt&gt; \
///       [-salinCampaign Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset]
///       [-salinProfile Authoring|Strict|Both]   (default: Both)
/// </code>
///
/// Report format (first line is a machine-readable summary, remaining lines are one issue each,
/// sorted deterministically so two runs can be diffed directly):
/// <code>
/// PROFILE=Authoring
/// TOTAL=90 ERRORS=0 WARNINGS=90
/// Warning|REQUIRED_MEDIA_MISSING|campaign.revised-v1.eras[0]...|Required context image...
/// </code>
///
/// Exit code reports whether the *run* succeeded, not whether validation passed: 0 when a report was
/// produced, 2 when the campaign asset could not be loaded or the report could not be written. Parse
/// the ERRORS= field to decide whether the content is acceptable — that keeps a failing campaign from
/// being indistinguishable from a broken harness.
/// </summary>
public static class CampaignConfigValidationBatch
{
    private const string DefaultCampaignAssetPath =
        "Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset";

    private const string CampaignArg = "-salinCampaign";
    private const string OutputArg = "-salinValidationOut";
    private const string ProfileArg = "-salinProfile";

    private const int ExitSuccess = 0;
    private const int ExitHarnessFailure = 2;

    public static void Run()
    {
        string campaignPath = GetArgument(CampaignArg) ?? DefaultCampaignAssetPath;
        string outputPath = GetArgument(OutputArg);
        string profileArg = GetArgument(ProfileArg) ?? "Both";

        CampaignConfigSO campaign = AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(campaignPath);
        if (campaign == null)
        {
            Fail("Could not load a CampaignConfigSO at '" + campaignPath +
                 "'. Pass " + CampaignArg + " <assetPath> to point at a different campaign.");
            return;
        }

        var builder = new StringBuilder();
        try
        {
            // Both profiles by default: a sprint's worth of gates measured Authoring and Strict
            // every time, and running them separately doubles the Unity start-up cost.
            if (!string.Equals(profileArg, "Strict", StringComparison.OrdinalIgnoreCase))
                AppendSection(builder, campaign, ContentValidationProfile.Authoring);

            if (!string.Equals(profileArg, "Authoring", StringComparison.OrdinalIgnoreCase))
                AppendSection(builder, campaign, ContentValidationProfile.Strict);
        }
        catch (Exception exception)
        {
            Fail("The validator threw while validating '" + campaignPath + "': " + exception);
            return;
        }

        string report = builder.ToString();

        if (string.IsNullOrEmpty(outputPath))
        {
            // No output path: the report still has to reach the caller, so put it on stdout.
            Console.WriteLine(report);
        }
        else
        {
            try
            {
                string directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(outputPath, report, new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                Fail("Could not write the report to '" + outputPath + "': " + exception);
                return;
            }
        }

        Debug.Log("[SALIN] Headless campaign validation complete for profile(s): " + profileArg);
        EditorApplication.Exit(ExitSuccess);
    }

    private static void AppendSection(
        StringBuilder builder,
        CampaignConfigSO campaign,
        ContentValidationProfile profile)
    {
        IReadOnlyList<ContentValidationIssue> issues = CampaignConfigValidator.Validate(campaign, profile);
        builder.Append("PROFILE=").Append(profile).Append('\n');
        builder.Append(BuildReport(issues));
    }

    internal static string BuildReport(IReadOnlyList<ContentValidationIssue> issues)
    {
        var lines = new List<string>(issues.Count);
        for (int index = 0; index < issues.Count; index++)
        {
            ContentValidationIssue issue = issues[index];
            lines.Add(
                issue.Severity + "|" +
                issue.Code + "|" +
                issue.Path + "|" +
                Flatten(issue.Message));
        }

        // Sorted so a baseline and an after-run diff cleanly; the validator's emission order is an
        // implementation detail and must not show up as spurious report churn.
        lines.Sort(StringComparer.Ordinal);

        var builder = new StringBuilder();
        builder.Append(SummaryLine(issues)).Append('\n');
        for (int index = 0; index < lines.Count; index++)
            builder.Append(lines[index]).Append('\n');

        return builder.ToString();
    }

    private static string SummaryLine(IReadOnlyList<ContentValidationIssue> issues)
    {
        int errors = Count(issues, ContentValidationSeverity.Error);
        int warnings = Count(issues, ContentValidationSeverity.Warning);
        return string.Format(
            CultureInfo.InvariantCulture,
            "TOTAL={0} ERRORS={1} WARNINGS={2}",
            issues.Count,
            errors,
            warnings);
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

    /// <summary>Keeps one issue on one line so the report stays diffable.</summary>
    private static string Flatten(string message)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;

        return message.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
    }

    private static string GetArgument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.Ordinal))
                return args[index + 1];
        }

        return null;
    }

    private static void Fail(string message)
    {
        Debug.LogError("[SALIN] Headless campaign validation failed. " + message);
        EditorApplication.Exit(ExitHarnessFailure);
    }
}
