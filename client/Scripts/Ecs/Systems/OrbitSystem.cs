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

        // Cache monster data to avoid repeated GetComponent calls in inner loops
        var monsterData = new List<(Vec2 pos, float radius, int id)>(monsters.Count);
        foreach (var m in monsters)
        {
            if (!m.IsAlive) continue;
            var col = m.Get<ColliderComponent>();
            monsterData.Add((m.Get<TransformComponent>().Position, col.Radius, m.Id));
        }

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

            float angleStep = 360f / orbit.Count;

            for (int i = 0; i < orbit.Count; i++)
            {
                float angle = GMath.DegToRad(orbit.CurrentAngle + angleStep * i);
                Vec2 orbitPos = playerTransform.Position + new Vec2(
                    GMath.Cos(angle) * UpgradeData.OrbitRadius,
                    GMath.Sin(angle) * UpgradeData.OrbitRadius
                );

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

                // Check collision with monsters — use squared distance to avoid sqrt
                float orbitRadius = 10f;
                foreach (var (monsterPos, monsterRadius, monsterId) in monsterData)
                {
                    if (cooldowns.ContainsKey(monsterId)) continue;

                    float dx = orbitPos.X - monsterPos.X;
                    float dy = orbitPos.Y - monsterPos.Y;
                    float distSq = dx * dx + dy * dy;
                    float maxDist = orbitRadius + monsterRadius;
                    if (distSq > maxDist * maxDist) continue;

                    // Hit — deal damage and record for damage number display
                    var monster = World.GetEntity(monsterId);
                    if (monster != null && monster.IsAlive)
                    {
                        var health = monster.Get<HealthComponent>();
                        if (health != null)
                        {
                            health.Hp -= UpgradeData.OrbitDamage;
                            if (health.Hp < 0) health.Hp = 0;

                            var playerComp = player.Get<PlayerComponent>();
                            if (playerComp != null)
                                playerComp.TotalDamageDealt += UpgradeData.OrbitDamage;

                            // Mark monster as just hit by orbit (for damage number display)
                            monster.Add(new OrbitHitComponent
                            {
                                Damage = UpgradeData.OrbitDamage,
                                IsOrbit = true
                            });
                        }
                    }

                    cooldowns[monsterId] = UpgradeData.OrbitHitInterval;
                }
            }
        }
    }
}
