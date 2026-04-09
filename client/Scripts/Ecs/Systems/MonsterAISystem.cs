using Godot;
using Game.Ecs.Components;

namespace Game.Ecs.Systems;

/// <summary>
/// Basic monster AI: all monsters chase the nearest player.
/// Phase 4 will add Skeleton dodge, Orc charge, and Boss phase behaviors.
/// </summary>
public class MonsterAISystem : GameSystem
{
    public override void Update(float delta)
    {
        var monsters = World.GetEntitiesWith<MonsterComponent, TransformComponent, VelocityComponent>();
        var players = World.GetEntitiesWith<PlayerComponent, TransformComponent>();

        if (players.Count == 0) return;

        foreach (var monster in monsters)
        {
            var monsterTransform = monster.Get<TransformComponent>();
            var velocity = monster.Get<VelocityComponent>();

            // Find nearest player
            float nearestDist = float.MaxValue;
            Vector2 nearestPos = Vector2.Zero;

            foreach (var player in players)
            {
                var playerHealth = player.Get<HealthComponent>();
                if (playerHealth != null && playerHealth.Hp <= 0) continue;

                var playerTransform = player.Get<TransformComponent>();
                float dist = monsterTransform.Position.DistanceTo(playerTransform.Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestPos = playerTransform.Position;
                }
            }

            if (nearestDist < float.MaxValue)
            {
                Vector2 direction = (nearestPos - monsterTransform.Position).Normalized();
                velocity.Velocity = direction * velocity.Speed;
            }
            else
            {
                velocity.Velocity = Vector2.Zero;
            }
        }
    }
}
