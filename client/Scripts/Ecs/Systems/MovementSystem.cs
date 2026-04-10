using Game.Ecs.Core;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.Systems;

/// <summary>
/// Updates entity positions based on velocity and handles arena boundary rules.
/// - Players are clamped inside the arena.
/// - Player arrows (ArrowComponent) are destroyed outside boundary.
/// - Monster projectiles (MonsterProjectileComponent) apply acceleration (if any) then are destroyed outside boundary.
/// - Monsters roam freely (spawn outside and move in).
/// </summary>
public class MovementSystem : GameSystem
{
    private const float PlayerRadius = 16f;
    private const float ArrowMargin  = 64f;

    public override void Update(float delta)
    {
        var entities = World.GetEntitiesWith<TransformComponent, VelocityComponent>();

        foreach (var entity in entities)
        {
            if (!entity.IsAlive) continue;

            var transform = entity.Get<TransformComponent>();
            var velocity  = entity.Get<VelocityComponent>();

            // 1. Move
            transform.Position += velocity.Velocity * delta;

            // 2. Per-type post-move rules
            if (entity.Has<PlayerComponent>())
            {
                transform.Position = new Vec2(
                    GMath.Clamp(transform.Position.X, PlayerRadius, ArenaData.Size.X - PlayerRadius),
                    GMath.Clamp(transform.Position.Y, PlayerRadius, ArenaData.Size.Y - PlayerRadius)
                );
            }
            else if (entity.Has<ArrowComponent>())
            {
                if (IsOutsideArena(transform.Position))
                    World.DestroyEntity(entity.Id);
            }
            else if (entity.Has<MonsterProjectileComponent>())
            {
                // Apply linear acceleration (Elite projectiles only; Skeleton has Acceleration = 0)
                var proj = entity.Get<MonsterProjectileComponent>();
                if (proj.Acceleration > 0f)
                {
                    velocity.Speed   += proj.Acceleration * delta;
                    velocity.Velocity = velocity.Velocity.Normalized() * velocity.Speed;
                }

                if (IsOutsideArena(transform.Position))
                    World.DestroyEntity(entity.Id);
            }
            // Monsters: no clamping — they spawn outside and move in.
        }
    }

    private bool IsOutsideArena(Vec2 pos) =>
        pos.X < -ArrowMargin || pos.X > ArenaData.Size.X + ArrowMargin ||
        pos.Y < -ArrowMargin || pos.Y > ArenaData.Size.Y + ArrowMargin;
}
