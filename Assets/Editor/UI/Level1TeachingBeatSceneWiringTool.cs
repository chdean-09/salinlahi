using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Scene authoring for Level 1's teaching-beat components. All of them compile, are covered by
/// tests, and are present in <c>Assets/_Scenes/Gameplay.unity</c>: <see cref="EnemyIntroductionBeat"/>,
/// <see cref="EnemyIntroductionCardView"/>, <see cref="AshGustController"/>,
/// <see cref="DrawFeedbackPresenter"/>, <see cref="InstantWinPresenter"/> and
/// <see cref="FirstDrawGuidePresenter"/> are all wired there.
/// This tool is idempotent re-wiring: every step looks for what it needs before it builds anything,
/// so running it against an already-wired scene finds nothing to do and writes nothing. It exists
/// so the wiring can be restored or re-checked without a hand edit to the .unity file.
///
/// <para>
/// <b>Why Gameplay.unity and not Level_01_Tutorial.unity.</b> <c>SceneLoader</c> has exactly one
/// gameplay scene constant and it is "Gameplay"; no code path loads Level_01_Tutorial and no
/// level-to-scene mapping exists, so every level including Level 1 plays here.
/// Level_01_Tutorial.unity is reachable only by a developer opening it and pressing Play, and it has
/// no <see cref="ActiveCluePresenter"/> at all — which is what the ash trigger, the instant-win
/// completion check and the draw-feedback classification all read. Wiring that scene would have put
/// four working components in front of three null dependencies. This tool never touches it.
/// </para>
///
/// <para>
/// <b>Why a tool and not a hand edit.</b> The Editor holds the scene open; a text edit to the
/// .unity would be clobbered on the next save or corrupt the file outright. This runs inside the
/// Editor against the live scene graph, which is also the only way the wiring can be re-checked
/// later: every step looks for what it needs before it builds anything, so a second run reports
/// "already wired" and writes nothing.
/// </para>
///
/// <para>
/// <b>Everything lands under the existing HUD canvas.</b> A second Canvas would put the card on its
/// own sorting axis and the first z-order surprise would be a card behind the dim. HUDCanvas is
/// Screen Space - Overlay at 1080x1920, and the introduction vignette flips itself to Screen Space -
/// Camera at <c>RenderOrder.SpotlightDim</c> the moment it is shown, so an Overlay canvas always
/// draws above it. That is exactly what the card needs and the reason it gets no canvas of its own.
/// </para>
/// </summary>
public static class Level1TeachingBeatSceneWiringTool
{
    private const string ScenePath = "Assets/_Scenes/Gameplay.unity";

    private const string SourceVfxPath = "Assets/Prefabs/VFX/SingleAttackHitVFX.prefab";
    private const string AshVfxPath = "Assets/Prefabs/VFX/AshGustVFX.prefab";

    // The Abo's ash: grey, slightly translucent, and the value AshGustController already carries as
    // its own serialized default. Authored onto the prefab as well so the asset reads as ash in the
    // Inspector instead of looking like a stray copy of the attack burst.
    private static readonly Color AshTint = new Color(0.63f, 0.61f, 0.58f, 0.9f);

    // Level 1's value from the design plan's §6 guidance-fade table. The plan lifts this to 1.0 by
    // Level 5, so it is authored rather than left to the component's default.
    private const float Level1IntroductionTimeScale = 0.15f;

    private const string IntroRootName = "EnemyIntroduction";
    private const string CardName = "Card";
    private const string PortraitName = "Portrait";
    private const string NameTextName = "NameText";
    private const string SubtitleTextName = "SubtitleText";
    private const string AbilityTextName = "AbilityText";
    private const string BannerName = "Banner";
    private const string BannerTextName = "BannerText";

    private const string DrawFeedbackRootName = "DrawFeedback";
    private const string DrawSiteAnchorName = "DrawSiteAnchor";
    private const string FlightGlyphName = "FlightGlyph";
    private const string MissGlyphName = "MissGlyph";
    private const string GhostStrokeOverlayName = "GhostStrokeOverlay";
    private const string DrawFeedbackMessageName = "DrawFeedbackMessage";

    private const string AshGustRootName = "[VFX] AshGust";
    private const string ClueArrivalAnchorName = "ClueArrivalAnchor";
    private const string InstantWinRootName = "[Manager] InstantWinPresenter";

    private const string FirstDrawGuideRootName = "FirstDrawGuide";
    private const string FirstDrawGuideImageName = "GuideImage";

    // NA, MA and A. Level1TutorialStep_EI is deliberately excluded — the lesson moved from Abo ng
    // Simula to Iligaw, and Iligaw is the E/I carrier, so E/I is now taught by the gated beat-8
    // draw and a second guide for it would double up. Abo's A goes the other way for the same
    // reason: he gets only the standard four-step card now, so his glyph has no gated draw and
    // needs the non-blocking guide like NA and MA.
    private static readonly string[] FirstDrawGuideStepPaths =
    {
        "Assets/ScriptableObjects/Tutorial/Level1TutorialStep_NA.asset",
        "Assets/ScriptableObjects/Tutorial/Level1TutorialStep_MA.asset",
        "Assets/ScriptableObjects/Tutorial/Level1TutorialStep_A.asset",
    };

    // ---- layout, all in HUDCanvas reference units (1080x1920, centre origin) ----
    //
    // Every number below was chosen against the bands Gameplay.unity's HUD already occupies:
    //   WaveText            +910..+830      MassClearBadge   +820..+680
    //   ActiveCluePanel     +730..+550      FeedbackMessage  +640..+550
    //   TraceHintPrompt     +510..+432      TraceHintGhost   -160..-480
    //   boss bars               +25..-25    GlyphCounter      -64..-96
    //   RestorationProgressText            -710..-960
    // which leaves two genuinely free bands: +430..-160, and -480..-710.
    // Shortened from 340 and nudged down after measuring the restoration rail: the rail is parented
    // to HUDLayer, which SafeAreaHandler insets at runtime, while these objects hang off
    // FullScreenOverlay, which is not inset. So the gap between the rail's bottom edge and the card's
    // top edge SHRINKS by the device's top inset, and a card that merely cleared the rail on a
    // notchless screen could meet it on a notched one. 320 at y=220 buys that margin back.
    private static readonly Vector2 CardPosition = new Vector2(0f, 220f);
    private static readonly Vector2 CardSize = new Vector2(860f, 320f);

    // The portrait is scaled 2x by the view at runtime (the design asks for the walk sprite at
    // double size), so the authored rect is half the space it will occupy: 150 -> 300 drawn, which
    // fits inside the 320-tall card with a 10-unit margin.
    private static readonly Vector2 PortraitSize = new Vector2(150f, 150f);
    private const float PortraitInset = 40f;
    private const float TextColumnX = 380f;
    private const float TextColumnInset = -420f;

    private const float MessageCentreY = -535f;
    private const float MessageHeight = 70f;
    private const float MessageSideInset = 0.06f;

    private const float BannerBottomOffset = 290f;
    private static readonly Vector2 BannerSize = new Vector2(900f, 76f);

    // The authored stroke guide (TraceHintGhost) sits here, so this rect IS the draw site.
    private static readonly Vector2 DrawSitePosition = new Vector2(0f, -320f);
    private static readonly Vector2 DrawSiteSize = new Vector2(320f, 320f);
    private static readonly Vector2 BadgeSize = new Vector2(140f, 140f);

