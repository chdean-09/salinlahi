using NUnit.Framework;
using Salinlahi.Debug.Sandbox;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Pool;

public class SandboxModeTests
{
    [TearDown]
    public void TearDown()
    {
        SandboxMode.Deactivate();
        SandboxMode.SetAvailabilityOverrideForTests(null);
    }

    [Test]
    public void AvailabilityTruthTableRequiresEditorOrSandboxDefine()
    {
        Assert.IsFalse(SandboxMode.IsAvailableForSymbols(false, false));
        Assert.IsTrue(SandboxMode.IsAvailableForSymbols(true, false));
        Assert.IsTrue(SandboxMode.IsAvailableForSymbols(false, true));
        Assert.IsTrue(SandboxMode.IsAvailableForSymbols(true, true));
    }

    [Test]
    public void TryActivateReturnsFalseWhenSandboxUnavailable()
    {
        SandboxMode.SetAvailabilityOverrideForTests(false);

        Assert.IsFalse(SandboxMode.TryActivate());
        Assert.IsFalse(SandboxMode.IsActive);
    }

    [Test]
    public void TryActivateEnablesSandboxWhenAvailable()
    {
        SandboxMode.SetAvailabilityOverrideForTests(true);

        Assert.IsTrue(SandboxMode.TryActivate());
        Assert.IsTrue(SandboxMode.IsActive);
        Assert.IsTrue(SandboxMode.ShouldBypassLifeLoss);
    }

    [Test]
    public void HeartSystemDoesNotLoseHeartsInSandbox()
    {
        SandboxMode.SetAvailabilityOverrideForTests(true);
        SandboxMode.TryActivate();
        GameObject heartObject = new("HeartSystem");
        HeartSystem heartSystem = heartObject.AddComponent<HeartSystem>();
        InvokePrivate(heartSystem, "Awake");
        bool gameOverRaised = false;
        EventBus.OnGameOver += MarkGameOverRaised;

        try
        {
            heartSystem.LoseHeart();

            Assert.AreEqual(heartSystem.GetMaxHearts(), heartSystem.GetCurrentHearts());
            Assert.IsFalse(gameOverRaised);
        }
        finally
        {
            EventBus.OnGameOver -= MarkGameOverRaised;
            Object.DestroyImmediate(heartObject);
        }

        void MarkGameOverRaised()
        {
            gameOverRaised = true;
        }
    }

    [Test]
    public void HeartSystemLosesHeartsWhenSandboxInactive()
    {
        SandboxMode.SetAvailabilityOverrideForTests(true);
        SandboxMode.Deactivate();
        GameObject heartObject = new("HeartSystem");
        HeartSystem heartSystem = heartObject.AddComponent<HeartSystem>();
        InvokePrivate(heartSystem, "Awake");

        heartSystem.LoseHeart();

        Assert.AreEqual(heartSystem.GetMaxHearts() - 1, heartSystem.GetCurrentHearts());
        Object.DestroyImmediate(heartObject);
    }

    [Test]
    public void EnemyCharacterOverrideDoesNotMutateEnemyData()
    {
        EnemyDataSO enemyData = ScriptableObject.CreateInstance<EnemyDataSO>();
        BaybayinCharacterSO assetCharacter = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
        BaybayinCharacterSO overrideCharacter = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
        enemyData.enemyID = "test";
        enemyData.maxHealth = 1;
        enemyData.moveSpeed = 1f;
        enemyData.assignedCharacter = assetCharacter;

        GameObject enemyObject = new("Enemy");
        enemyObject.AddComponent<BoxCollider2D>();
        Enemy enemy = enemyObject.AddComponent<Enemy>();

        try
        {
            enemy.Initialize(enemyData, new NoopEnemyPool(), overrideCharacter);

            Assert.AreSame(overrideCharacter, enemy.Character);
            Assert.AreSame(assetCharacter, enemyData.assignedCharacter);
        }
        finally
        {
            Object.DestroyImmediate(enemyObject);
            Object.DestroyImmediate(enemyData);
            Object.DestroyImmediate(assetCharacter);
            Object.DestroyImmediate(overrideCharacter);
        }
    }

