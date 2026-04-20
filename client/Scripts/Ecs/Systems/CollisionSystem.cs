using System;
using System.Collections.Generic;
using Game.Ecs.Core;
using Game.Data;
using Game.Ecs.Components;

namespace Game.Ecs.Systems;

public class CollisionSystem : GameSystem
{
    public record HitEvent(int AttackerId, int DefenderId, int Damage, bool IsArrow);

    public List<HitEvent> Hits = new();

    /// <summary>OrbitSystem and other non-collision sources add hits here so damage numbers can display.</summary>
    public void AddOrbitHit(int defenderId, int damage)
    {
        Hits.Add(new HitEvent(-1, defenderId, damage, true)); // attackerId=-1 signals orbit
    }

    public override void Update(float delta)
    {
        Hits.Clear();

        CheckArrowVsMonster();
        CheckMonsterVsPlayer();
        CheckMonsterProjectileVsPlayer();
        CheckProjectileVsObstacle();
        CheckAttackHitbox();
    }

    // ── Unified overlap test (Circle / OBB) ──────────────────────────

    /// <summary>
    /// Returns true if the two colliders overlap. Supports all combinations of
    /// Circle and Box (OBB) shapes.
    /// </summary>
    public static bool Overlaps(
        ColliderComponent a, TransformComponent ta,
        ColliderComponent b, TransformComponent tb)
    {
        if (a.Shape == ColliderShape.Circle && b.Shape == ColliderShape.Circle)
            return CircleVsCircle(ta.Position, a.Radius, tb.Position, b.Radius);

        if (a.Shape == ColliderShape.Circle && b.Shape == ColliderShape.Box)
            return CircleVsOBB(ta.Position, a.Radius, tb.Position, tb.Rotation, b.HalfWidth, b.HalfHeight);

        if (a.Shape == ColliderShape.Box && b.Shape == ColliderShape.Circle)
            return CircleVsOBB(tb.Position, b.Radius, ta.Position, ta.Rotation, a.HalfWidth, a.HalfHeight);

        // Box vs Box → SAT with 4 axes (2 per OBB)
        return OBBvsOBB(
            ta.Position, ta.Rotation, a.HalfWidth, a.HalfHeight,
            tb.Position, tb.Rotation, b.HalfWidth, b.HalfHeight);
    }

    private static bool CircleVsCircle(Vec2 posA, float rA, Vec2 posB, float rB)
    {
        float dx = posA.X - posB.X;
        float dy = posA.Y - posB.Y;
        float sumR = rA + rB;
        return dx * dx + dy * dy <= sumR * sumR;
    }

    private static bool CircleVsOBB(
        Vec2 circlePos, float radius,
        Vec2 boxPos, float boxRot, float hw, float hh)
    {
        // Transform circle center into box-local space
        float cos = MathF.Cos(-boxRot);
        float sin = MathF.Sin(-boxRot);
        float dx = circlePos.X - boxPos.X;
        float dy = circlePos.Y - boxPos.Y;
        float localX = dx * cos - dy * sin;
        float localY = dx * sin + dy * cos;

        // Clamp to box extents to find closest point
        float closestX = GMath.Clamp(localX, -hw, hw);
        float closestY = GMath.Clamp(localY, -hh, hh);

        float ex = localX - closestX;
        float ey = localY - closestY;
        return ex * ex + ey * ey <= radius * radius;
    }

