using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates the Ugat Levels 2-5 narrative assets from the copy of record in
/// <c>docs/content/ugat-levels-2-5-narrative.md</c> (SALIN-205), then wires what can be wired.
/// </summary>
/// <remarks>
/// The copy lives in the Markdown document, not here: this tool is a transcription of it, so the two
/// must be edited together. It is idempotent -- an existing asset is rewritten in place rather than
/// duplicated, so a copy revision is applied by editing the table below and re-running.
///
/// Per-word dialogue assets are generated but deliberately left unattached. Levels 2-5 currently have
/// zero focus-word slots (SALIN-204 is marked Done but authored none), and
/// FocusWordDefinition.media.dialogue is the only field they could hang on. The copy is therefore
/// ready as data and waits on that ticket rather than inventing slots this one does not own.
/// </remarks>
public static class UgatNarrativeContentTool
{
    private const string DialogueFolder = "Assets/ScriptableObjects/Dialogue";
    private const string Narrator = "Tagapagsalaysay";
    private const string Juan = "Juan";

    private sealed class Block
    {
        public string AssetName;
        public (string Speaker, string Text)[] Lines;
    }

    private sealed class LevelContent
    {
        public string StableId;
        public string MemoryId;
        public Block Intro;
        public Block Slot1;
        public Block Slot2;
        public Block Outro;
    }

    [MenuItem("Salinlahi/Campaign/Generate Ugat 2-5 Narrative (SALIN-205)")]
    public static void Generate()
    {
        List<LevelContent> content = BuildContent();
        var levels = new Dictionary<string, LevelConfigSO>();
        foreach (string guid in AssetDatabase.FindAssets("t:LevelConfigSO"))
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (level != null && !string.IsNullOrEmpty(level.stableId))
                levels[level.stableId] = level;
        }

        int written = 0;
        int wired = 0;
        foreach (LevelContent entry in content)
        {
            DialogueSO intro = WriteDialogue(entry.Intro, ref written);
            WriteDialogue(entry.Slot1, ref written);
            WriteDialogue(entry.Slot2, ref written);
            DialogueSO outro = WriteDialogue(entry.Outro, ref written);

            if (!levels.TryGetValue(entry.StableId, out LevelConfigSO level) || level == null)
            {
                UnityEngine.Debug.LogError($"SALIN-205: no LevelConfigSO with stableId '{entry.StableId}'.");
                continue;
            }

            var so = new SerializedObject(level);
            so.FindProperty("introDialogue").objectReferenceValue = intro;
            // Level 1 wires both ends; leaving the outro dangling would strand the level's closing
            // beat -- and at Level 5 that beat is the Ugat ending this ticket is meant to deliver.
            so.FindProperty("outroDialogue").objectReferenceValue = outro;

            // At least one non-blank reward is required by CampaignConfigValidator; without it the
            // level reports REQUIRED_REFERENCE_MISSING on rewardIds.
            SerializedProperty rewards = so.FindProperty("rewardIds");
            rewards.ClearArray();
            rewards.InsertArrayElementAtIndex(0);
            rewards.GetArrayElementAtIndex(0).stringValue = entry.MemoryId;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
            wired++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        UnityEngine.Debug.Log($"SALIN205TOOL: dialogueAssets={written} levelsWired={wired}");
    }

    private static DialogueSO WriteDialogue(Block block, ref int written)
    {
        string path = $"{DialogueFolder}/{block.AssetName}.asset";
        DialogueSO asset = AssetDatabase.LoadAssetAtPath<DialogueSO>(path);
        bool isNew = asset == null;
        if (isNew)
            asset = ScriptableObject.CreateInstance<DialogueSO>();

        var lines = new DialogueLine[block.Lines.Length];
        for (int i = 0; i < block.Lines.Length; i++)
        {
            lines[i] = new DialogueLine
            {
                speakerName = block.Lines[i].Speaker,
                text = block.Lines[i].Text,
                portrait = null,   // portrait art is SALIN-206
            };
        }

        asset.lines = lines;
        if (isNew)
            AssetDatabase.CreateAsset(asset, path);
        else
            EditorUtility.SetDirty(asset);

        written++;
        return asset;
    }

