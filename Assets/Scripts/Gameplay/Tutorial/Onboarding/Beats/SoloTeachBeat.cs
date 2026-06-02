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

        Level1TutorialStepSO step = ctx.Sequence.soloTeachStep;
        if (step == null || step.targetCharacter == null)
        {
            DebugLogger.LogError("SoloTeachBeat: soloTeachStep or its targetCharacter is missing on the sequence SO.");
            yield break;
        }

        yield return OnboardingDialogueRunner.Play(ctx.Dialogue, ctx.Sequence.soloTeachPreVideo);

        TutorialRuntimeState.SetCombatOverrideActive(true);
        Vector3 spawnPos = new(step.stopPosition.x, _walkInSpawnY, step.stopPosition.z);
        Level1TutorialEnemyController enemy = SpawnFrozenEnemy(ctx, step, spawnPos);
        if (enemy == null)
        {
            TutorialRuntimeState.SetCombatOverrideActive(false);
            yield break;
        }

        yield return WalkEnemyTo(enemy, step.stopPosition, _walkInDuration);

        // Play the intro video FIRST over the clean scene — no spotlight yet, so the video
        // overlay fully covers the area instead of competing with a bright enemy cut-out.
        if (ctx.IntroPlayer != null)
        {
            TutorialRuntimeState.SetDrawingInputLocked(true);
            try
            {
                yield return ctx.IntroPlayer.Play(ctx.Sequence.soloTeachVideo);
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
            if (feedbackHandler != null)
                EventBus.OnRecognitionResolved -= feedbackHandler;
        }

        if (ctx.GuideUI != null) ctx.GuideUI.Hide();

        enemy.Defeat();
        ctx.MarkFirstManualSuccess?.Invoke();

        if (ctx.Spotlight != null) ctx.Spotlight.Hide();

        yield return OnboardingDialogueRunner.Play(ctx.Dialogue, ctx.Sequence.soloTeachPostSuccess);
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
