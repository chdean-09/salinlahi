using System.Collections;
using UnityEngine;

/// <summary>
/// Beat 5 — Safe heart-loss demo. Spawns a demo enemy with contact damage disabled so it
/// can never fire the real OnBaseHit event. When the demo enemy reaches the base, the beat
/// raises EventBus.OnTutorialBaseHitDemo via <see cref="DemoHeartSimulator"/>, which drives
/// the heart-shake and camera-feedback visuals WITHOUT calling HeartSystem.LoseHeart.
/// </summary>
public sealed class HeartLossDemoBeat : OnboardingBeat
{
    public override OnboardingBeatType BeatType => OnboardingBeatType.HeartLossDemo;

    [Tooltip("Y proximity to the base (world units) that counts as 'reached the base'.")]
    [SerializeField] private float _baseProximityY = 0.6f;

    [Tooltip("Seconds for the demo enemy to descend from its spawn point to the base. Slower than a normal runner so the player can clearly follow the approach → hit → heart-loss sequence.")]
    [SerializeField] private float _descendDuration = 2.6f;

    [Tooltip("Spawn Y for the demo enemy (above the visible play area).")]
    [SerializeField] private float _spawnY = 6f;

    [Tooltip("Brief slow-motion right after the base hit so the player registers the heart loss. Time scale dips to this value.")]
    [SerializeField] [Range(0.05f, 1f)] private float _postHitSlowMoScale = 0.25f;

    [Tooltip("Real-time seconds the post-hit slow-motion lasts before normal speed resumes.")]
    [SerializeField] private float _postHitSlowMoSeconds = 0.8f;

    [Tooltip("Message shown when the base HP is intentionally restored for the tutorial, so the refill isn't a sudden unexplained snap-back.")]
    [TextArea(1, 3)]
    [SerializeField] private string _restoreMessage = "Don't worry, anak — I'll restore our strength for this lesson.";

    /// <summary>
    /// The stand-in currently on the field, held so an aborted demo can still undo what the demo
    /// did to it. A coroutine's <c>finally</c> does NOT run when Unity stops the coroutine — a
    /// disabled component drops the routine on the floor without unwinding it — so
    /// <see cref="OnDisable"/> repeats the restore. Without that path an abort mid-walk leaves a
    /// live enemy permanently unmarked and permanently unkillable.
    /// </summary>
    private Enemy _demoEnemy;

    public override IEnumerator Play(OnboardingContext ctx)
    {
        if (ctx == null || ctx.Sequence == null) yield break;

        EnemyDataSO data = ctx.Sequence.heartLossDemoEnemyData;
        if (data == null || EnemyPool.Instance == null)
        {
            DebugLogger.LogError("HeartLossDemoBeat: enemy data missing or EnemyPool not ready.");
            yield break;
        }

        // Opened BEFORE the stand-in is spawned and closed in the finally below, so that every
        // frame this beat owns is one in which a real heart cannot be lost. Belt and braces on top
        // of the specific leak fixed in DespawnSilently: this beat puts a pooled enemy on top of
        // the shrine on purpose, and the next person to change how it leaves the field should not
        // be able to reintroduce a real base hit without the guard shouting about it.
        TutorialRuntimeState.SetHeartLossDemoActive(true);
        try
        {
            yield return PlayGuarded(ctx, data);
        }
        finally
        {
            TutorialRuntimeState.SetHeartLossDemoActive(false);
        }
    }

    private IEnumerator PlayGuarded(OnboardingContext ctx, EnemyDataSO data)
    {
        Enemy demoEnemy = SpawnDemoEnemy(ctx, data);
        if (demoEnemy == null) yield break;

        Level1TutorialEnemyController controller = new(demoEnemy);
        BaybayinCharacterSO demoCharacter = ResolveDemoCharacter(ctx.Sequence, data);
        if (demoCharacter != null)
            demoEnemy.AssignCharacter(demoCharacter);

        controller.DisableContactDamage();
        ClaimDemoEnemy(demoEnemy);

        try
        {
            yield return PlayDemo(ctx, demoEnemy, controller);
        }
        finally
        {
            ReleaseDemoEnemy();
        }
    }

