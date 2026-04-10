using System.Collections.Generic;
using Game.Ecs.Core;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.Systems;

/// <summary>
/// Updates orbiting guard arrows around player entities.
/// Each orbit arrow rotates at 180°/s, radius 80px, deals 8 damage
/// with a 0.5s hit interval per target.
/// </summary>
public class OrbitSystem : GameSystem
{
    public override void Update(float delta)
    {
        var players = World.GetEntitiesWith<PlayerComponent, TransformComponent, OrbitComponent>();
        var monsters = World.GetEntitiesWith<MonsterComponent, TransformComponent, ColliderComponent>();

        foreach (var player in players)
        {
            if (!player.IsAlive) continue;

            var playerTransform = player.Get<TransformComponent>();
            var orbit = player.Get<OrbitComponent>();

            if (orbit.Count <= 0) continue;

            // Rotate base angle
            orbit.CurrentAngle += UpgradeData.OrbitRotationSpeed * delta;
            if (orbit.CurrentAngle >= 360f)
                orbit.CurrentAngle -= 360f;

            // Check each orbit arrow against monsters
            float angleStep = 360f / orbit.Count;

            for (int i = 0; i < orbit.Count; i++)
            {
                float angle = GMath.DegToRad(orbit.CurrentAngle + angleStep * i);
                Vec2 orbitPos = playerTransform.Position + new Vec2(
                    GMath.Cos(angle) * UpgradeData.OrbitRadius,
                    GMath.Sin(angle) * UpgradeData.OrbitRadius
                );

                // Ensure cooldown dict exists for this orbit index
                if (!orbit.HitCooldowns.ContainsKey(i))
                    orbit.HitCooldowns[i] = new Dictionary<int, float>();

                var cooldowns = orbit.HitCooldowns[i];

                // Tick down cooldowns
                var expired = new List<int>();
                foreach (var (monsterId, cd) in cooldowns)
                {
                    cooldowns[monsterId] = cd - delta;
                    if (cooldowns[monsterId] <= 0)
                        expired.Add(monsterId);
                }
                foreach (var id in expired)
                    cooldowns.Remove(id);

                // Check collision with monsters
                foreach (var monster in monsters)
                {
                    if (!monster.IsAlive) continue;
                    if (cooldowns.ContainsKey(monster.Id)) continue;

                    var monsterTransform = monster.Get<TransformComponent>();
                    var monsterCollider = monster.Get<ColliderComponent>();

                    float dist = orbitPos.DistanceTo(monsterTransform.Position);
                    if (dist <= 10f + monsterCollider.Radius) // orbit arrow radius ~10px
                    {
                        // Deal damage directly
                        var health = monster.Get<HealthComponent>();
                        if (health != null)
                        {
                            health.Hp -= UpgradeData.OrbitDamage;
                            if (health.Hp < 0) health.Hp = 0;

                            // Track damage for player stats
                            var playerComp = player.Get<PlayerComponent>();
                            if (playerComp != null)
                                playerComp.TotalDamageDealt += UpgradeData.OrbitDamage;
                        }

                        cooldowns[monster.Id] = UpgradeData.OrbitHitInterval;
                    }
                }
            }
        }
    }
}
