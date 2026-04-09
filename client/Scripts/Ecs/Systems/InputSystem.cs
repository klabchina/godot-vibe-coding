using Godot;
using Game.Ecs;
using Game.Ecs.Components;

namespace Game.Ecs.Systems;

public class InputSystem : GameSystem
{
    public override void Update(float delta)
    {
        var entities = World.GetEntitiesWith<PlayerComponent, VelocityComponent, TransformComponent>();

        foreach (var entity in entities)
        {
            var player = entity.Get<PlayerComponent>();
            if (!player.IsLocal)
                continue;

            var velocity = entity.Get<VelocityComponent>();

            Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_up", "move_down");
            velocity.Velocity = inputDir.Normalized() * velocity.Speed;
        }
    }
}
