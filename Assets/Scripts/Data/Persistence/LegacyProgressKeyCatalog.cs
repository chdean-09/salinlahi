using System;
using System.Collections.Generic;

public enum LegacyProgressValueType
{
    Int,
    Float,
    String,
}

public sealed class LegacyProgressKeyDefinition
{
    public string Key { get; }
    public LegacyProgressValueType ValueType { get; }

    public LegacyProgressKeyDefinition(string key, LegacyProgressValueType valueType)
    {
        Key = key;
        ValueType = valueType;
    }
}

public static class LegacyProgressKeyCatalog
{
    public static IReadOnlyList<LegacyProgressKeyDefinition> All { get; } = Build();

    private static IReadOnlyList<LegacyProgressKeyDefinition> Build()
    {
        var result = new List<LegacyProgressKeyDefinition>
        {
            new LegacyProgressKeyDefinition("SelectedLevel", LegacyProgressValueType.Int),
        };
        for (int i = 1; i <= 15; i++)
        {
            result.Add(new LegacyProgressKeyDefinition("salinlahi.progress.unlocked." + i, LegacyProgressValueType.Int));
            result.Add(new LegacyProgressKeyDefinition("salinlahi.progress.stars." + i, LegacyProgressValueType.Int));
        }
        result.Add(new LegacyProgressKeyDefinition("salinlahi.progress.endless_unlocked", LegacyProgressValueType.Int));
        result.Add(new LegacyProgressKeyDefinition("salinlahi.tutorial.level1_ftue_seen", LegacyProgressValueType.Int));
        result.Add(new LegacyProgressKeyDefinition("salinlahi.tutorial.level1_ftue_beat_index", LegacyProgressValueType.Int));
        result.Add(new LegacyProgressKeyDefinition("salinlahi.tutorial.level2_advanced_focus_chain_v3_seen", LegacyProgressValueType.Int));
        result.Add(new LegacyProgressKeyDefinition("salinlahi.tutorial.level2_advanced_focus_chain_v3_beat_index", LegacyProgressValueType.Int));
        result.Add(new LegacyProgressKeyDefinition("salinlahi.tutorial.level2_advanced_seen", LegacyProgressValueType.Int));
        result.Add(new LegacyProgressKeyDefinition("salinlahi.tutorial.level2_advanced_beat_index", LegacyProgressValueType.Int));
        result.Add(new LegacyProgressKeyDefinition("salinlahi.tutorial.level2_advanced_focus_v2_seen", LegacyProgressValueType.Int));
        result.Add(new LegacyProgressKeyDefinition("salinlahi.tutorial.level2_advanced_focus_v2_beat_index", LegacyProgressValueType.Int));
        result.Add(new LegacyProgressKeyDefinition("salinlahi.almanac.character_ids", LegacyProgressValueType.String));
        result.Add(new LegacyProgressKeyDefinition("salinlahi.discovery.enemy_ids", LegacyProgressValueType.String));
        result.Add(new LegacyProgressKeyDefinition("salinlahi.almanac.boss_ids", LegacyProgressValueType.String));
        result.Add(new LegacyProgressKeyDefinition("salinlahi.audio.master_volume", LegacyProgressValueType.Float));
        result.Add(new LegacyProgressKeyDefinition("salinlahi.audio.bgm_volume", LegacyProgressValueType.Float));
        result.Add(new LegacyProgressKeyDefinition("salinlahi.audio.sfx_volume", LegacyProgressValueType.Float));
        return result;
    }
}