    [Test]
    public void EnemyMoverUsesSandboxMovementSpeedScale()
    {
        SandboxMode.SetAvailabilityOverrideForTests(true);
        SandboxMode.TryActivate();
        SandboxMode.SetMovementSpeedScale(0.5f);

        GameObject enemyObject = new("Enemy");
        enemyObject.AddComponent<BoxCollider2D>();
        EnemyMover mover = enemyObject.AddComponent<EnemyMover>();

        try
        {
            mover.SetSpeed(4f);

            Assert.AreEqual(2f, mover.GetFinalSpeedForTests());
        }
        finally
        {
            Object.DestroyImmediate(enemyObject);
        }
    }

    [Test]
    public void EnemyMoverStopsWhenSandboxMovementIsPaused()
    {
        SandboxMode.SetAvailabilityOverrideForTests(true);
        SandboxMode.TryActivate();
        SandboxMode.SetMovementPaused(true);

        GameObject enemyObject = new("Enemy");
        enemyObject.AddComponent<BoxCollider2D>();
        EnemyMover mover = enemyObject.AddComponent<EnemyMover>();

        try
        {
            mover.SetSpeed(4f);

            Assert.AreEqual(0f, mover.GetFinalSpeedForTests());
        }
        finally
        {
            Object.DestroyImmediate(enemyObject);
        }
    }

    [Test]
    public void WaveSpawnerSpawnWaveStartsAfterSpawnOffset()
    {
        GameObject poolObject = new("EnemyPool");
        GameObject spawnerObject = new("WaveSpawner");
        GameObject prefabObject = new("EnemyPrefab");
        GameObject leftSpawn = new("LeftSpawn");
        GameObject rightSpawn = new("RightSpawn");
        EnemyDataSO enemyData = ScriptableObject.CreateInstance<EnemyDataSO>();
        BaybayinCharacterSO character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
        WaveConfigSO wave = ScriptableObject.CreateInstance<WaveConfigSO>();

        try
        {
            poolObject.SetActive(false);
            Enemy prefab = CreateEnemyPrefab(prefabObject);
            EnemyPool pool = poolObject.AddComponent<EnemyPool>();
            SetPrivateField(pool, "_enemyPrefab", prefab);
            SetPrivateField(pool, "_defaultCapacity", 0);
            SetPrivateField(pool, "_maxSize", 4);
            poolObject.SetActive(true);
            InvokePrivate(pool, "Awake");

            leftSpawn.transform.position = new Vector3(-1f, 3f, 0f);
            rightSpawn.transform.position = new Vector3(1f, 3f, 0f);
            WaveSpawner spawner = spawnerObject.AddComponent<WaveSpawner>();
            SetPrivateField(spawner, "_spawnPoints", new[] { leftSpawn.transform, rightSpawn.transform });

            enemyData.enemyID = "offset-test";
            enemyData.maxHealth = 1;
            enemyData.moveSpeed = 1f;
            enemyData.assignedCharacter = character;
            wave.enemyCount = 3;
            wave.spawnInterval = 0f;
            wave.enemyTypesInWave = new System.Collections.Generic.List<EnemyDataSO> { enemyData };
            wave.charactersInWave = new System.Collections.Generic.List<BaybayinCharacterSO> { character };

            int spawnedCallbacks = 0;
            IEnumerator routine = spawner.SpawnWave(wave, () => spawnedCallbacks++, 2);
            while (routine.MoveNext()) { }

            Assert.AreEqual(1, spawnedCallbacks);
        }
        finally
        {
            Object.DestroyImmediate(poolObject);
            Object.DestroyImmediate(spawnerObject);
            Object.DestroyImmediate(prefabObject);
            Object.DestroyImmediate(leftSpawn);
            Object.DestroyImmediate(rightSpawn);
            Object.DestroyImmediate(enemyData);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(wave);
            ClearSingletonInstance<EnemyPool>();
        }
    }

