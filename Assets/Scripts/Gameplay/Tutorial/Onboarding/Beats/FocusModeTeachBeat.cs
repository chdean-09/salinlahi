using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Level 2 beat — lets the player defeat two controlled enemies, then introduces
/// Focus Mode as the advanced combat tool unlocked by sustained momentum.
/// </summary>
public sealed class FocusModeTeachBeat : OnboardingBeat
{
    public override OnboardingBeatType BeatType => OnboardingBeatType.FocusModeTeach;

    [Tooltip("World Y where each practice enemy spawns before walking in.")]
    [SerializeField] private float _walkInSpawnY = 13f;

    [Tooltip("Seconds each practice enemy takes to reach the draw prompt position.")]
    [SerializeField] private float _walkInDuration = 1.1f;

    [Tooltip("Pause between controlled practice kills.")]
    [SerializeField] private float _betweenPracticeKillsSeconds = 0.25f;

    [Header("Focus Chain Practice")]
    [Tooltip("Horizontal spacing between focus-mode chain enemies.")]
    [SerializeField] private float _focusChainEnemySpacing = 1.6f;

    [Tooltip("Spawn Y for the focus-mode group. Kept close so focus mode is still active while they march.")]
    [SerializeField] private float _focusChainSpawnY = 7.5f;

    [Tooltip("World Y where the lead focus-mode enemy stops for the lightning prompt.")]
    [SerializeField] private float _focusChainLeadStopY = 4f;

    [Tooltip("Raw march speed for the focus-mode group. EnemyMover multiplies this by the active focus speed multiplier.")]
    [SerializeField] private float _focusChainMarchSpeed = 3f;

    [Tooltip("Seconds between focus-mode enemy spawns.")]
    [SerializeField] private float _focusChainStaggerSeconds = 0.25f;

    public override IEnumerator Play(OnboardingContext ctx)
    {
        if (ctx == null || ctx.Sequence == null)
            yield break;

        Level1TutorialStepSO step = ResolvePracticeStep(ctx.Sequence);
        if (step == null || step.targetCharacter == null)
        {
            DebugLogger.LogError("FocusModeTeachBeat: focus practice step or its targetCharacter is missing on the sequence SO.");
            yield break;
        }

        yield return OnboardingDialogueRunner.Play(ctx.Dialogue, ctx.Sequence.focusPracticeIntro);

        int killCount = Mathf.Max(1, ctx.Sequence.focusPracticeKillCount);
        for (int i = 0; i < killCount; i++)
            yield return PlayPracticeKill(ctx, step, i, killCount);

        yield return WaitForFocusModeActivation();
        yield return OnboardingDialogueRunner.Play(ctx.Dialogue, ctx.Sequence.focusModeIntro);
        yield return PlayFocusChainPractice(ctx);
        yield return OnboardingDialogueRunner.Play(ctx.Dialogue, ctx.Sequence.focusChainPostSuccess);
        ComboManager.Instance?.ResetStreakForTutorial();
    }

    private IEnumerator PlayPracticeKill(
        OnboardingContext ctx,
        Level1TutorialStepSO step,
        int killIndex,
        int killCount)
    {
        TutorialRuntimeState.SetCombatOverrideActive(true);

        Vector3 spawnPos = new(step.stopPosition.x, _walkInSpawnY, step.stopPosition.z);
        Level1TutorialEnemyController enemy = SoloTeachBeat.SpawnFrozenEnemy(ctx, step, spawnPos);
        if (enemy == null)
        {
            TutorialRuntimeState.SetCombatOverrideActive(false);
            yield break;
        }

        yield return SoloTeachBeat.WalkEnemyTo(enemy, step.stopPosition, _walkInDuration);

        if (ctx.Spotlight != null && enemy.Enemy != null)
        {
            ctx.Spotlight.SetCamera(ctx.WorldCamera);
            ctx.Spotlight.Show(enemy.Enemy.transform);
        }

        if (ctx.GuideUI != null)
        {
            string prompt = step.promptText ?? "Draw the mark to keep your momentum.";
            ctx.GuideUI.ShowMessage($"{prompt} ({killIndex + 1}/{killCount})", canSkip: false);
        }

        if (GameManager.Instance != null && !GameManager.Instance.AcceptsDrawingInput)
            GameManager.Instance.StartGame();

        try
        {
            yield return SoloTeachBeat.WaitForCorrectDraw(step.targetCharacter.characterID);
        }
        finally
        {
            TutorialRuntimeState.SetCombatOverrideActive(false);
        }

        if (ctx.GuideUI != null)
            ctx.GuideUI.Hide();
        if (ctx.Spotlight != null)
            ctx.Spotlight.Hide();

        Enemy enemyRef = enemy.Enemy;
        if (enemyRef != null)
        {
            EventBus.RaiseEnemyTargeted(enemyRef);
            EventBus.RaiseSingleAttackHit(enemyRef);
        }

        enemy.Defeat();
        ctx.MarkFirstManualSuccess?.Invoke();

        if (killIndex < killCount - 1 && _betweenPracticeKillsSeconds > 0f)
            yield return new WaitForSeconds(_betweenPracticeKillsSeconds);
    }

    private IEnumerator WaitForFocusModeActivation()
    {
        if (ComboManager.Instance == null || ComboManager.Instance.IsFocusModeActive)
            yield break;

        bool activated = false;
        System.Action handler = () => activated = true;
        EventBus.OnFocusModeActivated += handler;

        float timeoutSeconds = 0.75f;
        float start = Time.unscaledTime;
        try
        {
            while (!activated && Time.unscaledTime - start < timeoutSeconds)
                yield return null;
        }
        finally
        {
            EventBus.OnFocusModeActivated -= handler;
        }

        if (!activated)
            DebugLogger.LogWarning("FocusModeTeachBeat: Focus Mode did not activate after the 2/2 practice kill. Continuing so the tutorial does not stall.");
    }

