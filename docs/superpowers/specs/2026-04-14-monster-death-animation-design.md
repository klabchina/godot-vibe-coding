# 怪物死亡动画延迟销毁设计

## 背景与问题

当前 `DeathSystem` 在检测到怪物 `HealthComponent.Hp <= 0` 时，同帧直接调用 `World.DestroyEntity()` 将实体从 ECS 世界移除。

`RenderSystem` 已有死亡动画播放逻辑：
- `UpdateMonsterAnimation()` 检测 `health.Hp <= 0` 时播放 `AnimNames.Death`
- 动画播完后（`!animSprite.IsPlaying()`）设置 `entity.IsAlive = false`

但由于 DeathSystem 先于 RenderSystem 的 Update 清理阶段执行，实体已在动画播放前被移除，导致死亡动画无法正常呈现。

## 设计方案

### 核心原则

保持引擎（Godot）与纯 ECS 分离：
- **ECS 层**（DeathSystem / DeathCleanupSystem）负责游戏逻辑数据，不感知动画
- **引擎层**（RenderSystem）负责渲染和动画感知，在动画完成后通过 ECS API 设置标志

### 新增组件：`DeathPendingComponent`

```csharp
// client/Scripts/Ecs/Components/DeathPendingComponent.cs
namespace Game.Ecs.Components;

/// <summary>
/// 标记实体正在播放死亡动画，待 RenderSystem 动画结束后清理。
/// 纯 ECS 标记组件，无字段。
/// </summary>
public class DeathPendingComponent { }
```

### DeathSystem 修改

**修改前：**
```csharp
private void HandleMonsterDeath(Entity entity)
{
    // ... 经验球、掉落、波次计数等逻辑
    World.DestroyEntity(entity.Id);  // ← 立即销毁
}
```

**修改后：**
```csharp
private void HandleMonsterDeath(Entity entity)
{
    // ... 经验球、掉落、波次计数等逻辑保持不变

    // 不再直接销毁，改加标记组件，等待客户端动画播完后清理
    entity.Add<DeathPendingComponent>();
}
```

关键点：
- 死亡逻辑（击杀追踪、掉落生成、波次计数）继续在 DeathSystem 执行
- 只有 `World.DestroyEntity()` 移除，改为添加 `DeathPendingComponent`
- DeathSystem 不感知任何 Godot / 动画相关逻辑

### RenderSystem 修改

在 `UpdateMonsterAnimation()` 中：

**修改前（检测死亡动画完成）：**
```csharp
if (targetAnim == AnimNames.Death && !animSprite.IsPlaying())
{
    entity.IsAlive = false;
}
```

**修改后（新增 DyingComponent 检测 + 动画播放）：**
```csharp
private void UpdateMonsterAnimation(Entity entity, Node2D node, float delta)
{
    var animSprite = node.GetChildOrNull<AnimatedSprite2D>(0);
    if (animSprite == null) return;

    int id = entity.Id;
    var monster = entity.Get<MonsterComponent>();
    var transform = entity.Get<TransformComponent>();

    // 新增：检测 DeathPendingComponent，强制播放死亡动画
    bool isDying = entity.Has<DeathPendingComponent>();
    string targetAnim;
    if (isDying)
    {
        targetAnim = AnimNames.Death;
        if (!animSprite.IsPlaying() || _monsterAnims.GetValueOrDefault(id) != AnimNames.Death)
        {
            _monsterAnims[id] = AnimNames.Death;
            animSprite.Play(AnimNames.Death);
        }

        // 死亡动画播完后标记 entity 为 dead
        if (!animSprite.IsPlaying())
        {
            entity.IsAlive = false;
        }
    }
    else
    {
        // 原有逻辑：根据朝向翻转
        bool flipH = false;
        if (transform != null && monster != null)
        {
            var ai = entity.Get<MonsterAIState>();
            if (ai != null && ai.TargetId >= 0)
            {
                var targetEntity = World.GetEntity(ai.TargetId);
                var targetTransform = targetEntity?.Get<TransformComponent>();
                if (targetTransform != null)
                    flipH = targetTransform.Position.X < transform.Position.X;
            }
        }
        animSprite.FlipH = flipH;

        // 根据状态决定动画（walk / attack / death，但未进入 dying 状态时）
        targetAnim = GetMonsterTargetAnim(entity, monster);

        if (_monsterAnims.GetValueOrDefault(id) != targetAnim)
        {
            _monsterAnims[id] = targetAnim;
            animSprite.Play(targetAnim);
        }
    }
}
```