    private static bool OBBvsOBB(
        Vec2 posA, float rotA, float hwA, float hhA,
        Vec2 posB, float rotB, float hwB, float hhB)
    {
        // 4 axes: 2 from each OBB's local X and Y
        float cosA = MathF.Cos(rotA), sinA = MathF.Sin(rotA);
        float cosB = MathF.Cos(rotB), sinB = MathF.Sin(rotB);

        // Local axes
        float ax0X = cosA, ax0Y = sinA;   // A local X
        float ax1X = -sinA, ax1Y = cosA;  // A local Y
        float bx0X = cosB, bx0Y = sinB;   // B local X
        float bx1X = -sinB, bx1Y = cosB;  // B local Y

        float tx = posB.X - posA.X;
        float ty = posB.Y - posA.Y;

        // Test each of the 4 separating axes
        // Axis = A local X
        if (SATSeparated(ax0X, ax0Y, tx, ty, hwA, hhA, hwB, hhB,
            ax0X, ax0Y, ax1X, ax1Y, bx0X, bx0Y, bx1X, bx1Y)) return false;
        // Axis = A local Y
        if (SATSeparated(ax1X, ax1Y, tx, ty, hwA, hhA, hwB, hhB,
            ax0X, ax0Y, ax1X, ax1Y, bx0X, bx0Y, bx1X, bx1Y)) return false;
        // Axis = B local X
        if (SATSeparated(bx0X, bx0Y, tx, ty, hwA, hhA, hwB, hhB,
            ax0X, ax0Y, ax1X, ax1Y, bx0X, bx0Y, bx1X, bx1Y)) return false;
        // Axis = B local Y
        if (SATSeparated(bx1X, bx1Y, tx, ty, hwA, hhA, hwB, hhB,
            ax0X, ax0Y, ax1X, ax1Y, bx0X, bx0Y, bx1X, bx1Y)) return false;

        return true; // No separating axis found → overlapping
    }

    /// <summary>Returns true if the given axis separates the two OBBs.</summary>
    private static bool SATSeparated(
        float axisX, float axisY,
        float tx, float ty,
        float hwA, float hhA, float hwB, float hhB,
        float ax0X, float ax0Y, float ax1X, float ax1Y,
        float bx0X, float bx0Y, float bx1X, float bx1Y)
    {
        float projT = MathF.Abs(tx * axisX + ty * axisY);
        float projA = hwA * MathF.Abs(ax0X * axisX + ax0Y * axisY)
                    + hhA * MathF.Abs(ax1X * axisX + ax1Y * axisY);
        float projB = hwB * MathF.Abs(bx0X * axisX + bx0Y * axisY)
                    + hhB * MathF.Abs(bx1X * axisX + bx1Y * axisY);
        return projT > projA + projB;
    }

    // ── Collision checks ─────────────────────────────────────────────

    private void CheckArrowVsMonster()
    {
        var arrows = World.GetEntitiesWith<ArrowComponent, TransformComponent, ColliderComponent>();
        var monsters = World.GetEntitiesWith<MonsterComponent, TransformComponent, ColliderComponent>();

        foreach (var arrowEntity in arrows)
        {
            if (!arrowEntity.IsAlive) continue;

            var arrowTransform = arrowEntity.Get<TransformComponent>();
            var arrowCollider = arrowEntity.Get<ColliderComponent>();
            var arrowComp = arrowEntity.Get<ArrowComponent>();

            var alreadyHit = new HashSet<int>();

            foreach (var monsterEntity in monsters)
            {
                if (!monsterEntity.IsAlive) continue;
                if (alreadyHit.Contains(monsterEntity.Id)) continue;
                if (monsterEntity.Has<DeathPendingComponent>()) continue;

                var monsterTransform = monsterEntity.Get<TransformComponent>();
                var monsterCollider = monsterEntity.Get<ColliderComponent>();

                if (!Overlaps(arrowCollider, arrowTransform, monsterCollider, monsterTransform)) continue;

                alreadyHit.Add(monsterEntity.Id);
                Hits.Add(new HitEvent(arrowEntity.Id, monsterEntity.Id, arrowComp.Damage, true));

                if (arrowComp.PierceCount > 0)
                {
                    arrowComp.PierceCount--;
                }
                else
                {
                    World.DestroyEntity(arrowEntity.Id);
                    break;
                }
            }
        }
    }

