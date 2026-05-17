using System.Collections;
using UnityEngine;

public class GuidedLevel1TutorialController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WaveManager _waveManager;
    [SerializeField] private TutorialOverlayController _overlayController;

    [Header("Level 1 Guided Encounter")]
    [SerializeField] private int _levelNumber = 1;
    [SerializeField] private int _waveOneIndex = 0;
    [SerializeField] private string _guidedCharacterID = "BA";

    private Enemy _guidedEnemy;
    private bool _guidanceActive;
    private bool _spawnSuspended;
    private Coroutine _completionRoutine;

    private void OnEnable()
    {
        EventBus.OnEnemySpawned += HandleEnemySpawned;
        EventBus.OnEnemyTargeted += HandleEnemyTargeted;
        EventBus.OnBaseHit += HandleBaseHit;
        EventBus.OnWaveCleared += HandleWaveCleared;
        EventBus.OnGameOver += HandleTerminalState;
        EventBus.OnLevelComplete += HandleTerminalState;
    }

    private void OnDisable()
    {
        EventBus.OnEnemySpawned -= HandleEnemySpawned;
        EventBus.OnEnemyTargeted -= HandleEnemyTargeted;
        EventBus.OnBaseHit -= HandleBaseHit;
        EventBus.OnWaveCleared -= HandleWaveCleared;
        EventBus.OnGameOver -= HandleTerminalState;
        EventBus.OnLevelComplete -= HandleTerminalState;

        CleanupGuidedEncounter();
    }

    private bool IsLevelOneActive()
    {
        int selectedLevel = PlayerPrefs.GetInt(ProgressManager.SelectedLevelKey, 1);
        return selectedLevel == _levelNumber;
    }

    private bool ShouldGuideFirstEnemy(Enemy enemy)
    {
        if (enemy == null)
            return false;

        if (!IsLevelOneActive())
            return false;

        if (_waveManager == null
            || _waveManager.CurrentWaveIndex != _waveOneIndex
            || _waveManager.CurrentWaveSpawnedCount != 1)
        {
            return false;
        }

        if (LevelTutorialProgress.HasSeenLevel1FirstEnemyGuided())
            return false;

        BaybayinCharacterSO character = enemy.Character;
        return character != null && character.characterID == _guidedCharacterID;
    }

    private void HandleEnemySpawned(Enemy enemy)
    {
        if (!ShouldGuideFirstEnemy(enemy))
            return;

        _guidedEnemy = enemy;
        _guidanceActive = true;

        SetSpawnSuspended(true);
        FreezeEnemy(enemy);
        _overlayController?.ShowGuidedDraw(enemy);
        LevelTutorialProgress.MarkLevel1FirstEnemyGuided();
    }

    private void HandleEnemyTargeted(Enemy enemy)
    {
        if (!_guidanceActive || enemy == null || enemy != _guidedEnemy)
            return;

        if (_completionRoutine != null)
            StopCoroutine(_completionRoutine);

        _completionRoutine = StartCoroutine(CompleteGuidedEncounterAfterCombatFrame());
    }

    private IEnumerator CompleteGuidedEncounterAfterCombatFrame()
    {
        yield return null;

        if (_guidedEnemy != null
            && _guidedEnemy.gameObject.activeInHierarchy
            && !_guidedEnemy.IsDying
            && _guidedEnemy.CurrentHealth > 0)
        {
            _completionRoutine = null;
            yield break;
        }

        _overlayController?.HideGuidedDraw();
        SetSpawnSuspended(false);
        LevelTutorialProgress.MarkLevel1FirstEnemyDefeated();
        _overlayController?.ShowToast("Good. Draw the mark enemies carry to stop them.");

        _guidanceActive = false;
        _guidedEnemy = null;
        _completionRoutine = null;
    }

    private void HandleBaseHit(int damage)
    {
        if (!IsLevelOneActive())
            return;

        if (LevelTutorialProgress.HasSeenLevel1BaseHpExplained())
            return;

        LevelTutorialProgress.MarkLevel1BaseHpExplained();
        _overlayController?.ShowToast("Each enemy that reaches the Shrine breaks one heart.");
    }

    private void HandleWaveCleared(int waveIndex)
    {
        if (!IsLevelOneActive() || waveIndex != _waveOneIndex)
            return;

        if (!LevelTutorialProgress.HasSeenLevel1BaseHpExplained())
        {
            LevelTutorialProgress.MarkLevel1BaseHpExplained();
            _overlayController?.ShowToast("You protected the Shrine. These hearts show how many hits it can still take.");
        }

        if (!LevelTutorialProgress.HasSeenLevel1Wave1ClearExplained())
        {
            LevelTutorialProgress.MarkLevel1Wave1ClearExplained();
            _overlayController?.ShowToast("Wave cleared. Stop every enemy to finish each wave.");
        }
    }

    private void HandleTerminalState()
    {
        CleanupGuidedEncounter();
    }

    private static void FreezeEnemy(Enemy enemy)
    {
        if (enemy == null)
            return;

        EnemyMover mover = enemy.GetComponent<EnemyMover>();
        if (mover != null)
            mover.Stop();
    }

    private static void ResumeEnemy(Enemy enemy)
    {
        if (enemy == null || enemy.Data == null)
            return;

        EnemyMover mover = enemy.GetComponent<EnemyMover>();
        if (mover != null)
            mover.SetSpeed(enemy.EffectiveSpeed);
    }

    private void SetSpawnSuspended(bool suspended)
    {
        _spawnSuspended = suspended;

        if (_waveManager != null)
            _waveManager.SetTutorialSpawnSuspended(suspended);
    }

    private void CleanupGuidedEncounter()
    {
        if (_completionRoutine != null)
        {
            StopCoroutine(_completionRoutine);
            _completionRoutine = null;
        }

        if (_guidanceActive)
            ResumeEnemy(_guidedEnemy);

        if (_spawnSuspended)
            SetSpawnSuspended(false);

        _overlayController?.HideGuidedDraw();
        _guidanceActive = false;
        _guidedEnemy = null;
    }
}
