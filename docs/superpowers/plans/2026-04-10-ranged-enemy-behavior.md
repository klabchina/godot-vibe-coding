# Ranged Enemy Behavior Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 Skeleton 和 Elite 从近战追击改为远程弓箭手，各自拥有「随机走动→停顿射击」循环行为，Skeleton 发射单发匀速箭矢，Elite 发射 2-4 发线性加速扇形弹。

**Architecture:** 新增独立的 `MonsterProjectileComponent`（区别于玩家 `ArrowComponent`），新增碰撞层 `MonsterArrow = 16`（打 Player，不打 Monster）。`MonsterAIState` 扩展远程状态机字段。`MonsterAISystem` 替换 Skeleton/Elite 的行为逻辑并在 Attack 阶段生成投射物。`MovementSystem` 负责 Elite 子弹加速及越界销毁，`CollisionSystem` 新增 `CheckMonsterProjectileVsPlayer()`。`DamageSystem` 无需改动（现有 `IsArrow=false` 路径已覆盖护盾/无敌判断）。

**Tech Stack:** Godot 4.6, C# (.NET 8), 项目自定义 ECS（`Game.Ecs.Core / Game.Ecs.Systems / Game.Ecs.Components / Game.Data`）

---

## 文件地图

| 文件 | 操作 | 说明 |
|------|------|------|
| `client/Scripts/Ecs/Components/MonsterProjectileComponent.cs` | **CREATE** | 怪物子弹组件（Damage、OwnerId、Acceleration） |
| `client/Scripts/Ecs/Components/ColliderComponent.cs` | **MODIFY** | 新增 `MonsterArrow = 16` 碰撞层常量 |
| `client/Scripts/Ecs/Components/MonsterAIState.cs` | **MODIFY** | 新增 `RangedPhase` 枚举 + 远程状态字段 |
| `client/Scripts/Data/MonsterData.cs` | **MODIFY** | 新增 Skeleton/Elite 远程攻击数值常量 |
| `client/Scripts/Ecs/Systems/MonsterAISystem.cs` | **MODIFY** | 替换 Skeleton/Elite 为远程状态机 + 生成投射物 |
| `client/Scripts/Ecs/Systems/MovementSystem.cs` | **MODIFY** | MonsterProjectile 加速逻辑 + 越界销毁 |
| `client/Scripts/Ecs/Systems/CollisionSystem.cs` | **MODIFY** | 新增 `CheckMonsterProjectileVsPlayer()` |

---

## Task 1: 新增 `MonsterProjectileComponent` + `MonsterArrow` 碰撞层

**Files:**
- Create: `client/Scripts/Ecs/Components/MonsterProjectileComponent.cs`
- Modify: `client/Scripts/Ecs/Components/ColliderComponent.cs`

- [ ] **Step 1: 创建 `MonsterProjectileComponent.cs`**

```csharp
namespace Game.Ecs.Components;

/// <summary>
/// Marks an entity as a monster-fired projectile.
/// Acceleration = 0 → constant speed (Skeleton).
/// Acceleration > 0 → linearly accelerating (Elite).
/// </summary>
public class MonsterProjectileComponent
{
    public int   Damage;
    public int   OwnerId;      // Entity ID of the monster that fired this
    public float Acceleration; // px/s² added to speed each frame; 0 = constant
}
```

- [ ] **Step 2: 在 `ColliderComponent.cs` 的 `CollisionLayers` 中添加 `MonsterArrow = 16`**

文件路径：`client/Scripts/Ecs/Components/ColliderComponent.cs`

将：
```csharp
public static class CollisionLayers
{
    public const int Player  = 1;
    public const int Monster = 2;
    public const int Arrow   = 4;
    public const int Pickup  = 8;
}
```
改为：
```csharp
public static class CollisionLayers
{
    public const int Player      = 1;
    public const int Monster     = 2;
    public const int Arrow       = 4;
    public const int Pickup      = 8;
    public const int MonsterArrow = 16; // Monster projectiles; collide with Player only
}
```

- [ ] **Step 3: 提交**

