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
        var players = World.GetEntitiesWith<PlayerComponent, TransformComponent>();

        if (players.Count == 0) return;

        foreach (var monster in monsters)
        {
            if (!monster.IsAlive) continue;
            if (monster.Has<DeathPendingComponent>()) continue;

            var monsterComp = monster.Get<MonsterComponent>();
            var monsterTransform = monster.Get<TransformComponent>();
            var velocity = monster.Get<VelocityComponent>();

            // Find nearest alive player
            float nearestDist = float.MaxValue;
            Vec2 nearestPos = Vec2.Zero;
            int nearestId = -1;

            foreach (var player in players)
            {
                var playerHealth = player.Get<HealthComponent>();
                if (playerHealth != null && playerHealth.Hp <= 0) continue;

                var playerTransform = player.Get<TransformComponent>();
                float dist = monsterTransform.Position.DistanceTo(playerTransform.Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestPos = playerTransform.Position;
                    nearestId = player.Id;
                }
            }

            if (nearestDist >= float.MaxValue)
            {
                velocity.Velocity = Vec2.Zero;
                continue;
            }

            // Ensure AI state exists before writing TargetId
            var ai = monster.Get<MonsterAIState>();
            if (ai == null)
            {
                ai = new MonsterAIState();
                monster.Add(ai);
            }

            // 记录当前锁定的目标玩家 ID（供 RenderSystem 翻转判定用）
            ai.TargetId = nearestId;

            Vec2 toPlayer = (nearestPos - monsterTransform.Position).Normalized();

            // Apply freeze slow
            float baseSpeed = velocity.Speed;
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
                var monsterPos = monster.Get<TransformComponent>().Position;
                ai.WanderDir = GetWanderDirWithBoundaryCheck(monsterPos, 150f);
                ai.PhaseTimer = MonsterData.SkeletonWanderDuration;
                ai.FiredThisCycle = false;
            }

            velocity.Velocity = ai.WanderDir * baseSpeed * speedMul;
            ai.PhaseTimer -= delta;

            if (ai.PhaseTimer <= 0f)
            {
                ai.RangedPhase = RangedPhase.Attack;
                ai.PhaseTimer = MonsterData.SkeletonAttackDuration;
                ai.FiredThisCycle = false;
            }
        }
        else // RangedPhase.Attack
        {
            velocity.Velocity = Vec2.Zero; // stop while aiming
            // Freeze only reduces move speed; attack timing is unaffected by design.
            ai.PhaseTimer -= delta;

            if (ai.PhaseTimer <= 0f && !ai.FiredThisCycle)
            {
                SpawnSkeletonProjectile(monster, toPlayer);
                ai.FiredThisCycle = true;
                ai.RangedPhase = RangedPhase.Wander;
                ai.PhaseTimer = 0f; // triggers re-init next frame
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
            Speed = MonsterData.SkeletonProjectileSpeed
        });
        proj.Add(new MonsterProjectileComponent
        {
            Damage = MonsterData.SkeletonProjectileDamage,
            OwnerId = monster.Id,
            Acceleration = 0f
        });
        proj.Add(new ColliderComponent
        {
            Radius = 5f,
            Layer = CollisionLayers.MonsterArrow,
            Mask = CollisionLayers.Player
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
                var monsterPos = monster.Get<TransformComponent>().Position;
                ai.WanderDir = GetWanderDirWithBoundaryCheck(monsterPos, 150f);
                ai.PhaseTimer = MonsterData.EliteWanderDuration;
                ai.FiredThisCycle = false;
            }

            velocity.Velocity = ai.WanderDir * baseSpeed * speedMul;
            ai.PhaseTimer -= delta;

            if (ai.PhaseTimer <= 0f)
            {
                ai.RangedPhase = RangedPhase.Attack;
                ai.PhaseTimer = MonsterData.EliteAttackDuration;
                ai.FiredThisCycle = false;
            }
        }
        else // RangedPhase.Attack
        {
            velocity.Velocity = Vec2.Zero;
            // Freeze only reduces move speed; attack timing is unaffected by design.
            ai.PhaseTimer -= delta;

            if (ai.PhaseTimer <= 0f && !ai.FiredThisCycle)
            {
                SpawnEliteProjectiles(monster, toPlayer);
                ai.FiredThisCycle = true;
                ai.RangedPhase = RangedPhase.Wander;
                ai.PhaseTimer = 0f;
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
            // 2.0f ensures float division so the spread is symmetrically centered on toPlayer.
            float offsetDeg = (i - (count - 1) / 2.0f) * MonsterData.EliteProjectileSpreadDeg;
            Vec2 dir = toPlayer.Rotated(GMath.DegToRad(offsetDeg));

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
            proj.Add(new ColliderComponent
            {
                Radius = 5f,
                Layer = CollisionLayers.MonsterArrow,
                Mask = CollisionLayers.Player
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
            velocity.Velocity = Vec2.Zero;
            ai.StunTimer -= delta;
            if (ai.StunTimer <= 0)
            {
                ai.IsStunned = false;
                ai.IsCharging = false;
            }
            return;
        }

        if (ai.IsCharging)
        {
            velocity.Velocity = toPlayer * MonsterData.OrcChargeSpeed * speedMul;
            if (distToPlayer < 30f)
            {
                ai.IsCharging = false;
                ai.IsStunned = true;
                ai.StunTimer = MonsterData.OrcStunDuration;
                velocity.Velocity = Vec2.Zero;
            }
            return;
        }

        velocity.Velocity = toPlayer * baseSpeed * speedMul;
        if (distToPlayer <= MonsterData.OrcChargeRange)
            ai.IsCharging = true;
    }

    // ─── Boundary-aware wander direction ────────────────────────────────────

    /// <summary>
    /// 边界检查：靠近边界时返回指向中心的方向，否则返回随机方向。
    /// </summary>
    private Vec2 GetWanderDirWithBoundaryCheck(Vec2 monsterPos, float margin)
    {
        float left   = margin;
        float right  = ArenaData.Size.X - margin;
        float top    = margin;
        float bottom = ArenaData.Size.Y - margin;

        bool nearLeft   = monsterPos.X < left;
        bool nearRight  = monsterPos.X > right;
        bool nearTop    = monsterPos.Y < top;
        bool nearBottom = monsterPos.Y > bottom;

        if (!nearLeft && !nearRight && !nearTop && !nearBottom)
        {
            // 场景中央，随机游荡
            float angle = GameRandom.Randf() * GMath.Tau;
            return new Vec2(GMath.Cos(angle), GMath.Sin(angle));
        }

        // 向中心方向偏移，叠加一定随机性
        Vec2 toCenter = ((ArenaData.Size * 0.5f) - monsterPos).Normalized();
        float randAngle = GameRandom.Randf() * GMath.Pi * 0.5f - GMath.Pi * 0.25f; // ±45° 随机扰动
        return toCenter.Rotated(randAngle);
    }
}
