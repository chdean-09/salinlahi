using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-212. Removes `Char_RA` from `CharacterRegistry_Default`, bringing the registry in line with
/// the campaign catalog's 17 taught identities.
///
/// RA is a recognised glyph SHAPE (it has its own 5 templates and its own art, and the $P recognizer
/// tells it apart from DA) but it is not a TAUGHT identity: it is absent from
/// `CampaignConfig_RevisedV1.symbols`, has no `firstIntroductionLevelId`, appears in no level, and
/// carries no spoken value -- DA carries both `value.da` and `value.ra`.
///
/// The registry drives player-facing completion surfaces, so the mismatch was a live bug, not
/// cosmetics: `AlmanacController` renders "Learned {unlocked} / {registry.All.Count}", and enemies
/// carry only 17 distinct characters, RA not among them. At 18 the counter could never reach 100%.
///
/// The asset file itself is deliberately KEPT. Deleting it would take the recognition templates and
/// art with it, which are correct at 18.
/// </summary>
public static class RegistryTaughtSetTool
{
    private const string RegistryPath = "Assets/ScriptableObjects/Characters/CharacterRegistry_Default.asset";
    private const string RemoveId = "RA";

    [MenuItem("Salinlahi/SALIN-212/Align Registry To Taught Set")]
    public static void Apply()
    {
        // Load and mutate. CreateAsset over an existing path would reissue the GUID and unwire the
        // four scenes that reference this registry.
        var registry = AssetDatabase.LoadAssetAtPath<CharacterRegistrySO>(RegistryPath);
        if (registry == null) { Debug.LogError($"{RegistryPath} not found."); return; }

        var so = new SerializedObject(registry);
        SerializedProperty all = so.FindProperty("All");

        int index = -1;
        for (int i = 0; i < all.arraySize; i++)
        {
            var c = all.GetArrayElementAtIndex(i).objectReferenceValue as BaybayinCharacterSO;
            if (c != null && c.characterID == RemoveId) { index = i; break; }
        }

        if (index < 0)
        {
            Debug.Log($"Registry already aligned: {all.arraySize} entries, no {RemoveId}.");
            return;
        }

        int before = all.arraySize;
        all.DeleteArrayElementAtIndex(index);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();

        string ids = string.Join(", ", registry.All.Where(c => c != null).Select(c => c.characterID));
        Debug.Log($"Registry {before} -> {registry.All.Count} (removed {RemoveId} at index {index})\n{ids}");
    }
}
