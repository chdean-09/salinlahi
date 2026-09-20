using UnityEngine;

/// <summary>
/// Scene-scoped bridge between the authored restoration definition and runtime combat/UI systems.
/// Attempt state lives in <see cref="RestorationObjectiveState"/> and is reset whenever a level is
/// configured, so this component does not become a campaign-wide manager or persistence owner.
/// </summary>
[DisallowMultipleComponent]
public sealed class RestorationObjectiveController : MonoBehaviour
{
    private readonly RestorationObjectiveState _state = new RestorationObjectiveState();
    private LevelConfigSO _level;
    private bool _usesLegacyFallback;

    public static RestorationObjectiveController Active { get; private set; }
    public RestorationObjectiveState State => _state;
    public LevelConfigSO Level => _level;
    public bool IsConfigured => _level != null;
    public bool UsesLegacyFallback => _usesLegacyFallback;
    public bool IsComplete => _state.IsComplete;

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        if (Active == this)
            Active = null;
    }

    public void Configure(LevelConfigSO level)
    {
        _level = level;
        if (level == null)
        {
            _usesLegacyFallback = false;
            _state.Configure(null);
            return;
        }

        if (level.restorationObjective != null && level.restorationObjective.HasTargets)
        {
            _usesLegacyFallback = false;
            _state.Configure(level.restorationObjective);
        }
        else
        {
            _usesLegacyFallback = true;
            _state.ConfigureFromFocusWords(level.focusWords);
        }
    }

    public void ResetAttempt()
    {
        _state.Reset();
    }

    public RestorationProgressResult TryRestore(string symbolStableId)
    {
        return TryRestore(symbolStableId, null);
    }

    public RestorationProgressResult TryRestore(string symbolStableId, string spokenValueId)
    {
        return _state.TryRestore(symbolStableId, spokenValueId);
    }

    public bool IsOccurrenceRestored(string occurrenceId)
        => _state.IsOccurrenceRestored(occurrenceId);
}
