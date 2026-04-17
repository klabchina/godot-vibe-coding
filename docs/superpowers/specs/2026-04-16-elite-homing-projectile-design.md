# Elite 追踪子弹设计文档

**日期：** 2026-04-16  
**状态：** 已批准

---

## 1. 背景

当前 Elite 怪物发射 2-4 颗扇形加速子弹（初速 150 px/s，加速度 250 px/s²），子弹固定方向飞行，越界销毁。  
新需求：改为**慢速软追踪子弹**，子弹持续转向玩家，存活时间到期后自动销毁。

---

## 2. 行为规格

| 参数 | 值 | 说明 |
|------|----|------|
| 飞行速度 | `120 px/s`（常量，不再加速） | 比玩家慢，可绕圈 |
| 转向角速度 | `100 °/s` | 软追踪，玩家可走位闪避 |
| 存活时间 | `10 秒` | 超时后销毁，0 = 不限时（Skeleton 用） |
| 追踪目标 | 距离最近的存活玩家 | 每帧重新计算方向差 |

### 追踪算法（软追踪）

```
每帧：
1. 找最近玩家位置 targetPos
2. toTarget = (targetPos - proj.Position).Normalized()
3. currentAngle = proj.Velocity.Angle()
4. targetAngle  = toTarget.Angle()
5. angleDiff = NormalizeAngle(targetAngle - currentAngle)  // [-π, π]
6. turn = Clamp(angleDiff, -maxTurn, +maxTurn)            // maxTurn = TurnSpeed * delta (rad)
7. newDir = Vec2.FromAngle(currentAngle + turn)
8. proj.Velocity = newDir * speed
9. transform.Rotation = (currentAngle + turn)
```

---

## 3. 架构变更

### 3.1 `MonsterProjectileComponent.cs`

移除 `Acceleration`，新增追踪字段：

```csharp
public class MonsterProjectileComponent
{
    public int   Damage;
    public int   OwnerId;
    public bool  IsHoming;     // true = 追踪子弹（Elite）
    public float LifeTimer;    // 剩余存活秒数；0 = 永久（Skeleton）
}
```

### 3.2 `MonsterData.cs`

移除旧 Elite 加速常量，新增追踪常量：

```csharp
// 移除：
// public const float EliteProjectileInitSpeed = 150f;
// public const float EliteProjectileAccel     = 250f;

// 新增：
public const float EliteProjectileSpeed     = 120f;   // px/s 慢速
public const float EliteProjectileTurnSpeed = 100f;   // °/s 转向角速度
public const float EliteProjectileLifetime  = 10f;    // 秒 存活时间
```

### 3.3 `MonsterAISystem.cs` — `SpawnEliteProjectiles`

```csharp
proj.Add(new TransformComponent { Position = origin, Rotation = dir.Angle() });
proj.Add(new VelocityComponent  { Velocity = dir * MonsterData.EliteProjectileSpeed,
                                   Speed    = MonsterData.EliteProjectileSpeed });
proj.Add(new MonsterProjectileComponent
{
    Damage    = MonsterData.EliteProjectileDamage,
    OwnerId   = monster.Id,
    IsHoming  = true,
    LifeTimer = MonsterData.EliteProjectileLifetime
});
```

### 3.4 `MovementSystem.cs` — 怪物子弹更新逻辑

现有精英加速块替换为追踪块 + 存活计时器：

```csharp
else if (entity.Has<MonsterProjectileComponent>())
{
    var proj = entity.Get<MonsterProjectileComponent>();

    // 存活计时器
    if (proj.LifeTimer > 0f)
    {
        proj.LifeTimer -= delta;
        if (proj.LifeTimer <= 0f)
        {
            World.DestroyEntity(entity.Id);
            continue;
        }
    }

    // 软追踪（仅 IsHoming 子弹）
    if (proj.IsHoming)
    {
        // 找最近存活玩家
        Vec2? targetPos = FindNearestPlayerPos(transform.Position);
        if (targetPos.HasValue)
        {
            float maxTurn    = GMath.DegToRad(MonsterData.EliteProjectileTurnSpeed) * delta;
            float curAngle   = velocity.Velocity.Angle();
            float tgtAngle   = (targetPos.Value - transform.Position).Normalized().Angle();
            float diff       = NormalizeAngle(tgtAngle - curAngle);
            float turn       = GMath.Clamp(diff, -maxTurn, maxTurn);
            float newAngle   = curAngle + turn;
            Vec2  newDir     = Vec2.FromAngle(newAngle);
            velocity.Velocity    = newDir * MonsterData.EliteProjectileSpeed;
            transform.Rotation   = newAngle;
        }
    }

    if (IsOutsideArena(transform.Position))
        World.DestroyEntity(entity.Id);
}
```

`FindNearestPlayerPos` 为 `MovementSystem` 私有方法，遍历 `GetEntitiesWith<PlayerComponent, TransformComponent>()` 找距离最近的返回位置。

`NormalizeAngle(float a)` 将角度规范到 `[-π, π]`：
```csharp
private static float NormalizeAngle(float a)
{
    while (a >  GMath.Pi) a -= GMath.Tau;
    while (a < -GMath.Pi) a += GMath.Tau;
    return a;
}
```

---

## 4. 受影响文件清单

| 文件 | 变更类型 |
|------|----------|
| `client/Scripts/Ecs/Components/MonsterProjectileComponent.cs` | 修改：`Acceleration` → `IsHoming` + `LifeTimer` |
| `client/Scripts/Data/MonsterData.cs` | 修改：Elite 弹速常量替换 |
| `client/Scripts/Ecs/Systems/MonsterAISystem.cs` | 修改：`SpawnEliteProjectiles` |
| `client/Scripts/Ecs/Systems/MovementSystem.cs` | 修改：Elite 子弹更新逻辑 |

---

## 5. 不受影响

- `CollisionSystem`：`MonsterProjectileComponent` 碰撞检测逻辑不变
- `RenderSystem`：`node.Rotation = transform.Rotation` 已正确同步，追踪子弹旋转自动生效
- Skeleton 子弹：`IsHoming = false`，`LifeTimer = 0`，保持原匀速直线行为
- `pure_ecs_test/ServerTest.csproj`：引用同一份组件文件，编译需同步更新
