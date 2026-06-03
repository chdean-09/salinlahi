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

    public override IEnumerator Play(OnboardingContext ctx)
    {
        if (ctx == null || ctx.Sequence == null) yield break;

        EnemyDataSO data = ctx.Sequence.heartLossDemoEnemyData;
        if (data == null || EnemyPool.Instance == null)
        {
            DebugLogger.LogError("HeartLossDemoBeat: enemy data missing or EnemyPool not ready.");
            yield break;
        }

        Enemy demoEnemy = SpawnDemoEnemy(ctx, data);
        if (demoEnemy == null) yield break;

        Level1TutorialEnemyController controller = new(demoEnemy);
        BaybayinCharacterSO demoCharacter = ResolveDemoCharacter(ctx.Sequence, data);
        if (demoCharacter != null)
            demoEnemy.AssignCharacter(demoCharacter);

        controller.MarkAsTutorialTarget(demoCharacter != null ? demoCharacter.characterID : "DEMO");
        controller.DisableContactDamage();

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
        yield return SoloTeachBeat.WalkEnemyTo(controller, basePos, _descendDuration);

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

        controller.Defeat();

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