    private void CheckMonsterVsPlayer()
    {
        var monsters = World.GetEntitiesWith<MonsterComponent, TransformComponent, ColliderComponent>();
        var players = World.GetEntitiesWith<PlayerComponent, TransformComponent, ColliderComponent>();

        int waveNum = 1;
        var waveEntities = World.GetEntitiesWith<WaveComponent>();
        if (waveEntities.Count > 0)
        {
            var waveComp = waveEntities[0].Get<WaveComponent>();
            if (waveComp.CurrentWave > 0)
                waveNum = waveComp.CurrentWave;
        }

        foreach (var monsterEntity in monsters)
        {
            if (!monsterEntity.IsAlive) continue;
            if (monsterEntity.Has<DeathPendingComponent>()) continue;

            var monsterTransform = monsterEntity.Get<TransformComponent>();
            var monsterCollider = monsterEntity.Get<ColliderComponent>();
            var monsterComp = monsterEntity.Get<MonsterComponent>();

            // MeleeAttackComponent monsters: skip if in windup (no damage during animation)
            // Allow collision damage during cooldown — handled by MeleeAttackSystem timing
            if (monsterEntity.Has<MeleeAttackComponent>())
            {
                var melee = monsterEntity.Get<MeleeAttackComponent>();
                if (melee.AttackWindupTimer > 0f) continue; // In windup, no damage
                // In cooldown: allow collision damage below
            }

            foreach (var playerEntity in players)
            {
                if (!playerEntity.IsAlive) continue;

                var playerTransform = playerEntity.Get<TransformComponent>();
                var playerCollider = playerEntity.Get<ColliderComponent>();

                if (!Overlaps(monsterCollider, monsterTransform, playerCollider, playerTransform)) continue;

                int damage = (int)MonsterData.GetDamage(monsterComp.Type, waveNum);
                Hits.Add(new HitEvent(monsterEntity.Id, playerEntity.Id, damage, false));
            }
        }
    }

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

                if (!Overlaps(projCollider, projTransform, playerCollider, playerTransform)) continue;

                Hits.Add(new HitEvent(projEntity.Id, playerEntity.Id, projComp.Damage, false));
                World.DestroyEntity(projEntity.Id);
                break;
            }
        }
    }

    private void CheckProjectileVsObstacle()
    {
        var obstacles = World.GetEntitiesWith<ObstacleComponent, TransformComponent, ColliderComponent>();
        if (obstacles.Count == 0) return;

        var arrows = World.GetEntitiesWith<ArrowComponent, TransformComponent, ColliderComponent>();
        foreach (var arrow in arrows)
        {
            if (!arrow.IsAlive) continue;
            var at = arrow.Get<TransformComponent>();
            var ac = arrow.Get<ColliderComponent>();
            foreach (var obs in obstacles)
            {
                var ot = obs.Get<TransformComponent>();
                var oc = obs.Get<ColliderComponent>();
                if (Overlaps(ac, at, oc, ot))
                {
                    World.DestroyEntity(arrow.Id);
                    break;
                }
            }
        }

        var projectiles = World.GetEntitiesWith<MonsterProjectileComponent, TransformComponent, ColliderComponent>();
        foreach (var proj in projectiles)
        {
            if (!proj.IsAlive) continue;
            var pt = proj.Get<TransformComponent>();
            var pc = proj.Get<ColliderComponent>();
            foreach (var obs in obstacles)
            {
                var ot = obs.Get<TransformComponent>();
                var oc = obs.Get<ColliderComponent>();
                if (Overlaps(pc, pt, oc, ot))
                {
                    World.DestroyEntity(proj.Id);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Checks transient attack hitboxes against players. Each hitbox deals damage once
    /// then destroys itself — even if no player was hit (single-frame existence).
    /// </summary>
    private void CheckAttackHitbox()
    {
        var hitboxes = World.GetEntitiesWith<AttackHitboxComponent, TransformComponent, ColliderComponent>();
        if (hitboxes.Count == 0) return;

        var players = World.GetEntitiesWith<PlayerComponent, TransformComponent, ColliderComponent>();
        if (players.Count == 0) return;

        foreach (var hitboxEntity in hitboxes)
        {
            if (!hitboxEntity.IsAlive) continue;

            var hitboxTransform = hitboxEntity.Get<TransformComponent>();
            var hitboxCollider  = hitboxEntity.Get<ColliderComponent>();
            var hitboxComp     = hitboxEntity.Get<AttackHitboxComponent>();

            foreach (var playerEntity in players)
            {
                if (!playerEntity.IsAlive) continue;

                var playerTransform = playerEntity.Get<TransformComponent>();
                var playerCollider  = playerEntity.Get<ColliderComponent>();

                if (!Overlaps(hitboxCollider, hitboxTransform, playerCollider, playerTransform)) continue;

                Hits.Add(new HitEvent(hitboxComp.AttackerId, playerEntity.Id, hitboxComp.Damage, false));
                break; // One hit per hitbox
            }

            // Destroy the hitbox whether or not a player was hit
            World.DestroyEntity(hitboxEntity.Id);
        }
    }
}
