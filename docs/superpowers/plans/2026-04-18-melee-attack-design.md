# 近战怪物攻击逻辑设计

**日期**: 2026-04-18
**状态**: 设计中

---

## 1. 背景

当前三个近战怪物（Slime / Orc / Boss）只有移动逻辑，动画系统有 `attack` 帧但 AI 从未触发。远程怪物（Skeleton / Elite）已通过 `FiredThisCycle` + `RangedPhase` 实现攻击动画触发，近战怪物需要同等待遇。

---

## 2. 设计目标

| 目标 | 说明 |
|------|------|
| **攻击范围可配置** | 支持圆形和矩形（正向）两种范围定义 |
| **攻击时禁止移动** | 怪物进入攻击状态后速度清零，动画播完前不能移动 |
| **伤害时机精确** | 伤害判定在攻击动作时长的中间帧触发（不是开始、不是结束） |
| **兼容现有架构** | 复用 `MonsterAIState`，不破坏现有的 `FiredThisCycle` 动画触发机制 |

---

## 3. 攻击范围配置

### 3.1 范围类型

```csharp
public enum AttackShapeType { Circle, Rectangle }

// 攻击范围联合类型
public struct AttackHitbox
{
    public AttackShapeType ShapeType;
    public float Radius;          // 圆形用：半径（px）
    public float HalfWidth;       // 矩形用：半宽（px）
    public float HalfHeight;       // 矩形用：半高（px）
}
```

### 3.2 各怪物范围配置（MonsterData.cs）

| 怪物 | 形状 | 参数 | 说明 |
|------|------|------|------|
| **Slime** | Circle | Radius = 40 | 弹跳压缩冲击，近身接触即判定 |
| **Orc** | Rectangle | HalfWidth=60, HalfHeight=40 | 冲锋停止后正面矩形横扫 |
| **Boss** | Rectangle | HalfWidth=80, HalfHeight=60 | 大范围正面矩形拍击 |

### 3.3 配置常量命名

```csharp
// 攻击范围参数（MonsterData.cs）
// Slime
public const float SlimeAttackRadius = 40f;
public const float SlimeAttackDuration = 0.5f;    // 攻击动画总时长

// Orc（冲锋后衔接攻击）
public const float OrcAttackHalfWidth = 60f;
public const float OrcAttackHalfHeight = 40f;
public const float OrcAttackDuration = 0.5f;

// Boss
public const float BossAttackHalfWidth = 80f;
public const float BossAttackHalfHeight = 60f;
public const float BossAttackDuration = 0.8f;

// 所有近战通用
public const float MeleeDamageTimeRatio = 0.5f;    // 伤害在动画 50% 处触发
```

---

## 4. 攻击状态机

### 4.1 MeleePhase 枚举

```csharp
public enum MeleePhase
{
    Chasing,    // 正常追击（向玩家移动）
    WindingUp, // 前摇（播放攻击动画，到 50% 时判定伤害）
    Recovering // 收招（动画后半段，不可移动，等待恢复）
}
```

### 4.2 MonsterAIState 扩展字段

```csharp
// MonsterAIState.cs 新增字段

// 近战攻击状态机（仅 Slime/Orc/Boss 使用）
public MeleePhase MeleePhase = MeleePhase.Chasing;
public float      AttackTimer;          // 攻击动画计时（累加）
public float      AttackDuration;      // 当前攻击动画总时长
public bool       DamageApplied;       // 当前攻击周期内伤害是否已判定
public AttackHitbox AttackHitbox;      // 攻击范围（形状+尺寸）
```

### 4.3 攻击触发逻辑

```
每帧 Update 流程（以 Slime 为例）:

1. 检查 MeleePhase
   ├── Chasing:
   │   ├── 正常向玩家移动（velocity.Velocity = chaseDir * baseSpeed）
   │   ├── 检测与玩家距离 < SlimeAttackRadius
   │   │   ├── 是 → 进入 WindingUp，AttackTimer=0，DamageApplied=false
   │   │           AttackDuration = SlimeAttackDuration
   │   │           FiredThisCycle = true（触发攻击动画）
   │   │           velocity.Velocity = Vec2.Zero（禁止移动）
   │   └── 否 → 继续追击
   │
   ├── WindingUp:
   │   ├── velocity.Velocity = Vec2.Zero
   │   ├── AttackTimer += delta
   │   ├── 当 AttackTimer / AttackDuration >= MeleeDamageTimeRatio (0.5):
   │   │   ├── DamageApplied == false?
   │   │   │   ├── 是 → 对攻击范围内的玩家造成伤害，DamageApplied=true
   │   │   └── 否 → 跳过（已判定过）
   │   └── 当 AttackTimer >= AttackDuration → 进入 Recovering
   │
   └── Recovering:
       ├── velocity.Velocity = Vec2.Zero
       ├── AttackTimer += delta
       └── 当 AttackTimer >= AttackDuration → 
           ├── MeleePhase = Chasing
           ├── AttackTimer = 0
           └── FiredThisCycle = false（攻击动画结束）
```

