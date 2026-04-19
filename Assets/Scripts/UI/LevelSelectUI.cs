using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level Select UI driven by a serialized list of LevelSlot entries.
/// Each slot pairs a LevelConfigSO with a LevelButton.
/// Delegates all display logic to LevelButton.Setup().
/// </summary>
public class LevelSelectUI : MonoBehaviour
{
    [Serializable]
    public struct LevelSlot
    {
        [Tooltip("The LevelConfigSO for this slot. Null = slot is hidden/disabled.")]
        public LevelConfigSO config;
        [Tooltip("The LevelButton prefab instance in the scene.")]
        public LevelButton button;
    }

    [Header("Level Slots")]
    [SerializeField] private List<LevelSlot> _levelSlots = new();

    [Header("Navigation")]
    [SerializeField] private Button _backButton;

    private void Start()
    {
        RefreshLevelButtons();

        if (_backButton != null)
            _backButton.onClick.AddListener(OnBackPressed);

        DebugLogger.Log("LevelSelectUI: Initialized");
    }

    private void OnDestroy()
    {
        if (_backButton != null)
            _backButton.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// Refreshes all level buttons based on current progress.
    /// Public so other systems can trigger a refresh after progress changes.
    /// </summary>
    public void RefreshLevelButtons()
    {
        for (int i = 0; i < _levelSlots.Count; i++)
        {
            LevelSlot slot = _levelSlots[i];

            if (slot.button == null) continue;

            if (slot.config == null)
            {
                slot.button.gameObject.SetActive(false);
                continue;
            }

            int levelNumber = slot.config.levelNumber;
            bool unlocked = true;
            bool completed = false;

            if (ProgressManager.Instance != null)
            {
                unlocked = ProgressManager.Instance.IsLevelUnlocked(levelNumber);
                completed = ProgressManager.Instance.IsLevelCompleted(levelNumber);
            }
            else
            {
                DebugLogger.LogWarning(
                    "LevelSelectUI: ProgressManager not available. Defaulting all levels to unlocked.");
            }

            slot.button.gameObject.SetActive(true);
            slot.button.Setup(slot.config, unlocked, completed);
        }
    }

    private void OnBackPressed()
    {
        DebugLogger.Log("LevelSelectUI: Back to main menu");

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadMainMenu();
        else
            DebugLogger.LogError("LevelSelectUI: SceneLoader not available. Cannot load MainMenu.");
    }
}