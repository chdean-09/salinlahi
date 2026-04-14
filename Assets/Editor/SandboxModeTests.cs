using NUnit.Framework;
using Salinlahi.Debug.Sandbox;
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
        bool gameOverRaised = false;
        EventBus.OnGameOver += MarkGameOverRaised;

        heartSystem.LoseHeart();

        EventBus.OnGameOver -= MarkGameOverRaised;
        Assert.AreEqual(heartSystem.GetMaxHearts(), heartSystem.GetCurrentHearts());
        Assert.IsFalse(gameOverRaised);
        Object.DestroyImmediate(heartObject);

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
        Enemy enemy = enemyObject.AddComponent<Enemy>();

        enemy.Initialize(enemyData, new NoopEnemyPool(), overrideCharacter);

        Assert.AreSame(overrideCharacter, enemy.Character);
        Assert.AreSame(assetCharacter, enemyData.assignedCharacter);

        Object.DestroyImmediate(enemyObject);
        Object.DestroyImmediate(enemyData);
        Object.DestroyImmediate(assetCharacter);
        Object.DestroyImmediate(overrideCharacter);
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
}
