using System.Collections.Generic;
using System.Collections;
using UnityEngine;

/// <summary>
/// Beat 4 — Three-kill combo teach. Spawns N (default 3) frozen enemies all carrying the
/// same glyph, spotlights them as a single rect, plays the intro template, then waits for
/// the player to draw the glyph once. On success all matching enemies are defeated in a
/// single AOE batch and EventBus.RaiseAOETriggered(count) fires the combo VFX.
/// </summary>
public sealed class ComboTeachBeat : OnboardingBeat
{
    public override OnboardingBeatType BeatType => OnboardingBeatType.ComboTeach;

    [Tooltip("Horizontal spacing between the spawned enemies in world units.")]
    [SerializeField] private float _enemySpacing = 1.6f;

    [Tooltip("World Y where each combo enemy spawns before walking in.")]
    [SerializeField] private float _walkInSpawnY = 13f;

    [Tooltip("Downward march speed (world units per second) while combo enemies walk in.")]
    [SerializeField] private float _marchSpeed = 3.0f;

    [Tooltip("Seconds between successive enemy spawns. Larger value = bigger vertical gap in the final formation.")]
    [SerializeField] private float _staggerBetweenEnemies = 0.4f;

    [Tooltip("World Y at which the LEAD (first spawned) enemy stops. Trailing enemies stop above it, naturally staggered by their later start times.")]
    [SerializeField] private float _leadStopY = 4f;

    public override IEnumerator Play(OnboardingContext ctx)
    {
        if (ctx == null || ctx.Sequence == null) yield break;

        Level1TutorialStepSO step = ctx.Sequence.comboTeachStep;
        if (step == null || step.targetCharacter == null)
        {
            DebugLogger.LogError("ComboTeachBeat: comboTeachStep or its targetCharacter is missing on the sequence SO.");
            yield break;
        }

        yield return OnboardingDialogueRunner.Play(ctx.Dialogue, ctx.Sequence.comboTeachPreVideo);

        TutorialRuntimeState.SetCombatOverrideActive(true);
        List<Level1TutorialEnemyController> enemies = new();
        yield return SpawnEnemyRowSequential(ctx, step, ctx.Sequence.comboEnemyCount, _enemySpacing, enemies);
        if (enemies.Count == 0)
        {
            TutorialRuntimeState.SetCombatOverrideActive(false);
            yield break;
        }

        OnboardingVideoTemplate teachMedia = ctx.Sequence.comboTeachVideo;
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

        // Reveal the union spotlight only now, for the draw phase.
        if (ctx.Spotlight != null)
        {
            Bounds union = ComputeUnionBounds(enemies);
            ctx.Spotlight.SetCamera(ctx.WorldCamera);
            ctx.Spotlight.Show(union, paddingWorld: 0.6f);
        }

        if (ctx.GuideUI != null)
            ctx.GuideUI.ShowMessage(step.promptText ?? "Draw the mark to defeat them all!", canSkip: false);

        if (showGifDuringDraw)
            ctx.IntroPlayer.ShowGifHint(teachMedia);

        if (GameManager.Instance != null && !GameManager.Instance.AcceptsDrawingInput)
            GameManager.Instance.StartGame();

        try
        {
            yield return SoloTeachBeat.WaitForCorrectDraw(step.targetCharacter.characterID);
        }
        finally
        {
            TutorialRuntimeState.SetCombatOverrideActive(false);
            if (showGifDuringDraw)
                ctx.IntroPlayer.HideGifHint();
        }

        if (ctx.GuideUI != null) ctx.GuideUI.Hide();

        // Hide the spotlight BEFORE the kill so the chain-lightning VFX is fully visible
        // (the dim overlay would otherwise darken the effect on the non-cutout enemies).
        if (ctx.Spotlight != null) ctx.Spotlight.Hide();

        // Mirror the real combat AOE event sequence so the chain-lightning VFX plays:
        //   per-enemy OnChainAttackStep drives ChainAttackHitVfxController,
        //   OnChainAttackHit covers AOE-wide audio/systems,
        //   OnAOETriggered drives the mass-clear HUD badge.
        List<Enemy> defeated = new(enemies.Count);
        foreach (Level1TutorialEnemyController e in enemies)
        {
            Enemy enemyRef = e != null ? e.Enemy : null;
            if (enemyRef != null)
            {
                EventBus.RaiseChainAttackStep(enemyRef);
                defeated.Add(enemyRef);
            }
            e.Defeat();
        }
        if (defeated.Count > 0)
            EventBus.RaiseChainAttackHit(defeated);
        EventBus.RaiseAOETriggered(enemies.Count);
        ctx.MarkFirstManualSuccess?.Invoke();

        yield return OnboardingDialogueRunner.Play(ctx.Dialogue, ctx.Sequence.comboTeachPostSuccess);
    }

