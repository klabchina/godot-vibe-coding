using System.Collections.Generic;
using Game.Ecs.Core;
using Game.Data;
using Game.Ecs.Components;

namespace Game.Ecs.Systems;

public class CollisionSystem : GameSystem
{
    public record HitEvent(int AttackerId, int DefenderId, int Damage, bool IsArrow);

    public List<HitEvent> Hits = new();

    public override void Update(float delta)
    {
        Hits.Clear();

        CheckArrowVsMonster();
        CheckMonsterVsPlayer();
        CheckMonsterProjectileVsPlayer();
    }

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

                var monsterTransform = monsterEntity.Get<TransformComponent>();
                var monsterCollider = monsterEntity.Get<ColliderComponent>();

                float dist = arrowTransform.Position.DistanceTo(monsterTransform.Position);
                if (dist > arrowCollider.Radius + monsterCollider.Radius) continue;

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

            var monsterTransform = monsterEntity.Get<TransformComponent>();
            var monsterCollider = monsterEntity.Get<ColliderComponent>();
            var monsterComp = monsterEntity.Get<MonsterComponent>();

            foreach (var playerEntity in players)
            {
                if (!playerEntity.IsAlive) continue;

                var playerTransform = playerEntity.Get<TransformComponent>();
                var playerCollider = playerEntity.Get<ColliderComponent>();

                float dist = monsterTransform.Position.DistanceTo(playerTransform.Position);
                if (dist > monsterCollider.Radius + playerCollider.Radius) continue;

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

                float dist = projTransform.Position.DistanceTo(playerTransform.Position);
                if (dist > projCollider.Radius + playerCollider.Radius) continue;

                // Register hit with IsArrow=false so DamageSystem applies shield/invincible checks
                Hits.Add(new HitEvent(projEntity.Id, playerEntity.Id, projComp.Damage, false));
                World.DestroyEntity(projEntity.Id); // consumed on first hit
                break; // projectile is gone; skip remaining players
            }
        }
    }
}
