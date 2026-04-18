using Game.Ecs.Core;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.Systems;

/// <summary>
/// Handles melee attack logic for monsters equipped with MeleeAttackComponent.
///
/// Attack flow:
/// 1. When player is within MeleeAttackRange && CanAttack && CooldownTimer <= 0:
///    - Set AttackWindupTimer = MeleeWindupDuration
///    - Set CanAttack = false
///    - Velocity is zeroed by MonsterAISystem
/// 2. While AttackWindupTimer > 0: decrement timer
/// 3. When timer <= 0: trigger damage and set cooldown
/// 4. While CooldownTimer > 0: decrement timer
/// 5. When cooldown <= 0: set CanAttack = true
/// </summary>
public class MeleeAttackSystem : GameSystem
{
    public override void Update(float delta)
    {
        var attackers = World.GetEntitiesWith<MeleeAttackComponent, MonsterComponent,
            TransformComponent>();
        var players = World.GetEntitiesWith<PlayerComponent, TransformComponent>();

        if (players.Count == 0) return;

        foreach (var attacker in attackers)
        {
            if (!attacker.IsAlive) continue;
            if (attacker.Has<DeathPendingComponent>()) continue;

            var melee = attacker.Get<MeleeAttackComponent>();
            var monsterComp = attacker.Get<MonsterComponent>();
            var attackerTransform = attacker.Get<TransformComponent>();
            var attackerCollider = attacker.Get<ColliderComponent>();

            // ── Phase 1: Attack windup ──────────────────────────────────────
            if (melee.AttackWindupTimer > 0f)
            {
                melee.AttackWindupTimer -= delta;

                if (melee.AttackWindupTimer <= 0f)
                {
                    // Windup complete — deal damage once
                    float attackRange = MonsterData.MeleeAttackRange + attackerCollider.Radius;
                    DealMeleeDamage(attacker, monsterComp, attackerTransform, attackerCollider, attackRange);
                }
                continue;
            }

            // ── Phase 2: Cooldown countdown ───────────────────────────────
            if (melee.CooldownTimer > 0f)
            {
                melee.CooldownTimer -= delta;
                if (melee.CooldownTimer <= 0f)
                {
                    melee.CooldownTimer = 0f;
                    melee.CanAttack = true;
                }
                continue;
            }

            // ── Phase 3: Check if can start new attack ────────────────────
            if (!melee.CanAttack) continue;

            // Find nearest alive player within attack range
            float attackRangeCheck = MonsterData.MeleeAttackRange + attackerCollider.Radius;
            int nearestPlayerId = -1;
            float nearestDist = float.MaxValue;

            foreach (var player in players)
            {
                if (!player.IsAlive) continue;

                var playerTransform = player.Get<TransformComponent>();
                float dist = attackerTransform.Position.DistanceTo(playerTransform.Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestPlayerId = player.Id;
                }
            }

            if (nearestDist <= attackRangeCheck && nearestPlayerId >= 0)
            {
                // Start windup — monster will stop moving (MonsterAISystem checks AttackWindupTimer)
                melee.AttackWindupTimer = MonsterData.MeleeWindupDuration;
                melee.CanAttack = false;
                melee.CooldownTimer = GetAttackCooldown(monsterComp.Type);

                // Signal RenderSystem to play attack animation
                attacker.Add(new AttackAnimationComponent());
            }
        }
    }

    /// <summary>
    /// Deals melee damage to the nearest player and triggers any attack effects.
    /// Called once when windup completes.
    /// </summary>
    private void DealMeleeDamage(Entity attacker, MonsterComponent monsterComp,
        TransformComponent attackerTransform, ColliderComponent attackerCollider,
        float attackRange)
    {
        float damage = GetMeleeDamage(monsterComp.Type);

        var players = World.GetEntitiesWith<PlayerComponent, TransformComponent, ColliderComponent>();
        foreach (var player in players)
        {
            if (!player.IsAlive) continue;

            var playerTransform = player.Get<TransformComponent>();
            float dist = attackerTransform.Position.DistanceTo(playerTransform.Position);

            if (dist <= attackRange)
            {
                // Add hit to CollisionSystem via Hits list
                var collisionSystem = World.GetSystem<CollisionSystem>();
                collisionSystem?.Hits.Add(new CollisionSystem.HitEvent(
                    attacker.Id, player.Id, (int)damage, false));
                break; // Only damage one player per attack
            }
        }
    }

    /// <summary>
    /// Returns the melee damage value for a given monster type.
    /// </summary>
    private float GetMeleeDamage(MonsterType type) => type switch
    {
        MonsterType.Slime => MonsterData.SlimeAttackDamage,
        MonsterType.Orc => MonsterData.OrcAttackDamage,
        MonsterType.Boss => MonsterData.BossAttackDamage,
        _ => 0f
    };

    /// <summary>
    /// Returns a random cooldown duration for a given monster type.
    /// </summary>
    private float GetAttackCooldown(MonsterType type) => type switch
    {
        MonsterType.Slime => MonsterData.SlimeAttackCooldownMin
            + GameRandom.Randf() * (MonsterData.SlimeAttackCooldownMax - MonsterData.SlimeAttackCooldownMin),
        MonsterType.Orc => MonsterData.OrcAttackCooldownMin
            + GameRandom.Randf() * (MonsterData.OrcAttackCooldownMax - MonsterData.OrcAttackCooldownMin),
        MonsterType.Boss => MonsterData.BossAttackCooldownMin
            + GameRandom.Randf() * (MonsterData.BossAttackCooldownMax - MonsterData.BossAttackCooldownMin),
        _ => 2.0f
    };
}
