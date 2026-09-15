using NUnit.Framework;
using UnityEngine;

public class EnemyLessonLookupTests
{
    private static EnemyDataSO Enemy(string id)
    {
        var d = ScriptableObject.CreateInstance<EnemyDataSO>();
        d.enemyID = id;
        d.displayName = id;
        return d;
    }

    [Test]
    public void Find_ReturnsLessonMatchingTheEnemy()
    {
        EnemyDataSO abo = Enemy("abo-ng-simula");
        var lesson = ScriptableObject.CreateInstance<EnemyLessonSO>();
        lesson.enemy = abo;

        var config = ScriptableObject.CreateInstance<LevelConfigSO>();
        config.enemyLessons = new[] { lesson };

        Assert.AreSame(lesson, EnemyLessonLookup.Find(config, abo));
    }

    [Test]
    public void Find_ReturnsNullForAnEnemyWithNoLesson()
    {
        EnemyDataSO abo = Enemy("abo-ng-simula");
        EnemyDataSO iligaw = Enemy("iligaw");
        var lesson = ScriptableObject.CreateInstance<EnemyLessonSO>();
        lesson.enemy = abo;

        var config = ScriptableObject.CreateInstance<LevelConfigSO>();
        config.enemyLessons = new[] { lesson };

        Assert.IsNull(EnemyLessonLookup.Find(config, iligaw));
    }

    [Test]
    public void Find_MatchesByEnemyIDWhenTheReferenceDiffers()
    {
        // A pooled shell can carry a different EnemyDataSO instance with the same id after a
        // domain reload; identity must not be reference-only.
        var lesson = ScriptableObject.CreateInstance<EnemyLessonSO>();
        lesson.enemy = Enemy("abo-ng-simula");

        var config = ScriptableObject.CreateInstance<LevelConfigSO>();
        config.enemyLessons = new[] { lesson };

        Assert.AreSame(lesson, EnemyLessonLookup.Find(config, Enemy("abo-ng-simula")));
    }

    [Test]
    public void Find_ToleratesNullsEverywhere()
    {
        Assert.IsNull(EnemyLessonLookup.Find(null, Enemy("abo-ng-simula")));

        var config = ScriptableObject.CreateInstance<LevelConfigSO>();
        config.enemyLessons = new EnemyLessonSO[] { null };
        Assert.IsNull(EnemyLessonLookup.Find(config, Enemy("abo-ng-simula")));
        Assert.IsNull(EnemyLessonLookup.Find(config, null));
    }
}
