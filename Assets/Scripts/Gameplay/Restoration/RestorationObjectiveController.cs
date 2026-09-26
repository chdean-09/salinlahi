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
    private SpawnAssignmentCoordinator _assignmentCoordinator;

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
        _assignmentCoordinator = null;
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
        return _state.TryRestore(symbolStableId, spokenValueId, CanRestoreOccurrence);
    }

    public bool CanRestoreOccurrence(string occurrenceId)
    {
        if (_assignmentCoordinator == null || _assignmentCoordinator.Level != _level)
        {
            _assignmentCoordinator = null;
            SpawnAssignmentCoordinator[] candidates =
                FindObjectsByType<SpawnAssignmentCoordinator>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < candidates.Length; index++)
            {
                if (candidates[index].Level != _level)
                    continue;

                _assignmentCoordinator = candidates[index];
                break;
            }
        }

        return _assignmentCoordinator == null
            || !_assignmentCoordinator.IsActive
            || _assignmentCoordinator.CanRestoreOccurrence(occurrenceId);
    }

    public bool IsOccurrenceRestored(string occurrenceId)
        => _state.IsOccurrenceRestored(occurrenceId);
}
