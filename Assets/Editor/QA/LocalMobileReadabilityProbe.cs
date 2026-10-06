using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Temporary local QA driver. Removed after the screenshot audit.
[InitializeOnLoad]
internal static class LocalMobileReadabilityProbe
{
    private static readonly string Folder = Path.GetFullPath("Temp/MobileReadability");
    private static GameObject _gallery;
    private static GameObject _storyClone;
    private static List<ScriptableObject> _copies = new List<ScriptableObject>();
    private static Queue<string> _batch;
    private static string _batchSize;
    private static string _case;
    private static int _stage;
    private static int _done;
    private static int _total;
    private static double _after;
    static LocalMobileReadabilityProbe() { EditorApplication.update += Poll; EditorApplication.update += BatchTick; }
    [Serializable] private class Request
    {
        public string action;
        public int level;
        public string name;
    }
    private static void Poll()
    {
        string input = Path.Combine(Folder, "request.json");
        if (EditorApplication.isCompiling || !File.Exists(input)) return;
        try
        {
            Request request = JsonUtility.FromJson<Request>(File.ReadAllText(input));
            File.Delete(input);
            switch (request.action)
            {
                case "stop": _batch = null; EditorApplication.isPlaying = false; break;
                case "cleanup":
                    if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop QA before cleanup.");
                    AssetDatabase.DeleteAsset("Assets/Editor/QA/LocalReadabilityRunner");
                    AssetDatabase.DeleteAsset("Assets/Editor/QA/LocalMobileReadabilityProbe.cs");
                    AssetDatabase.Refresh();
                    break;
                case "start":
                    if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
                    LevelConfigSO level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>($"Assets/ScriptableObjects/Levels/Level{request.level}_Config.asset");
                    QaSessionWindow window = EditorWindow.GetWindow<QaSessionWindow>();
                    typeof(QaSessionWindow).GetMethod("StartSelectedLevel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { level });
                    break;
                case "protect": Salinlahi.Debug.Sandbox.SandboxMode.SetQaProtectionEnabled(true); break;
                case "load-almanac":
                    UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/_Scenes/Almanac.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
                    break;
                case "advance":
                    CutscenePlayer cutscene = UnityEngine.Object.FindFirstObjectByType<CutscenePlayer>();
                    if (cutscene != null && cutscene.IsPlaying) cutscene.TryAdvanceFromQa();
                    else
                    {
                        DialogueController dialogue = UnityEngine.Object.FindFirstObjectByType<DialogueController>();
                        if (dialogue != null) typeof(DialogueController).GetMethod("OnTapCatcherPressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(dialogue, null);
                    }
                    break;
                case "click":
                    Button button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                        .First(x => x.IsActive() && x.IsInteractable() && PathOf(x.transform).Contains(request.name));
                    button.onClick.Invoke();
                    break;
                case "hint": UnityEngine.Object.FindFirstObjectByType<SentenceHintController>().Open(); break;
                case "capture":
                    ScreenCapture.CaptureScreenshot(Path.Combine(Folder, request.name + ".png"));
                    break;
                case "gallery": ShowGallery(request.level, request.name); break;
                case "batch":
                    _batchSize = request.name;
                    _batch = new Queue<string>(File.ReadAllLines(Path.Combine(Folder, "catalog.txt")).Where(x => request.level == 0 || x.StartsWith(request.level + "|", StringComparison.Ordinal)));
                    _done = 0; _total = _batch.Count; _stage = 0;
                    _after = EditorApplication.timeSinceStartup + 0.5;
                    break;
                case "catalog": WriteCatalog(); break;
                case "scroll":
                    foreach (ScrollRect s in UnityEngine.Object.FindObjectsByType<ScrollRect>(FindObjectsSortMode.None))
                        if (s.IsActive() && PathOf(s.transform).Contains(request.name)) s.verticalNormalizedPosition = request.level == 0 ? 0f : 1f;
                    break;
                case "size":
                    // The Simulator's Screen shim overrides Game View resolutions while open.
                    // Close that view for exact pixel-size captures; reopen it for notch QA.
                    foreach (EditorWindow candidate in Resources.FindObjectsOfTypeAll<EditorWindow>())
                        if (candidate.titleContent.text == "Simulator") candidate.Close();
                    Type viewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                    EditorWindow view = EditorWindow.GetWindow(viewType);
                    string[] dimensions = request.name.Split('x');
                    viewType.GetMethod("SetCustomResolution", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                        .Invoke(view, new object[] { new Vector2(int.Parse(dimensions[0]), int.Parse(dimensions[1])), "Mobile readability QA" });
                    view.Show();
                    view.Focus();
                    break;
                case "api":
                    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                    File.WriteAllText(Path.Combine(Folder, "api.txt"), string.Join("\n", new[] { "UnityEditor.GameView", "UnityEditor.GameViewSizes", "UnityEditor.GameViewSizeGroup", "UnityEditor.GameViewSize" }
                        .SelectMany(n => { Type type = typeof(EditorWindow).Assembly.GetType(n); return new[] { n }.Concat(type.GetMembers(flags).Select(m => m.ToString())); })));
                    break;
                case "state": break;
                default: throw new InvalidOperationException("Unknown QA action " + request.action);
            }
            Canvas.ForceUpdateCanvases();
            string[] texts = UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None)
                .Where(x => x.IsActive() && x.GetComponentInParent<CanvasGroup>()?.alpha != 0f)
                .Select(x => { x.ForceMeshUpdate(); return $"{PathOf(x.transform)} | {x.text} | size={x.fontSize:F1} min={x.fontSizeMin:F1} rect={x.rectTransform.rect.size} preferred={x.preferredWidth:F1}x{x.preferredHeight:F1} overflow={x.isTextOverflowing} font={x.font?.faceInfo.familyName}"; }).ToArray();
            string[] buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(x => x.IsActive() && x.IsInteractable()).Select(x => PathOf(x.transform)).ToArray();
            File.WriteAllText(Path.Combine(Folder, "response.txt"), $"{DateTime.UtcNow:o}\n{request.action} OK\nPlay={EditorApplication.isPlaying} Screen={Screen.width}x{Screen.height} State={GameManager.Instance?.CurrentState} Qa={QaSessionContext.IsActive} Level={QaSessionContext.SelectedLevelNumber}\n" + string.Join("\n", texts) + "\nBUTTONS\n" + string.Join("\n", buttons));
        }
        catch (Exception exception) { File.WriteAllText(Path.Combine(Folder, "response.txt"), exception.ToString()); Debug.LogException(exception); }
    }
    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

    private static T Add<T>() where T : Component
    {
        GameObject host = new GameObject(typeof(T).Name, typeof(RectTransform));
        host.transform.SetParent(_gallery.transform, false);
        ScrollPanelArt.SetAnchors(host.GetComponent<RectTransform>(), Rect.MinMaxRect(0f, 0f, 1f, 1f));
        return host.AddComponent<T>();
    }
    private static void ShowGallery(int number, string feature)
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Gallery requires the isolated QA Play Mode session.");
        if (_gallery != null) UnityEngine.Object.DestroyImmediate(_gallery);
        if (_storyClone != null) UnityEngine.Object.DestroyImmediate(_storyClone);
        foreach (ScriptableObject copy in _copies) if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
        _copies.Clear();
        foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
        _gallery = new GameObject("MobileReadabilityGallery", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas c = _gallery.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 2000;
        CanvasScaler scaler = _gallery.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        LevelConfigSO level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>($"Assets/ScriptableObjects/Levels/Level{number}_Config.asset");
        string[] parts = feature.Split(':');
        int index = parts.Length > 1 ? int.Parse(parts[1]) : 0;
        switch (parts[0])
        {
            case "ready": Add<LevelReadyScreenController>().Present(level).MoveNext(); break;
            case "preview": Add<FocusWordPreviewController>().Present(level).MoveNext(); break;
            case "symbol":
            case "authored-symbol":
                SymbolLearningCardController card = parts[0] == "authored-symbol" ? CloneSymbolCard() : Add<SymbolLearningCardController>();
                card.Present(level).MoveNext();
                typeof(SymbolLearningCardController).GetMethod("PresentCard", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(card, new object[] { index });
                break;
            case "sentence":
                SentenceHintController hint = Add<SentenceHintController>();
                hint.ApplyLevel(level);
                typeof(SentenceHintController).GetMethod("EnsureOverlay", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hint, null);
                ((GameObject)typeof(SentenceHintController).GetField("_overlayRoot", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hint)).SetActive(true);
                break;
            case "challenge":
            case "hint":
            case "revealed":
            case "hint-empty":
            case "hint-exhausted":
                ChallengeSequenceSO sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
                _copies.Add(sequence);
                sequence.units = new[] { level.challengeSequence.units[index] };
                ChallengeSession session = new ChallengeSession(sequence, policy: level.challengePolicy);
                session.Enter();
                if (parts[0] == "challenge") Add<ChallengeModeUI>().Render(session);
                else
                {
                    HintModal modal = HintModal.CreateRuntime(_gallery.transform);
                    string synonyms = level.focusWords.FirstOrDefault()?.hintSynonyms ?? string.Empty;
                    modal.Open(session, parts[0] == "hint-empty" ? string.Empty : synonyms, session.RequestHint);
                    if (parts[0] == "revealed") modal.Confirm();
                    if (parts[0] == "hint-exhausted") typeof(HintModal).GetMethod("ShowExhausted", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(modal, null);
                }
                break;
            case "dialogue":
            case "dialogue-top":
                DialogueLine line = (parts[0] == "dialogue-top" ? BaseLines(level) : Lines(level)).ToArray()[index];
                DialogueSO dialogue = ScriptableObject.CreateInstance<DialogueSO>();
                _copies.Add(dialogue);
                dialogue.lines = new[] { line };
                DialogueController controller = DialogueController.CreateRuntime();
                controller.transform.SetParent(_gallery.transform, false);
                ScrollPanelArt.SetAnchors(controller.GetComponent<RectTransform>(), Rect.MinMaxRect(0f, 0f, 1f, 1f));
                typeof(DialogueController).GetField("_charsPerSecond", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, 0f);
                typeof(DialogueController).GetField("_presentAtTop", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, parts[0] == "dialogue-top");
                // Render the actual controller's line without requiring a campaign state transition.
                typeof(DialogueController).GetField("_currentDialogue", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, dialogue);
                ((GameObject)typeof(DialogueController).GetField("_overlayPanel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller)).SetActive(true);
                typeof(DialogueController).GetMethod("ShowLine", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, new object[] { line });
                break;
            case "almanac":
            case "unlock":
                AlmanacEnemyEntry subject = parts[0] == "almanac" ? Registry().entries[index] : null;
                AlmanacDetailScroll originalDetail = UnityEngine.Object.FindObjectsByType<AlmanacDetailScroll>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(x => x.gameObject.scene.name == (parts[0] == "almanac" ? "Almanac" : "Gameplay"));
                _storyClone = UnityEngine.Object.Instantiate(originalDetail.gameObject, _gallery.transform, false);
                _storyClone.name = "MobileReadabilityAlmanac";
                _storyClone.SetActive(true);
                if (subject != null) _storyClone.GetComponent<AlmanacDetailScroll>().Show(subject.ResolvePortrait(), subject.ResolveDisplayName(), subject.ResolveDescription(), subject.ResolveGlyph(), subject.ResolveGlyphLabel());
                else
                {
                    BaybayinCharacterSO character = level.allowedCharacters[index];
                    _storyClone.GetComponent<AlmanacDetailScroll>().Show(character.almanacSprite != null ? character.almanacSprite : character.displaySprite, $"\"{character.characterID}\"", character.description);
                }
                break;
            case "pause-restart":
            case "pause-leave":
                PauseMenuUI pause = Add<PauseMenuUI>();
                Type pendingType = typeof(PauseMenuUI).GetNestedType("PendingAction", BindingFlags.NonPublic);
                typeof(PauseMenuUI).GetMethod("RequestConfirmation", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pause, new[] { Enum.Parse(pendingType, parts[0] == "pause-restart" ? "Restart" : "Leave") });
                break;
            case "save-pending":
            case "save-rejected":
                CampaignOutcomeCommitResult result = parts[0] == "save-pending" ? CampaignOutcomeCommitResult.PendingRetry(null, CampaignSaveFailureCode.IoFailure, "QA") : CampaignOutcomeCommitResult.Rejected(null, CampaignSaveFailureCode.IoFailure, "QA");
                Add<CampaignOutcomeSaveFailurePanel>().Present(result, () => result, () => { }, () => { });
                break;
            case "reset":
            case "reset-success":
            case "reset-failed":
                ResetJourneyConfirmationPanel reset = Add<ResetJourneyConfirmationPanel>();
                reset.Present(() => ResetJourneyOutcome.Succeeded, () => { });
                Type stateType = typeof(ResetJourneyConfirmationPanel).GetNestedType("PanelState", BindingFlags.NonPublic);
                typeof(ResetJourneyConfirmationPanel).GetMethod("ApplyState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(reset, new[] { Enum.Parse(stateType, parts[0] == "reset-success" ? "Succeeded" : parts[0] == "reset-failed" ? "Failed" : "Confirming") });
                break;
            case "wave": Add<WaveClearedScreenUI>().Present(3, 3, () => { }); break;
            case "victory":
                VictoryScreenUI originalVictory = UnityEngine.Object.FindObjectsByType<VictoryScreenUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).First();
                _storyClone = UnityEngine.Object.Instantiate(originalVictory.gameObject, _gallery.transform, false);
                _storyClone.name = "MobileReadabilityVictory";
                _storyClone.SetActive(true);
                VictoryScreenUI victory = _storyClone.GetComponent<VictoryScreenUI>();
                victory.ConfigureEraCompletionAction(() => { }, number == 15 ? EraCompletionCopy.CompleteJourneyLabel : EraCompletionCopy.EnterNextEraLabel);
                victory.PresentResults(new LevelResults(new Dictionary<string, float>(), 3), number % 5 == 0);
                victory.PresentResultsSummary(new LevelResultsViewData { Stars = 3, Score = 100, HeartsRemaining = 3, HeartsMax = 3, RestoredLabels = level.focusWords.Select(x => x.displayLabel).ToArray() });
                break;
            case "defeat":
                DefeatScreenUI originalDefeat = UnityEngine.Object.FindObjectsByType<DefeatScreenUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).First();
                _storyClone = UnityEngine.Object.Instantiate(originalDefeat.gameObject, _gallery.transform, false);
                _storyClone.name = "MobileReadabilityDefeat";
                _storyClone.SetActive(true);
                _storyClone.GetComponent<DefeatScreenUI>().Show();
                break;
            case "settings":
                SettingsPanel originalSettings = UnityEngine.Object.FindObjectsByType<SettingsPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).First();
                _storyClone = UnityEngine.Object.Instantiate(originalSettings.gameObject, _gallery.transform, false);
                _storyClone.name = "MobileReadabilitySettings";
                _storyClone.GetComponent<SettingsPanel>().Show();
                break;
            case "cutscene":
                CutscenePlayer original = UnityEngine.Object.FindObjectsByType<CutscenePlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None).First();
                _storyClone = UnityEngine.Object.Instantiate(original.gameObject);
                _storyClone.name = "MobileReadabilityStory";
                foreach (Canvas canvas in _storyClone.GetComponentsInChildren<Canvas>(true)) { canvas.enabled = true; canvas.sortingOrder = 2100; }
                _storyClone.SetActive(true);
                CutsceneSO story = ScriptableObject.CreateInstance<CutsceneSO>();
                _copies.Add(story);
                CutscenePanel panel = Cutscenes(level).SelectMany(x => x.panels ?? Array.Empty<CutscenePanel>()).ToArray()[index];
                panel.typewriterSpeed = 0f;
                panel.transitionDuration = 0.05f;
                panel.transitionIn = TransitionType.None;
                story.panels = new[] { panel };
                story.defaultTypewriterSpeed = 0f;
                _storyClone.GetComponent<CutscenePlayer>().Play(story);
                break;
            case "boss":
            case "boss-library":
                BossTutorialScroll originalBoss = UnityEngine.Object.FindObjectsByType<BossTutorialScroll>(FindObjectsInactive.Include, FindObjectsSortMode.None).First();
                _storyClone = UnityEngine.Object.Instantiate(originalBoss.gameObject, _gallery.transform, false);
                _storyClone.name = "MobileReadabilityBoss";
                _storyClone.SetActive(true);
                BossTutorialScroll boss = _storyClone.GetComponent<BossTutorialScroll>();
                boss.Show(parts[0] == "boss" ? level.bossConfig.tutorial.pages : BossLibrary().pages);
                for (int pageIndex = 0; pageIndex < index; pageIndex++)
                    typeof(BossTutorialScroll).GetMethod("GoRight", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(boss, null);
                break;
            case "exit": Add<ExitConfirmationPanel>().Present(); break;
            case "locked": Add<LevelLockNoticePanel>().PresentPrerequisite(level.levelName, number == 6 || number == 11, level.chapterName); break;
            case "locked-objective": Add<LevelLockNoticePanel>().PresentMissingObjective(new[] { LevelObjectives.StoryViewed, LevelObjectives.SymbolsPracticed, LevelObjectives.WordsRestored, LevelObjectives.ContextPassed, LevelObjectives.FinalSyllableRestored }[index], CampaignLevelLabel.Format(Campaign().eras[0].eraName, number)); break;
            case "missing": Add<LevelContentMissingPanel>().Present(LevelPhase.SymbolLearning, () => { }); break;
            case "ending":
            case "ending-credits":
                CampaignEndingScreenUI ending = Add<CampaignEndingScreenUI>();
                ending.Present();
                if (parts[0] == "ending-credits")
                {
                    float cycle = (float)typeof(CampaignEndingScreenUI).GetField("_cycleHeight", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ending);
                    typeof(CampaignEndingScreenUI).GetField("_scrollOffset", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ending, cycle * index / 4f);
                    typeof(CampaignEndingScreenUI).GetMethod("PositionCredits", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ending, null);
                }
                break;
            case "archive":
                MemoryArchiveController emptyArchive = Add<MemoryArchiveController>();
                emptyArchive.Present(Campaign(), Array.Empty<string>(), () => { });
                System.Collections.IList entries = (System.Collections.IList)typeof(MemoryArchiveController).GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(emptyArchive);
                entries.Clear();
                foreach (MemoryArchiveEntry lockedEntry in MemoryArchiveModel.Build(Campaign(), null, false)) entries.Add(lockedEntry);
                typeof(MemoryArchiveController).GetMethod("RenderRows", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(emptyArchive, null);
                ((TMP_Text)typeof(MemoryArchiveController).GetField("_emptyStateText", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(emptyArchive)).gameObject.SetActive(true);
                RectTransform emptyViewport = emptyArchive.transform.Find("Viewport").GetComponent<RectTransform>();
                emptyViewport.offsetMax = new Vector2(-60f, -488f);
                break;
            case "archive-full":
                Add<MemoryArchiveController>().Present(Campaign(), MemoryArchiveModel.Build(Campaign(), null, true).Select(x => x.MemoryId).Where(x => x != null).ToArray(), () => { }); break;
            case "memory":
            case "memory-back":
            case "claim":
                MemoryArchiveEntry entry = MemoryArchiveModel.Build(Campaign(), null, true).First(x => x.LevelNumber == number);
                if (parts[0] == "claim") Add<MemoryClaimPanel>().Present(entry, 5, () => { });
                else
                {
                    MemoryCardUI memory = Add<MemoryCardUI>();
                    if (!memory.Present(entry, 5, () => { })) throw new InvalidOperationException("No authored memory card for level " + number);
                    if (parts[0] == "memory-back") memory.Flip();
                }
                break;
            case "era":
            case "era-locked":
                EraConfigSO era = Campaign().eras.First(x => x.levels.Contains(level));
                Add<EraCompletionScreenUI>().Present(era, MemoryArchiveModel.BuildForEra(era, null, parts[0] == "era"), number < 15, () => { }, () => { });
                break;
            default: throw new InvalidOperationException("Unknown gallery feature " + feature);
        }
    }
    private static CampaignConfigSO Campaign() => AssetDatabase.LoadAssetAtPath<CampaignConfigSO>("Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset");
    private static AlmanacEnemyRegistrySO Registry() => AssetDatabase.LoadAssetAtPath<AlmanacEnemyRegistrySO>("Assets/ScriptableObjects/Almanac/AlmanacEnemyRegistry_Default.asset");
    private static BossTutorialSO BossLibrary() => AssetDatabase.LoadAssetAtPath<BossTutorialSO>("Assets/ScriptableObjects/Enemies/Boss Configs/BossTutorial_ElInquisidor.asset");
    private static SymbolLearningCardController CloneSymbolCard()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        SymbolLearningCardController original = UnityEngine.Object.FindObjectsByType<SymbolLearningCardController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(x => x.GetType().GetField("_panelRoot", flags).GetValue(x) is GameObject);
        GameObject originalPanel = (GameObject)typeof(SymbolLearningCardController).GetField("_panelRoot", flags).GetValue(original);
        GameObject copy = UnityEngine.Object.Instantiate(originalPanel, _gallery.transform, false);
        SymbolLearningCardController controller = Add<SymbolLearningCardController>();
        foreach (FieldInfo field in typeof(SymbolLearningCardController).GetFields(flags).Where(x => x.IsDefined(typeof(SerializeField), true)))
        {
            object value = field.GetValue(original);
            Transform transform = value is GameObject go ? go.transform : (value as Component)?.transform;
            if (transform == null || (transform != originalPanel.transform && !transform.IsChildOf(originalPanel.transform))) continue;
            string path = AnimationUtility.CalculateTransformPath(transform, originalPanel.transform);
            Transform destination = path.Length == 0 ? copy.transform : copy.transform.Find(path);
            field.SetValue(controller, value is GameObject ? (object)destination.gameObject : destination.GetComponent(value.GetType()));
        }
        return controller;
    }
    private static IEnumerable<DialogueSO> Dialogues(LevelConfigSO level) => new[] { level.introDialogue, level.outroDialogue, level.contextMedia?.dialogue }
        .Concat(level.focusWords.Select(x => x.media?.dialogue)).Where(x => x != null).Distinct();
    private static IEnumerable<DialogueLine> Lines(LevelConfigSO level)
    {
        foreach (DialogueLine line in Dialogues(level).SelectMany(x => x.lines ?? Array.Empty<DialogueLine>())) yield return line;
        foreach (ScriptableObject owner in new ScriptableObject[] { level.onboardingSequence }.Concat(level.enemyLessons ?? Array.Empty<EnemyLessonSO>()).Where(x => x != null))
            foreach (FieldInfo field in owner.GetType().GetFields().Where(x => x.FieldType == typeof(OnboardingBeatCopy)))
            {
                OnboardingBeatCopy copy = (OnboardingBeatCopy)field.GetValue(owner);
                if (copy.dialogue != null) { foreach (DialogueLine line in copy.dialogue.lines ?? Array.Empty<DialogueLine>()) yield return line; }
                else if (!string.IsNullOrWhiteSpace(copy.fallbackText)) yield return new DialogueLine { text = copy.fallbackText, speakerName = string.Empty };
            }
    }
    private static IEnumerable<DialogueLine> BaseLines(LevelConfigSO level)
    {
        if (level.onboardingSequence == null) yield break;
        OnboardingBeatCopy copy = level.onboardingSequence.baseIntro;
        if (copy.dialogue != null) { foreach (DialogueLine line in copy.dialogue.lines ?? Array.Empty<DialogueLine>()) yield return line; }
        else if (!string.IsNullOrWhiteSpace(copy.fallbackText)) yield return new DialogueLine { text = copy.fallbackText, speakerName = string.Empty };
    }
    private static IEnumerable<CutsceneSO> Cutscenes(LevelConfigSO level) => AssetDatabase.FindAssets("t:CutsceneSO", new[] { "Assets/ScriptableObjects/Cutscenes" })
        .Select(AssetDatabase.GUIDToAssetPath).Where(x => Path.GetFileName(x).StartsWith($"Level{level.levelNumber}_", StringComparison.Ordinal))
        .Select(AssetDatabase.LoadAssetAtPath<CutsceneSO>).Concat(new[] { level.contextMedia?.cutscene }).Concat(level.focusWords.Select(x => x.media?.cutscene)).Where(x => x != null).Distinct();
    private static void WriteCatalog()
    {
        var rows = new List<string>();
        var glyphs = new HashSet<BaybayinCharacterSO>();
        for (int n = 1; n <= 15; n++)
        {
            LevelConfigSO level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>($"Assets/ScriptableObjects/Levels/Level{n}_Config.asset");
            rows.Add($"{n}|ready"); rows.Add($"{n}|preview"); rows.Add($"{n}|sentence");
            int i = 0;
            foreach (DialogueLine line in Lines(level)) rows.Add($"{n}|dialogue:{i++}");
            i = 0;
            foreach (DialogueLine line in BaseLines(level)) rows.Add($"{n}|dialogue-top:{i++}");
            for (i = 0; i < level.allowedCharacters.Count; i++) if (glyphs.Add(level.allowedCharacters[i])) rows.Add($"{n}|unlock:{i}");
            i = 0;
            foreach (CutscenePanel panel in Cutscenes(level).SelectMany(x => x.panels ?? Array.Empty<CutscenePanel>())) rows.Add($"{n}|cutscene:{i++}");
            if (!level.suppressSymbolLearningCards)
                for (i = 0; i < level.learningRequirements.Count(x => x.kind == ContentRequirementKind.Instruction && x.symbolValue?.symbol != null); i++) { rows.Add($"{n}|symbol:{i}"); rows.Add($"{n}|authored-symbol:{i}"); }
            if (level.challengeSequence != null)
                for (i = 0; i < level.challengeSequence.units.Length; i++) { rows.Add($"{n}|challenge:{i}"); rows.Add($"{n}|hint:{i}"); rows.Add($"{n}|revealed:{i}"); }
            if (level.bossConfig?.tutorial != null)
                for (i = 0; i < level.bossConfig.tutorial.pages.Count; i++) rows.Add($"{n}|boss:{i}");
            if (n <= 5) { rows.Add($"{n}|memory"); rows.Add($"{n}|memory-back"); rows.Add($"{n}|claim"); }
            rows.Add($"{n}|victory");
            if (n % 5 == 0) { rows.Add($"{n}|era"); rows.Add($"{n}|era-locked"); }
        }
        rows.Add("1|exit"); rows.Add("1|locked"); rows.Add("6|locked"); rows.Add("11|locked");
        rows.Add("1|missing"); rows.Add("1|archive"); rows.Add("1|archive-full"); rows.Add("15|ending");
        foreach (string common in new[] { "pause-restart", "pause-leave", "save-pending", "save-rejected", "reset", "reset-success", "reset-failed", "settings", "wave", "defeat" }) rows.Add("1|" + common);
        for (int i = 0; i < Registry().entries.Count; i++) rows.Add($"1|almanac:{i}");
        for (int i = 0; i < BossLibrary().pages.Count; i++) rows.Add($"5|boss-library:{i}");
        for (int i = 0; i < 5; i++) rows.Add($"1|locked-objective:{i}");
        rows.Add("1|hint-empty"); rows.Add("1|hint-exhausted");
        for (int i = 1; i <= 3; i++) rows.Add($"15|ending-credits:{i}");
        File.WriteAllLines(Path.Combine(Folder, "catalog.txt"), rows);
    }
    private static string CaseName(string row) => $"vt323-level{int.Parse(row.Split('|')[0]):00}-{row.Split('|')[1].Replace(':', '-')}-{_batchSize}";
    private static void BatchTick()
    {
        if (_batch == null || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < _after) return;
        try
        {
            if (_stage == 0)
            {
                if (_batch.Count == 0) { File.WriteAllText(Path.Combine(Folder, "batch-status.txt"), $"COMPLETE {_done}/{_total} {_batchSize}"); _batch = null; return; }
                _case = _batch.Dequeue();
                string[] parts = _case.Split('|');
                ShowGallery(int.Parse(parts[0]), parts[1]);
                _stage = 1; _after = EditorApplication.timeSinceStartup + 0.5;
                return;
            }
            if (_stage == 1)
            {
                foreach (Canvas canvas in _gallery.GetComponentsInChildren<Canvas>(true)) canvas.enabled = true;
                if (_storyClone != null) foreach (Canvas canvas in _storyClone.GetComponentsInChildren<Canvas>(true)) { canvas.enabled = true; canvas.sortingOrder = 2100; }
                Canvas.ForceUpdateCanvases();
                TextMeshProUGUI[] texts = UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None)
                    .Where(x => x.IsActive() && (x.transform.IsChildOf(_gallery.transform) || (_storyClone != null && x.transform.IsChildOf(_storyClone.transform)))).ToArray();
                string[] evidence = texts.Select(x => { x.ForceMeshUpdate(); float boldWeight = x.fontMaterial.GetFloat("_WeightBold"); return $"{PathOf(x.transform)} | {x.text.Replace('\n', ' ')} | size={x.fontSize:F1} rect={x.rectTransform.rect.size} preferred={x.preferredWidth:F1}x{x.preferredHeight:F1} bounds={x.textBounds.size.x:F1}x{x.textBounds.size.y:F1} overflow={x.isTextOverflowing} font={x.font?.faceInfo.familyName} bold={boldWeight:F2} style={x.fontStyle} outline={x.fontMaterial.GetFloat("_OutlineWidth"):F2}"; }).ToArray();
                string[] legacy = _gallery.GetComponentsInChildren<Text>().Select(x => $"{PathOf(x.transform)} | {x.text.Replace('\n', ' ')} | legacy size={x.fontSize} rect={x.rectTransform.rect.size} preferred={x.preferredWidth:F1}x{x.preferredHeight:F1}").ToArray();
                if (evidence.Length == 0 && legacy.Length == 0) throw new InvalidOperationException("No visible text rendered for this fixture.");
                File.WriteAllText(Path.Combine(Folder, CaseName(_case) + ".txt"), $"Screen={Screen.width}x{Screen.height}\n" + string.Join("\n", evidence.Concat(legacy)));
                ScreenCapture.CaptureScreenshot(Path.Combine(Folder, CaseName(_case) + ".png"));
                _stage = 2; _after = EditorApplication.timeSinceStartup + 0.1;
                return;
            }
            if (!File.Exists(Path.Combine(Folder, CaseName(_case) + ".png"))) return;
            if (_stage == 2)
            {
                ScrollRect[] scrolls = _gallery.GetComponentsInChildren<ScrollRect>().Concat(_storyClone == null ? Array.Empty<ScrollRect>() : _storyClone.GetComponentsInChildren<ScrollRect>()).Where(x => x.IsActive() && x.content != null && x.viewport != null && x.content.rect.height > x.viewport.rect.height + 1f).ToArray();
                if (scrolls.Length > 0)
                {
                    foreach (ScrollRect scroll in scrolls) { scroll.StopMovement(); scroll.verticalNormalizedPosition = 0f; }
                    _stage = 3; _after = EditorApplication.timeSinceStartup + 0.2; return;
                }
            }
            if (_stage == 3)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(Folder, CaseName(_case) + "-bottom.png"));
                _stage = 4; _after = EditorApplication.timeSinceStartup + 0.1; return;
            }
            if (_stage == 4 && !File.Exists(Path.Combine(Folder, CaseName(_case) + "-bottom.png"))) return;
            _done++; _stage = 0;
            File.WriteAllText(Path.Combine(Folder, "batch-status.txt"), $"CAPTURED {_done}/{_total} {_batchSize} {_case}");
        }
        catch (Exception e)
        {
            File.AppendAllText(Path.Combine(Folder, "batch-errors.txt"), $"{_case}: {e}\n");
            _done++; _stage = 0; _after = EditorApplication.timeSinceStartup + 0.1;
        }
    }
}
