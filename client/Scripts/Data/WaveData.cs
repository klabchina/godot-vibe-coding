namespace Game.Data;

public static class WaveData
{
    public record SpawnEntry(MonsterType Type, int Count);

    public static readonly SpawnEntry[][] Waves =
    {
        new[] { new SpawnEntry(MonsterType.Slime, 10) },
        new[] { new SpawnEntry(MonsterType.Slime, 8),  new SpawnEntry(MonsterType.Skeleton, 4) },
        new[] { new SpawnEntry(MonsterType.Slime, 5),  new SpawnEntry(MonsterType.Skeleton, 8), new SpawnEntry(MonsterType.Orc, 2) },
        new[] { new SpawnEntry(MonsterType.Skeleton, 8), new SpawnEntry(MonsterType.Orc, 4), new SpawnEntry(MonsterType.Elite, 1) },
        new[] { new SpawnEntry(MonsterType.Skeleton, 6), new SpawnEntry(MonsterType.Orc, 6), new SpawnEntry(MonsterType.Elite, 3) },
        new[] { new SpawnEntry(MonsterType.Orc, 8), new SpawnEntry(MonsterType.Elite, 5), new SpawnEntry(MonsterType.Skeleton, 5) },
        new[] { new SpawnEntry(MonsterType.Orc, 10), new SpawnEntry(MonsterType.Elite, 8), new SpawnEntry(MonsterType.Skeleton, 8) },
        new[] { new SpawnEntry(MonsterType.Boss, 1), new SpawnEntry(MonsterType.Orc, 5), new SpawnEntry(MonsterType.Elite, 3), new SpawnEntry(MonsterType.Slime, 10) },
    };

    public const int   TotalWaves       = 8;
    public const float WaveIntervalSec  = 5.0f;
    public const float SpawnIntervalMin = 0.3f;
    public const float SpawnIntervalMax = 0.5f;
}