    [Test]
    public void PausedRunSnapshotRemainsAvailableUntilExplicitlyDiscarded()
    {
        GameObject managerObject = new("GameManager");
        GameManager gameManager = managerObject.AddComponent<GameManager>();

        try
        {
            gameManager.CachePausedRunSnapshot(3, 2, 1, 4);

            Assert.IsTrue(gameManager.TryGetPausedRunLevelId(out int pausedLevelId));
            Assert.AreEqual(3, pausedLevelId);
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
            ClearSingletonInstance<GameManager>();
        }
    }

    [Test]
    public void DiscardPausedRunSnapshotClearsResumeState()
    {
        GameObject managerObject = new("GameManager");
        GameManager gameManager = managerObject.AddComponent<GameManager>();

        try
        {
            gameManager.CachePausedRunSnapshot(3, 2, 1, 4);

            gameManager.DiscardPausedRunSnapshot();

            Assert.IsFalse(gameManager.TryGetPausedRunLevelId(out _));
            Assert.IsFalse(gameManager.TryConsumePausedRunHearts(3, 3, out _));
            Assert.IsFalse(gameManager.TryGetPausedRunEnemies(3, out _));
            Assert.IsFalse(gameManager.TryGetPausedRunWaveProgress(3, out _, out _));
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
            ClearSingletonInstance<GameManager>();
        }
    }

    [Test]
    public void PauseMenuDoesNotCachePausedRunSnapshotInSandbox()
    {
        SandboxMode.SetAvailabilityOverrideForTests(true);
        SandboxMode.TryActivate();

        Assert.IsFalse(PauseMenuUI.ShouldCachePausedRunSnapshot());
    }

    [Test]
    public void PauseMenuCachesPausedRunSnapshotOutsideSandbox()
    {
        SandboxMode.SetAvailabilityOverrideForTests(true);
        SandboxMode.Deactivate();

        Assert.IsTrue(PauseMenuUI.ShouldCachePausedRunSnapshot());
    }

    [Test]
    public void CharacterSelectionReturnsSelectedCharacterInSpecificMode()
    {
        BaybayinCharacterSO first = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
        BaybayinCharacterSO second = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
        BaybayinCharacterSO[] characters = { first, second };

        BaybayinCharacterSO selected = SandboxCharacterSelection.ResolveCharacter(false, characters, 1);

        Assert.AreSame(second, selected);
        Object.DestroyImmediate(first);
        Object.DestroyImmediate(second);
    }

    [Test]
    public void CharacterSelectionReturnsConfiguredCharacterInRandomMode()
    {
        BaybayinCharacterSO first = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
        BaybayinCharacterSO second = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
        BaybayinCharacterSO[] characters = { first, second };

        BaybayinCharacterSO selected = SandboxCharacterSelection.ResolveCharacter(true, characters, 0);

        Assert.Contains(selected, characters);
        Object.DestroyImmediate(first);
        Object.DestroyImmediate(second);
    }

    [Test]
    public void CharacterSelectionReturnsNullForEmptyList()
    {
        BaybayinCharacterSO selected = SandboxCharacterSelection.ResolveCharacter(
            true,
            System.Array.Empty<BaybayinCharacterSO>(),
            0);

        Assert.IsNull(selected);
    }

    [Test]
    public void WaveManagerStartsSandboxWithoutLevelConfig()
    {
        SandboxMode.SetAvailabilityOverrideForTests(true);
        SandboxMode.TryActivate();

        GameObject managerObject = new("WaveManager");
        WaveManager waveManager = managerObject.AddComponent<WaveManager>();

        try
        {
            Assert.DoesNotThrow(() => InvokePrivate(waveManager, "StartLevel", 1));
            Assert.IsNull(WaveManager.CurrentAllowedCharacters);
            Assert.IsNotNull(Object.FindFirstObjectByType<SandboxController>());
        }
        finally
        {
            Object.DestroyImmediate(Object.FindFirstObjectByType<SandboxController>()?.gameObject);
            Object.DestroyImmediate(managerObject);
        }
    }

