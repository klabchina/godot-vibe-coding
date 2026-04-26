using System;
using System.Collections.Generic;
using System.Text.Json;
#if GODOT
using Godot;
#endif
using Game.Data;
using Game.Ecs;
using Game.Ecs.Core;
using Game.Ecs.Components;

namespace Game;

public static class StageLoader
{
    private static StageConfig _stage;

#if GODOT
    public static void Load(string name)
    {
        var file = FileAccess.Open($"res://Data/Stages/{name}.json", FileAccess.ModeFlags.Read);
        if (file == null)
            throw new InvalidOperationException($"Failed to load stage: res://Data/Stages/{name}.json");
        var json = file.GetAsText();
        file.Close();
        _stage = JsonSerializer.Deserialize<StageConfig>(json);
    }
#else
    public static void Load(string name)
    {
        string[] searchPaths = {
            $"Data/Stages/{name}.json",
            $"../client/Data/Stages/{name}.json",
            $"../../client/Data/Stages/{name}.json",
            $"../../../client/Data/Stages/{name}.json"
        };

        string? foundPath = null;
        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                foundPath = path;
                break;
            }
        }

        if (foundPath == null)
        {
            Console.WriteLine($"[StageLoader] Stage file not found: {name}");
            _stage = CreateDefaultStage();
            return;
        }

        try
        {
            var json = File.ReadAllText(foundPath);
            _stage = JsonSerializer.Deserialize<StageConfig>(json) ?? CreateDefaultStage();
            Console.WriteLine($"[StageLoader] Loaded stage from: {foundPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StageLoader] Failed to load stage {name}: {ex.Message}");
            _stage = CreateDefaultStage();
        }
    }

    private static StageConfig CreateDefaultStage()
    {
        var stage = new StageConfig { Id = 1 };

        stage.Waves.Add(new WaveEntry
        {
            Monsters = new List<MonsterSpawnEntry> { new() { Type = "Slime", Count = 5 } }
        });
        stage.Waves.Add(new WaveEntry
        {
            Monsters = new List<MonsterSpawnEntry>
            {
                new() { Type = "Slime", Count = 8 },
                new() { Type = "Skeleton", Count = 2 }
            }
        });
        stage.Waves.Add(new WaveEntry
        {
            Monsters = new List<MonsterSpawnEntry>
            {
                new() { Type = "Slime", Count = 3 },
                new() { Type = "Skeleton", Count = 5 },
                new() { Type = "Orc", Count = 2 }
            }
        });
        stage.Waves.Add(new WaveEntry
        {
            Monsters = new List<MonsterSpawnEntry>
            {
                new() { Type = "Skeleton", Count = 6 },
                new() { Type = "Elite", Count = 3 }
            }
        });
        stage.Waves.Add(new WaveEntry
        {
            Monsters = new List<MonsterSpawnEntry>
            {
                new() { Type = "Elite", Count = 2 },
                new() { Type = "Skeleton", Count = 5 }
            }
        });
        stage.Waves.Add(new WaveEntry
        {
            Monsters = new List<MonsterSpawnEntry>
            {
                new() { Type = "Skeleton", Count = 5 },
                new() { Type = "Orc", Count = 4 },
                new() { Type = "Elite", Count = 1 }
            }
        });
        stage.Waves.Add(new WaveEntry
        {
            Monsters = new List<MonsterSpawnEntry>
            {
                new() { Type = "Orc", Count = 5 },
                new() { Type = "Elite", Count = 3 }
            }
        });
        stage.Waves.Add(new WaveEntry
        {
            Monsters = new List<MonsterSpawnEntry>
            {
                new() { Type = "Boss", Count = 1 },
                new() { Type = "Skeleton", Count = 8 },
                new() { Type = "Elite", Count = 2 }
            }
        });

        Console.WriteLine($"[StageLoader] Using default stage with {stage.Waves.Count} waves.");
        return stage;
    }
#endif

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
