using Game.Ecs.Components;

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

            health.Hp -= hit.Damage;
            if (health.Hp < 0) health.Hp = 0;

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
    }
}
