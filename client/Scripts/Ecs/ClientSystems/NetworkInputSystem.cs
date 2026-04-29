using Game.Ecs.Components;
using Game.Ecs.Core;

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
        var mode = ResolveGameMode();
        var entities = World.GetEntitiesWith<PlayerComponent, ClientInputComponent, VelocityComponent>();

        foreach (var entity in entities)
        {
            var player = entity.Get<PlayerComponent>();
            if (!player.IsLocal)
                continue;

            var input = entity.Get<ClientInputComponent>();
            var velocity = entity.Get<VelocityComponent>();
            var moveVelocity = input.HasInput
                ? input.InputDir.Normalized() * velocity.Speed
                : Vec2.Zero;

            if (mode == GameMode.SinglePlayer)
            {
                velocity.LogicVelocity = moveVelocity;
            }
            else
            {
                velocity.ClientVelocity = moveVelocity;
            }
        }
    }

    private GameMode ResolveGameMode()
    {
        var settingEntities = World.GetEntitiesWith<GameSettingComponent>();
        if (settingEntities.Count > 0)
            return settingEntities[0].Get<GameSettingComponent>().Mode;

        return GameMode.SinglePlayer;
    }
}
