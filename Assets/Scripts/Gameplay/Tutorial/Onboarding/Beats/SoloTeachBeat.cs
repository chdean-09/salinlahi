using System.Collections;
using UnityEngine;

/// <summary>
/// Beat 3 — Single-enemy draw teach. Spawns one frozen enemy carrying the assigned step's
/// glyph, spotlights it, plays the intro template, then waits for the player to draw the
/// glyph correctly via the existing recognition flow.
/// </summary>
public sealed class SoloTeachBeat : OnboardingBeat
{
    public override OnboardingBeatType BeatType => OnboardingBeatType.SoloTeach;

    [Tooltip("World Y where the tutorial enemy spawns before walking in. Should be above the visible play area.")]
    [SerializeField] private float _walkInSpawnY = 13f;

    [Tooltip("Seconds the walk-in animation takes from spawn position to the step's stop position.")]
    [SerializeField] private float _walkInDuration = 1.4f;

    public override IEnumerator Play(OnboardingContext ctx)
    {
        if (ctx == null || ctx.Sequence == null)
            yield break;

        Level1TutorialStepSO[] steps = ResolveTeachSteps(ctx.Sequence);
        if (steps == null || steps.Length == 0)
        {
            DebugLogger.LogError("SoloTeachBeat: no soloTeachStep or basicTeachSteps are assigned on the sequence SO.");
            yield break;
        }

        yield return OnboardingDialogueRunner.Play(ctx.Dialogue, ctx.Sequence.soloTeachPreVideo);

        for (int i = 0; i < steps.Length; i++)
        {
            Level1TutorialStepSO step = steps[i];
            if (step == null || step.targetCharacter == null)
            {
                DebugLogger.LogError("SoloTeachBeat: teach step or its targetCharacter is missing on the sequence SO.");
                continue;
            }

            yield return PlaySingleTeachStep(ctx, step, ResolveTeachMedia(ctx.Sequence, i));
        }

        yield return OnboardingDialogueRunner.Play(ctx.Dialogue, ctx.Sequence.soloTeachPostSuccess);
    }

    private IEnumerator PlaySingleTeachStep(
        OnboardingContext ctx,
        Level1TutorialStepSO step,
        OnboardingVideoTemplate teachMedia)
    {
        TutorialRuntimeState.SetCombatOverrideActive(true);
        Vector3 spawnPos = new(step.stopPosition.x, _walkInSpawnY, step.stopPosition.z);
        Level1TutorialEnemyController enemy = SpawnFrozenEnemy(ctx, step, spawnPos);
        if (enemy == null)
        {
            TutorialRuntimeState.SetCombatOverrideActive(false);
            yield break;
        }

        yield return WalkEnemyTo(enemy, step.stopPosition, _walkInDuration);

        bool showGifDuringDraw = ctx.IntroPlayer != null && TutorialIntroPlayer.TemplateUsesGif(teachMedia);

        // Non-GIF media stays modal before the draw phase. GIF hints are shown later,
        // alongside the draw prompt, so they do not dim or block drawing input.
        if (ctx.IntroPlayer != null && !showGifDuringDraw)
        {
            TutorialRuntimeState.SetDrawingInputLocked(true);
            try
            {
                yield return ctx.IntroPlayer.Play(teachMedia);
            }
            finally
            {
                TutorialRuntimeState.SetDrawingInputLocked(false);
            }
        }

        // Reveal the enemy spotlight only now, for the draw phase, so the player sees which
        // enemy to mark. Hidden again after the correct draw (end of beat).
        if (ctx.Spotlight != null && enemy.Enemy != null)
        {
            ctx.Spotlight.SetCamera(ctx.WorldCamera);
            ctx.Spotlight.Show(enemy.Enemy.transform);
        }

        if (ctx.GuideUI != null)
            ctx.GuideUI.ShowMessage(step.promptText ?? "Draw the mark to defeat the enemy!", canSkip: false);

        if (showGifDuringDraw)
            ctx.IntroPlayer.ShowGifHint(teachMedia);

        string expectedID = step.targetCharacter.characterID;
        System.Action<RecognitionResult, bool, float> feedbackHandler = null;
        if (ctx.GuideUI != null)
        {
            feedbackHandler = (result, passed, _) =>
            {
                bool isCorrect = passed && string.Equals(result.characterID, expectedID, System.StringComparison.OrdinalIgnoreCase);
                if (isCorrect) return;
                string feedback = passed
                    ? (step.wrongCharacterFeedback ?? "Draw the shown syllable.")
                    : (step.recognitionFailedFeedback ?? "Try that shape again.");
                ctx.GuideUI.ShowFeedback(feedback);
            };
            EventBus.OnRecognitionResolved += feedbackHandler;
        }

        if (GameManager.Instance != null && !GameManager.Instance.AcceptsDrawingInput)
            GameManager.Instance.StartGame();

        try
        {
            yield return WaitForCorrectDraw(step.targetCharacter.characterID);
        }
        finally
        {
            TutorialRuntimeState.SetCombatOverrideActive(false);
            if (showGifDuringDraw)
                ctx.IntroPlayer.HideGifHint();
            if (feedbackHandler != null)
                EventBus.OnRecognitionResolved -= feedbackHandler;
        }

        if (ctx.GuideUI != null) ctx.GuideUI.Hide();

        enemy.Defeat();
        ctx.MarkFirstManualSuccess?.Invoke();

        if (ctx.Spotlight != null) ctx.Spotlight.Hide();
    }