    /// <summary>
    /// Takes the stand-in out of combat for the length of the demo: no glyph badge, and no way to
    /// resolve it.
    ///
    /// <para>
    /// <b>The badge.</b> This enemy's job is to walk through and cost a heart. A glyph over its
    /// head says "draw this to stop me", which is the exact reading the beat exists to disprove —
    /// the player who tries and fails to stop it learns the wrong lesson, and the player who
    /// doesn't try is left thinking they let a stoppable enemy through. It is hidden and restored
    /// the same way <c>EnemyIntroductionBeat</c> handles its subject's late glyph reveal.
    /// </para>
    ///
    /// <para>
    /// Hidden AFTER the spawn, deliberately: <c>ActiveCluePresenter.HandleEnemySpawned</c> sweeps
    /// the badge policy across an enemy the frame it appears, and on a clue-combat level with no
    /// clue yet that sweep calls <c>Show()</c>. A hide placed before the spawn would be undone.
    /// </para>
    ///
    /// <para>
    /// <b>The resolution block.</b> Nothing locks drawing input during this beat, so before it a
    /// player who drew the demo enemy's character had it resolved like any other carrier —
    /// <c>CombatResolver</c> matches on the assigned character alone. That killed the prop
    /// mid-walk, and on Level 1, whose demo enemy is Hati, a real defeat at the shrine splits into
    /// two live minions on top of the base. The block is refused by both
    /// <c>CombatResolver.IsEligibleCombatTarget</c> and <c>ActiveClueDirector</c>, which also stops
    /// the stand-in from ever being marked the active clue and swept back into view.
    /// </para>
    /// </summary>
    private void ClaimDemoEnemy(Enemy demoEnemy)
    {
        if (demoEnemy == null)
            return;

        _demoEnemy = demoEnemy;
        demoEnemy.AddResolutionBlock(this);
        demoEnemy.GlyphBadge?.Hide();
    }

    /// <summary>
    /// Undoes <see cref="ClaimDemoEnemy"/>. Idempotent, because it runs from both the beat's
    /// <c>finally</c> and <see cref="OnDisable"/> and either may be the one that gets there.
    ///
    /// <para>
    /// The badge is only shown again while the enemy is still on the field. On the normal path the
    /// stand-in has already gone back to the pool by now, and <c>EnemyGlyphBadge.ResetForPool</c>
    /// has restored the badge for its next user — re-showing it here would turn the renderer back
    /// on for a parked shell.
    /// </para>
    /// </summary>
    private void ReleaseDemoEnemy()
    {
        Enemy demoEnemy = _demoEnemy;
        _demoEnemy = null;
        if (demoEnemy == null)
            return;

        demoEnemy.RemoveResolutionBlock(this);
        if (demoEnemy.gameObject.activeInHierarchy)
            demoEnemy.GlyphBadge?.Show();
    }

    /// <summary>
    /// A coroutine stopped by Unity never runs its <c>finally</c>, and disabling the component is
    /// exactly that case. Without this an aborted demo leaves a live enemy with no glyph and a
    /// standing resolution block: unmarked and unkillable for the rest of the level.
    /// </summary>
    private void OnDisable() => ReleaseDemoEnemy();