    // Transcribed from docs/content/ugat-levels-2-5-narrative.md. Words come from the approved matrix
    // in docs/technical/TW-SPK-004-educational-content-matrix.md and are not invented here.
    private static List<LevelContent> BuildContent()
    {
        return new List<LevelContent>
        {
            new LevelContent
            {
                StableId = "level.ugat.02",
                MemoryId = "memory.ugat.02",
                Intro = new Block
                {
                    AssetName = "Dialogue_Ugat02_Intro",
                    Lines = new[]
                    {
                        (Narrator, "Bumalik si Juan sa lumang tahanan. May naaninag siyang anino ng isang batang naglalaro sa bakuran."),
                        (Juan, "Kilala ko ang batang iyan... ngunit hindi ko makita ang kanyang mukha."),
                        (Narrator, "Ang Paglimot ay unang kumukuha sa mga mata. Ibalik mo ang dalawang salitang magpapakita muli sa iyo: BATA at MATA."),
                    },
                },
                Slot1 = new Block
                {
                    AssetName = "Dialogue_Ugat02_Bata",
                    Lines = new[]
                    {
                        (Narrator, "BATA — ang musmos na sumisibol, ang simula ng bawat alaala. Binubuo ito ng dalawang titik: BA at TA."),
                        (Narrator, "Bakasin mo ang bawat titik upang maibalik ang alaala ng batang si Juan."),
                    },
                },
                Slot2 = new Block
                {
                    AssetName = "Dialogue_Ugat02_Mata",
                    Lines = new[]
                    {
                        (Narrator, "MATA — ang nakakikita at nakaaalala. Binubuo ito ng dalawang titik: MA at TA."),
                        (Narrator, "Bakasin mo ang bawat titik upang muling makita ang nakaraan."),
                    },
                },
                Outro = new Block
                {
                    AssetName = "Dialogue_Ugat02_Outro",
                    Lines = new[]
                    {
                        (Juan, "Nakikita ko na siya. Ako pala iyon noong bata pa ako."),
                        (Narrator, "Ang matang nakakikita sa sarili ang unang hakbang sa pag-alala, Juan."),
                    },
                },
            },
            new LevelContent
            {
                StableId = "level.ugat.03",
                MemoryId = "memory.ugat.03",
                Intro = new Block
                {
                    AssetName = "Dialogue_Ugat03_Intro",
                    Lines = new[]
                    {
                        (Narrator, "Sa lilim ng punong mangga, may naririnig si Juan na tinig — ang tinig ng kanyang ama."),
                        (Juan, "May sinasabi siya tungkol sa akin. Ngunit hindi ko na maalala ang buong pangungusap."),
                        (Narrator, "Hindi sapat ang isang salita ngayon. Buuin mo ang buong pangungusap: balikan ang BATA, at hanapin ang TAMA."),
                    },
                },
                Slot1 = new Block
                {
                    AssetName = "Dialogue_Ugat03_Bata",
                    Lines = new[]
                    {
                        (Narrator, "BATA — nabakas mo na ito noon. Ngayon, gagamitin mo ito sa loob ng isang buong pangungusap. Binubuo pa rin ito ng dalawang titik: BA at TA."),
                        (Narrator, "Bakasin mo itong muli — hindi bilang bagong salita, kundi bilang bahagi ng isang buong diwa."),
                    },
                },
                Slot2 = new Block
                {
                    AssetName = "Dialogue_Ugat03_Tama",
                    Lines = new[]
                    {
                        (Narrator, "TAMA — ang wasto, ang nararapat. Binubuo ito ng dalawang titik: TA at MA."),
                        (Narrator, "Bakasin mo ang bawat titik upang mabuo ang sinabi ni Ama."),
                    },
                },
                Outro = new Block
                {
                    AssetName = "Dialogue_Ugat03_Outro",
                    Lines = new[]
                    {
                        (Juan, "\"Tama ang bata.\" Iyon ang sinabi ni Ama sa akin."),
                        (Narrator, "Hindi lamang salita ang naibalik mo, Juan. Isang buong pangungusap — at ang tiwala ng iyong ama."),
                    },
                },
            },
            new LevelContent
            {
                StableId = "level.ugat.04",
                MemoryId = "memory.ugat.04",
                Intro = new Block
                {
                    AssetName = "Dialogue_Ugat04_Intro",
                    Lines = new[]
                    {
                        (Narrator, "Humina ang liwanag sa daan pauwi. Kakaunti na lamang ang palatandaan."),
                        (Juan, "Alam ko ang mga salitang ito. Nabakas ko na sila noon."),
                        (Narrator, "Kaya nga inaalis ko na ang ilang gabay. Kung tunay mong naaalala sina INA at AMA, mababakas mo sila kahit walang tulong."),
                    },
                },
                Slot1 = new Block
                {
                    AssetName = "Dialogue_Ugat04_Ina",
                    Lines = new[]
                    {
                        (Narrator, "INA — ang nagluwal at nag-aruga. Binubuo ito ng dalawang titik: I at NA."),
                        (Narrator, "Walang gabay sa pagkakataong ito. Bakasin mo mula sa alaala."),
                    },
                },
                Slot2 = new Block
                {
                    AssetName = "Dialogue_Ugat04_Ama",
                    Lines = new[]
                    {
                        (Narrator, "AMA — ang haligi ng tahanan. Binubuo ito ng dalawang titik: A at MA."),
                        (Narrator, "Walang gabay sa pagkakataong ito. Bakasin mo mula sa alaala."),
                    },
                },
                Outro = new Block
                {
                    AssetName = "Dialogue_Ugat04_Outro",
                    Lines = new[]
                    {
                        (Juan, "Hindi ko na kailangan ng palatandaan. Nasa akin na sila."),
                        (Narrator, "Ang alaalang nabakas nang walang gabay ay alaalang tunay nang naibalik."),
                    },
                },
            },
            new LevelContent
            {
                StableId = "level.ugat.05",
                MemoryId = "memory.ugat.05",
                Intro = new Block
                {
                    AssetName = "Dialogue_Ugat05_Intro",
                    Lines = new[]
                    {
                        (Narrator, "Sa dulo ng Ugat, hinarap ni Juan ang Paglimot — ang anino na kumain sa mga alaala ng kanyang pamilya."),
                        (Juan, "Iba na ang panahon. Iba na ang mundo. Ngunit hindi ibig sabihin niyon ay wala na akong natira."),
                        (Narrator, "Tama ka, Juan. May mana kang hindi kayang kainin ng Paglimot. Bakasin mo ang IBA at ang MANA."),
                    },
                },
                Slot1 = new Block
                {
                    AssetName = "Dialogue_Ugat05_Iba",
                    Lines = new[]
                    {
                        (Narrator, "IBA — ang naiiba, ang hindi katulad ng dati. Binubuo ito ng dalawang titik: I at BA."),
                        (Narrator, "Bakasin mo ito upang tanggapin na nagbabago ang panahon."),
                    },
                },
                Slot2 = new Block
                {
                    AssetName = "Dialogue_Ugat05_Mana",
                    Lines = new[]
                    {
                        (Narrator, "MANA — ang minana mula sa nauna, ang ipinapasa sa susunod. Binubuo ito ng dalawang titik: MA at NA."),
                        (Narrator, "Bakasin mo ito upang angkinin ang iyong pamana."),
                    },
                },
                // The Ugat ending. Hands off to Ugnayan without naming events that belong to it.
                Outro = new Block
                {
                    AssetName = "Dialogue_Ugat05_Outro",
                    Lines = new[]
                    {
                        (Juan, "Iba na nga ang panahon. Ngunit ang mana ko ay nasa akin pa rin — nasa mga titik na natutunan kong bakasin."),
                        (Narrator, "Ito ang Ugat, Juan: ang pinanggalingan. Malalim na ang iyong ugat ngayon."),
                        (Narrator, "Sa susunod na yugto, ang Ugnayan — kung paano nagkakaugnay-ugnay ang isa't isa."),
                    },
                },
            },
        };
    }
}