    [Test]
    public void SandboxCatalogSuppliesAllowedCharactersForKempeiScramble()
    {
        SandboxMode.SetAvailabilityOverrideForTests(true);
        SandboxMode.TryActivate();

        GameObject managerObject = new("WaveManager");
        WaveManager waveManager = managerObject.AddComponent<WaveManager>();
        LevelConfigSO levelConfig = ScriptableObject.CreateInstance<LevelConfigSO>();
        BaybayinCharacterSO firstCharacter = CreateCharacter("A");
        BaybayinCharacterSO secondCharacter = CreateCharacter("B");
        levelConfig.allowedCharacters = new List<BaybayinCharacterSO> { firstCharacter, secondCharacter };

        try
        {
            SetPrivateField(waveManager, "_levelConfigs", new[] { levelConfig });
            InvokePrivate(waveManager, "StartLevel", 1);

            Assert.IsNotNull(WaveManager.CurrentAllowedCharacters);
            Assert.Contains(firstCharacter, (System.Collections.ICollection)WaveManager.CurrentAllowedCharacters);
            Assert.Contains(secondCharacter, (System.Collections.ICollection)WaveManager.CurrentAllowedCharacters);
        }
        finally
        {
            SetCurrentAllowedCharacters(null);
            Object.DestroyImmediate(Object.FindFirstObjectByType<SandboxController>()?.gameObject);
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(levelConfig);
            Object.DestroyImmediate(firstCharacter);
            Object.DestroyImmediate(secondCharacter);
        }
    }

    [Test]
    public void SandboxCharacterRegistryExposesAllCharacters()
    {
        SandboxMode.SetAvailabilityOverrideForTests(true);
        SandboxMode.TryActivate();

        GameObject managerObject = new("WaveManager");
        WaveManager waveManager = managerObject.AddComponent<WaveManager>();
        CharacterRegistrySO registry = ScriptableObject.CreateInstance<CharacterRegistrySO>();
        LevelConfigSO levelConfig = ScriptableObject.CreateInstance<LevelConfigSO>();
        BaybayinCharacterSO levelCharacter = CreateCharacter("A");
        BaybayinCharacterSO registryOnlyCharacter = CreateCharacter("B");
        registry.All = new List<BaybayinCharacterSO> { levelCharacter, registryOnlyCharacter };
        levelConfig.allowedCharacters = new List<BaybayinCharacterSO> { levelCharacter };

        try
        {
            SetPrivateField(waveManager, "_sandboxCharacterRegistry", registry);
            SetPrivateField(waveManager, "_levelConfig", levelConfig);

            IReadOnlyList<BaybayinCharacterSO> characters = waveManager.GetConfiguredCharactersForSandbox();

            Assert.Contains(levelCharacter, (System.Collections.ICollection)characters);
            Assert.Contains(registryOnlyCharacter, (System.Collections.ICollection)characters);
            Assert.Contains(registryOnlyCharacter, (System.Collections.ICollection)WaveManager.CurrentAllowedCharacters);
        }
        finally
        {
            SetCurrentAllowedCharacters(null);
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(registry);
            Object.DestroyImmediate(levelConfig);
            Object.DestroyImmediate(levelCharacter);
            Object.DestroyImmediate(registryOnlyCharacter);
        }
    }

    [Test]
    public void SandboxEnemyRegistrySuppliesRuntimeEnemyData()
    {
        GameObject managerObject = new("WaveManager");
        WaveManager waveManager = managerObject.AddComponent<WaveManager>();
        EnemyDataSO registeredEnemy = CreateEnemyData("kempei", null);

        try
        {
            SetPrivateField(waveManager, "_sandboxEnemyData", new List<EnemyDataSO> { registeredEnemy });

            IReadOnlyList<EnemyDataSO> enemies = waveManager.GetConfiguredEnemyTypesForSandbox();

            Assert.Contains(registeredEnemy, (System.Collections.ICollection)enemies);
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(registeredEnemy);
        }
    }

