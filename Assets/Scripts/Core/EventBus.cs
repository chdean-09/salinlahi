using System;

// Static event bus. All cross-system communication goes through here.
// Subscribe in OnEnable. Unsubscribe in OnDisable. No exceptions.
public static class EventBus
{
    // ── Enemy Events ─────────────────────────────────────────────
    public static event Action<BaybayinCharacterSO> OnEnemyDefeated;
    public static event Action OnBaseHit;

    // ── Game State Events ────────────────────────────────────────
    public static event Action OnGameOver;
    public static event Action OnLevelComplete;
    public static event Action<int> OnWaveStarted; // int = wave index

    // ── Recognition Events ───────────────────────────────────────
    public static event Action<string> OnCharacterRecognized; // string = characterID
    public static event Action OnDrawingFailed;
    public static event Action OnDrawingStarted;

    // ── UI Events ────────────────────────────────────────────────
    public static event Action<int> OnHeartsChanged; // int = current hearts

    // ── Raisers ──────────────────────────────────────────────────
    public static void RaiseEnemyDefeated(BaybayinCharacterSO c) => OnEnemyDefeated?.Invoke(c);
    public static void RaiseBaseHit() => OnBaseHit?.Invoke();
    public static void RaiseGameOver() => OnGameOver?.Invoke();
    public static void RaiseLevelComplete() => OnLevelComplete?.Invoke();
    public static void RaiseWaveStarted(int index) => OnWaveStarted?.Invoke(index);
    public static void RaiseCharacterRecognized(string id) => OnCharacterRecognized?.Invoke(id);
    public static void RaiseDrawingFailed() => OnDrawingFailed?.Invoke();
    public static void RaiseDrawingStarted() => OnDrawingStarted?.Invoke();
    public static void RaiseHeartsChanged(int hearts) => OnHeartsChanged?.Invoke(hearts);
}