    private sealed class Report
    {
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Wired = new List<string>();
        public readonly List<string> AlreadyCorrect = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Unwired = new List<string>();
        public readonly List<string> Notes = new List<string>();

        // Every rect this run positioned, so the rail check at the end can measure them all without
        // each builder having to know the rail exists.
        public readonly List<string> PlacedNames = new List<string>();
        public readonly List<RectTransform> PlacedRects = new List<RectTransform>();

        public void Placed(string name, RectTransform rect)
        {
            PlacedNames.Add(name);
            PlacedRects.Add(rect);
        }
    }

    /// <summary>
    /// Builds and wires every scene object Level 1's teaching beats need in Gameplay.unity, then
    /// saves the scene. Safe to run repeatedly: existing objects, existing assets and existing
    /// correct references are left alone and reported as already correct.
    /// </summary>
    [MenuItem("Salinlahi/Campaign/Wire Level 1 Teaching Beats")]
    public static void Wire()
    {
        // The Editor is expected to be open on some scene with possibly unsaved work in it. Opening
        // Single would discard that silently, so the user gets the ordinary save prompt first and a
        // cancel aborts the whole run rather than half-wiring a scene.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.Log("Level 1 teaching-beat wiring: cancelled at the save prompt. Nothing changed.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        if (!scene.IsValid())
        {
            Debug.LogError($"Level 1 teaching-beat wiring: could not open {ScenePath}. Nothing changed.");
            return;
        }

        var report = new Report();

        var clue = Object.FindFirstObjectByType<ActiveCluePresenter>(FindObjectsInactive.Include);
        if (clue == null)
        {
            // Not fatal — the four components are still worth placing — but it changes what two of
            // them can be aimed at, so it is stated once, loudly, rather than implied by a gap.
            report.Warnings.Add(
                "no ActiveCluePresenter in this scene. The gust then has no clue panel to aim at and "
                + "falls back to a fixed viewport point, and the teaching font cannot be matched.");
        }

        Canvas hudCanvas = ResolveHudCanvas(clue, report);
        if (hudCanvas == null)
        {
            Debug.LogError(BuildLog(report, "aborted: no HUD canvas"));
            return;
        }

        Transform uiParent = ResolveUiParent(hudCanvas, report);
        Camera worldCamera = ResolveWorldCamera(report);
        TMP_FontAsset font = ResolveTeachingFont(clue, report);
        GameObject ashVfx = EnsureAshGustVariant(report);

        WireEnemyIntroduction(uiParent, worldCamera, font, report);
        WireDrawFeedback(uiParent, hudCanvas, worldCamera, font, clue, report);
        WireAshGust(ashVfx, hudCanvas, worldCamera, clue, report);
        WireInstantWin(report);
        WireFirstDrawGuide(uiParent, report);
        ReportRailBand(hudCanvas, clue, report);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        report.Notes.Add($"scene saved: {scene.path}");
        report.Notes.Add("Level_01_Tutorial.unity is not touched by this tool and nothing was ever added to it.");

        string log = BuildLog(report, report.Created.Count == 0 && report.Wired.Count == 0
            ? "already wired — nothing created, nothing re-pointed"
            : $"{report.Created.Count} object(s)/asset(s) created, {report.Wired.Count} field(s) wired");

        if (report.Warnings.Count > 0 || report.Unwired.Count > 0)
            Debug.LogWarning(log);
        else
            Debug.Log(log);
    }

    // ---------------------------------------------------------------- scene lookups

    /// <summary>
    /// The gameplay HUD canvas. ActiveCluePresenter's own canvas is preferred because the card is
    /// meant to share the surface the clue panel already lives on; the HUD component and then the
    /// canvas name stand in if it is missing.
    /// </summary>
    private static Canvas ResolveHudCanvas(ActiveCluePresenter clue, Report report)
    {
        if (clue != null)
        {
            Canvas fromClue = clue.GetComponentInParent<Canvas>();
            if (fromClue != null)
            {
                report.Notes.Add($"HUD canvas via ActiveCluePresenter -> {fromClue.rootCanvas.name}");
                return fromClue.rootCanvas;
            }
        }

        var hud = Object.FindFirstObjectByType<HUD>(FindObjectsInactive.Include);
        if (hud != null)
        {
            Canvas fromHud = hud.GetComponentInParent<Canvas>();
            if (fromHud != null)
            {
                report.Notes.Add($"HUD canvas via HUD component -> {fromHud.rootCanvas.name}");
                return fromHud.rootCanvas;
            }
        }

        var byName = GameObject.Find("HUDCanvas");
        Canvas named = byName != null ? byName.GetComponent<Canvas>() : null;
        if (named != null)
        {
            report.Notes.Add("HUD canvas by name -> HUDCanvas");
            return named;
        }

        report.Warnings.Add("no gameplay HUD canvas found (no ActiveCluePresenter, no HUD, no HUDCanvas).");
        return null;
    }

    /// <summary>
    /// The group new HUD graphics are parented under. FullScreenOverlay already holds every
    /// full-screen teaching surface in this scene (MassClearBadge, the trace hint, DrawingFeedback's
    /// message), so joining it keeps the new card in the same stretched, non-interactive band as its
    /// siblings instead of inventing a layout convention.
    /// </summary>
    private static Transform ResolveUiParent(Canvas hudCanvas, Report report)
    {
        Transform overlay = hudCanvas.transform.Find("FullScreenOverlay");
        if (overlay != null)
        {
            report.Notes.Add($"new UI parented under {hudCanvas.name}/FullScreenOverlay");
            return overlay;
        }

        report.Warnings.Add(
            $"{hudCanvas.name}/FullScreenOverlay not found — parenting the new UI directly to the "
            + "canvas. Placement stays correct but it will not sit with its siblings.");
        return hudCanvas.transform;
    }

