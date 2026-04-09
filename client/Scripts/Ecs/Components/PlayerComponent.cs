using Godot;

namespace Game.Ecs.Components;

/// <summary>
/// Tags an entity as a player. Holds player-specific state.
/// </summary>
public class PlayerComponent
{
    public int PlayerIndex;          // 0 or 1 (for multiplayer)
    public bool IsLocal = true;
    public int TotalXp;
    public int CurrentLevel;
    public int KillCount;
    public int TotalDamageDealt;
}
