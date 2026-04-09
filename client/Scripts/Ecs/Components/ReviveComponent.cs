namespace Game.Ecs.Components;

/// <summary>
/// Tracks player revive state. Each player can revive once per battle.
/// 10 second countdown, then revive at 50% HP.
/// </summary>
public class ReviveComponent
{
    public float ReviveTimer = 10f;  // 10 seconds to auto-revive
    public bool HasRevived;          // true after using the one-time revive
}
