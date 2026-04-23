using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Game.Data;
using Game.Ecs;
using Game.Ecs.Components;
using Game.Ecs.Core;

namespace Game;

/// <summary>
/// 服务端版 MapLoader，使用 System.IO 读取 JSON（不依赖 Godot FileAccess）。
/// 对应客户端的 client/Scripts/Game/MapLoader.cs。
/// </summary>
public static class MapLoader
{
    private static readonly List<MapConfig> _maps = new();
    private static readonly string[] MapFiles = { "plain", "mountain", "grassland" };

    public static void LoadAll()
    {
        _maps.Clear();
        foreach (var name in MapFiles)
        {
            var basePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "client", "Data", "Maps");
            var filePath = Path.GetFullPath(Path.Combine(basePath, $"{name}.json"));

            if (!File.Exists(filePath))
                continue;

            var json = File.ReadAllText(filePath);
            var config = JsonSerializer.Deserialize<MapConfig>(json);
            if (config != null)
                _maps.Add(config);
        }
    }

    public static MapConfig PickRandom()
    {
        if (_maps.Count == 0)
            throw new InvalidOperationException("No maps loaded. Call LoadAll() first.");
        return _maps[GameRandom.Next(_maps.Count)];
    }

    public static void SpawnObstacles(MapConfig map, World world)
    {
        foreach (var obs in map.Obstacles)
        {
            var entity = world.CreateEntity();
            entity.Add(new TransformComponent
            {
                Position = new Vec2(obs.X + obs.W / 2f, obs.Y + obs.H / 2f)
            });
            entity.Add(new ColliderComponent
            {
                Shape = ColliderShape.Box,
                HalfWidth = obs.W / 2f,
                HalfHeight = obs.H / 2f,
                Layer = CollisionLayers.Obstacle,
                Mask = 0
            });
            entity.Add(new ObstacleComponent { SpritePath = obs.Sprite });

            Console.WriteLine($"[ECS] Spawned obstacle: {obs.Sprite} at {obs.X}, {obs.Y}, {obs.W}, {obs.H}");
        }
    }
}