```bash
git add client/Scripts/Ecs/Components/MonsterProjectileComponent.cs \
        client/Scripts/Ecs/Components/ColliderComponent.cs
git commit -m "feat: add MonsterProjectileComponent and MonsterArrow collision layer"
```

---

## Task 2: 扩展 `MonsterAIState` 远程状态字段

**Files:**
- Modify: `client/Scripts/Ecs/Components/MonsterAIState.cs`

- [ ] **Step 1: 用以下内容完整替换 `MonsterAIState.cs`**

```csharp
using Game.Ecs.Core;

namespace Game.Ecs.Components;

/// <summary>
/// Ranged phase for Skeleton and Elite: wander briefly then stop and shoot.
/// </summary>
public enum RangedPhase { Wander, Attack }

/// <summary>
/// Per-monster AI state for advanced behaviors.
/// - Skeleton/Elite: ranged wander-attack cycle
/// - Orc: charge + stun
/// </summary>
public class MonsterAIState
{
    // Orc charge
    public bool  IsCharging;
    public bool  IsStunned;
    public float StunTimer;

    // Ranged (Skeleton & Elite)
    public RangedPhase RangedPhase = RangedPhase.Wander;
    public float       PhaseTimer;        // counts down; 0 triggers phase transition/re-init
    public Vec2        WanderDir;         // direction chosen at wander-phase start
    public bool        FiredThisCycle;    // ensures one fire per attack phase
}
```

> 注意：删除了旧的 `DodgeTimer / DodgeDuration / DodgeDir` 字段（Skeleton 不再使用横向闪避）。

- [ ] **Step 2: 提交**

```bash
git add client/Scripts/Ecs/Components/MonsterAIState.cs
git commit -m "feat: extend MonsterAIState with ranged phase state machine fields"
```

---

## Task 3: 在 `MonsterData` 中添加远程攻击常量

**Files:**
- Modify: `client/Scripts/Data/MonsterData.cs`

- [ ] **Step 1: 在 `MonsterData` 末尾（`SkeletonDodgeInterval/Duration` 之后）添加新常量**

将末尾的：
```csharp
    // Skeleton dodge parameters
    public const float SkeletonDodgeInterval = 3.0f;
    public const float SkeletonDodgeDuration = 0.5f;
}
```

替换为：
```csharp
    // Orc charge parameters (unchanged)
    // (already above)

    // Shared ranged parameters
    public const float RangedLateralBias = 0.6f; // max lateral component added to forward dir

    // Skeleton ranged parameters
    public const float SkeletonWanderDuration   = 2.0f;  // seconds in wander phase
    public const float SkeletonAttackDuration   = 0.4f;  // pause before firing
    public const float SkeletonProjectileSpeed  = 280f;  // px/s (constant)
    public const int   SkeletonProjectileDamage = 6;

    // Elite ranged parameters
    public const float EliteWanderDuration      = 2.5f;  // seconds in wander phase
    public const float EliteAttackDuration      = 0.6f;  // pause before firing
    public const float EliteProjectileInitSpeed = 150f;  // px/s at spawn
    public const float EliteProjectileAccel     = 250f;  // px/s² linear acceleration
    public const int   EliteProjectileMinCount  = 2;
    public const int   EliteProjectileMaxCount  = 4;
    public const float EliteProjectileSpreadDeg = 12f;   // degrees between adjacent shots
    public const int   EliteProjectileDamage    = 10;
}
```

- [ ] **Step 2: 提交**

```bash
git add client/Scripts/Data/MonsterData.cs
git commit -m "feat: add Skeleton and Elite ranged attack constants to MonsterData"
```

---

## Task 4: 重写 `MonsterAISystem` — Skeleton/Elite 远程状态机 + 生成投射物

**Files:**
- Modify: `client/Scripts/Ecs/Systems/MonsterAISystem.cs`

- [ ] **Step 1: 用以下内容完整替换 `MonsterAISystem.cs`**

