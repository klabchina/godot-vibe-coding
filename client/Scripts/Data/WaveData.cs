namespace Game.Data;

public static class WaveData
{
    public record SpawnEntry(MonsterType Type, int Count);

    public const float WaveIntervalSec = 5.0f;
    public const float SpawnIntervalMin = 0.3f;
    public const float SpawnIntervalMax = 1.5f;
}
