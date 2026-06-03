using System;
using System.Globalization;
using UnityEngine;

public readonly struct EnemyDiscoveryCopy
{
    public EnemyDiscoveryCopy(string title, string description, string power)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Unknown" : title.Trim();
        Description = string.IsNullOrWhiteSpace(description)
            ? "A new enemy has appeared."
            : description.Trim();
        Power = string.IsNullOrWhiteSpace(power)
            ? "Observe its movement and draw the matching Baybayin character."
            : power.Trim();
    }

    public string Title { get; }
    public string Description { get; }
    public string Power { get; }
}

public static class EnemyDiscoveryCopyProvider
{
    private const string PowerSeparator = "Power:";

    public static EnemyDiscoveryCopy Resolve(EnemyDataSO data)
    {
        if (data == null)
            return new EnemyDiscoveryCopy(null, null, null);

        SplitDescription(data.description, out string description, out string power);
        return new EnemyDiscoveryCopy(BuildTitle(data), description, power);
    }

    private static string BuildTitle(EnemyDataSO data)
    {
        string name = string.IsNullOrWhiteSpace(data.displayName)
            ? TitleCaseID(EnemyDiscoveryProgress.NormalizeEnemyID(data))
            : data.displayName.Trim();

        string subtitle = data.discoverySubtitle?.Trim();
        return string.IsNullOrEmpty(subtitle) ? name : $"{name} - {subtitle}";
    }

    private static void SplitDescription(string raw, out string description, out string power)
    {
        description = null;
        power = null;
        if (string.IsNullOrWhiteSpace(raw))
            return;

        int idx = raw.IndexOf(PowerSeparator, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            description = raw.Trim();
            return;
        }

        description = raw.Substring(0, idx).Trim();
        power = raw.Substring(idx + PowerSeparator.Length).Trim();
    }

    private static string TitleCaseID(string normalizedID)
    {
        return string.IsNullOrWhiteSpace(normalizedID)
            ? "Unknown"
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalizedID.Replace('_', ' ').Replace('-', ' '));
    }
}
