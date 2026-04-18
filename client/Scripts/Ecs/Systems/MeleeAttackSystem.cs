using Game.Ecs.Core;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.Systems;

/// <summary>
/// Handles melee attack timing (windup + cooldown) for monsters equipped with MeleeAttackComponent.
/// Damage is handled by CollisionSystem — this system only controls attack frequency.
/// 
/// Attack flow:
/// 1. When player overlaps && CanAttack && CooldownTimer <= 0:
///    - Set AttackWindupTimer = MeleeWindupDuration
///    - Set CanAttack = false
///    - Velocity is zeroed by MonsterAISystem (windup animation plays)
/// 2. While AttackWindupTimer > 0: countdown, NO damage
/// 3. When timer <= 0: start cooldown, CollisionSystem now deals contact damage
/// 4. While CooldownTimer > 0: CollisionSystem deals damage on collision
/// 5. When cooldown <= 0: set CanAttack = true
/// </summary>
public class MeleeAttackSystem : GameSystem
{
    public override void Update(float delta)
    {
        var attackers = World.GetEntitiesWith<MeleeAttackComponent, MonsterComponent,
            TransformComponent>();
        var players = World.GetEntitiesWith<PlayerComponent, TransformComponent, ColliderComponent>();

        if (players.Count == 0) return;

        foreach (var attacker in attackers)
        {
            if (!attacker.IsAlive) continue;
            if (attacker.Has<DeathPendingComponent>()) continue;

            var melee = attacker.Get<MeleeAttackComponent>();
            var monsterComp = attacker.Get<MonsterComponent>();
            var attackerTransform = attacker.Get<TransformComponent>();
            var attackerCollider = attacker.Get<ColliderComponent>();

            // ── Phase 1: Attack windup countdown ────────────────────────────────
            if (melee.AttackWindupTimer > 0f)
            {
                melee.AttackWindupTimer -= delta;

                if (melee.AttackWindupTimer <= 0f)
                {
                    // Windup complete — create a transient hitbox that deals damage once
                    // then auto-destroys itself in the same tick
                    melee.AttackWindupTimer = 0f;
                    melee.CooldownTimer = GetAttackCooldown(monsterComp.Type);
                    melee.CanAttack = false;

                    CreateAttackHitbox(attacker, monsterComp.Type, attackerTransform);
                }
                continue;
            }

            // ── Phase 2: Cooldown countdown ─────────────────────────────────────
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

            // ── Phase 3: Check if player is overlapping → start windup ──────────
            if (!melee.CanAttack) continue;

            float attackRangeCheck = MonsterData.MeleeAttackRange + attackerCollider.Radius;

            foreach (var player in players)
            {
                if (!player.IsAlive) continue;

                var playerTransform = player.Get<TransformComponent>();
                float dist = attackerTransform.Position.DistanceTo(playerTransform.Position);
                if (dist > attackRangeCheck) continue;

                // Start windup — monster will stop moving (MonsterAISystem checks AttackWindupTimer)
                melee.AttackWindupTimer = MonsterData.MeleeWindupDuration;
                melee.CanAttack = false;

                // Signal RenderSystem to play attack animation
                attacker.Add(new AttackAnimationComponent());
                break; // One windup per frame max
            }
        }
    }

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

    /// <summary>
    /// Creates a transient hitbox entity at the attacker's position. The hitbox checks
    /// for a Player overlap once, deals damage, then is destroyed by CollisionSystem —
    /// even if no player was hit.
    /// </summary>
    private void CreateAttackHitbox(Entity attacker, MonsterType type, TransformComponent attackerTransform)
    {
        if (!MonsterData.Hitbox.TryGetValue(type, out var hitboxData)) return;

        var hitbox = World.CreateEntity();
        hitbox.Add(new TransformComponent
        {
            Position = attackerTransform.Position,
            Rotation = attackerTransform.Rotation
        });
        hitbox.Add(new ColliderComponent
        {
            Shape = hitboxData.Shape,
            Radius = hitboxData.Radius
        });
        hitbox.Add(new AttackHitboxComponent
        {
            AttackerId = attacker.Id,
            Damage = (int)MonsterData.GetDamage(type, 1)
        });
    }
}