### 4.4 Orc 特殊情况

Orc 的攻击与冲刺绑定（不同于 Slime/Boss 的距离触发）：

```
冲刺结束（DashTimer >= DashInterval）后：
  → 进入 WindingUp（不是 Chasing）
  → AttackTimer=0, AttackDuration=OrcAttackDuration
  → FiredThisCycle=true（播放攻击动画）
  → Recovering 结束后才恢复普通追击
```

---

## 5. 伤害判定实现

### 5.1 攻击范围碰撞检测

在 `CollisionSystem.cs` 或 `DamageSystem.cs` 中增加近战攻击碰撞检测：

```csharp
// 伪代码：AttackHitboxCollisionCheck(monster, ai)
private void ApplyMeleeAttackDamage(Entity monster, MonsterAIState ai)
{
    if (ai.MeleePhase != MeleePhase.WindingUp) return;
    if (ai.DamageApplied) return;
    
    // 检查是否到达伤害时刻
    float progress = ai.AttackTimer / ai.AttackDuration;
    if (progress < MeleeDamageTimeRatio) return;
    
    var transform = monster.Get<TransformComponent>();
    var hitbox = ai.AttackHitbox;
    var damage = MonsterData.GetDamage(monster.Get<MonsterComponent>().Type, currentWave);
    
    // 遍历所有玩家，检测碰撞
    foreach (var player in World.GetEntitiesWith<PlayerComponent, TransformComponent>())
    {
        if (Intersects(hitbox, transform.Position, player.Get<TransformComponent>().Position))
        {
            ApplyDamageToPlayer(player, damage, monster);
        }
    }
    
    ai.DamageApplied = true;
}

// 矩形碰撞（正向，无旋转）
private bool Intersects(AttackHitbox hitbox, Vec2 attackerPos, Vec2 playerPos)
{
    if (hitbox.ShapeType == AttackShapeType.Circle)
    {
        return (playerPos - attackerPos).Length() <= hitbox.Radius;
    }
    else // Rectangle（正向，不考虑朝向旋转）
    {
        float dx = Math.Abs(playerPos.X - attackerPos.X);
        float dy = Math.Abs(playerPos.Y - attackerPos.Y);
        return dx <= hitbox.HalfWidth && dy <= hitbox.HalfHeight;
    }
}
```

### 5.2 伤害只触发一次

`DamageApplied` 标志确保每次攻击只造成一次伤害，防止动画多帧重复判定。

---

## 6. 攻击动画时长配置

| 怪物 | 动画时长 | 伤害时刻（50%处） | 动画帧率 |
|------|---------|-----------------|---------|
| Slime | 0.5s | 0.25s | 10fps（5帧） |
| Orc | 0.5s | 0.25s | 10fps（5帧） |
| Boss | 0.8s | 0.4s | 10fps（8帧） |

---

## 7. 修改文件清单

| 文件 | 修改内容 |
|------|---------|
| `MonsterAIState.cs` | 新增 MeleePhase 枚举、AttackTimer、AttackDuration、DamageApplied、AttackHitbox 字段 |
| `MonsterData.cs` | 新增三个怪物的 AttackShape、AttackDuration 常量 |
| `MonsterAISystem.cs` | 新增 `UpdateSlimeMelee`/`UpdateOrcMelee`/`UpdateBossMelee` 方法，替换当前追击逻辑 |
| `CollisionSystem.cs` 或 `DamageSystem.cs` | 新增 `ApplyMeleeAttackDamage` 方法，在 Update 中调用 |
| `RenderSystem.cs` | 无需修改（`FiredThisCycle` 已支持动画触发） |

---

## 8. 行为对比总览

| 怪物 | 触发方式 | 攻击范围 | 攻击时移动 |
|------|---------|---------|-----------|
| **Slime** | 进入 `SlimeAttackRadius` 范围 | 圆形 r=40 | ❌ 禁止 |
| **Orc** | 冲刺结束后自动衔接 | 正向矩形 120×80 | ❌ 禁止 |
| **Boss** | 进入 `BossAttackHalfWidth` 范围 | 正向矩形 160×120 | ❌ 禁止 |
| **Skeleton** | 远程，不再修改 | — | — |
| **Elite** | 远程，不再修改 | — | — |
