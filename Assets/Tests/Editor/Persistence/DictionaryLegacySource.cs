using System;
using System.Collections.Generic;

namespace Salinlahi.Tests.Editor.Persistence
{
    /// <summary>
    /// Dictionary-backed stand-in for historical PlayerPrefs. Setters exist only for
    /// test seeding; production migration sees the read-only ILegacyProgressSource.
    /// </summary>
    public sealed class DictionaryLegacySource : ILegacyProgressSource
    {
        private readonly Dictionary<string, object> _values =
            new Dictionary<string, object>(StringComparer.Ordinal);

        public static DictionaryLegacySource CreateRepresentativeHistoricalSave()
        {
            DictionaryLegacySource source = new DictionaryLegacySource();
            source.SetInt("SelectedLevel", 4);
            for (int i = 1; i <= 5; i++)
                source.SetInt("salinlahi.progress.unlocked." + i, 1);
            for (int i = 1; i <= 4; i++)
                source.SetInt("salinlahi.progress.stars." + i, 3);
            source.SetInt("salinlahi.tutorial.level1_ftue_seen", 1);
            source.SetString("salinlahi.almanac.character_ids", "A,BA,KA");
            source.SetString("salinlahi.discovery.enemy_ids", "soldado,fraile");
            source.SetFloat("salinlahi.audio.master_volume", 0.8f);
            source.SetFloat("salinlahi.audio.bgm_volume", 0.55f);
            source.SetFloat("salinlahi.audio.sfx_volume", 0.35f);
            return source;
        }

        public void SetInt(string key, int value) => _values[key] = value;
        public void SetFloat(string key, float value) => _values[key] = value;
        public void SetString(string key, string value) => _values[key] = value;

        public bool HasKey(string key) => _values.ContainsKey(key);

        public int GetInt(string key, int defaultValue) =>
            _values.TryGetValue(key, out object value) && value is int typed ? typed : defaultValue;

        public float GetFloat(string key, float defaultValue) =>
            _values.TryGetValue(key, out object value) && value is float typed ? typed : defaultValue;

        public string GetString(string key, string defaultValue) =>
            _values.TryGetValue(key, out object value) && value is string typed ? typed : defaultValue;
    }
}
