using Game.Ecs.Core;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.Systems;

/// <summary>
/// Updates entity positions based on velocity and handles arena boundary rules.
/// Players are clamped inside, arrows are destroyed outside, monsters roam freely.
/// </summary>
public class MovementSystem : GameSystem
{
    private const float PlayerRadius = 16f;
    private const float ArrowMargin = 64f;

    public override void Update(float delta)
    {
        var entities = World.GetEntitiesWith<TransformComponent, VelocityComponent>();

        foreach (var entity in entities)
        {
            var transform = entity.Get<TransformComponent>();
            var velocity = entity.Get<VelocityComponent>();

            transform.Position += velocity.Velocity * delta;

            if (entity.Has<PlayerComponent>())
            {
                transform.Position = new Vec2(
                    GMath.Clamp(transform.Position.X, PlayerRadius, ArenaData.Size.X - PlayerRadius),
                    GMath.Clamp(transform.Position.Y, PlayerRadius, ArenaData.Size.Y - PlayerRadius)
                );
            }
            else if (entity.Has<ArrowComponent>())
            {
                if (transform.Position.X < -ArrowMargin
                    || transform.Position.X > ArenaData.Size.X + ArrowMargin
                    || transform.Position.Y < -ArrowMargin
                    || transform.Position.Y > ArenaData.Size.Y + ArrowMargin)
                {
                    World.DestroyEntity(entity.Id);
                }
            }
            // Monsters: no clamping — they spawn outside and move in.
        }
    }
}
