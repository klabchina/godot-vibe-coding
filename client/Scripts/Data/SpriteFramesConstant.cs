namespace Game.Data;

/// <summary>
/// 精灵图路径常量 — 所有 sprite frames 资源路径统一管理
/// </summary>
public static class SpriteFramesConstant
{
    // ========== 玩家角色 (Archer) ==========
    public const string ArcherIdlePrefix = "res://Assets/Sprites/Roles/archer_idle_";
    public const string ArcherWalkPrefix = "res://Assets/Sprites/Roles/archer_walk_";
    public const string ArcherAttackPrefix = "res://Assets/Sprites/Roles/archer_attack_";
    public const string ArcherExt = ".png";
    public const int ArcherFrameCount = 10;

    // ========== 怪物 (Enemies) ==========
    /// <summary>怪物 sprite 基础路径，例如 GetMonsterPath(MonsterType.Slime, "walk", 1)</summary>
    public static string GetMonsterPath(MonsterType type, string anim, int frame)
        => $"res://Assets/Sprites/Enemies/{type.ToString().ToLower()}_{anim}_{frame}.png";

    public static string[] MonsterAnims => new[] { "walk", "attack", "death" };
    public const int MonsterFrameCount = 5;

    // ========== 怪物动画配置 ==========
    /// <summary>各怪物的默认播放动画（无特殊状态时）</summary>
    public static string GetDefaultAnim(MonsterType type) => "walk";

    /// <summary>攻击动画是否只播放一次（死亡/攻击）</summary>
    public static bool AnimOnce(MonsterType type, string anim) => anim is "attack" or "death";

    // ========== 道具 / 投射物 (Props) ==========
    public const string ArrowNormal = "res://Assets/Sprites/Props/arrow_normal.png";
    public const string ArrowFire   = "res://Assets/Sprites/Props/arrow_fire.png";
    public const string ArrowStar   = "res://Assets/Sprites/Props/arrow_star.png";
}