关键点：
- `isDying` 检测到 `DeathPendingComponent` 时，强制播放死亡动画
- 动画播完后设置 `entity.IsAlive = false`
- `entity.IsAlive = false` 是 RenderSystem 对 ECS 世界状态的单点修改职责（引擎感知层 → ECS 数据层）

### 新增系统：`DeathCleanupSystem`

```csharp
// client/Scripts/Ecs/Systems/DeathCleanupSystem.cs
namespace Game.Ecs.Systems;

/// <summary>
/// 纯 ECS 系统：检查 entity.IsAlive == false 的实体，执行真正的销毁。
/// 所有死亡动画结束后统一清理，避免实体泄漏。
/// </summary>
public class DeathCleanupSystem : GameSystem
{
    public override void Update(float delta)
    {
        // 注意：这里不能遍历 World.Entities（已被标记为 not alive 的实体不会被 GetEntitiesWith 返回）
        // 需要直接遍历 _entities 私有字段，或者利用 World.Update() 的清理逻辑
        // 方案：直接调用 World.DestroyEntity 再次（idempotent）
        // 但更好的方式是让 World.Update() 自动清理，不需要这个系统。
        //
        // 实际上 RenderSystem 设置 entity.IsAlive = false 后，
        // World.Update() 的清理逻辑已经在下一帧自动移除实体。
        // DeathCleanupSystem 实际上不需要。
    }
}
```

**结论**：由于 `World.Update()` 已有的清理逻辑（`_pendingDestroy`），`DeathCleanupSystem` 不需要额外实现。`RenderSystem` 设置 `entity.IsAlive = false` 后，`World.Update()` 自然在下帧清理时移除实体。

### 数据流总结

```
怪物死亡（HealthComponent.Hp = 0）
    ↓
DeathSystem.HandleMonsterDeath()
    ├── 击杀追踪 +1
    ├── 生成经验球
    ├── 生成物品掉落
    ├── 波次存活数 -1
    └── entity.Add<DeathPendingComponent>()  ← 不再 DestroyEntity
           ↓
    RenderSystem.UpdateMonsterAnimation()
    ├── 检测到 DeathPendingComponent → 强制播放 AnimNames.Death
    └── 动画播完 (!animSprite.IsPlaying()) → entity.IsAlive = false
           ↓
    World.Update() 清理阶段
    └── 从 _entities 中移除该实体（_pendingDestroy）
           ↓
    RenderSystem.Update() 下一帧
    └── 检测到 entity == null || !entity.IsAlive → node.QueueFree()
```

## 文件变更清单

| 操作 | 文件 |
|------|------|
| 新增 | `client/Scripts/Ecs/Components/DeathPendingComponent.cs` |
| 修改 | `client/Scripts/Ecs/Systems/DeathSystem.cs`（移除 DestroyEntity 调用） |
| 修改 | `client/Scripts/Ecs/ClientSystems/RenderSystem.cs`（新增 Dying 分支逻辑） |

## 风险与注意事项

1. **客户端/服务端一致性**：DeathSystem 在服务端同样运行，但服务端没有 RenderSystem 响应 `DeathPendingComponent`。服务端需要在一定延迟后确保实体被清理——由于 `World.Update()` 仍会在 RenderSystem 设置 `IsAlive=false` 后自动清理（若服务端也有 RenderSystem），或可额外加一个超时清理机制。

2. **实体泄漏保护**：若 RenderSystem 因任何原因未能设置 `entity.IsAlive = false`（例如动画帧率为0），实体将永久保留。可考虑在 `DeathPendingComponent` 上加 `float LifeTime` 字段，DeathCleanupSystem 检查超时后强制清理。**本次实现暂不加，作为后续改进点。**

3. **动画帧率**：死亡动画按 `AnimFps = 10fps`，5帧 = 0.5秒，与现有 attack 动画一致。
