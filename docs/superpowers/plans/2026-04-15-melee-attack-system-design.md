# 近战怪物攻击系统设计方案

## 当前状态

| 怪物 | 类型 | 当前行为 |
|------|------|----------|
| Slime | 近战 | 直接追向玩家，碰撞触发伤害 |
| Orc | 近战 | 定时冲刺，冲到玩家身边后无攻击动作 |
| Boss | 近战 | 根据相位移动，无主动攻击 |
| Skeleton | 远程 | 游荡→停止→发射弓箭 ✅ |
| Elite | 远程 | 游荡→停止→发射多个追踪箭 ✅ |

**问题**：近战怪物（Slime/Orc/Boss）没有攻击前摇、攻击动画、攻击冷却逻辑。

---

## 实现方案

### 1. 新建组件 `MeleeAttackComponent.cs`

```csharp
namespace Game.Ecs.Components;

/// <summary>
/// Marks a monster as capable of melee attacks.
/// Tracks cooldown and windup state.
/// </summary>
public class MeleeAttackComponent
{
    public float CooldownTimer;        // seconds until next attack ready
    public float AttackWindupTimer;    // seconds in windup (0 = not attacking)
    public bool CanAttack = true;
}
```

### 2. 配置数据 `MonsterData.cs` 新增

```csharp
// Melee attack parameters (shared)
public const float MeleeAttackRange = 60f;      // px, attack triggers within this distance
public const float MeleeWindupDuration = 0.3f; // seconds before damage frames
public const float MeleeCooldownMin = 1.2f;     // seconds between attacks (random min)
public const float MeleeCooldownMax = 2.5f;     // seconds between attacks (random max)

// Slime attack parameters
public const float SlimeAttackDamage = 8f;

// Orc attack parameters  
public const float OrcAttackDamage = 20f;

// Boss attack parameters
public const float BossAttackDamage = 30f;
```

### 3. 新建 System `MeleeAttackSystem.cs`

```csharp
/// <summary>
/// Handles melee attack logic for all melee monsters:
/// - Detect if player within attack range
/// - Trigger windup (monster stops, plays attack animation)
/// - After windup, call DealDamage once
/// - Apply cooldown before next attack
/// </summary>
public class MeleeAttackSystem : GameSystem
{
    public override void Update(float delta)
    {
        // 1. Find monsters with MeleeAttackComponent
        // 2. Check if player within MeleeAttackRange
        // 3. If CanAttack && CooldownTimer <= 0:
        //    - Set AttackWindupTimer = MeleeWindupDuration
        //    - Set CanAttack = false
        //    - Stop monster movement (velocity = zero)
        // 4. If AttackWindupTimer > 0:
        //    - Decrement timer
        //    - When timer <= 0: DealDamage and set CooldownTimer
        // 5. If CooldownTimer > 0:
        //    - Decrement timer
        //    - When timer <= 0: Set CanAttack = true
    }
}
```

### 4. 修改 `MonsterAISystem.cs`

#### Slime 更新逻辑
```csharp
case MonsterType.Slime:
    float slimeRadius = monster.Get<ColliderComponent>()?.Radius ?? 15f;
    float slimeRange = slimeRadius + MonsterData.MeleeAttackRange;
    
    // If attacking, don't set velocity here (MeleeAttackSystem handles it)
    var melee = monster.Get<MeleeAttackComponent>();
    if (melee != null && melee.AttackWindupTimer > 0)
    {
        // Do not override velocity - let windup play
    }
    else
    {
        // Normal chase behavior
        Vec2 slimeDetour = ApplyDetourMemory(ai, monsterTransform.Position, nearestPos, toPlayer, slimeRadius, delta);
        Vec2 slimeDir = AdjustForObstacles(monsterTransform.Position, slimeDetour, baseSpeed * speedMultiplier, delta, slimeRadius);
        velocity.Velocity = slimeDir * baseSpeed * speedMultiplier;
    }
    break;
```

#### Orc 更新逻辑
类似 Slime，在冲刺时检查是否进入攻击范围。

#### Boss 更新逻辑
在追踪阶段（Frenzy）添加近战攻击。

---

## 攻击状态流程

```
[Idle/Chasing]
     ↓ (player in range && CanAttack && CooldownTimer <= 0)
[Windup: 0.3s] ← 停止移动，播放攻击动画
     ↓
[Damage Frame] ← 触发 DealDamage 一次
     ↓
[Cooldown: 1.2-2.5s] ← 重置 CanAttack
     ↓
[Ready] → 返回追逐或下一轮攻击
```

---

## 伤害数值

| 怪物 | 伤害值 | 来源 |
|------|--------|------|
| Slime | 8 | MonsterData.SlimeAttackDamage |
| Orc | 20 | MonsterData.OrcAttackDamage |
| Boss | 30 | MonsterData.BossAttackDamage |

伤害调用：
```csharp
battle.DealDamage(monster, targetPlayerId, damage);
```

---

## 需要确认的问题

1. **攻击动画**：是否需要在客户端播放攻击动画？还是只需要停止移动即可？
2. **攻击范围**：建议 60px 是否合适？
3. **冷却时间**：Slime/Orc/Boss 是否需要不同的冷却时间？
4. **Boss 特殊处理**：Boss 只有在 Frenzy 阶段攻击，还是所有阶段都需要攻击？