    private static Camera ResolveWorldCamera(Report report)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            camera = Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
            if (camera != null)
                report.Warnings.Add($"no MainCamera tag — using {camera.name} as the world camera.");
        }

        if (camera == null)
            report.Warnings.Add("no Camera in the scene — world-camera fields left empty (both components fall back to Camera.main at runtime).");

        return camera;
    }

    /// <summary>
    /// The font the teaching surfaces use. This scene runs two font families — the generic HUD
    /// (WaveText, FeedbackMessage) is LiberationSans while the clue panel, the restoration progress
    /// line and the draw instruction are TutorialFont — and the introduction card belongs to the
    /// second group, so the font is taken from ActiveCluePresenter's own labels rather than from
    /// whichever TMP_Text happens to be found first.
    /// </summary>
    private static TMP_FontAsset ResolveTeachingFont(ActiveCluePresenter clue, Report report)
    {
        if (clue != null)
        {
            var so = new SerializedObject(clue);
            foreach (string path in new[] { "_clueText", "_restorationProgressText" })
            {
                SerializedProperty property = so.FindProperty(path);
                var label = property != null ? property.objectReferenceValue as TMP_Text : null;
                if (label != null && label.font != null)
                {
                    report.Notes.Add($"label font taken from ActiveCluePresenter.{path} ('{label.name}') -> {label.font.name}");
                    return label.font;
                }
            }
        }

        foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text.font != null)
            {
                report.Warnings.Add($"no clue-panel label to copy a font from — falling back to '{text.name}' ({text.font.name}), which may be the wrong family.");
                return text.font;
            }
        }

        report.Warnings.Add("no existing TMP_Text to copy a font from — new labels keep the TMP default.");
        return null;
    }

    /// <summary>The clue panel's rect, taken from the presenter's own wiring rather than by name.</summary>
    private static RectTransform ResolveCluePanelRect(ActiveCluePresenter clue, Report report)
    {
        if (clue == null)
            return null;

        SerializedProperty property = new SerializedObject(clue).FindProperty("_cluePanelRoot");
        var root = property != null ? property.objectReferenceValue as GameObject : null;
        if (root == null)
        {
            report.Warnings.Add("ActiveCluePresenter._cluePanelRoot is empty — cannot locate the clue panel's rect.");
            return null;
        }

        var rect = root.GetComponent<RectTransform>();
        if (rect == null)
            report.Warnings.Add($"clue panel '{root.name}' has no RectTransform — cannot derive the gust's destination.");

        return rect;
    }

    // ---------------------------------------------------------------- the ash VFX asset

    /// <summary>
    /// The ash-grey burst the gust sweeps. SingleAttackHitVFX is a sprite-frame burst rather than a
    /// particle system, so "ash-grey" is a colour on its SpriteRenderer; AshGustController re-applies
    /// the tint to every renderer it finds at runtime, and authoring it here as well is what makes
    /// the asset legible in the Inspector instead of looking like a stray copy of the attack burst.
    /// The original prefab is never touched and the variant is never re-created over itself: writing
    /// a new asset over an existing path reissues its GUID and silently unwires every reference to it.
    /// </summary>
    private static GameObject EnsureAshGustVariant(Report report)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(AshVfxPath);
        if (existing != null)
        {
            report.AlreadyCorrect.Add($"{AshVfxPath} exists ({PrefabUtility.GetPrefabAssetType(existing)}) — reused, not rewritten");
            return existing;
        }

        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceVfxPath);
        if (source == null)
        {
            report.Warnings.Add($"{SourceVfxPath} not found — no gust VFX asset created, so no gust will play.");
            return null;
        }

        var instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (instance == null)
        {
            report.Warnings.Add($"could not instantiate {SourceVfxPath} — no gust VFX asset created.");
            return null;
        }

        instance.name = "AshGustVFX";
        var renderer = instance.GetComponent<SpriteRenderer>();
        if (renderer != null)
            renderer.color = AshTint;
        else
            report.Warnings.Add($"{SourceVfxPath} has no SpriteRenderer — the variant carries no authored tint (the runtime tint still applies).");

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, AshVfxPath);
        Object.DestroyImmediate(instance);

        if (saved == null)
        {
            report.Warnings.Add($"failed to save {AshVfxPath} — no gust VFX asset created.");
            return null;
        }

        report.Created.Add($"{AshVfxPath} ({PrefabUtility.GetPrefabAssetType(saved)} of SingleAttackHitVFX, SpriteRenderer tinted {AshTint})");
        return saved;
    }

    // ---------------------------------------------------------------- enemy introduction

    private static void WireEnemyIntroduction(Transform uiParent, Camera worldCamera, TMP_FontAsset font, Report report)
    {
        // An existing component anywhere in the scene wins over a new host: another pass may already
        // have placed the beat, and a second copy would race it for the static handle both of them
        // set on enable.
        GameObject root = AdoptOrCreate<EnemyIntroductionBeat>(uiParent, IntroRootName, report, out bool adopted);
        if (!adopted)
            Stretch(Ensure<RectTransform>(root));

        var beat = Ensure<EnemyIntroductionBeat>(root, report, $"{root.name}.EnemyIntroductionBeat");

        // The view is looked up across the whole scene rather than only on the beat's host: a second
        // card view would be built and hidden with nothing pointing at it, and the beat would keep
        // declining because the copy it holds is not the copy that got the surfaces.
        var view = Object.FindFirstObjectByType<EnemyIntroductionCardView>(FindObjectsInactive.Include);
        if (view != null && view.gameObject != root)
        {
            report.AlreadyCorrect.Add($"EnemyIntroductionCardView already on '{view.gameObject.name}' — reused");
            root = view.gameObject;
        }
        else
        {
            view = Ensure<EnemyIntroductionCardView>(root, report, $"{root.name}.EnemyIntroductionCardView");
        }

        // The card sits in the free band between the trace-hint prompt and the stroke guide. It is
        // the placement in this tool with the least to copy: no existing surface occupies that band,
        // so the numbers are a judgement call rather than a match to a sibling.
        GameObject card = EnsureChild(root.transform, CardName, report);
        RectTransform cardRect = Ensure<RectTransform>(card);
        Centre(cardRect, CardPosition, CardSize);
        var cardBackground = Ensure<Image>(card, report, $"{CardName}.Image");
        // The pill colour the trace-hint prompt already uses, so the two teaching surfaces read as
        // one family rather than two designers.
        cardBackground.color = new Color(0.10f, 0.08f, 0.02f, 0.72f);
        cardBackground.raycastTarget = false;
        var cardGroup = Ensure<CanvasGroup>(card, report, $"{CardName}.CanvasGroup");
        cardGroup.blocksRaycasts = false;
        cardGroup.interactable = false;

        GameObject portrait = EnsureChild(card.transform, PortraitName, report);
        RectTransform portraitRect = Ensure<RectTransform>(portrait);
        portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0f, 0.5f);
        portraitRect.pivot = new Vector2(0f, 0.5f);
        portraitRect.anchoredPosition = new Vector2(PortraitInset, 0f);
        portraitRect.sizeDelta = PortraitSize;
        portraitRect.localScale = Vector3.one;
        var portraitImage = Ensure<Image>(portrait, report, $"{PortraitName}.Image");
        portraitImage.preserveAspect = true;
        portraitImage.raycastTarget = false;
        portraitImage.enabled = false;

        TMP_Text nameText = EnsureLabel(card.transform, NameTextName, font, 54f, FontStyles.Bold,
            TextAlignmentOptions.Left, Color.white, report);
        TextColumn(nameText.rectTransform, -30f, 70f);

        TMP_Text subtitleText = EnsureLabel(card.transform, SubtitleTextName, font, 32f, FontStyles.Italic,
            TextAlignmentOptions.Left, new Color(0.86f, 0.83f, 0.74f, 1f), report);
        TextColumn(subtitleText.rectTransform, -102f, 48f);

        TMP_Text abilityText = EnsureLabel(card.transform, AbilityTextName, font, 38f, FontStyles.Normal,
            TextAlignmentOptions.TopLeft, new Color(0.98f, 0.94f, 0.82f, 1f), report);
        RectTransform abilityRect = abilityText.rectTransform;
        abilityRect.anchorMin = new Vector2(0f, 0f);
        abilityRect.anchorMax = new Vector2(1f, 0f);
        abilityRect.pivot = new Vector2(0f, 0f);
        abilityRect.anchoredPosition = new Vector2(TextColumnX, 34f);
        abilityRect.sizeDelta = new Vector2(TextColumnInset, 130f);
        abilityRect.localScale = Vector3.one;

        // The banner outlives the card and persists for that enemy's lifetime, so it goes in the
        // only band that is free on every level: above the restoration progress line and below the
        // stroke guide. The margins there are tight (40 px and 24 px) and are reported below.
        GameObject banner = EnsureChild(root.transform, BannerName, report);
        RectTransform bannerRect = Ensure<RectTransform>(banner);
        bannerRect.anchorMin = bannerRect.anchorMax = new Vector2(0.5f, 0f);
        bannerRect.pivot = new Vector2(0.5f, 0f);
        bannerRect.anchoredPosition = new Vector2(0f, BannerBottomOffset);
        bannerRect.sizeDelta = BannerSize;
        bannerRect.localScale = Vector3.one;
        var bannerBackground = Ensure<Image>(banner, report, $"{BannerName}.Image");
        bannerBackground.color = new Color(0.10f, 0.08f, 0.02f, 0.60f);
        bannerBackground.raycastTarget = false;
        var bannerGroup = Ensure<CanvasGroup>(banner, report, $"{BannerName}.CanvasGroup");
        bannerGroup.blocksRaycasts = false;
        bannerGroup.interactable = false;

        TMP_Text bannerText = EnsureLabel(banner.transform, BannerTextName, font, 34f, FontStyles.Normal,
            TextAlignmentOptions.Center, Color.white, report);
        Stretch(bannerText.rectTransform, 24f, 8f);

        var viewObject = new SerializedObject(view);
        WireReference(viewObject, "_cardGroup", cardGroup, "CardView._cardGroup", report);
        WireReference(viewObject, "_cardRect", cardRect, "CardView._cardRect", report);
        WireReference(viewObject, "_portrait", portraitImage, "CardView._portrait", report);
        WireReference(viewObject, "_nameText", nameText, "CardView._nameText", report);
        WireReference(viewObject, "_subtitleText", subtitleText, "CardView._subtitleText", report);
        WireReference(viewObject, "_abilityText", abilityText, "CardView._abilityText", report);
        WireReference(viewObject, "_bannerGroup", bannerGroup, "CardView._bannerGroup", report);
        WireReference(viewObject, "_bannerText", bannerText, "CardView._bannerText", report);
        viewObject.ApplyModifiedPropertiesWithoutUndo();

        var beatObject = new SerializedObject(beat);
        WireReference(beatObject, "_card", view, "Beat._card", report);
        WireReference(beatObject, "_worldCamera", worldCamera, "Beat._worldCamera", report);
        WireFloat(beatObject, "_introductionTimeScale", Level1IntroductionTimeScale,
            "Beat._introductionTimeScale", report);
        beatObject.ApplyModifiedPropertiesWithoutUndo();

        report.Notes.Add(
            "Beat._vignette left empty on purpose: TutorialSpotlightOverlay.CreateRuntime builds one "
            + "on the first card and flips it to Screen Space - Camera, which is what keeps the dim "
            + "above the sprites but below the drawing strokes. An authored overlay would have to "
            + "reproduce that render-mode switch itself.");

        report.Placed("card", cardRect);
        report.Placed("banner", bannerRect);
        report.Notes.Add(OverlapReport(cardRect, "card"));
        report.Notes.Add(OverlapReport(bannerRect, "banner"));
    }

    /// <summary>Top-anchored inside the card, in the column to the right of the 2x portrait.</summary>
    private static void TextColumn(RectTransform rt, float topOffset, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(TextColumnX, topOffset);
        rt.sizeDelta = new Vector2(TextColumnInset, height);
        rt.localScale = Vector3.one;
    }

    // ---------------------------------------------------------------- draw feedback

    private static void WireDrawFeedback(Transform uiParent, Canvas hudCanvas, Camera worldCamera,
        TMP_FontAsset font, ActiveCluePresenter clue, Report report)
    {
        GameObject root = AdoptOrCreate<DrawFeedbackPresenter>(uiParent, DrawFeedbackRootName, report, out bool adopted);
        if (!adopted)
            Stretch(Ensure<RectTransform>(root));

        var presenter = Ensure<DrawFeedbackPresenter>(root, report, $"{root.name}.DrawFeedbackPresenter");

        // The draw site: where the player's stroke lands. TraceHintGhost is already authored at this
        // rect as the stroke guide, so this IS the draw site, and copying it keeps the miss glyph
        // flashing where the player was just looking.
        GameObject drawSite = EnsureChild(root.transform, DrawSiteAnchorName, report);
        RectTransform drawSiteRect = Ensure<RectTransform>(drawSite);
        Centre(drawSiteRect, DrawSitePosition, DrawSiteSize);

        Image flightGlyph = EnsureOverlayImage(root.transform, FlightGlyphName, Vector2.zero, BadgeSize, report);
        Image missGlyph = EnsureOverlayImage(root.transform, MissGlyphName, DrawSitePosition, BadgeSize, report);
        Image ghost = EnsureOverlayImage(root.transform, GhostStrokeOverlayName, DrawSitePosition, DrawSiteSize, report);

        // Filled, not Simple. The replay wipes the ideal form on by driving fillAmount from 0 to 1;
        // the presenter checks the Image type and silently degrades to a plain alpha fade when it is
        // anything else, so this one property is the difference between a stroke being replayed and
        // a glyph just appearing.
        if (ghost.type != Image.Type.Filled)
        {
            ghost.type = Image.Type.Filled;
            report.Wired.Add($"{GhostStrokeOverlayName}.Image.type -> Filled");
        }
        else
        {
            report.AlreadyCorrect.Add($"{GhostStrokeOverlayName}.Image.type is Filled");
        }

        ghost.fillMethod = Image.FillMethod.Horizontal;
        ghost.fillOrigin = (int)Image.OriginHorizontal.Left;
        ghost.fillAmount = 0f;

        // A SECOND message label, not DrawingFeedback's. A syllable needed later is still a kill, so
        // DrawingFeedback's defeat cue fires just after this presenter writes its line; on a shared
        // label the explanation the player needs would be overwritten milliseconds later.
        TMP_Text message = EnsureLabel(root.transform, DrawFeedbackMessageName, font, 40f,
            FontStyles.Normal, TextAlignmentOptions.Center, Color.white, report);
        CentreBand(message.rectTransform, MessageCentreY, MessageHeight, MessageSideInset);

        ReportSlotAnchorSituation(clue, report);

        var so = new SerializedObject(presenter);
        WireReference(so, "_canvas", hudCanvas, "Presenter._canvas", report);
        WireReference(so, "_worldCamera", worldCamera, "Presenter._worldCamera", report);
        WireReference(so, "_drawSiteAnchor", drawSiteRect, "Presenter._drawSiteAnchor", report);
        WireReference(so, "_flightGlyph", flightGlyph, "Presenter._flightGlyph", report);
        WireReference(so, "_missGlyph", missGlyph, "Presenter._missGlyph", report);
        WireReference(so, "_ghostStrokeOverlay", ghost, "Presenter._ghostStrokeOverlay", report);
        WireReference(so, "_messageLabel", message, "Presenter._messageLabel", report);
        so.ApplyModifiedPropertiesWithoutUndo();

        ReportSharedLabelCheck(message, report);

        report.Unwired.Add(
            "Presenter._playerInk — DrawingCanvas renders the player's ink as world-space "
            + "LineRenderers built at runtime, and there is no CanvasGroup anywhere in the ink path. "
            + "Any CanvasGroup wired here would fade an unrelated HUD panel instead of the stroke, so "
            + "it is left empty; the refused-drawing ink fade stays inert until the ink gets a group.");

        report.Placed("draw-feedback message", message.rectTransform);
        report.Placed("draw site / ghost overlay", drawSiteRect);
        report.Notes.Add(OverlapReport(message.rectTransform, "draw-feedback message"));
    }

    /// <summary>
    /// Explains why <c>_slotAnchors</c> is deliberately left empty, and names the change that fills
    /// it properly.
    ///
    /// <para>
    /// <b>This tool must not author slot anchors, and authoring them would be worse than leaving the
    /// field empty.</b> The real per-slot rects do exist — <see cref="ActiveCluePresenter"/> builds a
    /// restoration slot rail whose anchors it flattens in exactly
    /// <c>TargetTextSlotMap.Build</c> order, which is the order the feedback report's SlotIndex comes
    /// from, and it publishes them as <c>RestorationSlotAnchors</c>. But the rail is built at
    /// RUNTIME, under a <c>[Runtime]</c> object that does not exist in the saved scene, so no Editor
    /// pass can reference it.
    /// </para>
    ///
    /// <para>
    /// Fabricated stand-ins would then be actively harmful in two ways. They would sit at fixed
    /// screen positions that do not track the rail, so every badge would fly to a place where no slot
    /// is drawn — and they would make the field LOOK wired, hiding the fact that the one correct fix
    /// is still missing. The rail also switches the old printed readout off when it builds, so
    /// anchors parented to that label would be inactive as well as misplaced. The presenter already
    /// null-guards an empty array and simply declines the flight, which is the honest degradation.
    /// </para>
    ///
    /// <para>
    /// <b>The fix is a runtime handoff, not scene wiring:</b> have DrawFeedbackPresenter read
    /// <c>ActiveCluePresenter.RestorationSlotAnchors</c> when it resolves a slot, instead of only its
    /// serialized array. That is a change inside DrawFeedbackPresenter, which this tool does not own.
    /// </para>
    /// </summary>
    private static void ReportSlotAnchorSituation(ActiveCluePresenter clue, Report report)
    {
        bool railPublished = clue != null
            && clue.GetType().GetProperty("RestorationSlotAnchors") != null;

        report.Unwired.Add(
            "Presenter._slotAnchors — left EMPTY on purpose, not overlooked. ActiveCluePresenter "
            + "builds the real per-slot rail at runtime (\"[Runtime] ActiveClueRestorationRail\"), "
            + "flattened in TargetTextSlotMap.Build order, so the anchors that badges should fly to "
            + "cannot be referenced from a saved scene. Authoring stand-ins would aim every badge at "
            + "a spot where no slot is drawn AND would make this field look done. "
            + (railPublished
                ? "The rail already publishes ActiveCluePresenter.RestorationSlotAnchors: the fix is "
                  + "for DrawFeedbackPresenter to read that list when its serialized array is empty."
                : "Expose the rail's anchors from ActiveCluePresenter and have DrawFeedbackPresenter "
                  + "read them when its serialized array is empty."));
    }

    /// <summary>
    /// Confirms the presenter's message label is a different object from DrawingFeedback's. The two
    /// components fire on the same draw, so a shared label is the failure this check exists for.
    /// </summary>
    private static void ReportSharedLabelCheck(TMP_Text mine, Report report)
    {
        var feedback = Object.FindFirstObjectByType<DrawingFeedback>(FindObjectsInactive.Include);
        if (feedback == null)
        {
            report.Warnings.Add("no DrawingFeedback in the scene — could not confirm the two message labels are distinct.");
            return;
        }

        SerializedProperty theirProperty = new SerializedObject(feedback).FindProperty("_messageLabel");
        if (theirProperty == null)
        {
            report.Warnings.Add("DrawingFeedback has no '_messageLabel' field any more — could not confirm the two labels are distinct.");
            return;
        }

        var theirs = theirProperty.objectReferenceValue as TMP_Text;
        if (theirs == null)
        {
            report.Notes.Add("DrawingFeedback._messageLabel is empty, so no label is shared.");
            return;
        }

        if (ReferenceEquals(theirs, mine))
            report.Warnings.Add($"the two message labels are the SAME object ({mine.name}) — the defeat cue will overwrite the feedback line.");
        else
            report.Notes.Add($"message labels are distinct: DrawingFeedback -> '{theirs.name}', DrawFeedbackPresenter -> '{mine.name}'");
    }

    // ---------------------------------------------------------------- ash gust

    private static void WireAshGust(GameObject ashVfx, Canvas hudCanvas, Camera worldCamera,
        ActiveCluePresenter clue, Report report)
    {
        // Scene-level rather than on the enemy: the gust must outlive the Abo that fired it, since
        // the sweep is still crossing the screen while that shell may already be back in the pool.
        GameObject root = AdoptOrCreate<AshGustController>(null, AshGustRootName, report, out _);
        var controller = Ensure<AshGustController>(root, report, $"{root.name}.AshGustController");

        var so = new SerializedObject(controller);
        WireReference(so, "_gustVfxPrefab", ashVfx, "AshGust._gustVfxPrefab", report);
        WireColor(so, "_ashTint", AshTint, "AshGust._ashTint", report);

        WireClueDestination(root, so, hudCanvas, worldCamera, clue, report);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Aims the gust at the real clue panel.
    ///
    /// <para>
    /// <b>The panel's own RectTransform is deliberately NOT wired here.</b> The gust is a world-space
    /// sprite and <c>_cluePanelAnchor</c> is read as a world position — its tooltip asks for "an
    /// empty child of the stage placed under the panel". HUDCanvas is Screen Space - Overlay, where a
    /// RectTransform's position is in screen PIXELS, so wiring the panel directly would aim the sweep
    /// at roughly (540, 1690) in world units: several hundred metres off-stage, a gust that flies
    /// away and never arrives. So the panel's rect is measured, converted to a viewport fraction, and
    /// projected back through the camera into a world-space anchor — which is the same destination,
    /// expressed in the units the component actually reads.
    /// </para>
    /// </summary>
    private static void WireClueDestination(GameObject root, SerializedObject so, Canvas hudCanvas,
        Camera worldCamera, ActiveCluePresenter clue, Report report)
    {
        RectTransform panel = ResolveCluePanelRect(clue, report);
        var canvasRect = hudCanvas.transform as RectTransform;

        if (panel == null || canvasRect == null || worldCamera == null)
        {
            report.Unwired.Add(
                "AshGust._cluePanelAnchor — could not measure the clue panel against the canvas "
                + "through a camera, so the anchor is left empty and the component's viewport "
                + "fallback (0.5, 0.88) aims the sweep at the top centre of the screen.");
            return;
        }

        Rect panelRect = WorldRect(panel);
        Rect canvasBounds = WorldRect(canvasRect);
        if (canvasBounds.width <= 0f || canvasBounds.height <= 0f)
        {
            report.Unwired.Add("AshGust._cluePanelAnchor — the canvas has no measurable rect yet; left empty, viewport fallback applies.");
            return;
        }

        var viewportPoint = new Vector2(
            Mathf.Clamp01((panelRect.center.x - canvasBounds.xMin) / canvasBounds.width),
            Mathf.Clamp01((panelRect.center.y - canvasBounds.yMin) / canvasBounds.height));

        // Keep the fallback in step with the measurement as well, so clearing the anchor later
        // degrades to the real panel position rather than to the component's generic default.
        WireVector2(so, "_cluePanelViewportPoint", viewportPoint, "AshGust._cluePanelViewportPoint", report);

        SerializedProperty depthProperty = so.FindProperty("_cluePanelViewportDepth");
        float depth = depthProperty != null ? depthProperty.floatValue : 10f;

        Vector3 world = worldCamera.ViewportToWorldPoint(new Vector3(viewportPoint.x, viewportPoint.y, depth));
        world.z = 0f;

        // A plain Transform, not a RectTransform: this anchor lives on the stage rather than in a
        // canvas, and a RectTransform with no Canvas above it is a layout object in a place that has
        // no layout.
        GameObject anchorObject = EnsureStageChild(root.transform, ClueArrivalAnchorName, report);
        anchorObject.transform.position = world;
        WireReference(so, "_cluePanelAnchor", anchorObject.transform, "AshGust._cluePanelAnchor", report);

        report.Notes.Add(
            $"gust destination derived from '{panel.name}': viewport {viewportPoint} -> world {world} "
            + $"(camera '{worldCamera.name}', orthographic={worldCamera.orthographic}). The anchor is a "
            + "static world point, which is right for this scene's locked orthographic camera; a level "
            + "that pans or zooms the camera during play would need the anchor re-derived or cleared.");
    }

    // ---------------------------------------------------------------- instant win

    private static void WireInstantWin(Report report)
    {
        GameObject root = AdoptOrCreate<InstantWinPresenter>(null, InstantWinRootName, report, out _);
        var presenter = Ensure<InstantWinPresenter>(root, report, $"{root.name}.InstantWinPresenter");

        // The presenter builds its own non-dimming overlay canvas and carries the whole §6 hold table
        // as C# defaults, so authoring it into the scene is about making the tuning visible and
        // editable rather than about supplying references. Only the consumer link is worth wiring.
        var waveManager = Object.FindFirstObjectByType<WaveManager>(FindObjectsInactive.Include);
        if (waveManager == null)
        {
            report.Warnings.Add("no WaveManager in the scene — InstantWinPresenter is placed but nothing points at it (WaveManager would find it by type at runtime anyway).");
        }
        else
        {
            var so = new SerializedObject(waveManager);
            WireReference(so, "_instantWinPresenter", presenter, "WaveManager._instantWinPresenter", report);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        report.Notes.Add(
            "InstantWinPresenter._dissolveVfxPrefab left empty: the dissolve is a brighten-and-fade "
            + "that needs no prefab, and the only burst asset available is an attack hit — the beat's "
            + "whole point is that the remaining enemies are never struck.");
    }

    // ---------------------------------------------------------------- first-draw trace guide

    /// <summary>
    /// Places the non-blocking first-draw trace guide for Iligaw, Nawalang Mukha and Mantsa. Their
    /// glyphs get only the standard introduction card (unlike Abo, whose gated beat-8 draw is a
    /// full lesson), so without this each of those three would be drawn completely cold the first
    /// time it is needed.
    ///
    /// <para>
    /// Shares the draw site rect (<see cref="DrawSitePosition"/> / <see cref="DrawSiteSize"/>) with
    /// <see cref="DrawFeedbackPresenter"/>'s ghost-stroke overlay, since the guide has to sit where
    /// the player is actually drawing, not somewhere else on the HUD.
    /// </para>
    /// </summary>
    private static void WireFirstDrawGuide(Transform uiParent, Report report)
    {
        GameObject root = AdoptOrCreate<FirstDrawGuidePresenter>(uiParent, FirstDrawGuideRootName, report, out bool adopted);
        if (!adopted)
            Stretch(Ensure<RectTransform>(root));

        var presenter = Ensure<FirstDrawGuidePresenter>(root, report, $"{root.name}.FirstDrawGuidePresenter");

        Image guideImage = EnsureOverlayImage(root.transform, FirstDrawGuideImageName, DrawSitePosition, DrawSiteSize, report);

        var steps = new List<Level1TutorialStepSO>();
        foreach (string path in FirstDrawGuideStepPaths)
        {
            var step = AssetDatabase.LoadAssetAtPath<Level1TutorialStepSO>(path);
            if (step == null)
            {
                report.Warnings.Add($"FirstDrawGuide: '{path}' not found — that glyph's trace guide will never show.");
                continue;
            }
            steps.Add(step);
        }

        var so = new SerializedObject(presenter);
        WireReference(so, "_guideImage", guideImage, "FirstDrawGuide._guideImage", report);
        WireObjectArray(so, "_steps", steps, "FirstDrawGuide._steps", report);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------------------------------------------------------- rail collision check

    /// <summary>
    /// Measures the restoration slot rail against everything this run placed.
    ///
    /// <para>
    /// The rail is the real target-text HUD and it is built at runtime, so it cannot be seen in the
    /// Scene view and cannot be caught by the ordinary sibling overlap check — which is exactly the
    /// kind of collision that ships. Its geometry is fully determined by serialized fields on
    /// <see cref="ActiveCluePresenter"/>, so it is reconstructed here from those values rather than
    /// from a number copied into this file: if someone retunes the rail, a re-run re-measures it.
    /// </para>
    ///
    /// <para>
    /// Two frames are being compared and they are easy to confuse. The rail's authored
    /// <c>-345</c> is anchored to the TOP of HUDLayer, while this tool's draw site sits at
    /// <c>-320</c> from the CENTRE of the canvas — similar numbers, roughly 800 units apart on
    /// screen. Everything below is therefore converted into the rail parent's own local space before
    /// anything is compared.
    /// </para>
    ///
    /// <para>
    /// It also reports the one asymmetry no static measurement covers: the rail hangs off HUDLayer,
    /// which SafeAreaHandler insets on a notched device, while this tool's objects hang off
    /// FullScreenOverlay, which is not inset. The clearances printed here are therefore the
    /// best case, and they shrink by the device's top inset.
    /// </para>
    /// </summary>
    private static void ReportRailBand(Canvas hudCanvas, ActiveCluePresenter clue, Report report)
    {
        if (clue == null)
        {
            report.Notes.Add("rail check skipped: no ActiveCluePresenter, so no restoration rail will be built.");
            return;
        }

        Transform railParent = FindRailParent(hudCanvas);
        var parentRect = railParent as RectTransform;
        if (parentRect == null)
        {
            report.Warnings.Add("rail check skipped: could not find the rail's parent (HUDLayer / HUDRoot / canvas) as a RectTransform.");
            return;
        }

        var so = new SerializedObject(clue);
        Vector2 railPosition = ReadVector2(so, "_railAnchoredPosition", new Vector2(0f, -345f));
        Vector2 slotSize = ReadVector2(so, "_slotSize", new Vector2(62f, 62f));
        float slotSpacing = ReadFloat(so, "_slotSpacing", 9f);
        float wordGap = ReadFloat(so, "_wordGap", 46f);
        float labelRow = ReadFloat(so, "_latinWordLabelRowHeight", 30f) + ReadFloat(so, "_latinWordLabelGap", 6f);

        // Level 1's target text is INA AMA: two focus words of two slots each. The rail sizes itself
        // from the level's own focus words, so this is the Level 1 shape rather than a general answer.
        const int wordCount = 2;
        const int slotsPerWord = 2;
        float wordWidth = (slotsPerWord * slotSize.x) + ((slotsPerWord - 1) * slotSpacing);
        float railWidth = (wordCount * wordWidth) + ((wordCount - 1) * wordGap);
        float railHeight = labelRow + slotSize.y;

        Rect parentLocal = parentRect.rect;
        float railTop = parentLocal.yMax + railPosition.y;
        float railCentreX = parentLocal.center.x + railPosition.x;
        var rail = new Rect(railCentreX - (railWidth * 0.5f), railTop - railHeight, railWidth, railHeight);

        report.Notes.Add(
            $"restoration rail (runtime, rebuilt from ActiveCluePresenter's serialized values) will occupy "
            + $"{rail} in {parentRect.name}'s local space — {railWidth}x{railHeight} at {railPosition} "
            + "from the top. It is not visible in the Scene view.");

        bool anyHit = false;
        for (int i = 0; i < report.PlacedRects.Count; i++)
        {
            RectTransform mine = report.PlacedRects[i];
            if (mine == null)
                continue;

            Rect local = ToLocalRect(mine, parentRect);
            if (local.Overlaps(rail))
            {
                report.Warnings.Add($"RAIL COLLISION: '{report.PlacedNames[i]}' {local} overlaps the rail {rail}.");
                anyHit = true;
                continue;
            }

            float gap = local.yMax <= rail.yMin
                ? rail.yMin - local.yMax
                : (local.yMin >= rail.yMax ? local.yMin - rail.yMax : 0f);
            report.Notes.Add($"    '{report.PlacedNames[i]}' clears the rail by {gap:0} units vertically (best case; less the device's top safe-area inset).");
        }

        if (!anyHit)
            report.Notes.Add("    no rail collision with anything this tool placed.");

        // Not this tool's to fix — ActiveCluePresenter owns the rail's position and is not edited
        // here — but it is a visible collision and worth saying once rather than discovering in play.
        ReportRailVsExistingHud(rail, parentRect, report);
    }

    /// <summary>The container ActiveCluePresenter parents its rail to, resolved the same way it does.</summary>
    private static Transform FindRailParent(Canvas hudCanvas)
    {
        var hudLayer = GameObject.Find("HUDLayer");
        if (hudLayer != null)
            return hudLayer.transform;

        var hudRoot = GameObject.Find("HUDRoot");
        if (hudRoot != null)
            return hudRoot.transform;

        return hudCanvas != null ? hudCanvas.transform : null;
    }

    private static void ReportRailVsExistingHud(Rect rail, RectTransform parentRect, Report report)
    {
        Canvas canvas = parentRect.GetComponentInParent<Canvas>();
        Transform canvasRoot = canvas != null ? canvas.rootCanvas.transform : parentRect.root;

        var hits = new List<string>();
        foreach (var candidate in canvasRoot.GetComponentsInChildren<RectTransform>(true))
        {
            if (candidate == parentRect || candidate.GetComponent<Graphic>() == null)
                continue;
            if (!candidate.gameObject.activeInHierarchy)
                continue;
            if (report.PlacedRects.Contains(candidate))
                continue;

            if (ToLocalRect(candidate, parentRect).Overlaps(rail))
                hits.Add(candidate.name);
        }

        report.Notes.Add(hits.Count == 0
            ? "    rail vs pre-existing HUD: no overlap."
            : $"    rail vs pre-existing HUD: OVERLAPS {string.Join(", ", hits)}. Owned by ActiveCluePresenter's "
              + "_railAnchoredPosition, not by this tool — reported, not moved.");
    }

    private static Rect ToLocalRect(RectTransform rect, RectTransform space)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        Vector3 min = space.InverseTransformPoint(corners[0]);
        Vector3 max = space.InverseTransformPoint(corners[2]);
        return new Rect(
            Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y),
            Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
    }

    private static float ReadFloat(SerializedObject so, string path, float fallback)
    {
        SerializedProperty property = so.FindProperty(path);
        return property != null ? property.floatValue : fallback;
    }

    private static Vector2 ReadVector2(SerializedObject so, string path, Vector2 fallback)
    {
        SerializedProperty property = so.FindProperty(path);
        return property != null ? property.vector2Value : fallback;
    }

    // ---------------------------------------------------------------- building blocks

    /// <summary>
    /// Reuses an existing component anywhere in the scene if one is already placed, otherwise
    /// creates the named host. Adoption comes first so a re-run — or another pass that placed the
    /// component elsewhere — never ends up with two copies competing for the same static handle.
    /// </summary>
    private static GameObject AdoptOrCreate<T>(Transform parent, string name, Report report, out bool adopted)
        where T : Component
    {
        var existing = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        if (existing != null)
        {
            adopted = true;
            report.AlreadyCorrect.Add($"{typeof(T).Name} already in the scene on '{existing.gameObject.name}' — reused");
            return existing.gameObject;
        }

        adopted = false;

        if (parent != null)
            return EnsureChild(parent, name, report);

        var byName = GameObject.Find(name);
        if (byName != null)
        {
            report.AlreadyCorrect.Add($"root '{name}' exists — reused");
            return byName;
        }

        var go = new GameObject(name);
        report.Created.Add($"root '{name}'");
        return go;
    }

    private static GameObject EnsureChild(Transform parent, string name, Report report)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            report.AlreadyCorrect.Add($"'{parent.name}/{name}' exists — reused");
            return existing.gameObject;
        }

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        report.Created.Add($"'{parent.name}/{name}'");
        return go;
    }

    /// <summary>A world-space child, for anchors that belong to the stage rather than to a canvas.</summary>
    private static GameObject EnsureStageChild(Transform parent, string name, Report report)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            report.AlreadyCorrect.Add($"'{parent.name}/{name}' exists — reused");
            return existing.gameObject;
        }

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        report.Created.Add($"'{parent.name}/{name}'");
        return go;
    }

    private static T Ensure<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    private static T Ensure<T>(GameObject go, Report report, string label) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component != null)
        {
            report.AlreadyCorrect.Add($"{label} present");
            return component;
        }

        report.Created.Add(label);
        return go.AddComponent<T>();
    }

    /// <summary>
    /// One of the three reused overlay graphics. Each starts disabled with raycasts off: they are
    /// cues drawn over the column the player draws in, and a graphic there with raycasts left on
    /// eats drawing input without any test noticing.
    /// </summary>
    private static Image EnsureOverlayImage(Transform parent, string name, Vector2 position,
        Vector2 size, Report report)
    {
        GameObject go = EnsureChild(parent, name, report);
        Centre(Ensure<RectTransform>(go), position, size);
        var image = Ensure<Image>(go, report, $"{name}.Image");
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    private static TMP_Text EnsureLabel(Transform parent, string name, TMP_FontAsset font,
        float fontSize, FontStyles style, TextAlignmentOptions alignment, Color color, Report report)
    {
        GameObject go = EnsureChild(parent, name, report);
        Ensure<RectTransform>(go);
        var label = Ensure<TextMeshProUGUI>(go, report, $"{name}.TextMeshProUGUI");
        if (font != null)
            label.font = font;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = color;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        label.text = string.Empty;
        return label;
    }

    private static void Stretch(RectTransform rt, float horizontalInset = 0f, float verticalInset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(horizontalInset, verticalInset);
        rt.offsetMax = new Vector2(-horizontalInset, -verticalInset);
        rt.localScale = Vector3.one;
    }

    private static void Centre(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
    }

    /// <summary>Full width less a fractional side inset, centred vertically on a fixed offset.</summary>
    private static void CentreBand(RectTransform rt, float centreY, float height, float sideInset)
    {
        rt.anchorMin = new Vector2(sideInset, 0.5f);
        rt.anchorMax = new Vector2(1f - sideInset, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, centreY);
        rt.sizeDelta = new Vector2(0f, height);
        rt.localScale = Vector3.one;
    }

    // ---------------------------------------------------------------- wiring primitives

    private static void WireReference(SerializedObject so, string path, Object value, string label, Report report)
    {
        SerializedProperty property = so.FindProperty(path);
        if (property == null)
        {
            report.Warnings.Add($"{label}: no serialized field named '{path}' — field renamed or removed, nothing wired.");
            return;
        }

        if (ReferenceEquals(property.objectReferenceValue, value))
        {
            report.AlreadyCorrect.Add($"{label} already -> {Describe(value)}");
            return;
        }

        property.objectReferenceValue = value;
        report.Wired.Add($"{label} -> {Describe(value)}");
    }

    private static void WireFloat(SerializedObject so, string path, float value, string label, Report report)
    {
        SerializedProperty property = so.FindProperty(path);
        if (property == null)
        {
            report.Warnings.Add($"{label}: no serialized field named '{path}' — nothing set.");
            return;
        }

        if (Mathf.Approximately(property.floatValue, value))
        {
            report.AlreadyCorrect.Add($"{label} already {value}");
            return;
        }

        report.Wired.Add($"{label}: {property.floatValue} -> {value}");
        property.floatValue = value;
    }

    private static void WireColor(SerializedObject so, string path, Color value, string label, Report report)
    {
        SerializedProperty property = so.FindProperty(path);
        if (property == null)
        {
            report.Warnings.Add($"{label}: no serialized field named '{path}' — nothing set.");
            return;
        }

        if (property.colorValue == value)
        {
            report.AlreadyCorrect.Add($"{label} already {value}");
            return;
        }

        report.Wired.Add($"{label}: {property.colorValue} -> {value}");
        property.colorValue = value;
    }

    private static void WireVector2(SerializedObject so, string path, Vector2 value, string label, Report report)
    {
        SerializedProperty property = so.FindProperty(path);
        if (property == null)
        {
            report.Warnings.Add($"{label}: no serialized field named '{path}' — nothing set.");
            return;
        }

        if ((property.vector2Value - value).sqrMagnitude < 0.000001f)
        {
            report.AlreadyCorrect.Add($"{label} already {value}");
            return;
        }

        report.Wired.Add($"{label}: {property.vector2Value} -> {value}");
        property.vector2Value = value;
    }

    /// <summary>Replaces a serialized Object array field only if its contents differ, element for element.</summary>
    private static void WireObjectArray(SerializedObject so, string path, List<Level1TutorialStepSO> values, string label, Report report)
    {
        SerializedProperty property = so.FindProperty(path);
        if (property == null)
        {
            report.Warnings.Add($"{label}: no serialized field named '{path}' — nothing set.");
            return;
        }

        bool matches = property.arraySize == values.Count;
        for (int i = 0; matches && i < values.Count; i++)
            matches = ReferenceEquals(property.GetArrayElementAtIndex(i).objectReferenceValue, values[i]);

        if (matches)
        {
            report.AlreadyCorrect.Add($"{label} already [{string.Join(", ", values.ConvertAll(Describe))}]");
            return;
        }

        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

        report.Wired.Add($"{label} -> [{string.Join(", ", values.ConvertAll(Describe))}]");
    }

    private static string Describe(Object value)
    {
        if (value == null)
            return "(none)";

        var component = value as Component;
        return component != null
            ? $"{component.gameObject.name}.{component.GetType().Name}"
            : value.name;
    }

    // ---------------------------------------------------------------- reporting

    /// <summary>
    /// Guards the failure a green test suite cannot see: a new surface landing on top of existing
    /// HUD. Reported rather than thrown, because two overlapping rects can be intentional — this
    /// scene already overlaps its own clue panel with the feedback message.
    /// </summary>
    private static string OverlapReport(RectTransform mine, string what)
    {
        var sb = new StringBuilder($"    {what} overlap vs HUD siblings:");
        Rect a = WorldRect(mine);

        Canvas canvas = mine.GetComponentInParent<Canvas>();
        Transform canvasRoot = canvas != null ? canvas.rootCanvas.transform : mine.root;

        bool any = false;
        foreach (var candidate in canvasRoot.GetComponentsInChildren<RectTransform>(true))
        {
            if (candidate == mine || candidate.IsChildOf(mine) || mine.IsChildOf(candidate))
                continue;
            if (!candidate.gameObject.activeInHierarchy)
                continue;
            if (candidate.GetComponent<Graphic>() == null)
                continue;

            Rect b = WorldRect(candidate);
            if (a.Overlaps(b))
            {
                sb.Append($"\n      OVERLAPS {candidate.name} {b}");
                any = true;
            }
        }

        if (!any)
            sb.Append(" none");
        sb.Append($"\n      {what} worldRect={a}");
        return sb.ToString();
    }

    private static Rect WorldRect(RectTransform rt)
    {
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return new Rect(corners[0].x, corners[0].y, corners[2].x - corners[0].x, corners[2].y - corners[0].y);
    }

    private static string BuildLog(Report report, string headline)
    {
        var sb = new StringBuilder($"=== Level 1 teaching-beat wiring, {ScenePath} — {headline} ===\n");
        Section(sb, "CREATED", report.Created);
        Section(sb, "WIRED", report.Wired);
        Section(sb, "ALREADY CORRECT", report.AlreadyCorrect);
        Section(sb, "COULD NOT WIRE", report.Unwired);
        Section(sb, "WARNINGS", report.Warnings);
        Section(sb, "NOTES", report.Notes);
        sb.AppendLine();
        sb.AppendLine("LOOK AT THESE — none of the above is confirmed by a green test:");
        sb.AppendLine("  1. the introduction card at phone aspect: portrait not clipped at its runtime 2x, name/subtitle/ability not colliding, and the card clear of TraceHintPrompt above it and the boss bars below it;");
        sb.AppendLine("  2. the banner and the draw-feedback message: both sit in a 230 px band between the stroke guide and the restoration progress line, with only ~24-40 px of margin. Check nothing touches;");
        sb.AppendLine("  3. the badge flights: they should land on the runtime restoration rail's slots. _slotAnchors is empty by design — DrawFeedbackPresenter reads the rail first — so if a badge flies nowhere, the rail did not build, not that this wiring is missing;");
        sb.AppendLine("  4. the rail itself, on a NOTCHED device: it hangs off HUDLayer (safe-area inset) while the card hangs off FullScreenOverlay (not inset), so their gap is smaller in play than in the Scene view. Check the card's top edge against the rail's bottom edge there;");
        sb.AppendLine("  5. the gust: play until the ash arms and confirm the sweep ARRIVES at the clue panel rather than drifting off-stage;");
        sb.AppendLine("  6. the ghost stroke overlay: Image Type reads Filled, Fill Method Horizontal, Fill Amount 0;");
        sb.AppendLine("  7. the two message labels are different objects (FeedbackMessage vs DrawFeedbackMessage);");
        sb.AppendLine("  8. the card's font matches the clue panel family (TutorialFont), not the generic HUD font;");
        sb.AppendLine("  9. AshGustVFX.prefab in the Project window: grey, not white, and SingleAttackHitVFX itself unchanged.");
        return sb.ToString();
    }

    private static void Section(StringBuilder sb, string title, List<string> lines)
    {
        if (lines.Count == 0)
            return;

        sb.AppendLine($"  {title}:");
        for (int i = 0; i < lines.Count; i++)
            sb.AppendLine($"    - {lines[i]}");
    }
}
