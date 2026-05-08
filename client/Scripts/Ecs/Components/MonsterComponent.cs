using Game.Data;

namespace Game.Ecs.Components;

public class MonsterComponent
{
    public MonsterType Type;
    public int Reward; // XP reward on death
    public int LastHitPlayerEntityId = -1; // Entity ID of the player who last hit this monster
}
