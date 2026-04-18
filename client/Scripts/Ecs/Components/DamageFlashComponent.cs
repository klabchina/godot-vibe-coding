namespace Game.Ecs.Components;

/// <summary>
/// 玩家受伤时的红色闪烁组件。
/// RenderSystem 读取此组件并控制闪烁效果，完成后移除。
/// </summary>
public class DamageFlashComponent
{
    public float Timer;   // 剩余闪烁时间（秒）
}
