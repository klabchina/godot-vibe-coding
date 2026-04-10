using Godot;
using System.Collections.Generic;
using Game.Ecs;
using Game.Ecs.Core;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.ClientSystems;

/// <summary>
/// Client-only: syncs ECS state to Godot visual nodes.
/// </summary>
public class RenderSystem : GameSystem
{
    public Node2D RenderRoot { get; set; }

    private readonly Dictionary<int, Node2D> _entityNodes = new();
    private readonly Dictionary<int, List<Node2D>> _orbitNodes = new();

    /// <summary>Helper: convert Vec2 → Godot.Vector2</summary>
    private static Vector2 ToGodot(Vec2 v) => new(v.X, v.Y);

    public override void Update(float delta)
    {
        if (RenderRoot == null)
            return;

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

            node.Position = ToGodot(transform.Position);

            if (entity.Has<ArrowComponent>() || entity.Has<MonsterProjectileComponent>())
                node.Rotation = transform.Rotation;

            UpdateEffectVisuals(entity, node);
        }

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

        RenderOrbitArrows();
    }

    private void UpdateEffectVisuals(Entity entity, Node2D node)
    {
        if (!entity.Has<MonsterComponent>()) return;

        var effect = entity.Get<EffectComponent>();
        if (effect == null) return;

        var rect = node.GetChildOrNull<ColorRect>(0);
        if (rect == null) return;

        var monster = entity.Get<MonsterComponent>();
        Color baseColor = GetMonsterColor(monster.Type);

        if (effect.IsFrozen && effect.IsBurning)
            rect.Color = new Color(0.5f, 0.3f, 0.8f);
        else if (effect.IsFrozen)
            rect.Color = new Color(0.4f, 0.7f, 1.0f);
        else if (effect.IsBurning)
            rect.Color = new Color(1.0f, 0.5f, 0.1f);
        else
            rect.Color = baseColor;
    }

    private void RenderOrbitArrows()
    {
        var players = World.GetEntitiesWith<PlayerComponent, TransformComponent, OrbitComponent>();
        var activePlayerIds = new HashSet<int>();

        foreach (var player in players)
        {
            activePlayerIds.Add(player.Id);
            var orbit = player.Get<OrbitComponent>();
            var playerTransform = player.Get<TransformComponent>();

            if (!_orbitNodes.ContainsKey(player.Id))
                _orbitNodes[player.Id] = new List<Node2D>();

            var nodes = _orbitNodes[player.Id];

            while (nodes.Count < orbit.Count)
            {
                var orbitVisual = new Node2D();
                var rect = new ColorRect();
                rect.Color = Colors.Cyan;
                rect.Size = new Vector2(12, 6);
                rect.Position = new Vector2(-6, -3);
                orbitVisual.AddChild(rect);
                RenderRoot.AddChild(orbitVisual);
                nodes.Add(orbitVisual);
            }

            while (nodes.Count > orbit.Count)
            {
                var last = nodes[^1];
                last.QueueFree();
                nodes.RemoveAt(nodes.Count - 1);
            }

            if (orbit.Count > 0)
            {
                float angleStep = 360f / orbit.Count;
                for (int i = 0; i < orbit.Count; i++)
                {
                    float angle = GMath.DegToRad(orbit.CurrentAngle + angleStep * i);
                    var pos = playerTransform.Position + new Vec2(
                        GMath.Cos(angle) * UpgradeData.OrbitRadius,
                        GMath.Sin(angle) * UpgradeData.OrbitRadius
                    );
                    nodes[i].Position = ToGodot(pos);
                    nodes[i].Rotation = angle;
                }
            }
        }

        var toRemove = new List<int>();
        foreach (var (playerId, nodes) in _orbitNodes)
        {
            if (!activePlayerIds.Contains(playerId))
            {
                foreach (var n in nodes) n.QueueFree();
                toRemove.Add(playerId);
            }
        }
        foreach (var id in toRemove)
            _orbitNodes.Remove(id);
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
            var arrowComp = entity.Get<ArrowComponent>();
            if (arrowComp.Freezing)
                rect.Color = new Color(0.4f, 0.8f, 1.0f);
            else if (arrowComp.Burning)
                rect.Color = new Color(1.0f, 0.5f, 0.0f);
            else if (arrowComp.Explosive)
                rect.Color = new Color(1.0f, 0.2f, 0.2f);
            else
                rect.Color = Colors.Yellow;
            rect.Size = new Vector2(8, 4);
            rect.Position = new Vector2(-4, -2);
        }
        else if (entity.Has<MonsterProjectileComponent>())
        {
            rect = new ColorRect();
            rect.Color = new Color(1.0f, 0.3f, 0.1f); // orange-red, distinct from yellow player arrows
            rect.Size = new Vector2(8, 8);
            rect.Position = new Vector2(-4, -4);
        }
        else if (entity.Has<PickupComponent>())
        {
            var pickup = entity.Get<PickupComponent>();
            rect = new ColorRect();
            rect.Color = GetPickupColor(pickup.Type);
            float size = pickup.Type == PickupType.ExpOrb ? 10 : 14;
            rect.Size = new Vector2(size, size);
            rect.Position = new Vector2(-size / 2, -size / 2);
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

    private static Color GetPickupColor(PickupType type)
    {
        return type switch
        {
            PickupType.ExpOrb => Colors.LimeGreen,
            PickupType.HealthPotion => new Color(1.0f, 0.2f, 0.4f),
            PickupType.Frenzy => new Color(1.0f, 0.8f, 0.0f),
            PickupType.Invincible => Colors.White,
            PickupType.Bomb => new Color(1.0f, 0.0f, 0.0f),
            _ => Colors.White,
        };
    }
}
