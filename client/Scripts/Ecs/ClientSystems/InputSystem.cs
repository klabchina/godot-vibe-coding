using Godot;
using Game.Ecs;
using Game.Ecs.Core;
using Game.Ecs.Components;

namespace Game.Ecs.ClientSystems;
/// <summary>
/// Client-only: reads Godot input and writes to ECS VelocityComponent.
/// Runs at render frequency (every frame) for responsive input.
/// </summary>
public class InputSystem : GameSystem
{
	public override bool IsRenderSystem => true;

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
            var dir = new Vec2(inputDir.X, inputDir.Y).Normalized();
            velocity.Velocity = dir * velocity.Speed;
        }
    }
}