    private static Level1TutorialStepSO[] ResolveTeachSteps(OnboardingSequenceSO sequence)
    {
        if (sequence.basicTeachSteps != null && sequence.basicTeachSteps.Length > 0)
            return sequence.basicTeachSteps;

        return sequence.soloTeachStep != null
            ? new[] { sequence.soloTeachStep }
            : System.Array.Empty<Level1TutorialStepSO>();
    }

    private static OnboardingVideoTemplate ResolveTeachMedia(OnboardingSequenceSO sequence, int index)
    {
        if (sequence.basicTeachVideos != null
            && index >= 0
            && index < sequence.basicTeachVideos.Length)
        {
            OnboardingVideoTemplate media = sequence.basicTeachVideos[index];
            if (TemplateHasMediaOrPrompt(media))
                return media;
        }

        return sequence.soloTeachVideo;
    }

    private static bool TemplateHasMediaOrPrompt(OnboardingVideoTemplate media)
    {
        return media.videoClip != null
            || media.gifTexture != null
            || (media.gifFrames != null && media.gifFrames.Length > 0)
            || media.animationClip != null
            || !string.IsNullOrWhiteSpace(media.tapToProceedText);
    }

    internal static Level1TutorialEnemyController SpawnFrozenEnemy(OnboardingContext ctx, Level1TutorialStepSO step, Vector3 position)
    {
        EnemyPool pool = EnemyPool.Instance;
        if (pool == null || step.enemyData == null)
        {
            DebugLogger.LogError("SoloTeachBeat.SpawnFrozenEnemy: EnemyPool missing or step.enemyData null.");
            return null;
        }

        Enemy enemy = pool.Get(step.enemyData);
        if (enemy == null) return null;
        enemy.transform.position = position;
        enemy.AssignCharacter(step.targetCharacter);

        Level1TutorialEnemyController controller = new(enemy);
        controller.MarkAsTutorialTarget("TUTORIAL");
        controller.FreezeThreat();
        return controller;
    }

    internal static IEnumerator WalkEnemyTo(Level1TutorialEnemyController controller, Vector3 targetPos, float duration)
    {
        if (controller == null || controller.Enemy == null || duration <= 0f)
        {
            if (controller != null && controller.Enemy != null)
                controller.Enemy.transform.position = targetPos;
            yield break;
        }

        Transform enemyTransform = controller.Enemy.transform;
        EnemyMover mover = controller.Enemy.GetComponent<EnemyMover>();

        if (mover != null) mover.SetExternallyMoving(true);

        Vector3 startPos = enemyTransform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - t) * (1f - t);
            if (enemyTransform == null)
            {
                if (mover != null) mover.SetExternallyMoving(false);
                yield break;
            }
            enemyTransform.position = Vector3.Lerp(startPos, targetPos, eased);
            yield return null;
        }

        if (enemyTransform != null) enemyTransform.position = targetPos;
        if (mover != null) mover.SetExternallyMoving(false);
    }

    internal static IEnumerator WaitForCorrectDraw(string expectedCharacterID)
    {
        bool resolved = false;
        System.Action<RecognitionResult, bool, float> handler = (result, passed, _) =>
        {
            if (passed && string.Equals(result.characterID, expectedCharacterID, System.StringComparison.OrdinalIgnoreCase))
                resolved = true;
        };
        EventBus.OnRecognitionResolved += handler;
        try { yield return new WaitUntil(() => resolved); }
        finally { EventBus.OnRecognitionResolved -= handler; }
    }
}
