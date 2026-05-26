using System;
using System.Collections.Generic;

// Static event bus. All cross-system communication goes through here.
// Subscribe in OnEnable. Unsubscribe in OnDisable. No exceptions.
public static class EventBus
{
    // -- Enemy Events --
    public static event Action<BaybayinCharacterSO> OnEnemyDefeated;
    public static event Action<int> OnBaseHit;

    // -- Game State Events --
    public static event Action OnGameOver;
    public static event Action OnLevelComplete;
    public static event Action<int> OnWaveStarted; // int = wave index
    public static event Action<int> OnWaveCleared; // int = wave index

    // -- Recognition Events --
    public static event Action<string> OnCharacterRecognized; // string = characterID
    public static event Action<RecognitionResult, bool, float> OnRecognitionResolved; // result, passed threshold, threshold
    public static event Action OnDrawingFailed;
    public static event Action OnDrawingStarted;

    // -- UI Events --
    public static event Action<int> OnHeartsChanged; // int = current hearts

    // -- Combat Events --
    public static event Action<Enemy> OnEnemyTargeted;
    public static event Action<Enemy> OnSingleAttackHit;
    public static event Action OnDrawingMissed;
    public static event Action<int> OnAOETriggered; // int = number of enemies mass-defeated
    public static event Action<IReadOnlyList<Enemy>> OnChainAttackHit;
    public static event Action<Enemy> OnChainAttackStep;

    // -- Combo Events --
    public static event Action<int> OnComboChanged; // int = current streak

    // -- Focus Mode Events --
    public static event Action OnFocusModeActivated;
    public static event Action OnFocusModeDeactivated;

// -- Pause Events --
    public static event Action OnGamePaused;
    public static event Action OnGameResumed;

    // -- Boss Events --
    public static event Action<BossConfigSO> OnBossStarted;
    public static event Action<int> OnBossPhaseStarted;        // phase index (0-based) — entered SummoningPhase sub-state
    public static event Action<int> OnBossExhausted;             // phase index — boss enters WindingDown
    public static event Action<int> OnBossVulnerable;            // phase index — boss enters Vulnerable (collapse begins)
    public static event Action<int> OnBossVulnerabilityWindowActive; // phase index — collapse finished, boss is targetable and the vulnerability timer starts
    public static event Action<int> OnBossVulnerabilityExpired;  // phase index — vulnerability timer expired with < N correct draws
    public static event Action<int, int> OnBossDamaged;          // phase index, HP remaining after the hit
    public static event Action OnBossDefeated;

    // -- Dialogue Events --
    public static event Action OnDialogueStarted;
    public static event Action OnDialogueComplete;

    // -- Environment Events --
    public static event Action<EraThemeSO> OnThemeApplied; // raised by EnvironmentThemeSwapper after ApplyTheme

    // -- Raisers --
    public static void RaiseEnemyDefeated(BaybayinCharacterSO c) => OnEnemyDefeated?.Invoke(c);
    public static void RaiseBaseHit(int damage = 1) => OnBaseHit?.Invoke(damage);
    public static void RaiseGameOver() => OnGameOver?.Invoke();
    public static void RaiseLevelComplete() => OnLevelComplete?.Invoke();
    public static void RaiseWaveStarted(int index) => OnWaveStarted?.Invoke(index);
    public static void RaiseWaveCleared(int index) => OnWaveCleared?.Invoke(index);
    public static void RaiseCharacterRecognized(string id) => OnCharacterRecognized?.Invoke(id);
    public static void RaiseRecognitionResolved(RecognitionResult result, bool passedThreshold, float threshold)
        => OnRecognitionResolved?.Invoke(result, passedThreshold, threshold);
    public static void RaiseDrawingFailed() => OnDrawingFailed?.Invoke();
    public static void RaiseDrawingStarted() => OnDrawingStarted?.Invoke();
    public static void RaiseHeartsChanged(int hearts) => OnHeartsChanged?.Invoke(hearts);
    public static void RaiseEnemyTargeted(Enemy e) => OnEnemyTargeted?.Invoke(e);
    public static void RaiseSingleAttackHit(Enemy enemy) => OnSingleAttackHit?.Invoke(enemy);
    public static void RaiseDrawingMissed() => OnDrawingMissed?.Invoke();
    public static void RaiseAOETriggered(int defeatedCount) => OnAOETriggered?.Invoke(defeatedCount);
    public static void RaiseChainAttackHit(IReadOnlyList<Enemy> enemies) => OnChainAttackHit?.Invoke(enemies);
    public static void RaiseChainAttackStep(Enemy enemy) => OnChainAttackStep?.Invoke(enemy);
    public static void RaiseComboChanged(int streak) => OnComboChanged?.Invoke(streak);
    public static void RaiseFocusModeActivated() => OnFocusModeActivated?.Invoke();
    public static void RaiseFocusModeDeactivated() => OnFocusModeDeactivated?.Invoke();
    public static void RaiseGamePaused() => OnGamePaused?.Invoke();
    public static void RaiseGameResumed() => OnGameResumed?.Invoke();
    public static void RaiseBossStarted(BossConfigSO config) => OnBossStarted?.Invoke(config);
    public static void RaiseBossPhaseStarted(int phaseIndex) => OnBossPhaseStarted?.Invoke(phaseIndex);
    public static void RaiseBossExhausted(int phaseIndex) => OnBossExhausted?.Invoke(phaseIndex);
    public static void RaiseBossVulnerable(int phaseIndex) => OnBossVulnerable?.Invoke(phaseIndex);
    public static void RaiseBossVulnerabilityWindowActive(int phaseIndex) => OnBossVulnerabilityWindowActive?.Invoke(phaseIndex);
    public static void RaiseBossVulnerabilityExpired(int phaseIndex) => OnBossVulnerabilityExpired?.Invoke(phaseIndex);
    public static void RaiseBossDamaged(int phaseIndex, int hpRemaining) => OnBossDamaged?.Invoke(phaseIndex, hpRemaining);
    public static void RaiseBossDefeated() => OnBossDefeated?.Invoke();
    public static void RaiseDialogueStarted() => OnDialogueStarted?.Invoke();
    public static void RaiseDialogueComplete() => OnDialogueComplete?.Invoke();
    public static void RaiseThemeApplied(EraThemeSO theme) => OnThemeApplied?.Invoke(theme);
}
