using Game.Ecs.Core;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.Systems;

public class DamageSystem : GameSystem
{
    public override void Update(float delta)
    {
        var collisionSystem = World.GetSystem<CollisionSystem>();
        if (collisionSystem == null) return;

        foreach (var hit in collisionSystem.Hits)
        {
            var defender = World.GetEntity(hit.DefenderId);
            if (defender == null || !defender.IsAlive) continue;

            var health = defender.Get<HealthComponent>();
            if (health == null) continue;

            // --- Player taking damage: check invincible and shield ---
            if (defender.Has<PlayerComponent>() && !hit.IsArrow)
            {
                var buff = defender.Get<BuffComponent>();

                // Invincible: immune to all damage
                if (buff != null && buff.ActiveTimedBuff == BuffType.Invincible)
                    continue;

                // Shield: absorb one hit
                if (buff != null && buff.ShieldActive)
                {
                    buff.ShieldActive = false;
                    buff.ShieldCooldown = UpgradeData.ShieldRegenInterval;
                    continue; // damage fully absorbed
                }
            }

            health.Hp -= hit.Damage;
            if (health.Hp < 0) health.Hp = 0;

            // Track arrow damage to player stats
            if (hit.IsArrow)
            {
                var arrow = World.GetEntity(hit.AttackerId);
                if (arrow != null)
                {
                    var arrowComp = arrow.Get<ArrowComponent>();
                    if (arrowComp != null)
                    {
                        var owner = World.GetEntity(arrowComp.OwnerId);
                        var player = owner?.Get<PlayerComponent>();
                        if (player != null)
                        {
                            player.TotalDamageDealt += hit.Damage;
                        }
                    }
                }
            }
        }

        // Handle player death / revive countdown
        UpdatePlayerDeathRevive(delta);
    }

    private void UpdatePlayerDeathRevive(float delta)
    {
        var players = World.GetEntitiesWith<PlayerComponent, HealthComponent>();
        foreach (var player in players)
        {
            var health = player.Get<HealthComponent>();
            var playerComp = player.Get<PlayerComponent>();
            if (health.Hp > 0) continue;

            // Player is dead — manage revive
            var revive = player.Get<ReviveComponent>();
            if (revive == null)
            {
                // First death: start revive countdown
                revive = new ReviveComponent();
                player.Add(revive);
            }

            if (revive.HasRevived)
            {
                // Already used revive — stay dead
                continue;
            }

            revive.ReviveTimer -= delta;
            if (revive.ReviveTimer <= 0)
            {
                // Revive with 50% HP
                health.Hp = health.MaxHp / 2;
                revive.HasRevived = true;
            }
        }
    }
}
