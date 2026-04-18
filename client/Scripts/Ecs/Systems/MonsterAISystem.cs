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
                    UpdateOrc(monster, velocity, ai, toPlayer, nearestPos, nearestDist, baseSpeed, speedMultiplier, delta);
                    break;
                case MonsterType.Boss:
                    // Speed is overridden by BossAISystem; just set direction
                    float bossRadius = monster.Get<ColliderComponent>()?.Radius ?? 40f;
                    Vec2 bossDetour = ApplyDetourMemory(ai, monsterTransform.Position, nearestPos, toPlayer, bossRadius, delta);
                    Vec2 bossDir = AdjustForObstacles(monsterTransform.Position, bossDetour, velocity.Speed * speedMultiplier, delta, bossRadius);
                    velocity.Velocity = bossDir * velocity.Speed * speedMultiplier;
                    break;
                default:
                    // Slime: straight chase
                    float slimeRadius = monster.Get<ColliderComponent>()?.Radius ?? 15f;
                    Vec2 slimeDetour = ApplyDetourMemory(ai, monsterTransform.Position, nearestPos, toPlayer, slimeRadius, delta);
                    Vec2 slimeDir = AdjustForObstacles(monsterTransform.Position, slimeDetour, baseSpeed * speedMultiplier, delta, slimeRadius);
                    velocity.Velocity = slimeDir * baseSpeed * speedMultiplier;
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

            float skelRadius = monster.Get<ColliderComponent>()?.Radius ?? 18f;
            Vec2 skelDir = AdjustForObstacles(monster.Get<TransformComponent>().Position, ai.WanderDir, baseSpeed * speedMul, delta, skelRadius);
            velocity.Velocity = skelDir * baseSpeed * speedMul;
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
            Damage    = MonsterData.SkeletonProjectileDamage,
            OwnerId   = monster.Id,
            IsHoming  = false,
            LifeTimer = 0f
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

            float eliteRadius = monster.Get<ColliderComponent>()?.Radius ?? 25f;
            Vec2 eliteDir = AdjustForObstacles(monster.Get<TransformComponent>().Position, ai.WanderDir, baseSpeed * speedMul, delta, eliteRadius);
            velocity.Velocity = eliteDir * baseSpeed * speedMul;
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
            proj.Add(new ColliderComponent
            {
                Radius = 5f,
                Layer = CollisionLayers.MonsterArrow,
                Mask = CollisionLayers.Player
            });
        }
    }

    // ─── Orc — accelerating dash toward player ─────────────────────────────────

    private void UpdateOrc(Entity monster, VelocityComponent velocity, MonsterAIState ai,
        Vec2 toPlayer, Vec2 nearestPos, float distToPlayer, float baseSpeed, float speedMul, float delta)
    {
        if (ai == null) { velocity.Velocity = toPlayer * baseSpeed * speedMul; return; }

        // If currently dashing, accelerate from 0 to peak speed
        if (ai.IsDashing)
        {
            ai.DashTimer += delta;
            if (ai.DashTimer >= ai.DashInterval)
            {
                // End dash, set new random interval
                ai.IsDashing = false;
                ai.DashTimer = 0f;
                ai.DashInterval = MonsterData.OrcDashIntervalMin
                    + GameRandom.Randf() * (MonsterData.OrcDashIntervalMax - MonsterData.OrcDashIntervalMin);
            }
            else
            {
                // Accelerating dash: speed ramps from 0 → peakSpeed over DashDuration
                float progress = ai.DashTimer / ai.DashInterval;
                float currentSpeed = MonsterData.OrcDashSpeed * progress * speedMul;
                float orcRadius = monster.Get<ColliderComponent>()?.Radius ?? 22f;
                Vec2 dashDir = AdjustForObstacles(monster.Get<TransformComponent>().Position, toPlayer,
                    currentSpeed, delta, orcRadius);
                velocity.Velocity = dashDir * currentSpeed;
            }
        }
        else
        {
            // Countdown to next dash
            ai.DashInterval -= delta;
            if (ai.DashInterval <= 0f)
            {
                // Start dash with random duration; store it in DashInterval (reused for duration countdown)
                ai.IsDashing = true;
                ai.DashTimer = 0f;
                ai.DashInterval = MonsterData.OrcDashDurationMin
                    + GameRandom.Randf() * (MonsterData.OrcDashDurationMax - MonsterData.OrcDashDurationMin);
                // Note: above DashInterval value is now dash duration; after dash ends it will be reset to interval 2-7s
            }
            else
            {
                // Normal chase at base speed
                float orcRadius = monster.Get<ColliderComponent>()?.Radius ?? 22f;
                Vec2 orcDir = AdjustForObstacles(monster.Get<TransformComponent>().Position, toPlayer,
                    baseSpeed * speedMul, delta, orcRadius);
                velocity.Velocity = orcDir * baseSpeed * speedMul;
            }
        }
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

    // ─── Obstacle avoidance ──────────────────────────────────────────────────

    /// <summary>
    /// If moving in desiredDir would overlap an obstacle, try ±90° to slide around it.
    /// </summary>
    private Vec2 AdjustForObstacles(Vec2 pos, Vec2 desiredDir, float speed, float delta, float entityRadius)
    {
        if (desiredDir == Vec2.Zero) return desiredDir;

        var obstacles = World.GetEntitiesWith<ObstacleComponent, TransformComponent, ColliderComponent>();
        if (obstacles.Count == 0) return desiredDir;

        Vec2 predictedPos = pos + desiredDir * speed * delta;

        foreach (var obs in obstacles)
        {
            var ot = obs.Get<TransformComponent>();
            var oc = obs.Get<ColliderComponent>();

            float dx = GMath.Abs(predictedPos.X - ot.Position.X);
            float dy = GMath.Abs(predictedPos.Y - ot.Position.Y);
            if (dx < entityRadius + oc.HalfWidth && dy < entityRadius + oc.HalfHeight)
            {
                Vec2 left = desiredDir.Rotated(GMath.Pi * 0.5f);
                Vec2 right = desiredDir.Rotated(-GMath.Pi * 0.5f);

                bool leftBlocked = IsBlockedByObstacle(pos + left * speed * delta, entityRadius, obstacles);
                bool rightBlocked = IsBlockedByObstacle(pos + right * speed * delta, entityRadius, obstacles);

                if (!leftBlocked && !rightBlocked)
                {
                    float leftDot = left.X * desiredDir.X + left.Y * desiredDir.Y;
                    float rightDot = right.X * desiredDir.X + right.Y * desiredDir.Y;
                    return leftDot >= rightDot ? left : right;
                }
                if (!leftBlocked) return left;
                if (!rightBlocked) return right;
                return Vec2.Zero;
            }
        }

        return desiredDir;
    }

    private static bool IsBlockedByObstacle(Vec2 pos, float radius, System.Collections.Generic.List<Entity> obstacles)
    {
        foreach (var obs in obstacles)
        {
            var ot = obs.Get<TransformComponent>();
            var oc = obs.Get<ColliderComponent>();
            float dx = GMath.Abs(pos.X - ot.Position.X);
            float dy = GMath.Abs(pos.Y - ot.Position.Y);
            if (dx < radius + oc.HalfWidth && dy < radius + oc.HalfHeight)
                return true;
        }
        return false;
    }

    // ─── Detour memory — path obstruction detection & bypass ──────────────────

    private const float DetourTimeout = 2f;

    /// <summary>
    /// AABB line-segment intersection: checks if the straight path from start to end
    /// is blocked by any obstacle (expanded by entityRadius via Minkowski sum).
    /// Returns the blocking obstacle's position if blocked, or null if clear.
    /// </summary>
    private Vec2? FindBlockingObstacle(Vec2 start, Vec2 end, float entityRadius)
    {
        var obstacles = World.GetEntitiesWith<ObstacleComponent, TransformComponent, ColliderComponent>();
        Vec2 dir = end - start;

        foreach (var obs in obstacles)
        {
            var ot = obs.Get<TransformComponent>();
            var oc = obs.Get<ColliderComponent>();

            // Expanded AABB (Minkowski sum with entity circle)
            float hw = oc.HalfWidth + entityRadius;
            float hh = oc.HalfHeight + entityRadius;
            float minX = ot.Position.X - hw;
            float maxX = ot.Position.X + hw;
            float minY = ot.Position.Y - hh;
            float maxY = ot.Position.Y + hh;

            // Slab intersection test (tMin/tMax reset per obstacle)
            if (RayIntersectsAABB(start, dir, minX, maxX, minY, maxY, 0f, 1f))
                return ot.Position;
        }
        return null;
    }

    private static bool RayIntersectsAABB(Vec2 origin, Vec2 dir, float minX, float maxX, float minY, float maxY, float tMin, float tMax)
    {
        // X slab
        if (GMath.Abs(dir.X) < 1e-6f)
        {
            if (origin.X < minX || origin.X > maxX) return false;
        }
        else
        {
            float invD = 1f / dir.X;
            float t1 = (minX - origin.X) * invD;
            float t2 = (maxX - origin.X) * invD;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = t1 > tMin ? t1 : tMin;
            tMax = t2 < tMax ? t2 : tMax;
            if (tMin > tMax) return false;
        }

        // Y slab
        if (GMath.Abs(dir.Y) < 1e-6f)
        {
            if (origin.Y < minY || origin.Y > maxY) return false;
        }
        else
        {
            float invD = 1f / dir.Y;
            float t1 = (minY - origin.Y) * invD;
            float t2 = (maxY - origin.Y) * invD;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = t1 > tMin ? t1 : tMin;
            tMax = t2 < tMax ? t2 : tMax;
            if (tMin > tMax) return false;
        }

        return true;
    }

    /// <summary>
    /// Choose detour direction based on obstacle position relative to monster→player line.
    /// Cross product sign determines which side to go around.
    /// </summary>
    private Vec2 GetDetourDirection(Vec2 toPlayer, Vec2 monsterPos, Vec2 obstaclePos, float entityRadius)
    {
        Vec2 toObs = obstaclePos - monsterPos;
        // cross = toPlayer.X * toObs.Y - toPlayer.Y * toObs.X
        float cross = toPlayer.X * toObs.Y - toPlayer.Y * toObs.X;

        var obstacles = World.GetEntitiesWith<ObstacleComponent, TransformComponent, ColliderComponent>();

        // Try ±45° first (keeps forward progress toward player while bypassing)
        Vec2 primary45 = cross >= 0
            ? toPlayer.Rotated(-GMath.Pi * 0.25f)
            : toPlayer.Rotated(GMath.Pi * 0.25f);
        if (!IsBlockedByObstacle(monsterPos + primary45 * 30f, entityRadius, obstacles))
            return primary45;

        Vec2 secondary45 = cross >= 0
            ? toPlayer.Rotated(GMath.Pi * 0.25f)
            : toPlayer.Rotated(-GMath.Pi * 0.25f);
        if (!IsBlockedByObstacle(monsterPos + secondary45 * 30f, entityRadius, obstacles))
            return secondary45;

        // Fallback: ±90° (pure lateral slide)
        Vec2 primary90 = cross >= 0
            ? toPlayer.Rotated(-GMath.Pi * 0.5f)
            : toPlayer.Rotated(GMath.Pi * 0.5f);
        if (!IsBlockedByObstacle(monsterPos + primary90 * 30f, entityRadius, obstacles))
            return primary90;

        Vec2 secondary90 = cross >= 0
            ? toPlayer.Rotated(GMath.Pi * 0.5f)
            : toPlayer.Rotated(-GMath.Pi * 0.5f);
        if (!IsBlockedByObstacle(monsterPos + secondary90 * 30f, entityRadius, obstacles))
            return secondary90;

        // Last resort: ±135°
        Vec2 wide1 = toPlayer.Rotated(GMath.Pi * 0.75f);
        if (!IsBlockedByObstacle(monsterPos + wide1 * 30f, entityRadius, obstacles))
            return wide1;
        Vec2 wide2 = toPlayer.Rotated(-GMath.Pi * 0.75f);
        if (!IsBlockedByObstacle(monsterPos + wide2 * 30f, entityRadius, obstacles))
            return wide2;

        return Vec2.Zero; // completely stuck
    }

    /// <summary>
    /// Apply detour memory: if path to player is blocked, commit to a bypass direction
    /// until the path is clear or the timer expires.
    /// Returns the adjusted movement direction (still needs AdjustForObstacles as safety net).
    /// </summary>
    private Vec2 ApplyDetourMemory(MonsterAIState ai, Vec2 monsterPos, Vec2 nearestPos, Vec2 toPlayer, float entityRadius, float delta)
    {
        var blocking = FindBlockingObstacle(monsterPos, nearestPos, entityRadius);

        if (blocking == null)
        {
            // Path is clear — stop detouring
            ai.IsDetouring = false;
            return toPlayer;
        }

        // Path is blocked
        if (!ai.IsDetouring)
        {
            // Enter detour mode
            ai.IsDetouring = true;
            ai.DetourDir = GetDetourDirection(toPlayer, monsterPos, blocking.Value, entityRadius);
            ai.DetourTimer = DetourTimeout;
        }
        else
        {
            ai.DetourTimer -= delta;
            if (ai.DetourTimer <= 0f)
            {
                // Re-evaluate direction
                ai.DetourDir = GetDetourDirection(toPlayer, monsterPos, blocking.Value, entityRadius);
                ai.DetourTimer = DetourTimeout;
            }
        }

        return ai.DetourDir;
    }
}
