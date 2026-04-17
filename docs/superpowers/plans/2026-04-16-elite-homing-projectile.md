# Elite 追踪子弹实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 Elite 怪物子弹改为慢速软追踪玩家，并支持存活时间到期自动销毁。

**Architecture:** 修改 `MonsterProjectileComponent` 字段（移除 Acceleration，加 IsHoming/LifeTimer），更新 `MonsterData` 常量，改写 `MonsterAISystem.SpawnEliteProjectiles` 和 `MovementSystem` 中的 Elite 子弹逻辑。

**Tech Stack:** Godot 4.6 C# (.NET 8), 项目自定义 ECS（`Game.Ecs.Core / Systems / Components / Data`）

---

## 文件地图

| 文件 | 操作 |
|------|------|
| `client/Scripts/Ecs/Components/MonsterProjectileComponent.cs` | **MODIFY** — 移除 Acceleration，新增 IsHoming + LifeTimer |
| `client/Scripts/Data/MonsterData.cs` | **MODIFY** — 移除旧常量，新增追踪常量 |
| `client/Scripts/Ecs/Systems/MonsterAISystem.cs` | **MODIFY** — SpawnEliteProjectiles 用新字段 |
| `client/Scripts/Ecs/Systems/MovementSystem.cs` | **MODIFY** — 追踪逻辑 + 存活计时器 |

---

## Task 1: 更新 MonsterProjectileComponent

**Files:**
- Modify: `client/Scripts/Ecs/Components/MonsterProjectileComponent.cs`

- [ ] **Step 1: 覆写文件**

将文件内容改为：

```csharp
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
```

- [ ] **Step 2: 确认编译**

```bash
dotnet build client/client.csproj -nologo -clp:NoSummary 2>&1 | tail -5
```

此时会有编译错误，因为 `MonsterAISystem` 和 `MovementSystem` 还在使用 `Acceleration`——这是预期的，在后续 Task 修复。

---

## Task 2: 更新 MonsterData 常量

**Files:**
- Modify: `client/Scripts/Data/MonsterData.cs`

- [ ] **Step 1: 替换 Elite 子弹参数**

找到文件中的 Elite 参数块：

```csharp
    // Elite ranged parameters
    public const float EliteWanderDuration      = 2.5f;  // seconds in wander phase
    public const float EliteAttackDuration      = 0.6f;  // pause before firing
    public const float EliteProjectileInitSpeed = 150f;  // px/s at spawn
    public const float EliteProjectileAccel     = 250f;  // px/s² linear acceleration
    public const int   EliteProjectileMinCount  = 2;
    public const int   EliteProjectileMaxCount  = 4;
    public const float EliteProjectileSpreadDeg = 12f;   // degrees between adjacent shots
    public const int   EliteProjectileDamage    = 10;
```

替换为：

```csharp
    // Elite ranged parameters
    public const float EliteWanderDuration       = 2.5f;  // seconds in wander phase
    public const float EliteAttackDuration        = 0.6f;  // pause before firing
    public const float EliteProjectileSpeed       = 120f;  // px/s 慢速追踪
    public const float EliteProjectileTurnSpeed   = 100f;  // °/s 每秒最大转向角度
    public const float EliteProjectileLifetime    = 10f;   // 秒，未命中存活时间
    public const int   EliteProjectileMinCount    = 2;
    public const int   EliteProjectileMaxCount    = 4;
    public const float EliteProjectileSpreadDeg   = 12f;   // degrees between adjacent shots
    public const int   EliteProjectileDamage      = 10;
```

---

## Task 3: 更新 MonsterAISystem.SpawnEliteProjectiles

**Files:**
- Modify: `client/Scripts/Ecs/Systems/MonsterAISystem.cs`

- [ ] **Step 1: 找到 SpawnEliteProjectiles 方法中的子弹生成代码**

找到这段（大约 231-244 行）：

```csharp
            var proj = World.CreateEntity();
            proj.Add(new TransformComponent { Position = origin, Rotation = dir.Angle() });
            proj.Add(new VelocityComponent
            {
                Velocity = dir * MonsterData.EliteProjectileInitSpeed,
                Speed = MonsterData.EliteProjectileInitSpeed
            });
            proj.Add(new MonsterProjectileComponent
            {
                Damage = MonsterData.EliteProjectileDamage,
                OwnerId = monster.Id,
                Acceleration = MonsterData.EliteProjectileAccel
            });
```

替换为：

```csharp
            var proj = World.CreateEntity();
            proj.Add(new TransformComponent { Position = origin, Rotation = dir.Angle() });
            proj.Add(new VelocityComponent
            {
                Velocity = dir * MonsterData.EliteProjectileSpeed,
                Speed    = MonsterData.EliteProjectileSpeed
            });
            proj.Add(new MonsterProjectileComponent
            {
                Damage    = MonsterData.EliteProjectileDamage,
                OwnerId   = monster.Id,
                IsHoming  = true,
                LifeTimer = MonsterData.EliteProjectileLifetime
            });
```

- [ ] **Step 2: 检查 SpawnSkeletonProjectile 方法中 MonsterProjectileComponent 的用法**

找到（大约 158-163 行）：

