using System.Collections.Generic;
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
    private static readonly Dictionary<string, EnemyDiscoveryCopy> CopyByEnemyID = new()
    {
        {
            "soldado",
            new EnemyDiscoveryCopy(
                "Soldado - The Conscripted Shadows",
                "During the Spanish occupation, many natives were forced into military service under colonial command. They became symbols of obedience to foreign rule.",
                "Marches forward.")
        },
        {
            "fraile",
            new EnemyDiscoveryCopy(
                "Fraile - The Word Keeper",
                "Frailes controlled education, religion, and writing, helping replace Baybayin with the Latin alphabet. Their influence caused generations to forget the old script.",
                "Fades in and out.")
        },
        {
            "guardia",
            new EnemyDiscoveryCopy(
                "Guardia - The Patrol of Control",
                "The Guardia Civil enforced Spanish authority across towns and villages. Their presence discouraged resistance and protected colonial rule.",
                "Moves faster.")
        },
        {
            "capitan",
            new EnemyDiscoveryCopy(
                "Capitan - The Armored Authority",
                "Captains held positions of power and commanded colonial forces. Their rank and protection made them difficult to challenge.",
                "Requires 2 hits.")
        },
        {
            "highrankingfriar",
            new EnemyDiscoveryCopy(
                "High-ranking Friar",
                "A corrupted high-ranking friar who ordered the burning of Baybayin manuscripts during the Spanish era. He wants to erase the script and the memory of the people.",
                "Summons all enemies.")
        },
        {
            "high_ranking_friar",
            new EnemyDiscoveryCopy(
                "High-ranking Friar",
                "A corrupted high-ranking friar who ordered the burning of Baybayin manuscripts during the Spanish era. He wants to erase the script and the memory of the people.",
                "Summons all enemies.")
        },
        {
            "high-ranking-friar",
            new EnemyDiscoveryCopy(
                "High-ranking Friar",
                "A corrupted high-ranking friar who ordered the burning of Baybayin manuscripts during the Spanish era. He wants to erase the script and the memory of the people.",
                "Summons all enemies.")
        }
    };

    public static EnemyDiscoveryCopy Resolve(EnemyDataSO data)
    {
        string normalizedID = NormalizeEnemyID(data);
        if (normalizedID != null && CopyByEnemyID.TryGetValue(normalizedID, out EnemyDiscoveryCopy copy))
            return copy;

        return CreateFallbackCopy(normalizedID);
    }

    private static string NormalizeEnemyID(EnemyDataSO data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.enemyID))
            return null;

        return data.enemyID.Trim().ToLowerInvariant();
    }

    private static EnemyDiscoveryCopy CreateFallbackCopy(string normalizedID)
    {
        string title = string.IsNullOrWhiteSpace(normalizedID)
            ? "Unknown"
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalizedID.Replace('_', ' ').Replace('-', ' '));

        return new EnemyDiscoveryCopy(
            title,
            "A new enemy has appeared.",
            "Observe its movement and draw the matching Baybayin character.");
    }
}