    [Test]
    public void KempeiKeepsScrambledCharacterStableWhileTargetRemainsAffected()
    {
        GameObject trackerObject = new("ActiveEnemyTracker");
        ActiveEnemyTracker tracker = trackerObject.AddComponent<ActiveEnemyTracker>();
        BaybayinCharacterSO realCharacter = CreateCharacter("A");
        BaybayinCharacterSO firstScramble = CreateCharacter("B");
        BaybayinCharacterSO secondScramble = CreateCharacter("C");
        EnemyDataSO kempeiData = CreateEnemyData("kempei", realCharacter);
        EnemyDataSO targetData = CreateEnemyData("target", realCharacter);
        GameObject kempeiObject = CreateEnemyObject("Kempei", out Enemy kempei);
        GameObject targetObject = CreateEnemyObject("Target", out Enemy target);
        KempeiScrambleController scrambleController = kempeiObject.AddComponent<KempeiScrambleController>();

        try
        {
            kempeiData.scrambleRadius = 10f;
            kempeiObject.transform.position = Vector3.zero;
            targetObject.transform.position = Vector3.right;
            kempei.Initialize(kempeiData);
            target.Initialize(targetData);

            SetCurrentAllowedCharacters(new List<BaybayinCharacterSO> { realCharacter, firstScramble });
            InvokePrivate(scrambleController, "Update");

            Assert.AreSame(firstScramble, target.VisualCharacter);

            SetCurrentAllowedCharacters(new List<BaybayinCharacterSO> { realCharacter, secondScramble });
            InvokePrivate(scrambleController, "Update");

            Assert.AreSame(firstScramble, target.VisualCharacter);
            Assert.AreEqual(2, tracker.ActiveCount);
        }
        finally
        {
            SetCurrentAllowedCharacters(null);
            Object.DestroyImmediate(kempeiObject);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(trackerObject);
            Object.DestroyImmediate(kempeiData);
            Object.DestroyImmediate(targetData);
            Object.DestroyImmediate(realCharacter);
            Object.DestroyImmediate(firstScramble);
            Object.DestroyImmediate(secondScramble);
            ClearSingletonInstance<ActiveEnemyTracker>();
        }
    }

    private sealed class NoopEnemyPool : IObjectPool<Enemy>
    {
        public int CountInactive => 0;
        public Enemy Get() => null;
        public UnityEngine.Pool.PooledObject<Enemy> Get(out Enemy v)
        {
            v = null;
            return default;
        }
        public void Release(Enemy element) { }
        public void Clear() { }
    }

    private static Enemy CreateEnemyPrefab(GameObject prefabObject)
    {
        prefabObject.SetActive(false);
        prefabObject.AddComponent<BoxCollider2D>();
        prefabObject.AddComponent<EnemyMover>();
        return prefabObject.AddComponent<Enemy>();
    }

    private static GameObject CreateEnemyObject(string name, out Enemy enemy)
    {
        GameObject enemyObject = new(name);
        enemyObject.AddComponent<BoxCollider2D>();
        enemyObject.AddComponent<EnemyMover>();
        enemy = enemyObject.AddComponent<Enemy>();
        return enemyObject;
    }

    private static EnemyDataSO CreateEnemyData(string enemyID, BaybayinCharacterSO character)
    {
        EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
        data.enemyID = enemyID;
        data.maxHealth = 1;
        data.moveSpeed = 1f;
        data.assignedCharacter = character;
        return data;
    }

    private static BaybayinCharacterSO CreateCharacter(string characterID)
    {
        BaybayinCharacterSO character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
        character.characterID = characterID;
        return character;
    }

    private static void SetCurrentAllowedCharacters(IReadOnlyList<BaybayinCharacterSO> characters)
    {
        PropertyInfo property = typeof(WaveManager).GetProperty(
            nameof(WaveManager.CurrentAllowedCharacters),
            BindingFlags.Static | BindingFlags.Public);
        MethodInfo setter = property?.GetSetMethod(true);
        Assert.IsNotNull(setter, "Missing WaveManager.CurrentAllowedCharacters setter.");
        setter.Invoke(null, new object[] { characters });
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private static void ClearSingletonInstance<T>() where T : MonoBehaviour
    {
        FieldInfo instanceField = typeof(Singleton<T>).GetField(
            "<Instance>k__BackingField",
            BindingFlags.Static | BindingFlags.NonPublic);
        instanceField?.SetValue(null, null);
    }

    private static object InvokePrivate(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}.");
        return method.Invoke(target, args);
    }
}