```csharp
using Game.Ecs.Core;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.Systems;

/// <summary>
/// Monster AI: per-type movement and attack behaviors.
/// - Slime:    straight chase
/// - Skeleton: ranged (lateral wander → stop → fire single constant-speed arrow)
/// - Orc:      straight chase + charge+stun when close
/// - Elite:    ranged (lateral wander → stop → fire 2-4 accelerating arrows in spread)
/// - Boss:     direction set here; speed set by BossAISystem
/// Frozen monsters have speed reduced by EffectComponent.FreezeSlowPercent.
/// </summary>
public class MonsterAISystem : GameSystem
{
    public override void Update(float delta)
    {
        var monsters = World.GetEntitiesWith<MonsterComponent, TransformComponent, VelocityComponent>();
        var players  = World.GetEntitiesWith<PlayerComponent, TransformComponent>();

        if (players.Count == 0) return;

        foreach (var monster in monsters)
        {
            if (!monster.IsAlive) continue;

            var monsterComp      = monster.Get<MonsterComponent>();
            var monsterTransform = monster.Get<TransformComponent>();
            var velocity         = monster.Get<VelocityComponent>();

            // Find nearest alive player
            float nearestDist = float.MaxValue;
            Vec2  nearestPos  = Vec2.Zero;

            foreach (var player in players)
            {
                var playerHealth = player.Get<HealthComponent>();
                if (playerHealth != null && playerHealth.Hp <= 0) continue;

                var playerTransform = player.Get<TransformComponent>();
                float dist = monsterTransform.Position.DistanceTo(playerTransform.Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestPos  = playerTransform.Position;
                }
            }

            if (nearestDist >= float.MaxValue)
            {
                velocity.Velocity = Vec2.Zero;
                continue;
            }

            Vec2 toPlayer = (nearestPos - monsterTransform.Position).Normalized();

            // Ensure AI state exists for types that need it
            var ai = monster.Get<MonsterAIState>();
            if (ai == null && (monsterComp.Type == MonsterType.Skeleton ||
                               monsterComp.Type == MonsterType.Orc      ||
                               monsterComp.Type == MonsterType.Elite))
            {
                ai = new MonsterAIState();
                monster.Add(ai);
            }

            // Apply freeze slow
            float baseSpeed      = velocity.Speed;
            float speedMultiplier = 1f;
            var effect = monster.Get<EffectComponent>();
            if (effect != null && effect.IsFrozen)
                speedMultiplier = 1f - effect.FreezeSlowPercent;

            switch (monsterComp.Type)
            {
                case MonsterType.Skeleton:
                    UpdateSkeletonRanged(monster, velocity, ai, toPlayer, baseSpeed, speedMultiplier, delta);
                    break;
                case MonsterType.Elite:
                    UpdateEliteRanged(monster, velocity, ai, toPlayer, baseSpeed, speedMultiplier, delta);
                    break;
                case MonsterType.Orc:
                    UpdateOrc(velocity, ai, toPlayer, nearestDist, baseSpeed, speedMultiplier, delta);
                    break;
                case MonsterType.Boss:
                    // Speed is overridden by BossAISystem; just set direction
                    velocity.Velocity = toPlayer * velocity.Speed * speedMultiplier;
                    break;
                default:
                    // Slime: straight chase
                    velocity.Velocity = toPlayer * baseSpeed * speedMultiplier;
                    break;
            }
        }
    }

    // ─── Skeleton — ranged, single constant-speed arrow ───────────────────────

    private void UpdateSkeletonRanged(Entity monster, VelocityComponent velocity, MonsterAIState ai,
        Vec2 toPlayer, float baseSpeed, float speedMul, float delta)
    {
        if (ai == null) { velocity.Velocity = toPlayer * baseSpeed * speedMul; return; }

        if (ai.RangedPhase == RangedPhase.Wander)
        {
            // Re-initialize direction at the start of each wander phase (PhaseTimer == 0)
            if (ai.PhaseTimer <= 0f)
            {
                Vec2  perp = new Vec2(-toPlayer.Y, toPlayer.X);
                float bias = (GameRandom.Randf() * 2f - 1f) * MonsterData.RangedLateralBias;
                ai.WanderDir  = (toPlayer + perp * bias).Normalized();
                ai.PhaseTimer = MonsterData.SkeletonWanderDuration;
            }

            velocity.Velocity  = ai.WanderDir * baseSpeed * speedMul;
            ai.PhaseTimer     -= delta;

            if (ai.PhaseTimer <= 0f)
            {
                ai.RangedPhase   = RangedPhase.Attack;
                ai.PhaseTimer    = MonsterData.SkeletonAttackDuration;
                ai.FiredThisCycle = false;
            }
        }
        else // RangedPhase.Attack
        {
            velocity.Velocity  = Vec2.Zero; // stop while aiming
            ai.PhaseTimer     -= delta;

            if (ai.PhaseTimer <= 0f && !ai.FiredThisCycle)
            {
                SpawnSkeletonProjectile(monster, toPlayer);
                ai.FiredThisCycle = true;
                ai.RangedPhase    = RangedPhase.Wander;
                ai.PhaseTimer     = 0f; // triggers re-init next frame
            }
        }
    }

    private void SpawnSkeletonProjectile(Entity monster, Vec2 direction)
    {
        Vec2 origin = monster.Get<TransformComponent>().Position;

        var proj = World.CreateEntity();
        proj.Add(new TransformComponent { Position = origin, Rotation = direction.Angle() });
        proj.Add(new VelocityComponent
        {
            Velocity = direction * MonsterData.SkeletonProjectileSpeed,
            Speed    = MonsterData.SkeletonProjectileSpeed
        });
        proj.Add(new MonsterProjectileComponent
        {
            Damage       = MonsterData.SkeletonProjectileDamage,
            OwnerId      = monster.Id,
            Acceleration = 0f
        });
        proj.Add(new ColliderComponent
        {
            Radius = 5f,
            Layer  = CollisionLayers.MonsterArrow,
            Mask   = CollisionLayers.Player
        });
    }

    // ─── Elite — ranged, 2-4 accelerating arrows in fan spread ────────────────

    private void UpdateEliteRanged(Entity monster, VelocityComponent velocity, MonsterAIState ai,
        Vec2 toPlayer, float baseSpeed, float speedMul, float delta)
    {
        if (ai == null) { velocity.Velocity = toPlayer * baseSpeed * speedMul; return; }

        if (ai.RangedPhase == RangedPhase.Wander)
        {
            if (ai.PhaseTimer <= 0f)
            {
                Vec2  perp = new Vec2(-toPlayer.Y, toPlayer.X);
                float bias = (GameRandom.Randf() * 2f - 1f) * MonsterData.RangedLateralBias;
                ai.WanderDir  = (toPlayer + perp * bias).Normalized();
                ai.PhaseTimer = MonsterData.EliteWanderDuration;
            }

            velocity.Velocity  = ai.WanderDir * baseSpeed * speedMul;
            ai.PhaseTimer     -= delta;

            if (ai.PhaseTimer <= 0f)
            {
                ai.RangedPhase    = RangedPhase.Attack;
                ai.PhaseTimer     = MonsterData.EliteAttackDuration;
                ai.FiredThisCycle = false;
            }
        }
        else // RangedPhase.Attack
        {
            velocity.Velocity  = Vec2.Zero;
            ai.PhaseTimer     -= delta;

            if (ai.PhaseTimer <= 0f && !ai.FiredThisCycle)
            {
                SpawnEliteProjectiles(monster, toPlayer);
                ai.FiredThisCycle = true;
                ai.RangedPhase    = RangedPhase.Wander;
                ai.PhaseTimer     = 0f;
            }
        }
    }

    private void SpawnEliteProjectiles(Entity monster, Vec2 toPlayer)
    {
        Vec2 origin = monster.Get<TransformComponent>().Position;

        // 2-4 arrows, random count each burst
        int count = GameRandom.Next(
            MonsterData.EliteProjectileMaxCount - MonsterData.EliteProjectileMinCount + 1
        ) + MonsterData.EliteProjectileMinCount;

        for (int i = 0; i < count; i++)
        {
            // Fan spread: center the burst on toPlayer direction
            float offsetDeg = (i - (count - 1) / 2.0f) * MonsterData.EliteProjectileSpreadDeg;
            Vec2  dir       = toPlayer.Rotated(GMath.DegToRad(offsetDeg));

            var proj = World.CreateEntity();
            proj.Add(new TransformComponent { Position = origin, Rotation = dir.Angle() });
            proj.Add(new VelocityComponent
            {
                Velocity = dir * MonsterData.EliteProjectileInitSpeed,
                Speed    = MonsterData.EliteProjectileInitSpeed
            });
            proj.Add(new MonsterProjectileComponent
            {
                Damage       = MonsterData.EliteProjectileDamage,
                OwnerId      = monster.Id,
                Acceleration = MonsterData.EliteProjectileAccel
            });
            proj.Add(new ColliderComponent
            {
                Radius = 5f,
                Layer  = CollisionLayers.MonsterArrow,
                Mask   = CollisionLayers.Player
            });
        }
    }

    // ─── Orc — unchanged ──────────────────────────────────────────────────────

    private void UpdateOrc(VelocityComponent velocity, MonsterAIState ai,
        Vec2 toPlayer, float distToPlayer, float baseSpeed, float speedMul, float delta)
    {
        if (ai == null) { velocity.Velocity = toPlayer * baseSpeed * speedMul; return; }

        if (ai.IsStunned)
        {
            velocity.Velocity  = Vec2.Zero;
            ai.StunTimer      -= delta;
            if (ai.StunTimer <= 0)
            {
                ai.IsStunned  = false;
                ai.IsCharging = false;
            }
            return;
        }

        if (ai.IsCharging)
        {
            velocity.Velocity = toPlayer * MonsterData.OrcChargeSpeed * speedMul;
            if (distToPlayer < 30f)
            {
                ai.IsCharging     = false;
                ai.IsStunned      = true;
                ai.StunTimer      = MonsterData.OrcStunDuration;
                velocity.Velocity = Vec2.Zero;
            }
            return;
        }

        velocity.Velocity = toPlayer * baseSpeed * speedMul;
        if (distToPlayer <= MonsterData.OrcChargeRange)
            ai.IsCharging = true;
    }
}
```

