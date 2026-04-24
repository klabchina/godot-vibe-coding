using Game.Ecs;
using Game.Ecs.Components;

namespace Game.Ecs.ClientSystems;
/// <summary>
/// Logic layer: reads ClientInputComponent (set by InputSystem at render frequency)
/// and applies movement to VelocityComponent.
/// This separates input reading from logic application, enabling future network sync.
/// </summary>
public class NetworkInputSystem : GameSystem
{
    public override void Update(float delta)
    {
        var entities = World.GetEntitiesWith<PlayerComponent, ClientInputComponent, VelocityComponent>();

        foreach (var entity in entities)
        {
            var player = entity.Get<PlayerComponent>();
            if (!player.IsLocal)
                continue;

            var input = entity.Get<ClientInputComponent>();
            if (!input.HasInput)
                continue;

            var velocity = entity.Get<VelocityComponent>();
            velocity.Velocity = input.InputDir.Normalized() * velocity.Speed;
        }
    }
}
