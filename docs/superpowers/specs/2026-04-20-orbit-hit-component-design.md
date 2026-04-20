# Orbit Hit Component 设计

## 背景

`OrbitSystem` 直接调用 `World.GetSystem<CollisionSystem>().AddOrbitHit()`，违反 ECS 系统独立性原则。需要改为纯组件交互。

## 架构设计

### 数据流

```
OrbitSystem.Update()
    ↓ 添加 OrbitHitComponent（写 monster）
    monster.Add(new OrbitHitComponent { Damage, IsOrbit=true })

BattleScene.SpawnDamageNumbers()
    ↓ 查询有 OrbitHitComponent 的实体
    显示伤害数字 → 移除组件
```

### 关键约束

- `OrbitSystem` 只负责检测碰撞和**添加组件**（写）
- 伤害扣血由 `BattleScene.SpawnDamageNumbers()` 处理后统一扣除
- `CollisionSystem.AddOrbitHit()` 保留，但仅作向后兼容（未来其他非碰撞伤害源可能还用）

## 组件设计

### OrbitHitComponent.cs

```csharp
namespace Game.Ecs.Components;

public class OrbitHitComponent
{
    public int Damage;
    public bool IsOrbit = true;
}
```

字段说明：
- `Damage`：卫星箭造成的伤害值
- `IsOrbit`：标识来源为卫星箭（用于伤害数字颜色/样式区分）

## 改动点

| 文件 | 改动 |
|------|------|
| `Components/OrbitHitComponent.cs` | 新建，存储卫星命中信息 |
| `Systems/OrbitSystem.cs` | 移除 `AddOrbitHit()` 调用，改用 `monster.Add(new OrbitHitComponent {...})` |
| `UI/BattleScene.cs` | `SpawnDamageNumbers()` 改为查询有 `OrbitHitComponent` 的 monster 实体 |
| `Systems/CollisionSystem.cs` | `AddOrbitHit()` 方法保留（向后兼容），不再被 `OrbitSystem` 调用 |

## BattleScene.SpawnDamageNumbers 逻辑调整

**当前（有问题）**：
```csharp
// 依赖 CollisionSystem.Hits
var hits = _world.GetSystem<CollisionSystem>().Hits;
foreach (var hit in hits) { ... }
```

**改为（纯组件）**：
```csharp
// 查询有 OrbitHitComponent 的 monster
var hitEntities = World.GetEntitiesWith<OrbitHitComponent, TransformComponent>();
foreach (var entity in hitEntities)
{
    var hit = entity.Get<OrbitHitComponent>();
    var transform = entity.Get<TransformComponent>();
    SpawnDamageNumber(transform.Position, hit.Damage, hit.IsOrbit);
    World.Remove<OrbitHitComponent>(entity.Id); // 消费掉
}
```

注意：原有的 `CollisionSystem.Hits` 逻辑（箭击中怪物、怪物攻击玩家等）保持不变，`SpawnDamageNumbers` 需要同时处理两种来源。

## 验证条件

- [ ] 构建通过
- [ ] 卫星命中怪物时，伤害数字正确显示
- [ ] 伤害数字在显示后被正确清除（不重复显示）
