using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Salinlahi.Debug.Sandbox
{
    public class SandboxController : MonoBehaviour
    {
        private const string CatalogResourcesPath = "Sandbox/SandboxCatalog";

        private readonly List<EnemyDataSO> _enemyTypes = new();
        private readonly List<BaybayinCharacterSO> _characters = new();

        private WaveManager _waveManager;
        private WaveSpawner _spawner;
        private TMP_Text _enemyLabel;
        private TMP_Text _modeLabel;
        private TMP_Text _characterLabel;
        private TMP_Text _movementLabel;
        private TMP_Text _recognitionLabel;
        private TMP_Text _statusLabel;
        private GameObject _panelObject;
        private GameObject _restoreButtonObject;
        private Button _spawnButton;
        private Button _previousEnemyButton;
        private Button _nextEnemyButton;
        private Button _previousCharacterButton;
        private Button _nextCharacterButton;

        private int _selectedEnemyIndex;
        private int _selectedCharacterIndex;
        private bool _useRandomCharacter = true;

        private void OnEnable()
        {
            EventBus.OnRecognitionResolved += HandleRecognitionResolved;
        }

        private void OnDisable()
        {
            EventBus.OnRecognitionResolved -= HandleRecognitionResolved;
        }

        public static void EnsureExists(WaveManager waveManager, WaveSpawner spawner)
        {
            if (!SandboxMode.IsActive)
                return;

            SandboxController existing = FindFirstObjectByType<SandboxController>();
            if (existing != null)
            {
                existing.Initialize(waveManager, spawner);
                return;
            }

            var controllerObject = new GameObject("[Sandbox] Controller");
            var controller = controllerObject.AddComponent<SandboxController>();
            controller.Initialize(waveManager, spawner);
        }

        public void Initialize(WaveManager waveManager, WaveSpawner spawner)
        {
            if (!SandboxMode.IsActive)
            {
                gameObject.SetActive(false);
                return;
            }

            _waveManager = waveManager;
            _spawner = spawner != null ? spawner : FindFirstObjectByType<WaveSpawner>();
            _waveManager?.PauseWaves();

            LoadCatalog();
            BuildUi();
            RefreshUi();
        }

        private void Awake()
        {
            if (!SandboxMode.IsActive)
                gameObject.SetActive(false);
        }

        private void LoadCatalog()
        {
            _enemyTypes.Clear();
            _characters.Clear();

            SandboxCatalogSO catalog = Resources.Load<SandboxCatalogSO>(CatalogResourcesPath);
            if (catalog != null)
            {
                AddEnemies(catalog.EnemyTypes);
                AddCharacters(catalog.Characters);
            }

            if (_waveManager != null)
            {
                AddEnemies(_waveManager.GetConfiguredEnemyTypesForSandbox());
                AddCharacters(_waveManager.GetConfiguredCharactersForSandbox());
            }

            _selectedEnemyIndex = Mathf.Clamp(_selectedEnemyIndex, 0, Mathf.Max(0, _enemyTypes.Count - 1));
            _selectedCharacterIndex = Mathf.Clamp(_selectedCharacterIndex, 0, Mathf.Max(0, _characters.Count - 1));
        }

        private void AddEnemies(IReadOnlyList<EnemyDataSO> enemies)
        {
            if (enemies == null)
                return;

            foreach (EnemyDataSO enemy in enemies)
            {
                if (enemy == null || _enemyTypes.Contains(enemy))
                    continue;

                _enemyTypes.Add(enemy);
            }
        }

        private void AddCharacters(IReadOnlyList<BaybayinCharacterSO> characters)
        {
            if (characters == null)
                return;

            foreach (BaybayinCharacterSO character in characters)
            {
                if (character == null || _characters.Contains(character))
                    continue;

                _characters.Add(character);
            }
        }

        private void BuildUi()
        {
            if (_statusLabel != null)
                return;

            var canvasObject = new GameObject("[Sandbox] Overlay");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            canvasObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            var safeAreaObject = new GameObject("[Sandbox] SafeArea");
            safeAreaObject.transform.SetParent(canvasObject.transform, false);
            safeAreaObject.AddComponent<RectTransform>();
            safeAreaObject.AddComponent<SafeAreaHandler>();

            _panelObject = new GameObject("Panel");
            _panelObject.transform.SetParent(safeAreaObject.transform, false);
            var panelRect = _panelObject.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.0f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -20f);
            panelRect.sizeDelta = Vector2.zero;

            var panelImage = _panelObject.AddComponent<Image>();
            panelImage.color = new Color(0.06f, 0.07f, 0.08f, 0.92f);

            var layout = _panelObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var fitter = _panelObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _statusLabel = CreateLabel(_panelObject.transform, "SANDBOX MODE", 30, 40f);
            CreateLabel(_panelObject.transform, "Manual enemy spawning is isolated from level progress.", 20, 56f);

            _enemyLabel = CreateLabel(_panelObject.transform, "", 24, 34f);
            CreateButtonRow(
                _panelObject.transform,
                out _previousEnemyButton,
                "Previous Enemy",
                SelectPreviousEnemy,
                out _nextEnemyButton,
                "Next Enemy",
                SelectNextEnemy);

            _modeLabel = CreateLabel(_panelObject.transform, "", 24, 34f);
            CreateButton(_panelObject.transform, "Toggle Character Mode", ToggleCharacterMode);

            _characterLabel = CreateLabel(_panelObject.transform, "", 24, 44f);
            CreateButtonRow(
                _panelObject.transform,
                out _previousCharacterButton,
                "Previous Character",
                SelectPreviousCharacter,
                out _nextCharacterButton,
                "Next Character",
                SelectNextCharacter);

            _spawnButton = CreateButton(_panelObject.transform, "Spawn Selected Enemy", SpawnSelectedEnemy);

            _movementLabel = CreateLabel(_panelObject.transform, "", 22, 34f);
            CreateButtonRow(
                _panelObject.transform,
                out _,
                "Slower",
                DecreaseMovementSpeed,
                out _,
                "Faster",
                IncreaseMovementSpeed);
            CreateButtonRow(
                _panelObject.transform,
                out _,
                "Pause/Resume",
                ToggleMovementPause,
                out _,
                "Reset Speed",
                ResetMovementSpeed);

            _recognitionLabel = CreateLabel(
                _panelObject.transform,
                "Recognition: draw a Baybayin character to test.",
                20,
                82f);

            CreateButton(_panelObject.transform, "Hide Sandbox Panel", HidePanel);
            _restoreButtonObject = CreateRestoreButton(safeAreaObject.transform);
            _restoreButtonObject.SetActive(false);

            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        }

        private TMP_Text CreateLabel(Transform parent, string text, int fontSize, float preferredHeight)
        {
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(parent, false);
            var layoutElement = labelObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = preferredHeight;
            layoutElement.preferredHeight = preferredHeight;

            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null)
                return;

            var eventSystemObject = new GameObject("[Sandbox] EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private Button CreateButton(Transform parent, string text, UnityEngine.Events.UnityAction action)
        {
            var buttonObject = new GameObject(text);
            buttonObject.transform.SetParent(parent, false);
            var layoutElement = buttonObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = 56f;
            layoutElement.preferredHeight = 56f;

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.19f, 0.42f, 0.72f, 1f);

            var button = buttonObject.AddComponent<Button>();
            button.onClick.AddListener(action);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 56f);

            var label = CreateLabel(buttonObject.transform, text, 20, 56f);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 6f);
            labelRect.offsetMax = new Vector2(-10f, -6f);
            label.alignment = TextAlignmentOptions.Center;

            return button;
        }

        private GameObject CreateRestoreButton(Transform parent)
        {
            Button button = CreateButton(parent, "Show", ShowPanel);
            GameObject buttonObject = button.gameObject;
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-112f, -20f);
            rect.sizeDelta = new Vector2(92f, 56f);
            return buttonObject;
        }

        private void CreateButtonRow(
            Transform parent,
            out Button firstButton,
            string firstText,
            UnityEngine.Events.UnityAction firstAction,
            out Button secondButton,
            string secondText,
            UnityEngine.Events.UnityAction secondAction)
        {
            var rowObject = new GameObject("ButtonRow");
            rowObject.transform.SetParent(parent, false);
            var layoutElement = rowObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = 56f;
            layoutElement.preferredHeight = 56f;

            var rowLayout = rowObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childForceExpandWidth = true;

            firstButton = CreateButton(rowObject.transform, firstText, firstAction);
            secondButton = CreateButton(rowObject.transform, secondText, secondAction);
        }

        private void RefreshUi()
        {
            bool hasEnemies = _enemyTypes.Count > 0;
            bool hasCharacters = _characters.Count > 0;
            bool canSpawn = SandboxMode.IsActive && _spawner != null && hasEnemies && hasCharacters;

            if (_enemyLabel != null)
            {
                _enemyLabel.text = hasEnemies
                    ? $"Enemy: {GetEnemyDisplayName(_enemyTypes[_selectedEnemyIndex])}"
                    : "Enemy: no enemy data configured";
            }

            if (_modeLabel != null)
            {
                _modeLabel.text = _useRandomCharacter
                    ? "Character mode: Random"
                    : "Character mode: Specific";
            }

            if (_characterLabel != null)
            {
                _characterLabel.text = hasCharacters
                    ? $"Specific character: {GetCharacterDisplayName(_characters[_selectedCharacterIndex])}"
                    : "Specific character: no Baybayin characters configured";
            }

            if (_statusLabel != null)
            {
                _statusLabel.text = canSpawn
                    ? "SANDBOX MODE"
                    : BuildErrorMessage(hasEnemies, hasCharacters);
                _statusLabel.color = canSpawn ? Color.white : new Color(1f, 0.56f, 0.42f, 1f);
            }

            if (_movementLabel != null)
            {
                string pauseText = SandboxMode.IsMovementPaused ? "Paused" : "Moving";
                _movementLabel.text = $"Movement: {pauseText} | Speed x{SandboxMode.MovementSpeedScale:0.00}";
            }

            SetButtonInteractable(_spawnButton, canSpawn);
            SetButtonInteractable(_previousEnemyButton, hasEnemies && _enemyTypes.Count > 1);
            SetButtonInteractable(_nextEnemyButton, hasEnemies && _enemyTypes.Count > 1);
            SetButtonInteractable(_previousCharacterButton, hasCharacters && _characters.Count > 1 && !_useRandomCharacter);
            SetButtonInteractable(_nextCharacterButton, hasCharacters && _characters.Count > 1 && !_useRandomCharacter);
        }

        private void HandleRecognitionResolved(
            RecognitionResult result,
            bool passedThreshold,
            float threshold)
        {
            if (!SandboxMode.IsActive || _recognitionLabel == null)
                return;

            string bestScore = FormatPercent(result.score);
            string secondScore = result.secondBestScore < 0f
                ? "0%"
                : FormatPercent(result.secondBestScore);
            string status = passedThreshold ? "Accepted" : "Rejected";

            _recognitionLabel.text =
                $"Recognition: {status}\n"
                + $"Best: {result.characterID} ({bestScore}) | "
                + $"Close: {result.secondBestID} ({secondScore})\n"
                + $"Threshold: {FormatPercent(threshold)} | Gap: {FormatPercent(result.scoreGap)}";
        }

        private string BuildErrorMessage(bool hasEnemies, bool hasCharacters)
        {
            if (_spawner == null)
                return "SANDBOX ERROR: WaveSpawner was not found.";
            if (!hasEnemies)
                return "SANDBOX ERROR: no EnemyDataSO assets are configured.";
            if (!hasCharacters)
                return "SANDBOX ERROR: no BaybayinCharacterSO assets are configured.";
            return "SANDBOX ERROR: sandbox is unavailable.";
        }

        private void SpawnSelectedEnemy()
        {
            if (!SandboxMode.IsActive || _spawner == null || _enemyTypes.Count == 0 || _characters.Count == 0)
            {
                RefreshUi();
                return;
            }

            EnemyDataSO enemyData = _enemyTypes[_selectedEnemyIndex];
            BaybayinCharacterSO character = SandboxCharacterSelection.ResolveCharacter(
                _useRandomCharacter,
                _characters,
                _selectedCharacterIndex);

            if (enemyData == null || character == null)
            {
                RefreshUi();
                return;
            }

            Enemy spawned = _spawner.SpawnEnemy(enemyData, character);
            if (spawned == null)
            {
                _statusLabel.text = "SANDBOX ERROR: spawn failed. Check EnemyPool and spawn points.";
                _statusLabel.color = new Color(1f, 0.56f, 0.42f, 1f);
                return;
            }

            _statusLabel.text = $"Spawned {GetEnemyDisplayName(enemyData)} with {GetCharacterDisplayName(character)}";
            _statusLabel.color = Color.white;
        }

        private void SelectPreviousEnemy()
        {
            if (_enemyTypes.Count == 0)
                return;

            _selectedEnemyIndex = (_selectedEnemyIndex - 1 + _enemyTypes.Count) % _enemyTypes.Count;
            RefreshUi();
        }

        private void HidePanel()
        {
            if (_panelObject != null)
                _panelObject.SetActive(false);

            if (_restoreButtonObject != null)
                _restoreButtonObject.SetActive(true);
        }

        private void ShowPanel()
        {
            if (_panelObject != null)
                _panelObject.SetActive(true);

            if (_restoreButtonObject != null)
                _restoreButtonObject.SetActive(false);

            RefreshUi();
        }

        private void SelectNextEnemy()
        {
            if (_enemyTypes.Count == 0)
                return;

            _selectedEnemyIndex = (_selectedEnemyIndex + 1) % _enemyTypes.Count;
            RefreshUi();
        }

        private void ToggleCharacterMode()
        {
            _useRandomCharacter = !_useRandomCharacter;
            RefreshUi();
        }

        private void DecreaseMovementSpeed()
        {
            SandboxMode.SetMovementSpeedScale(SandboxMode.MovementSpeedScale - 0.25f);
            RefreshUi();
        }

        private void IncreaseMovementSpeed()
        {
            SandboxMode.SetMovementSpeedScale(SandboxMode.MovementSpeedScale + 0.25f);
            RefreshUi();
        }

        private void ToggleMovementPause()
        {
            SandboxMode.ToggleMovementPaused();
            RefreshUi();
        }

        private void ResetMovementSpeed()
        {
            SandboxMode.SetMovementPaused(false);
            SandboxMode.SetMovementSpeedScale(1f);
            RefreshUi();
        }

        private void SelectPreviousCharacter()
        {
            if (_characters.Count == 0)
                return;

            _selectedCharacterIndex = (_selectedCharacterIndex - 1 + _characters.Count) % _characters.Count;
            RefreshUi();
        }

        private void SelectNextCharacter()
        {
            if (_characters.Count == 0)
                return;

            _selectedCharacterIndex = (_selectedCharacterIndex + 1) % _characters.Count;
            RefreshUi();
        }

        private static void SetButtonInteractable(Button button, bool interactable)
        {
            if (button != null)
                button.interactable = interactable;
        }

        private static string GetEnemyDisplayName(EnemyDataSO enemy)
        {
            if (enemy == null)
                return "<missing>";

            return string.IsNullOrWhiteSpace(enemy.enemyID)
                ? enemy.name
                : enemy.enemyID;
        }

        private static string GetCharacterDisplayName(BaybayinCharacterSO character)
        {
            if (character == null)
                return "<missing>";

            if (!string.IsNullOrWhiteSpace(character.characterID)
                && !string.IsNullOrWhiteSpace(character.syllable))
            {
                return $"{character.characterID} ({character.syllable})";
            }

            return string.IsNullOrWhiteSpace(character.characterID)
                ? character.name
                : character.characterID;
        }

        private static string FormatPercent(float value)
        {
            return $"{Mathf.Clamp01(value) * 100f:0}%";
        }
    }
}
