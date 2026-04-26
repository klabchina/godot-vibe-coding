using System;
using System.Collections.Generic;
using System.Text.Json;
using Game.Data;
using Game.Ecs;
using Game.Ecs.Core;
using Game.Ecs.Components;

namespace Game;

/// <summary>
/// 服务端地图加载器（不依赖 Godot，使用 System.IO）
/// </summary>
public static class MapLoader
{
    private static readonly List<MapConfig> _maps = new();
    private static readonly string[] MapFiles = { "plain", "mountain", "grassland" };

    /// <summary>
    /// 加载所有地图配置（从相对于工作目录的 Data/Maps 目录）
    /// </summary>
    public static void LoadAll()
    {
        _maps.Clear();
        foreach (var name in MapFiles)
        {
            string[] searchPaths = {
                $"Data/Maps/{name}.json",
                $"../client/Data/Maps/{name}.json",
                $"../../client/Data/Maps/{name}.json",
                $"../../../client/Data/Maps/{name}.json"
            };

            string foundPath = null;
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
                Console.WriteLine($"[MapLoader] Map file not found for: {name}");
                continue;
            }

            try
            {
                var json = File.ReadAllText(foundPath);
                var config = JsonSerializer.Deserialize<MapConfig>(json);
                if (config != null)
                {
                    _maps.Add(config);
                    Console.WriteLine($"[MapLoader] Loaded map: {config.Id} ({foundPath})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MapLoader] Failed to load map {name}: {ex.Message}");
            }
        }

        Console.WriteLine($"[MapLoader] Loaded {_maps.Count} maps.");
    }

    /// <summary>
    /// 随机选择一个地图
    /// </summary>
    public static MapConfig PickRandom()
    {
        if (_maps.Count == 0)
            throw new InvalidOperationException("No maps loaded. Call LoadAll() first.");
        var index = GameRandom.Next(_maps.Count);
        return _maps[index];
    }

    /// <summary>
    /// 获取指定名称的地图
    /// </summary>
    public static MapConfig? GetMap(string id)
    {
        foreach (var map in _maps)
        {
            if (map.Id == id)
                return map;
        }
        return null;
    }

    /// <summary>
    /// 在 ECS World 中生成障碍物
    /// </summary>
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

            Console.WriteLine($"[MapLoader] Spawned obstacle: {obs.Sprite} at ({obs.X}, {obs.Y}, {obs.W}, {obs.H})");
        }
    }
}
