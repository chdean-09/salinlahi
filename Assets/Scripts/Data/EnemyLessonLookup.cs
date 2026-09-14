/// <summary>
/// Finds the <see cref="EnemyLessonSO"/> a level authored for an enemy, if any. Pure and
/// allocation-free so the introduction beat can call it on every spawn without a scene.
/// </summary>
public static class EnemyLessonLookup
{
    public static EnemyLessonSO Find(LevelConfigSO config, EnemyDataSO data)
    {
        if (config?.enemyLessons == null || data == null)
            return null;

        for (int i = 0; i < config.enemyLessons.Length; i++)
        {
            EnemyLessonSO lesson = config.enemyLessons[i];
            if (lesson == null || lesson.enemy == null)
                continue;

            if (lesson.enemy == data)
                return lesson;

            // Identity by id as well as reference: a pooled shell can carry a different
            // EnemyDataSO instance with the same enemyID across a domain reload.
            if (!string.IsNullOrEmpty(lesson.enemy.enemyID)
                && string.Equals(lesson.enemy.enemyID, data.enemyID,
                    System.StringComparison.OrdinalIgnoreCase))
                return lesson;
        }

        return null;
    }
}