- [ ] **Step 2: 提交**

```bash
git add client/Scripts/Ecs/Systems/MonsterAISystem.cs
git commit -m "feat: replace Skeleton/Elite melee AI with ranged wander-attack state machine"
```

---

## Task 5: 更新 `MovementSystem` — MonsterProjectile 加速 + 越界销毁

**Files:**
- Modify: `client/Scripts/Ecs/Systems/MovementSystem.cs`

- [ ] **Step 1: 用以下内容完整替换 `MovementSystem.cs`**

```csharp
using Game.Ecs.Core;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.Systems;

/// <summary>
/// Updates entity positions based on velocity and handles arena boundary rules.
/// - Players are clamped inside the arena.
/// - Player arrows (ArrowComponent) are destroyed outside boundary.
/// - Monster projectiles (MonsterProjectileComponent) apply acceleration (if any) then are destroyed outside boundary.
/// - Monsters roam freely (spawn outside and walk in).
/// </summary>
public class MovementSystem : GameSystem
{
    private const float PlayerRadius = 16f;
    private const float ArrowMargin  = 64f;

    public override void Update(float delta)
    {
        var entities = World.GetEntitiesWith<TransformComponent, VelocityComponent>();

        foreach (var entity in entities)
        {
            if (!entity.IsAlive) continue;

            var transform = entity.Get<TransformComponent>();
            var velocity  = entity.Get<VelocityComponent>();

            // 1. Move
            transform.Position += velocity.Velocity * delta;

            // 2. Per-type post-move rules
            if (entity.Has<PlayerComponent>())
            {
                transform.Position = new Vec2(
                    GMath.Clamp(transform.Position.X, PlayerRadius, ArenaData.Size.X - PlayerRadius),
                    GMath.Clamp(transform.Position.Y, PlayerRadius, ArenaData.Size.Y - PlayerRadius)
                );
            }
            else if (entity.Has<ArrowComponent>())
            {
                if (IsOutsideArena(transform.Position))
                    World.DestroyEntity(entity.Id);
            }
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
            // Monsters: no clamping — they spawn outside and move in.
        }
    }

    private bool IsOutsideArena(Vec2 pos) =>
        pos.X < -ArrowMargin || pos.X > ArenaData.Size.X + ArrowMargin ||
        pos.Y < -ArrowMargin || pos.Y > ArenaData.Size.Y + ArrowMargin;
}
```

