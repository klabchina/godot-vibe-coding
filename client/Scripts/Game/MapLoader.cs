using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using Game.Data;
using Game.Ecs;
using Game.Ecs.Core;
using Game.Ecs.Components;

namespace Game;

public static class MapLoader
{
    private static readonly List<MapConfig> _maps = new();
    private static readonly string[] MapFiles = { "plain", "mountain", "grassland" };

    public static void LoadAll()
    {
        _maps.Clear();
        foreach (var name in MapFiles)
        {
            var file = FileAccess.Open($"res://Data/Maps/{name}.json", FileAccess.ModeFlags.Read);
            if (file == null) continue;
            var json = file.GetAsText();
            file.Close();
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

    public static void ApplyBackground(MapConfig map, Node2D sceneRoot)
    {
        var bg = sceneRoot.GetNodeOrNull<ColorRect>("Background");
        if (bg != null)
            bg.Visible = false;

        var bgSprite = new Sprite2D();
        bgSprite.Texture = GD.Load<Texture2D>(map.Background);
        bgSprite.Centered = false;
        bgSprite.Name = "BackgroundSprite";
        sceneRoot.AddChild(bgSprite);
        sceneRoot.MoveChild(bgSprite, 0);
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
        }
    }
}
