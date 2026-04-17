using System;
using Game.Ecs.Core;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.Systems;

/// <summary>
/// Updates entity positions based on velocity and handles arena boundary rules.
/// - Players are clamped inside the arena.
/// - Player arrows (ArrowComponent) are destroyed outside boundary.
/// - Monster projectiles (MonsterProjectileComponent) apply acceleration (if any) then are destroyed outside boundary.
/// - Monsters roam freely (spawn outside and move in).
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
                        Vec2  newDir   = new Vec2(MathF.Cos(newAngle), MathF.Sin(newAngle));
                        velocity.Velocity  = newDir * MonsterData.EliteProjectileSpeed;
                        transform.Rotation = newAngle;
                    }
                }

                if (IsOutsideArena(transform.Position))
                    World.DestroyEntity(entity.Id);
            }
            // Monsters: no clamping — they spawn outside and move in.
        }
    }

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

    private bool IsOutsideArena(Vec2 pos) =>
        pos.X < -ArrowMargin || pos.X > ArenaData.Size.X + ArrowMargin ||
        pos.Y < -ArrowMargin || pos.Y > ArenaData.Size.Y + ArrowMargin;
}
