using Game.Ecs.Core;
using Game.Ecs;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.Systems;

public class DeathSystem : GameSystem
{
    private static readonly System.Random _rng = new();

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
            // Player death is handled in DamageSystem (revive logic)
        }
    }

    private void HandleMonsterDeath(Entity entity)
    {
        var transform = entity.Get<TransformComponent>();
        var monster = entity.Get<MonsterComponent>();

        // Track kill for attacker (find the player who last hit)
        var players = World.GetEntitiesWith<PlayerComponent>();
        foreach (var player in players)
        {
            player.Get<PlayerComponent>().KillCount++;
            break; // single player for now
        }

        // Spawn experience orb at monster's position
        SpawnExpOrb(transform.Position, monster.Reward);

        // Item drop (5% chance, Boss guaranteed)
        bool isBoss = monster.Type == MonsterType.Boss;
        if (isBoss || _rng.NextDouble() < PickupData.TotalDropChance)
        {
            SpawnItemDrop(transform.Position);
        }

        // Decrement alive monster count
        var waveEntities = World.GetEntitiesWith<WaveComponent>();
        foreach (var waveEntity in waveEntities)
        {
            var wave = waveEntity.Get<WaveComponent>();
            wave.AliveMonsters--;
            break;
        }

        // 客户端：标记待死亡状态，等待 RenderSystem 播放死亡动画后由 World 清理
        // 服务端：直接销毁（无渲染层）
#if CLIENT
        entity.Add(new DeathPendingComponent());
#else
        World.DestroyEntity(entity.Id);
#endif
    }

    private void SpawnExpOrb(Vec2 position, int xpValue)
    {
        var orb = World.CreateEntity();
        orb.Add(new TransformComponent { Position = position, Rotation = 0f });
        orb.Add(new ColliderComponent
        {
            Radius = 10f,
            Layer = CollisionLayers.Pickup,
            Mask = CollisionLayers.Player
        });
        orb.Add(new PickupComponent
        {
            Type = PickupType.ExpOrb,
            Value = xpValue,
            LifeTime = PickupData.ExpOrbLifeTime
        });
        orb.Add(new VelocityComponent { Velocity = Vec2.Zero, Speed = 0f });
    }

    private void SpawnItemDrop(Vec2 position)
    {
        // Roll which item drops
        double roll = _rng.NextDouble() * PickupData.TotalDropChance;
        PickupType itemType;
        int value = 0;

        if (roll < PickupData.HealthPotionChance)
        {
            itemType = PickupType.HealthPotion;
        }
        else if (roll < PickupData.HealthPotionChance + PickupData.FrenzyChance)
        {
            itemType = PickupType.Frenzy;
        }
        else if (roll < PickupData.HealthPotionChance + PickupData.FrenzyChance + PickupData.InvincibleChance)
        {
            itemType = PickupType.Invincible;
        }
        else
        {
            itemType = PickupType.Bomb;
        }

        var item = World.CreateEntity();
        item.Add(new TransformComponent
        {
            Position = position + new Vec2((float)GameRandom.RandRange(-20, 20), (float)GameRandom.RandRange(-20, 20)),
            Rotation = 0f
        });
        item.Add(new ColliderComponent
        {
            Radius = 12f,
            Layer = CollisionLayers.Pickup,
            Mask = CollisionLayers.Player
        });
        item.Add(new PickupComponent
        {
            Type = itemType,
            Value = value,
            LifeTime = PickupData.ExpOrbLifeTime // items also last 30s
        });
        item.Add(new VelocityComponent { Velocity = Vec2.Zero, Speed = 0f });
    }
}
