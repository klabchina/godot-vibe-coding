using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using Game.Data;
using Game.Ecs;
using Game.Ecs.Core;
using Game.Ecs.Components;

namespace Game;

public static class StageLoader
{
    private static StageConfig _stage;

    public static void Load(string name)
    {
        var file = FileAccess.Open($"res://Data/Stages/{name}.json", FileAccess.ModeFlags.Read);
        if (file == null)
            throw new InvalidOperationException($"Failed to load stage: res://Data/Stages/{name}.json");
        var json = file.GetAsText();
        file.Close();
        _stage = JsonSerializer.Deserialize<StageConfig>(json);
    }

    public static StageConfig GetStage() => _stage;

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
