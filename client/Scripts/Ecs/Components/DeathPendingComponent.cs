namespace Game.Ecs.Components;

/// <summary>
/// 标记实体正在播放死亡动画，待 RenderSystem 动画结束后清理。
/// DeathSystem 在怪物死亡时不直接销毁实体，而是添加此标记组件。
/// RenderSystem 检测到此组件后强制播放死亡动画，动画播完后设置 entity.IsAlive = false。
/// World.Update() 的清理阶段会自动移除 IsAlive == false 的实体。
/// 纯 ECS 标记组件，无字段。
/// </summary>
public class DeathPendingComponent { }