    private IEnumerator PlayFocusChainPractice(OnboardingContext ctx)
    {
        Level1TutorialStepSO step = ResolveFocusChainStep(ctx.Sequence);
        if (step == null || step.targetCharacter == null)
        {
            DebugLogger.LogError("FocusModeTeachBeat: focus chain step or its targetCharacter is missing on the sequence SO.");
            yield break;
        }

        yield return OnboardingDialogueRunner.Play(ctx.Dialogue, ctx.Sequence.focusChainIntro);

        TutorialRuntimeState.SetCombatOverrideActive(true);
        List<Level1TutorialEnemyController> enemies = new();
        yield return SpawnFocusChainEnemies(ctx, step, Mathf.Max(3, ctx.Sequence.focusChainEnemyCount), enemies);
        if (enemies.Count == 0)
        {
            TutorialRuntimeState.SetCombatOverrideActive(false);
            yield break;
        }

        if (ctx.Spotlight != null)
        {
            Bounds union = ComputeUnionBounds(enemies);
            ctx.Spotlight.SetCamera(ctx.WorldCamera);
            ctx.Spotlight.Show(union, paddingWorld: 0.6f);
        }

        if (ctx.GuideUI != null)
            ctx.GuideUI.ShowMessage("Focus is active. Draw once to chain them again.", canSkip: false);

        if (GameManager.Instance != null && !GameManager.Instance.AcceptsDrawingInput)
            GameManager.Instance.StartGame();

        try
        {
            yield return SoloTeachBeat.WaitForCorrectDraw(step.targetCharacter.characterID);
        }
        finally
        {
            TutorialRuntimeState.SetCombatOverrideActive(false);
        }

        if (ctx.GuideUI != null)
            ctx.GuideUI.Hide();
        if (ctx.Spotlight != null)
            ctx.Spotlight.Hide();

        ComboTeachBeat.DefeatChainTargets(enemies, awardComboCredit: true);
    }

    private IEnumerator SpawnFocusChainEnemies(
        OnboardingContext ctx,
        Level1TutorialStepSO step,
        int count,
        List<Level1TutorialEnemyController> spawned)
    {
        EnemyPool pool = EnemyPool.Instance;
        if (pool == null || step.enemyData == null)
        {
            DebugLogger.LogError("FocusModeTeachBeat: EnemyPool missing or focus chain enemyData null.");
            yield break;
        }

        float leftX = step.stopPosition.x - (_focusChainEnemySpacing * (count - 1) / 2f);
        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = new(leftX + _focusChainEnemySpacing * i, _focusChainSpawnY, step.stopPosition.z);
            Level1TutorialEnemyController controller = SpawnMarchingEnemy(pool, step, spawnPos, _focusChainMarchSpeed);
            if (controller != null)
                spawned.Add(controller);

            if (i < count - 1 && _focusChainStaggerSeconds > 0f)
                yield return new WaitForSeconds(_focusChainStaggerSeconds);
        }

        if (spawned.Count == 0)
            yield break;

        Transform leadTransform = spawned[0].Enemy != null ? spawned[0].Enemy.transform : null;
        while (leadTransform != null && leadTransform.position.y > _focusChainLeadStopY)
            yield return null;

        for (int i = 0; i < spawned.Count; i++)
            spawned[i]?.FreezeThreat();
    }

    private static Level1TutorialEnemyController SpawnMarchingEnemy(
        EnemyPool pool,
        Level1TutorialStepSO step,
        Vector3 spawnPos,
        float marchSpeed)
    {
        Enemy enemy = pool.Get(step.enemyData);
        if (enemy == null)
            return null;

        enemy.transform.position = spawnPos;
        enemy.AssignCharacter(step.targetCharacter);

        Level1TutorialEnemyController controller = new(enemy);
        controller.MarkAsTutorialTarget("FOCUS");
        controller.DisableContactDamage();

        EnemyMover mover = enemy.GetComponent<EnemyMover>();
        if (mover != null)
            mover.SetSpeed(marchSpeed);

        return controller;
    }

    private static Bounds ComputeUnionBounds(List<Level1TutorialEnemyController> enemies)
    {
        Bounds? union = null;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i] != null ? enemies[i].Enemy : null;
            if (e == null)
                continue;

            Bounds enemyBounds = new(e.transform.position, Vector3.one);
            SpriteRenderer[] sprites = e.GetComponentsInChildren<SpriteRenderer>(includeInactive: false);
            for (int j = 0; j < sprites.Length; j++)
            {
                SpriteRenderer sprite = sprites[j];
                if (sprite != null && sprite.enabled)
                    enemyBounds.Encapsulate(sprite.bounds);
            }

            if (union.HasValue)
            {
                Bounds b = union.Value;
                b.Encapsulate(enemyBounds);
                union = b;
            }
            else
            {
                union = enemyBounds;
            }
        }

        return union ?? new Bounds(Vector3.zero, Vector3.one);
    }

    private static Level1TutorialStepSO ResolvePracticeStep(OnboardingSequenceSO sequence)
    {
        if (sequence.focusPracticeStep != null)
            return sequence.focusPracticeStep;
        if (sequence.soloTeachStep != null)
            return sequence.soloTeachStep;
        return sequence.comboTeachStep;
    }

    private static Level1TutorialStepSO ResolveFocusChainStep(OnboardingSequenceSO sequence)
    {
        if (sequence.focusChainStep != null)
            return sequence.focusChainStep;
        if (sequence.comboTeachStep != null)
            return sequence.comboTeachStep;
        return ResolvePracticeStep(sequence);
    }
}
