using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-200: idempotent authoring of the Level 1 narrative — intro/outro
/// dialogue, INA/AMA explanations, and the restored-memory cutscene — attached to
/// the campaign assets produced by <see cref="RevisedCampaignBootstrap"/>. The
/// source copy (with English support translations and SALIN-188 review status)
/// lives in docs/content/level-01-narrative.md; all Filipino copy is a review
/// input for the language and cultural adviser.
/// </summary>
public static class Level1NarrativeBootstrap
{
    private const string DialogueFolder = "Assets/ScriptableObjects/Dialogue";
    private const string CutsceneFolder = "Assets/ScriptableObjects/Cutscenes";
    private const string Narrator = "Tagapagsalaysay";

    [MenuItem("Salinlahi/Campaign/Author Level 1 Narrative")]
    public static void Run()
    {
        DialogueSO intro = EnsureDialogue("Dialogue_Ugat01_Intro",
            Line(Narrator, "Noong unang panahon, isinusulat ng ating mga ninuno ang kanilang mga alaala sa Baybayin."),
            Line(Narrator, "Ngunit dumating ang Paglimot, at unti-unting kinain nito ang mga alaala ng pamilya ni Juan."),
            Line("Juan", "Hindi ko na maalala ang mukha nina Ina at Ama..."),
            Line(Narrator, "Sa bawat titik ng Baybayin na matututunan mo, isang alaala ang maibabalik. Simulan natin sa dalawang salitang pinakamalapit sa puso: INA at AMA."));

        DialogueSO ina = EnsureDialogue("Dialogue_Ugat01_Ina",
            Line(Narrator, "INA — ang nagluwal at nag-aruga. Binubuo ito ng dalawang titik: I at NA."),
            Line(Narrator, "Bakasin mo ang bawat titik upang maibalik ang alaala ni Ina."));

        DialogueSO ama = EnsureDialogue("Dialogue_Ugat01_Ama",
            Line(Narrator, "AMA — ang haligi ng tahanan. Binubuo ito ng dalawang titik: A at MA."),
            Line(Narrator, "Bakasin mo ang bawat titik upang maibalik ang alaala ni Ama."));

        DialogueSO outro = EnsureDialogue("Dialogue_Ugat01_Outro",
            Line("Juan", "Naaalala ko na sila. Sina Ina at Ama."),
            Line(Narrator, "Ito pa lamang ang simula, Juan. Marami pang alaala ang naghihintay na maibalik."));

        CutsceneSO memory = EnsureMemoryCutscene();

        var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
            "Assets/ScriptableObjects/Levels/Level1_Config.asset");
        if (level == null || level.focusWords == null || level.focusWords.Count < 2)
        {
            Debug.LogError(
                "Level1NarrativeBootstrap: run the campaign bootstrap first — Level 1 focus words are missing.");
            return;
        }

        level.introDialogue = intro;
        level.outroDialogue = outro;

        level.focusWords[0].media ??= new ContentMediaReferences();
        level.focusWords[0].media.dialogue = ina;
        level.focusWords[0].media.cutscene = memory;
        level.focusWords[1].media ??= new ContentMediaReferences();
        level.focusWords[1].media.dialogue = ama;
        level.focusWords[1].media.cutscene = memory;

        level.contextMedia ??= new ContentMediaReferences();
        level.contextMedia.dialogue = intro;
        level.contextMedia.cutscene = memory;
        EditorUtility.SetDirty(level);

        // The Ugat era's framing story is the Level 1 intro until SALIN-173
        // authors the campaign-wide narrative.
        var era = AssetDatabase.LoadAssetAtPath<EraConfigSO>(
            "Assets/ScriptableObjects/Themes/Era_01.asset");
        if (era != null)
        {
            era.storyReference = intro;
            era.memoryReference = memory;
            EditorUtility.SetDirty(era);
        }

        AssetDatabase.SaveAssets();
    }

    private static DialogueSO EnsureDialogue(string assetName, params DialogueLine[] lines)
    {
        string path = $"{DialogueFolder}/{assetName}.asset";
        var dialogue = AssetDatabase.LoadAssetAtPath<DialogueSO>(path);
        if (dialogue == null)
        {
            dialogue = ScriptableObject.CreateInstance<DialogueSO>();
            AssetDatabase.CreateAsset(dialogue, path);
        }

        dialogue.lines = lines;
        EditorUtility.SetDirty(dialogue);
        return dialogue;
    }

    private static DialogueLine Line(string speaker, string text)
    {
        return new DialogueLine { speakerName = speaker, text = text };
    }

    private static CutsceneSO EnsureMemoryCutscene()
    {
        string path = $"{CutsceneFolder}/Cutscene_Ugat01_Memory.asset";
        var cutscene = AssetDatabase.LoadAssetAtPath<CutsceneSO>(path);
        if (cutscene == null)
        {
            cutscene = ScriptableObject.CreateInstance<CutsceneSO>();
            AssetDatabase.CreateAsset(cutscene, path);
        }

        cutscene.cutsceneId = "cutscene.ugat.01.memory";
        cutscene.panels = new[]
        {
            Panel("Sa liwanag ng gabing iyon, muling nabuo ang mga mukha."),
            Panel("Naalala ni Juan ang init ng yakap ni Ina at ang tawa ni Ama sa hapag."),
            Panel("Dalawang salita, dalawang alaalang naibalik: INA at AMA."),
        };
        EditorUtility.SetDirty(cutscene);
        return cutscene;
    }

    private static CutscenePanel Panel(string text)
    {
        // Panel art is SALIN-199 manifest scope; the cutscene renders text over
        // the default background until the memory illustrations land.
        return new CutscenePanel { text = text, transitionIn = TransitionType.Fade };
    }
}
