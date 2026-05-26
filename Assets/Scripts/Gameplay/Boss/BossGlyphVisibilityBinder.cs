using UnityEngine;

[RequireComponent(typeof(Enemy))]
public class BossGlyphVisibilityBinder : MonoBehaviour
{
    private Enemy _enemy;
    private EnemyGlyphBadge _badge;
    private BossController _boss;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _badge = _enemy != null ? _enemy.GlyphBadge : null;
    }

    private void OnEnable()
    {
        EventBus.OnBossStarted += HandleBossStarted;
        EventBus.OnBossVulnerabilityWindowActive += HandleVulnerabilityActive;
        EventBus.OnBossVulnerabilityExpired += HandleVulnerabilityExpired;
        EventBus.OnBossDamaged += HandleBossDamaged;
        EventBus.OnBossDefeated += HandleBossDefeated;
        EventBus.OnDrawingFailed += HandleDrawingFailed;
        if (_badge != null) _badge.Hide();
    }

    private void OnDisable()
    {
        EventBus.OnBossStarted -= HandleBossStarted;
        EventBus.OnBossVulnerabilityWindowActive -= HandleVulnerabilityActive;
        EventBus.OnBossVulnerabilityExpired -= HandleVulnerabilityExpired;
        EventBus.OnBossDamaged -= HandleBossDamaged;
        EventBus.OnBossDefeated -= HandleBossDefeated;
        EventBus.OnDrawingFailed -= HandleDrawingFailed;
        UnsubscribeFromBossInstance();
    }

    private void HandleBossStarted(BossConfigSO _)
    {
        UnsubscribeFromBossInstance();
        _boss = GameManager.Instance != null ? GameManager.Instance.CurrentBoss : null;
        if (_boss == null) return;
        _boss.OnDrawnThisPhaseChanged += HandleDrawnThisPhaseChanged;
    }

    private void HandleVulnerabilityActive(int phaseIndex)
    {
        if (_boss == null || _badge == null) return;
        _badge.SetCharacter(_boss.CurrentExpectedCharacter);
        _badge.Show();
    }

    private void HandleVulnerabilityExpired(int phaseIndex)
    {
        if (_badge != null) _badge.Hide();
    }

    private void HandleBossDamaged(int phaseIndex, int hpRemaining)
    {
        if (_badge == null) return;
        // Let the terminal final-draw routine finish on its own. It self-hides
        // by disabling the renderer in its last frame. Calling Hide() here would
        // StopCoroutine on _finalDrawRoutine and cancel the seal-broken animation.
        if (_badge.IsPlayingFinalDraw) return;
        _badge.Hide();
    }

    private void HandleBossDefeated()
    {
        if (_badge != null) _badge.Hide();
        UnsubscribeFromBossInstance();
    }

    private void HandleDrawnThisPhaseChanged()
    {
        if (_boss == null || _badge == null) return;
        // BossController raises this when the vulnerability window initializes
        // the first expected glyph (before any player draw). Treat that as the
        // initial show, not a swap result — HandleVulnerabilityActive already
        // handles the initial Show + SetCharacter on the same frame.
        if (_boss.CorrectDrawsThisWindow <= 0) return;
        bool terminal = _boss.CorrectDrawsThisWindow >= _boss.RequiredCharactersForCurrentPhase;
        if (terminal)
            _badge.PlayFinalDraw();
        else
            _badge.PlaySwap(_boss.CurrentExpectedCharacter);
    }

    private void HandleDrawingFailed()
    {
        if (_badge == null || _boss == null) return;
        // Suppress fail flash outside the active vulnerability window — otherwise
        // a recognizer failure during summon/winddown/damage would briefly reveal
        // the hidden boss glyph because FailFlashRoutine writes alpha-1 color.
        if (!_boss.IsTargetable) return;
        _badge.PlayFailFlash();
    }

    private void UnsubscribeFromBossInstance()
    {
        if (_boss != null)
            _boss.OnDrawnThisPhaseChanged -= HandleDrawnThisPhaseChanged;
        _boss = null;
    }
}
