using Godot;
using Game.Ecs;
using Game.Ecs.Core;
using Game.Ecs.Components;

namespace Game.Ecs.ClientSystems;
/// <summary>
/// Client-only: reads Godot input and writes to ClientInputComponent.
/// Runs at render frequency (every frame) for responsive input.
/// Does NOT directly modify VelocityComponent - that happens in NetworkInputSystem.
/// </summary>
public class InputSystem : GameSystem
{
    public override bool IsRenderSystem => true;

    public override void Update(float delta)
    {
        var entities = World.GetEntitiesWith<PlayerComponent, ClientInputComponent>();

        foreach (var entity in entities)
        {
            var player = entity.Get<PlayerComponent>();
            if (!player.IsLocal)
                continue;

            var input = entity.Get<ClientInputComponent>();

            Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_up", "move_down");
            input.InputDir = new Vec2(inputDir.X, inputDir.Y);
            input.HasInput = inputDir.Length() > 0.01f;
        }
    }
}
