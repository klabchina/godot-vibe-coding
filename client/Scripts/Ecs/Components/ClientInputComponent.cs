using Game.Ecs.Core;

namespace Game.Ecs.Components;

/// <summary>
/// Client-only: stores raw input direction from InputSystem.
/// Consumed by NetworkInputSystem (logic layer) to apply to VelocityComponent.
/// </summary>
public class ClientInputComponent
{
    public Vec2 InputDir;
    public bool HasInput;
}
