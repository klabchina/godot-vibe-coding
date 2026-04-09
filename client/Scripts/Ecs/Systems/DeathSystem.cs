using Godot;
using Game.Ecs;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.Systems;

public class DeathSystem : GameSystem
{
    public override void Update(float delta)
    {
        var entities = World.GetEntitiesWith<HealthComponent>();

        foreach (var entity in entities)
        {
            var health = entity.Get<HealthComponent>();
            if (health.Hp > 0)
                continue;

            if (entity.Has<MonsterComponent>())
            {
                HandleMonsterDeath(entity);
            }
            else if (entity.Has<PlayerComponent>())
            {
                HandlePlayerDeath(entity, health);
            }
        }
    }

    private void HandleMonsterDeath(Entity entity)
    {
        var transform = entity.Get<TransformComponent>();
        var monster = entity.Get<MonsterComponent>();

        // Spawn experience orb at monster's position
        var orb = World.CreateEntity();
        orb.Add(new TransformComponent
        {
            Position = transform.Position,
            Rotation = 0f
        });
        orb.Add(new ColliderComponent
        {
            Radius = 10f,
            Layer = CollisionLayers.Pickup,
            Mask = CollisionLayers.Player
        });
        orb.Add(new PickupComponent
        {
            Type = PickupType.ExpOrb,
            Value = monster.Reward,
            LifeTime = PickupData.ExpOrbLifeTime
        });
        orb.Add(new VelocityComponent
        {
            Velocity = Vector2.Zero,
            Speed = 0f
        });

        // Decrement alive monster count
        var waveEntities = World.GetEntitiesWith<WaveComponent>();
        foreach (var waveEntity in waveEntities)
        {
            var wave = waveEntity.Get<WaveComponent>();
            wave.AliveMonsters--;
            break;
        }

        World.DestroyEntity(entity.Id);
    }

    private void HandlePlayerDeath(Entity entity, HealthComponent health)
    {
        // Clamp HP to zero (don't go negative)
        health.Hp = 0;

        // TODO: Phase 2 — handle death screen / revive logic
        GD.Print($"[DeathSystem] Player {entity.Get<PlayerComponent>().PlayerIndex} has died!");
    }
}