- [ ] **Step 2: 提交**

```bash
git add client/Scripts/Ecs/Systems/MovementSystem.cs
git commit -m "feat: apply acceleration and arena-boundary cleanup for MonsterProjectile in MovementSystem"
```

---

## Task 6: 在 `CollisionSystem` 中添加怪物投射物 vs 玩家碰撞检测

**Files:**
- Modify: `client/Scripts/Ecs/Systems/CollisionSystem.cs`

- [ ] **Step 1: 在 `Update` 方法中新增调用，并在文件末尾添加 `CheckMonsterProjectileVsPlayer` 方法**

将：
```csharp
    public override void Update(float delta)
    {
        Hits.Clear();

        CheckArrowVsMonster();
        CheckMonsterVsPlayer();
    }
```
改为：
```csharp
    public override void Update(float delta)
    {
        Hits.Clear();

        CheckArrowVsMonster();
        CheckMonsterVsPlayer();
        CheckMonsterProjectileVsPlayer();
    }
```

然后在 `CheckMonsterVsPlayer()` 方法之后、类结束括号 `}` 之前插入以下方法：

```csharp
    private void CheckMonsterProjectileVsPlayer()
    {
        var projectiles = World.GetEntitiesWith<MonsterProjectileComponent, TransformComponent, ColliderComponent>();
        var players     = World.GetEntitiesWith<PlayerComponent, TransformComponent, ColliderComponent>();

        foreach (var projEntity in projectiles)
        {
            if (!projEntity.IsAlive) continue;

            var projTransform = projEntity.Get<TransformComponent>();
            var projCollider  = projEntity.Get<ColliderComponent>();
            var projComp      = projEntity.Get<MonsterProjectileComponent>();

            foreach (var playerEntity in players)
            {
                if (!playerEntity.IsAlive) continue;

                var playerTransform = playerEntity.Get<TransformComponent>();
                var playerCollider  = playerEntity.Get<ColliderComponent>();

                float dist = projTransform.Position.DistanceTo(playerTransform.Position);
                if (dist > projCollider.Radius + playerCollider.Radius) continue;

                // Register hit with IsArrow=false so DamageSystem applies shield/invincible checks
                Hits.Add(new HitEvent(projEntity.Id, playerEntity.Id, projComp.Damage, false));
                World.DestroyEntity(projEntity.Id); // consumed on first hit
                break; // projectile is gone; skip remaining players
            }
        }
    }
```

- [ ] **Step 2: 提交**

```bash
git add client/Scripts/Ecs/Systems/CollisionSystem.cs
git commit -m "feat: add CheckMonsterProjectileVsPlayer to CollisionSystem"
```

---

## 验证步骤

运行游戏后按以下要点确认：

1. **Skeleton 不再近战**：Wave 2 出现后，Skeleton 应停下来射箭，而非一直冲向玩家。
2. **Skeleton 单发匀速箭**：每次射击恰好 1 发，子弹飞行速度均匀无加速。
3. **Elite 不再近战**：Wave 4 出现的 Elite 停下来后射出扇形多发子弹。
4. **Elite 多发加速**：每次攻击射出 2-4 发子弹，子弹越飞越快（明显加速感）。
5. **护盾/无敌有效**：玩家有护盾时，怪物子弹应被吸收（不扣 HP）；无敌状态下子弹打到玩家无效。
6. **Orc/Slime/Boss 不受影响**：行为与修改前一致。
7. **子弹越界销毁**：飞出场地边界的怪物子弹不积累（无内存泄漏）。
