using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Game.Data;
using Game.Ecs;
using Game.Ecs.Components;

namespace Game;

/// <summary>
/// 服务端版 StageLoader，使用 System.IO 读取 JSON（不依赖 Godot FileAccess）。
/// 对应客户端的 client/Scripts/Game/StageLoader.cs。
/// </summary>
public static class StageLoader
{
    private static StageConfig? _stage;

    public static void Load(string name)
    {
        // 相对路径：从 pure_ecs_test/ 运行时定位到 client/Data/Stages/
        var basePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "client", "Data", "Stages");
        var filePath = Path.GetFullPath(Path.Combine(basePath, $"{name}.json"));

        if (!File.Exists(filePath))
            throw new InvalidOperationException($"Failed to load stage: {filePath}");

        var json = File.ReadAllText(filePath);
        _stage = JsonSerializer.Deserialize<StageConfig>(json);
    }

    public static StageConfig? GetStage() => _stage;

    public static int GetTotalWaves() => _stage?.Waves.Count ?? 0;

    public static WaveData.SpawnEntry[] GetWaveSpawnEntries(int waveIndex)
    {
        if (_stage == null || waveIndex < 0 || waveIndex >= _stage.Waves.Count)
            return Array.Empty<WaveData.SpawnEntry>();

        var wave = _stage.Waves[waveIndex];
        var entries = new List<WaveData.SpawnEntry>();
        foreach (var m in wave.Monsters)
        {
            if (Enum.TryParse<MonsterType>(m.Type, out var monsterType))
            {
                entries.Add(new WaveData.SpawnEntry(monsterType, m.Count));
            }
        }
        return entries.ToArray();
    }
}
