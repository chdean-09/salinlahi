using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class WaveCurveExpanderTests
{
    private static WaveCurveShape Ugat() => new WaveCurveShape
    {
        WaveCount = 5,
        OpeningEnemyCount = 2, OpeningSpawnInterval = 6f, OpeningWaveStartDelay = 3f,
        RampFirstEnemyCount = 4, RampLastEnemyCount = 7,
        RampFirstSpawnInterval = 5f, RampLastSpawnInterval = 3.5f, RampWaveStartDelay = 2f,
    };

    private static BaybayinCharacterSO Glyph(string id)
    {
        var c = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
        c.name = id;
        return c;
    }

    private static EnemyDataSO Enemy(string id)
    {
        var e = ScriptableObject.CreateInstance<EnemyDataSO>();
        e.name = id;
        return e;
    }

    [Test]
    public void Expand_UgatShape_ReproducesLevelOnePacing()
    {
        List<WaveDefinition> waves = WaveCurveExpander.Expand(Ugat(),
            new[] { Glyph("a") }, new[] { Enemy("iligaw") });

        Assert.AreEqual(5, waves.Count);
        CollectionAssert.AreEqual(new[] { 2, 4, 5, 6, 7 }, waves.ConvertAll(w => w.enemyCount));
        Assert.AreEqual(6f, waves[0].spawnInterval, 1e-4f);
        Assert.AreEqual(5f, waves[1].spawnInterval, 1e-4f);
        Assert.AreEqual(4.5f, waves[2].spawnInterval, 1e-4f);
        Assert.AreEqual(4f, waves[3].spawnInterval, 1e-4f);
        Assert.AreEqual(3.5f, waves[4].spawnInterval, 1e-4f);
        Assert.AreEqual(3f, waves[0].waveStartDelay, 1e-4f);
        for (int i = 1; i < 5; i++)
            Assert.AreEqual(2f, waves[i].waveStartDelay, 1e-4f, $"wave {i + 1} delay");
        Assert.IsTrue(waves.TrueForAll(w => !w.isIntermissionWave));
    }

    [Test]
    public void Expand_TwoWaves_SecondTakesRampLastValues()
    {
        WaveCurveShape shape = Ugat();
        shape.WaveCount = 2;

        List<WaveDefinition> waves = WaveCurveExpander.Expand(shape,
            new BaybayinCharacterSO[0], new EnemyDataSO[0]);

        Assert.AreEqual(2, waves.Count);
        Assert.AreEqual(2, waves[0].enemyCount);
        Assert.AreEqual(7, waves[1].enemyCount);
        Assert.AreEqual(3.5f, waves[1].spawnInterval, 1e-4f);
        Assert.AreEqual(2f, waves[1].waveStartDelay, 1e-4f);
    }

    [Test]
    public void Expand_OneWave_IsOpeningOnly()
    {
        WaveCurveShape shape = Ugat();
        shape.WaveCount = 1;

        List<WaveDefinition> waves = WaveCurveExpander.Expand(shape,
            new BaybayinCharacterSO[0], new EnemyDataSO[0]);

        Assert.AreEqual(1, waves.Count);
        Assert.AreEqual(2, waves[0].enemyCount);
        Assert.AreEqual(6f, waves[0].spawnInterval, 1e-4f);
        Assert.AreEqual(3f, waves[0].waveStartDelay, 1e-4f);
    }

    [Test]
    public void Expand_ZeroOrNegativeWaveCount_IsEmpty()
    {
        WaveCurveShape shape = Ugat();
        shape.WaveCount = 0;
        Assert.IsEmpty(WaveCurveExpander.Expand(shape, new BaybayinCharacterSO[0], new EnemyDataSO[0]));
        shape.WaveCount = -3;
        Assert.IsEmpty(WaveCurveExpander.Expand(shape, new BaybayinCharacterSO[0], new EnemyDataSO[0]));
    }

    [Test]
    public void EnemyCountAt_RoundsHalfAwayFromZero()
    {
        // Ramp 1 -> 2 over 3 ramp waves: t = 0, 0.5, 1 -> 1, 1.5, 2 -> 1, 2, 2.
        var shape = new WaveCurveShape
        {
            WaveCount = 4, OpeningEnemyCount = 9,
            RampFirstEnemyCount = 1, RampLastEnemyCount = 2,
        };

        Assert.AreEqual(9, WaveCurveExpander.EnemyCountAt(shape, 0));
        Assert.AreEqual(1, WaveCurveExpander.EnemyCountAt(shape, 1));
        Assert.AreEqual(2, WaveCurveExpander.EnemyCountAt(shape, 2));
        Assert.AreEqual(2, WaveCurveExpander.EnemyCountAt(shape, 3));
    }

    [Test]
    public void SpawnIntervalAt_InterpolatesLinearly()
    {
        var shape = new WaveCurveShape
        {
            WaveCount = 4, OpeningSpawnInterval = 9f,
            RampFirstSpawnInterval = 4f, RampLastSpawnInterval = 2f,
        };

        Assert.AreEqual(9f, WaveCurveExpander.SpawnIntervalAt(shape, 0), 1e-4f);
        Assert.AreEqual(4f, WaveCurveExpander.SpawnIntervalAt(shape, 1), 1e-4f);
        Assert.AreEqual(3f, WaveCurveExpander.SpawnIntervalAt(shape, 2), 1e-4f);
        Assert.AreEqual(2f, WaveCurveExpander.SpawnIntervalAt(shape, 3), 1e-4f);
    }

    [Test]
    public void Expand_EveryWave_CarriesTheFullRosterWithNullsDropped()
    {
        BaybayinCharacterSO a = Glyph("a"), ma = Glyph("ma");
        EnemyDataSO iligaw = Enemy("iligaw"), mantsa = Enemy("mantsa");

        List<WaveDefinition> waves = WaveCurveExpander.Expand(Ugat(),
            new[] { a, null, ma }, new[] { null, iligaw, mantsa });

        foreach (WaveDefinition wave in waves)
        {
            CollectionAssert.AreEqual(new[] { a, ma }, wave.characters);
            CollectionAssert.AreEqual(new[] { iligaw, mantsa }, wave.enemyTypes);
        }
    }

    [Test]
    public void Expand_RosterLists_AreCopiesNotAliases()
    {
        var roster = new List<BaybayinCharacterSO> { Glyph("a") };
        var enemies = new List<EnemyDataSO> { Enemy("iligaw") };

        List<WaveDefinition> waves = WaveCurveExpander.Expand(Ugat(), roster, enemies);
        waves[0].characters.Clear();
        waves[0].enemyTypes.Clear();

        Assert.AreEqual(1, roster.Count, "clearing a wave must not touch the level roster");
        Assert.AreEqual(1, enemies.Count);
        Assert.AreEqual(1, waves[1].characters.Count, "waves must not share one list");
        Assert.AreNotSame(waves[0].characters, waves[1].characters);
    }

    [Test]
    public void Expand_NullRosters_YieldEmptyListsNotNull()
    {
        List<WaveDefinition> waves = WaveCurveExpander.Expand(Ugat(), null, null);

        Assert.AreEqual(5, waves.Count);
        Assert.IsNotNull(waves[0].characters);
        Assert.IsNotNull(waves[0].enemyTypes);
        Assert.IsEmpty(waves[0].characters);
    }
}
