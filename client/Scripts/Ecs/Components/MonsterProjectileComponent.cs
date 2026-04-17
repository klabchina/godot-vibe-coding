namespace Game.Ecs.Components;

/// <summary>
/// 怪物发射的投射物标记组件。
/// IsHoming = false → 匀速直线（Skeleton）
/// IsHoming = true  → 软追踪玩家，LifeTimer 秒后销毁（Elite）
/// </summary>
public class MonsterProjectileComponent
{
    public int   Damage;
    public int   OwnerId;       // 发射该子弹的怪物 Entity ID
    public bool  IsHoming;      // true = 追踪子弹
    public float LifeTimer;     // 剩余存活秒数；0 = 永久（不销毁）
}