    private IEnumerator SpawnEnemyRowSequential(OnboardingContext ctx, Level1TutorialStepSO step, int count, float spacing, List<Level1TutorialEnemyController> spawned)
    {
        if (count <= 0) yield break;
        float leftX = step.stopPosition.x - (spacing * (count - 1) / 2f);
        EnemyPool pool = EnemyPool.Instance;
        if (pool == null || step.enemyData == null)
        {
            DebugLogger.LogError("ComboTeachBeat: EnemyPool missing or step.enemyData null.");
            yield break;
        }

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = new(leftX + spacing * i, _walkInSpawnY, step.stopPosition.z);
            Level1TutorialEnemyController controller = SpawnMarchingEnemy(pool, step, spawnPos, _marchSpeed);
            if (controller == null) continue;
            spawned.Add(controller);

            if (i < count - 1 && _staggerBetweenEnemies > 0f)
                yield return new WaitForSeconds(_staggerBetweenEnemies);
        }

        if (spawned.Count == 0) yield break;

        Transform leadTransform = spawned[0].Enemy != null ? spawned[0].Enemy.transform : null;
        while (leadTransform != null && leadTransform.position.y > _leadStopY)
            yield return null;

        foreach (Level1TutorialEnemyController e in spawned)
            e.FreezeThreat();
    }

    private static Level1TutorialEnemyController SpawnMarchingEnemy(EnemyPool pool, Level1TutorialStepSO step, Vector3 spawnPos, float marchSpeed)
    {
        Enemy enemy = pool.Get(step.enemyData);
        if (enemy == null) return null;
        enemy.transform.position = spawnPos;
        enemy.AssignCharacter(step.targetCharacter);

        Level1TutorialEnemyController controller = new(enemy);
        controller.MarkAsTutorialTarget("TUTORIAL");
        controller.DisableContactDamage();

        EnemyMover mover = enemy.GetComponent<EnemyMover>();
        if (mover != null) mover.SetSpeed(marchSpeed);

        return controller;
    }

    private static Bounds ComputeUnionBounds(List<Level1TutorialEnemyController> enemies)
    {
        Bounds? union = null;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i] != null ? enemies[i].Enemy : null;
            if (e == null) continue;
            Bounds enemyBounds = ComputeEnemyBounds(e.transform);
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

    private static Bounds ComputeEnemyBounds(Transform enemyTransform)
    {
        if (enemyTransform == null) return new Bounds(Vector3.zero, Vector3.one);
        // SpriteRenderers only (body + glyph badge); skip TMP debug labels that hang below
        // the enemy so the union spotlight stays tight around the characters.
        SpriteRenderer[] sprites = enemyTransform.GetComponentsInChildren<SpriteRenderer>(includeInactive: false);
        Bounds? union = null;
        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer r = sprites[i];
            if (r == null || !r.enabled) continue;
            if (union.HasValue)
            {
                Bounds b = union.Value;
                b.Encapsulate(r.bounds);
                union = b;
            }
            else
            {
                union = r.bounds;
            }
        }
        return union ?? new Bounds(enemyTransform.position, Vector3.one);
    }
}