    private IEnumerator PlayDemo(
        OnboardingContext ctx,
        Enemy demoEnemy,
        Level1TutorialEnemyController controller)
    {
        if (ctx.Spotlight != null && ctx.PlayerBase != null)
        {
            Bounds baseBounds = ResolveBaseBounds(ctx.PlayerBase);
            // Encapsulate the enemy spawn into the spotlight rect so both the enemy and the base
            // are visible (and the HUD heart sits within the dim frame too).
            Bounds composite = baseBounds;
            composite.Encapsulate(new Bounds(demoEnemy.transform.position, Vector3.one));
            ctx.Spotlight.SetCamera(ctx.WorldCamera);
            ctx.Spotlight.Show(composite, paddingWorld: 1.0f);
        }

        float targetY = ctx.PlayerBase != null ? ctx.PlayerBase.transform.position.y + _baseProximityY : 0f;
        Vector3 basePos = new(demoEnemy.transform.position.x, targetY, demoEnemy.transform.position.z);
        yield return Level1TutorialEnemyController.WalkEnemyTo(controller, basePos, _descendDuration);

        controller.FreezeThreat();
        RevealHeartHudForDemo();

        // The hit: empties a heart with shake/flash (DemoHeartSimulator → HeartDisplay /
        // BaseHitFeedbackController). No real HP is lost — tutorial-only events.
        if (ctx.DemoHearts != null)
        {
            yield return ctx.DemoHearts.PlayDemoHit();
        }
        else
        {
            EventBus.RaiseTutorialBaseHitDemo(1);
            yield return new WaitForSecondsRealtime(0.6f);
        }

        // NOT Defeat(). Level 1's demo enemy splits on defeat, and the two pieces landed on the
        // shrine and took two real hearts. See Level1TutorialEnemyController.DespawnSilently.
        controller.DespawnSilently();

        // Brief slow-motion so the player registers what just happened to the heart.
        yield return PostHitSlowMo();

        // Explain the loss while the heart is visibly EMPTY (no silent snap-back yet).
        yield return OnboardingDialogueRunner.Play(ctx.Dialogue, ctx.Sequence.heartLossDialogue);

        // Now restore the heart intentionally and visibly, with a message so it reads as
        // a deliberate tutorial reset rather than an unexplained refill.
        if (ctx.GuideUI != null && !string.IsNullOrEmpty(_restoreMessage))
            ctx.GuideUI.ShowMessage(_restoreMessage, canSkip: false);
        if (ctx.DemoHearts != null)
        {
            yield return ctx.DemoHearts.PlayDemoRestore();
        }
        else
        {
            EventBus.RaiseTutorialBaseRestoreDemo();
            yield return new WaitForSecondsRealtime(0.6f);
        }
        if (ctx.GuideUI != null)
            ctx.GuideUI.Hide();

        if (ctx.Spotlight != null) ctx.Spotlight.Hide();
    }

    internal static bool RevealHeartHudForDemo()
    {
        HeartDisplay display = FindFirstObjectByType<HeartDisplay>(FindObjectsInactive.Include);
        if (display == null)
            return false;

        Transform current = display.transform;
        Transform root = current;
        while (root.parent != null)
            root = root.parent;

        ActivateHierarchyPath(root, current);
        return display.gameObject.activeInHierarchy;
    }

    private static bool ActivateHierarchyPath(Transform current, Transform target)
    {
        if (current == null)
            return false;

        if (current == target)
        {
            current.gameObject.SetActive(true);
            return true;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            Transform child = current.GetChild(i);
            if (!ActivateHierarchyPath(child, target))
                continue;

            current.gameObject.SetActive(true);
            return true;
        }

        return false;
    }

    // Brief slow-motion emphasis after the base hit. Uses unscaled real-time to time itself
    // so it works regardless of the dipped timeScale, and always restores timeScale to 1.
    private IEnumerator PostHitSlowMo()
    {
        float seconds = Mathf.Max(0f, _postHitSlowMoSeconds);
        if (seconds <= 0f) yield break;

        float previous = Time.timeScale;
        Time.timeScale = Mathf.Clamp(_postHitSlowMoScale, 0.05f, 1f);
        yield return new WaitForSecondsRealtime(seconds);
        Time.timeScale = previous <= 0f ? 1f : previous;
    }

    private Enemy SpawnDemoEnemy(OnboardingContext ctx, EnemyDataSO data)
    {
        Enemy enemy = EnemyPool.Instance.Get(data);
        if (enemy == null) return null;
        float x = ctx.PlayerBase != null ? ctx.PlayerBase.transform.position.x : 0f;
        enemy.transform.position = new Vector3(x, _spawnY, 0f);
        BaybayinCharacterSO character = ResolveDemoCharacter(ctx.Sequence, data);
        if (character != null)
            enemy.AssignCharacter(character);

        // Stop the data-driven mover; the descent is driven externally by WalkEnemyTo at a
        // fixed duration so Enemy.Update can't reset us back to the slow EnemyDataSO speed.
        EnemyMover mover = enemy.GetComponent<EnemyMover>();
        if (mover != null) mover.Stop();
        return enemy;
    }

    private static BaybayinCharacterSO ResolveDemoCharacter(OnboardingSequenceSO sequence, EnemyDataSO data)
    {
        if (sequence != null && sequence.heartLossDemoCharacter != null)
            return sequence.heartLossDemoCharacter;

        return data != null ? data.assignedCharacter : null;
    }

    private static Bounds ResolveBaseBounds(PlayerBase playerBase)
    {
        Collider2D col = playerBase.GetComponent<Collider2D>();
        if (col != null) return col.bounds;
        Renderer r = playerBase.GetComponent<Renderer>();
        if (r != null) return r.bounds;
        return new Bounds(playerBase.transform.position, Vector3.one);
    }
}
