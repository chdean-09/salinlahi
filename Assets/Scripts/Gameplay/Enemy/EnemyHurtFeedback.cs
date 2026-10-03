using System.Collections;
using UnityEngine;

// Sibling component on an Enemy prefab. Plays a configurable on-hit reaction
// (movement pause, sprite shake, optional character swap, optional hurt-frame
// animation) when the Enemy takes non-lethal damage. All toggles and tuning
// come from EnemyDataSO so designers configure per variant.
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(EnemyMover))]
public class EnemyHurtFeedback : MonoBehaviour
{
    private Enemy _enemy;
    private EnemyMover _mover;
    private SpriteRenderer _renderer;

    private bool _hasSwappedCharacter;
    private Coroutine _hurtRoutine;
    // Tracks the shake offset currently applied to the root transform so it can
    // be undone even when the hurt routine is cancelled (e.g. by Defeat()).
    private Vector3 _appliedShake;

    public bool IsPlayingHurtAnimation => _hurtRoutine != null;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _mover = GetComponent<EnemyMover>();
        _renderer = GetComponent<SpriteRenderer>();
    }

    private void OnDisable()
    {
        // Pool return / scene unload — drop hurt state silently so the next
        // spawn starts clean.
        ResetState();
    }

    // Called by Enemy.ResetForPool so the next reuse from the pool starts clean.
    // Also called from Enemy.Defeat() when the death animation takes over —
    // must leave the root transform with no leftover shake offset.
    public void ResetState()
    {
        if (_hurtRoutine != null)
        {
            StopCoroutine(_hurtRoutine);
            _hurtRoutine = null;
        }
        ClearShakeOffset();
        _hasSwappedCharacter = false;
    }

    private void ClearShakeOffset()
    {
        if (_appliedShake == Vector3.zero) return;
        transform.position -= _appliedShake;
        _appliedShake = Vector3.zero;
    }

    // Called by Enemy.TakeDamage on a non-lethal hit (currentHealth > 0).
    public void OnHurt()
    {
        if (_enemy == null) return;
        // Defensive: TakeDamage already early-returns when dying, but guard here
        // too so a future caller can't start hurt feedback on a dying enemy.
        if (_enemy.IsDying) return;
        EnemyDataSO data = _enemy.Data;
        if (data == null || !data.useHurtFeedback) return;

        // If a hurt routine is already in flight, the second hit is fully
        // discarded — no additional pause, shake, or swap plays. This keeps
        // timing stable when an enemy is hit twice in rapid succession.
        if (_hurtRoutine != null) return;

        if (data.hurtSwapsCharacter
            && !_hasSwappedCharacter
            && data.postHurtCharacter != null)
        {
            _enemy.GlyphBadge?.PlaySwap(data.postHurtCharacter);
            _enemy.AssignCharacter(data.postHurtCharacter);
            _hasSwappedCharacter = true;
        }

        _hurtRoutine = StartCoroutine(PlayHurt(data));
    }

    private IEnumerator PlayHurt(EnemyDataSO data)
    {
        bool pause = data.hurtPausesMovement && data.hurtPauseDuration > 0f;
        bool shake = data.hurtShakesSprite
                     && data.hurtShakeDuration > 0f
                     && data.hurtShakeMagnitude > 0f;
        bool anim = data.hurtFrames != null
                    && data.hurtFrames.Length > 0
                    && _renderer != null;

        float pauseDur = pause ? data.hurtPauseDuration : 0f;
        float shakeDur = shake ? data.hurtShakeDuration : 0f;
        float animFps = data.hurtAnimationFps > 0f ? data.hurtAnimationFps : 8f;
        float animFrameDur = anim ? (1f / animFps) : 0f;
        float animTotalDur = anim ? (data.hurtFrames.Length * animFrameDur) : 0f;
        float totalDur = Mathf.Max(pauseDur, shakeDur, animTotalDur);
        // When hurt frames exist, keep movement frozen until the shield-break
        // animation completes even if hurtPauseDuration is shorter.
        float resumeDur = Mathf.Max(pauseDur, animTotalDur);

        if (pause) _mover.Stop();

        _appliedShake = Vector3.zero;
        int animFrameIndex = -1;
        bool resumed = !pause;
        float t = 0f;

        while (t < totalDur)
        {
            if (shake)
            {
                // Subtract last frame's offset so the mover's contribution is
                // preserved on the root transform. Then add this frame's offset.
                transform.position -= _appliedShake;

                if (t < shakeDur)
                {
                    float angle = t * data.hurtShakeFrequency * Mathf.PI * 2f;
                    float decay = 1f - Mathf.Clamp01(t / shakeDur);
                    Vector3 next = new Vector3(
                        Mathf.Sin(angle) * data.hurtShakeMagnitude * decay,
                        Mathf.Cos(angle * 1.7f) * data.hurtShakeMagnitude * decay,
                        0f);
                    transform.position += next;
                    _appliedShake = next;
                }
                else
                {
                    _appliedShake = Vector3.zero;
                }
            }

            if (anim && t < animTotalDur)
            {
                int wantIdx = Mathf.Min(
                    (int)(t / animFrameDur),
                    data.hurtFrames.Length - 1);
                if (wantIdx != animFrameIndex)
                {
                    animFrameIndex = wantIdx;
                    Sprite frame = data.hurtFrames[animFrameIndex];
                    if (frame != null) _renderer.sprite = frame;
                }
            }

            if (!resumed && t >= resumeDur)
            {
                // Pause window has elapsed — re-apply EffectiveSpeed (which
                // includes any aura buffs and Focus Mode) to the mover.
                // Skip if the enemy entered the death-animation path mid-pause:
                // Defeat() already cancelled this routine via ResetState, but
                // this guard makes the contract explicit at the resume site.
                if (_enemy != null && !_enemy.IsDying)
                    _mover.SetSpeed(_enemy.EffectiveSpeed);
                resumed = true;
            }

            yield return null;
            t += Time.deltaTime;
        }

        // Cleanup: remove any leftover shake offset from the root transform.
        ClearShakeOffset();

        // Belt-and-suspenders: if the loop exited before the resume branch ran,
        // make sure movement resumes (unless the enemy is dying, in which case
        // we must leave the mover stopped for the death animation).
        if (!resumed && _enemy != null && !_enemy.IsDying)
            _mover.SetSpeed(_enemy.EffectiveSpeed);

        _hurtRoutine = null;
    }
}
