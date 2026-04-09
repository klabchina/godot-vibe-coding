using Godot;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.Systems;

/// <summary>
/// Monster AI: chase nearest player, with per-type special behaviors.
/// - Slime/Elite: straight chase
/// - Skeleton: every 3s random lateral dodge for 0.5s
/// - Orc: charge at 150px/s within 150px, then 1s stun
/// - Boss: speed handled by BossAISystem
/// Frozen monsters have their speed reduced.
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

            var monsterComp = monster.Get<MonsterComponent>();
            var monsterTransform = monster.Get<TransformComponent>();
            var velocity = monster.Get<VelocityComponent>();

            // Find nearest alive player
            float nearestDist = float.MaxValue;
            Vector2 nearestPos = Vector2.Zero;

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
                }
            }

            if (nearestDist >= float.MaxValue)
            {
                velocity.Velocity = Vector2.Zero;
                continue;
            }

            Vector2 toPlayer = (nearestPos - monsterTransform.Position).Normalized();

            // Ensure AI state exists
            var ai = monster.Get<MonsterAIState>();
            if (ai == null && (monsterComp.Type == MonsterType.Skeleton || monsterComp.Type == MonsterType.Orc))
            {
                ai = new MonsterAIState();
                monster.Add(ai);
            }

            // Calculate effective speed (apply freeze slow)
            float baseSpeed = velocity.Speed;
            float speedMultiplier = 1f;
            var effect = monster.Get<EffectComponent>();
            if (effect != null && effect.IsFrozen)
            {
                speedMultiplier = 1f - effect.FreezeSlowPercent;
            }

            switch (monsterComp.Type)
            {
                case MonsterType.Skeleton:
                    UpdateSkeleton(velocity, ai, toPlayer, baseSpeed, speedMultiplier, delta);
                    break;
                case MonsterType.Orc:
                    UpdateOrc(velocity, ai, toPlayer, nearestDist, baseSpeed, speedMultiplier, delta);
                    break;
                case MonsterType.Boss:
                    // Boss direction: chase, but speed is set by BossAISystem
                    velocity.Velocity = toPlayer * velocity.Speed * speedMultiplier;
                    break;
                default:
                    // Slime, Elite: straight chase
                    velocity.Velocity = toPlayer * baseSpeed * speedMultiplier;
                    break;
            }
        }
    }

    private void UpdateSkeleton(VelocityComponent velocity, MonsterAIState ai,
        Vector2 toPlayer, float baseSpeed, float speedMul, float delta)
    {
        if (ai == null)
        {
            velocity.Velocity = toPlayer * baseSpeed * speedMul;
            return;
        }

        if (ai.DodgeDuration > 0)
        {
            // Currently dodging: move laterally
            ai.DodgeDuration -= delta;
            velocity.Velocity = ai.DodgeDir * baseSpeed * 1.5f * speedMul;
        }
        else
        {
            // Normal chase
            velocity.Velocity = toPlayer * baseSpeed * speedMul;

            // Count up to next dodge
            ai.DodgeTimer += delta;
            if (ai.DodgeTimer >= MonsterData.SkeletonDodgeInterval)
            {
                ai.DodgeTimer = 0;
                ai.DodgeDuration = MonsterData.SkeletonDodgeDuration;

                // Random lateral direction (perpendicular to toPlayer)
                Vector2 perp = new Vector2(-toPlayer.Y, toPlayer.X);
                ai.DodgeDir = GD.Randf() > 0.5f ? perp : -perp;
            }
        }
    }

    private void UpdateOrc(VelocityComponent velocity, MonsterAIState ai,
        Vector2 toPlayer, float distToPlayer, float baseSpeed, float speedMul, float delta)
    {
        if (ai == null)
        {
            velocity.Velocity = toPlayer * baseSpeed * speedMul;
            return;
        }

        if (ai.IsStunned)
        {
            // Stunned after charge: don't move
            velocity.Velocity = Vector2.Zero;
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
            // Charging: move fast toward player
            velocity.Velocity = toPlayer * MonsterData.OrcChargeSpeed * speedMul;

            // If very close, end charge with stun
            if (distToPlayer < 30f)
            {
                ai.IsCharging = false;
                ai.IsStunned = true;
                ai.StunTimer = MonsterData.OrcStunDuration;
                velocity.Velocity = Vector2.Zero;
            }
            return;
        }

        // Normal movement
        velocity.Velocity = toPlayer * baseSpeed * speedMul;

        // Start charge when within range
        if (distToPlayer <= MonsterData.OrcChargeRange)
        {
            ai.IsCharging = true;
        }
    }
}
