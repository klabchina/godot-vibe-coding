using Godot;
using System.Collections.Generic;
using Game.Ecs;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.Systems;

public class RenderSystem : GameSystem
{
    public Node2D RenderRoot { get; set; }

    private readonly Dictionary<int, Node2D> _entityNodes = new();

    public override void Update(float delta)
    {
        if (RenderRoot == null)
            return;

        // Update or create visuals for alive entities
        foreach (var (id, entity) in World.Entities)
        {
            if (!entity.IsAlive)
                continue;

            var transform = entity.Get<TransformComponent>();
            if (transform == null)
                continue;

            if (!_entityNodes.TryGetValue(id, out var node))
            {
                node = CreateVisualNode(entity);
                if (node == null)
                    continue;

                RenderRoot.AddChild(node);
                _entityNodes[id] = node;
            }

            node.Position = transform.Position;

            var arrow = entity.Get<ArrowComponent>();
            if (arrow != null)
                node.Rotation = transform.Rotation;
        }

        // Clean up nodes for dead or removed entities
        var toRemove = new List<int>();
        foreach (var (id, node) in _entityNodes)
        {
            var entity = World.GetEntity(id);
            if (entity == null || !entity.IsAlive)
            {
                node.QueueFree();
                toRemove.Add(id);
            }
        }

        foreach (var id in toRemove)
            _entityNodes.Remove(id);
    }

    private Node2D CreateVisualNode(Entity entity)
    {
        var wrapper = new Node2D();
        ColorRect rect = null;

        if (entity.Has<PlayerComponent>())
        {
            rect = new ColorRect();
            rect.Color = Colors.DodgerBlue;
            rect.Size = new Vector2(32, 32);
            rect.Position = new Vector2(-16, -16);
        }
        else if (entity.Has<MonsterComponent>())
        {
            var monster = entity.Get<MonsterComponent>();
            var collider = entity.Get<ColliderComponent>();
            float size = collider != null ? collider.Radius * 2 : 24;

            rect = new ColorRect();
            rect.Color = GetMonsterColor(monster.Type);
            rect.Size = new Vector2(size, size);
            rect.Position = new Vector2(-size / 2, -size / 2);
        }
        else if (entity.Has<ArrowComponent>())
        {
            rect = new ColorRect();
            rect.Color = Colors.Yellow;
            rect.Size = new Vector2(8, 4);
            rect.Position = new Vector2(-4, -2);
        }
        else if (entity.Has<PickupComponent>())
        {
            rect = new ColorRect();
            rect.Color = Colors.LimeGreen;
            rect.Size = new Vector2(10, 10);
            rect.Position = new Vector2(-5, -5);
        }
        else
        {
            return null;
        }

        wrapper.AddChild(rect);
        return wrapper;
    }

    private static Color GetMonsterColor(MonsterType type)
    {
        return type switch
        {
            MonsterType.Slime => Colors.Green,
            MonsterType.Skeleton => Colors.Gray,
            MonsterType.Orc => new Color(0.55f, 0.15f, 0.15f),
            MonsterType.Elite => Colors.Purple,
            MonsterType.Boss => Colors.Red,
            _ => Colors.White,
        };
    }
}
