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
    public static event Action OnDrawingMissed;
    public static event Action<List<BaybayinCharacterSO>> OnAOETriggered;

    // -- Combo Events --
    public static event Action<int> OnComboChanged; // int = current streak

    // -- Focus Mode Events --
    public static event Action OnFocusModeActivated;
    public static event Action OnFocusModeDeactivated;

// -- Pause Events --
    public static event Action OnGamePaused;
    public static event Action OnGameResumed;

    // -- Boss Events --
    public static event Action OnBossDefeated;

    // -- Dialogue Events --
    public static event Action OnDialogueStarted;
    public static event Action OnDialogueComplete;

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
    public static void RaiseDrawingMissed() => OnDrawingMissed?.Invoke();
    public static void RaiseAOETriggered(List<BaybayinCharacterSO> defeated) => OnAOETriggered?.Invoke(defeated);
    public static void RaiseComboChanged(int streak) => OnComboChanged?.Invoke(streak);
    public static void RaiseFocusModeActivated() => OnFocusModeActivated?.Invoke();
    public static void RaiseFocusModeDeactivated() => OnFocusModeDeactivated?.Invoke();
    public static void RaiseGamePaused() => OnGamePaused?.Invoke();
    public static void RaiseGameResumed() => OnGameResumed?.Invoke();
    public static void RaiseBossDefeated() => OnBossDefeated?.Invoke();
    public static void RaiseDialogueStarted() => OnDialogueStarted?.Invoke();
    public static void RaiseDialogueComplete() => OnDialogueComplete?.Invoke();
}