```csharp
        proj.Add(new MonsterProjectileComponent
        {
            Damage = MonsterData.SkeletonProjectileDamage,
            OwnerId = monster.Id,
            Acceleration = 0f
        });
```

替换为（移除 Acceleration，IsHoming 和 LifeTimer 默认为 false/0）：

```csharp
        proj.Add(new MonsterProjectileComponent
        {
            Damage   = MonsterData.SkeletonProjectileDamage,
            OwnerId  = monster.Id,
            IsHoming = false,
            LifeTimer = 0f
        });
```

- [ ] **Step 3: 确认编译**

```bash
dotnet build client/client.csproj -nologo -clp:NoSummary 2>&1 | tail -5
```

此时 `MonsterAISystem` 错误应消失，`MovementSystem` 仍可能有 Acceleration 引用报错。

---

## Task 4: 更新 MovementSystem — 追踪逻辑 + 存活计时器

**Files:**
- Modify: `client/Scripts/Ecs/Systems/MovementSystem.cs`

- [ ] **Step 1: 在文件顶部 using 区域确认有 `using Game.Ecs.Components;`**

读取文件开头，确认命名空间引用完整。

- [ ] **Step 2: 找到现有 Elite 加速逻辑块并替换**

找到（大约 46-58 行）：

```csharp
            else if (entity.Has<MonsterProjectileComponent>())
            {
                // Apply linear acceleration (Elite projectiles only; Skeleton has Acceleration = 0)
                var proj = entity.Get<MonsterProjectileComponent>();
                if (proj.Acceleration > 0f)
                {
                    velocity.Speed   += proj.Acceleration * delta;
                    velocity.Velocity = velocity.Velocity.Normalized() * velocity.Speed;
                }

                if (IsOutsideArena(transform.Position))
                    World.DestroyEntity(entity.Id);
            }
```

替换为：

```csharp
            else if (entity.Has<MonsterProjectileComponent>())
            {
                var proj = entity.Get<MonsterProjectileComponent>();

                // 存活计时器：LifeTimer > 0 时倒计，归零则销毁
                if (proj.LifeTimer > 0f)
                {
                    proj.LifeTimer -= delta;
                    if (proj.LifeTimer <= 0f)
                    {
                        World.DestroyEntity(entity.Id);
                        continue;
                    }
                }

                // 软追踪（仅 Elite 子弹）
                if (proj.IsHoming)
                {
                    Vec2? targetPos = FindNearestPlayerPos(transform.Position);
                    if (targetPos.HasValue)
                    {
                        float maxTurn  = GMath.DegToRad(MonsterData.EliteProjectileTurnSpeed) * delta;
                        float curAngle = velocity.Velocity.Angle();
                        float tgtAngle = (targetPos.Value - transform.Position).Normalized().Angle();
                        float diff     = NormalizeAngle(tgtAngle - curAngle);
                        float turn     = GMath.Clamp(diff, -maxTurn, maxTurn);
                        float newAngle = curAngle + turn;
                        Vec2  newDir   = Vec2.FromAngle(newAngle);
                        velocity.Velocity  = newDir * MonsterData.EliteProjectileSpeed;
                        transform.Rotation = newAngle;
                    }
                }

                if (IsOutsideArena(transform.Position))
                    World.DestroyEntity(entity.Id);
            }
```

- [ ] **Step 3: 在 MovementSystem 类末尾（IsOutsideArena 方法之前）添加两个私有方法**

```csharp
    /// <summary>找距离 origin 最近的存活玩家位置；无玩家时返回 null。</summary>
    private Vec2? FindNearestPlayerPos(Vec2 origin)
    {
        float best = float.MaxValue;
        Vec2? result = null;
        foreach (var p in World.GetEntitiesWith<PlayerComponent, TransformComponent>())
        {
            if (!p.IsAlive) continue;
            var pt = p.Get<TransformComponent>();
            float d = origin.DistanceTo(pt.Position);
            if (d < best) { best = d; result = pt.Position; }
        }
        return result;
    }

    /// <summary>将角度规范到 [-π, π]。</summary>
    private static float NormalizeAngle(float a)
    {
        while (a >  GMath.Pi) a -= GMath.Tau;
        while (a < -GMath.Pi) a += GMath.Tau;
        return a;
    }
```

- [ ] **Step 4: 确认 MovementSystem 的 using 区包含 `Game.Data`**

如果文件顶部缺少 `using Game.Data;`，需要添加（MonsterData 常量所在命名空间）。

- [ ] **Step 5: 最终编译验证**

```bash
dotnet build client/client.csproj -nologo -clp:NoSummary 2>&1 | tail -5
```

预期输出（0 错误）：
```
  client -> .../bin/Debug/client.dll
```

- [ ] **Step 6: 提交**

```bash
git add client/Scripts/Ecs/Components/MonsterProjectileComponent.cs \
        client/Scripts/Data/MonsterData.cs \
        client/Scripts/Ecs/Systems/MonsterAISystem.cs \
        client/Scripts/Ecs/Systems/MovementSystem.cs \
        docs/superpowers/specs/2026-04-16-elite-homing-projectile-design.md
git commit -m "feat: elite projectiles changed to slow homing with 10s lifetime"
```
