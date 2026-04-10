using Game.Ecs.Core;
using Game.Ecs;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.Systems;

public class WaveSpawnSystem : GameSystem
{
    private const float SpawnOffset = 20f;

    public override void Update(float delta)
    {
        var entities = World.GetEntitiesWith<WaveComponent>();
        if (entities.Count == 0)
            return;

        var wave = entities[0].Get<WaveComponent>();

        if (wave.AllWavesComplete)
            return;

        if (wave.IsWaveInterval)
        {
            wave.WaveIntervalTimer -= delta;
            if (wave.WaveIntervalTimer <= 0f)
            {
                wave.IsWaveInterval = false;
                StartNextWave(wave);
            }
            return;
        }

        if (wave.IsSpawning)
        {
            wave.SpawnTimer -= delta;
            if (wave.SpawnTimer <= 0f)
            {
                SpawnMonster(wave);
                wave.SpawnTimer = (float)GameRandom.RandRange(WaveData.SpawnIntervalMin, WaveData.SpawnIntervalMax);
            }
            return;
        }

        // Not spawning and no wave interval — check if wave is cleared
        if (wave.AliveMonsters <= 0)
        {
            // Auto-collect all remaining XP orbs on wave clear
            CollectAllExpOrbs();

            if (wave.CurrentWave >= WaveData.TotalWaves)
            {
                wave.AllWavesComplete = true;
            }
            else
            {
                wave.IsWaveInterval = true;
                wave.WaveIntervalTimer = WaveData.WaveIntervalSec;
            }
        }
    }

    public void StartNextWave(WaveComponent wave)
    {
        wave.CurrentWave++;
        wave.SpawnList = WaveData.Waves[wave.CurrentWave - 1];
        wave.SpawnIndex = 0;
        wave.SpawnCountInEntry = 0;
        wave.TotalSpawned = 0;
        wave.SpawnTimer = 0f;
        wave.IsSpawning = true;

        int total = 0;
        foreach (var entry in wave.SpawnList)
            total += entry.Count;
        wave.TotalToSpawn = total;
    }

    private void SpawnMonster(WaveComponent wave)
    {
        var entry = wave.SpawnList[wave.SpawnIndex];
        var type = entry.Type;
        int currentWave = wave.CurrentWave;

        int hp = MonsterData.GetHp(type, currentWave);
        float speed = MonsterData.GetSpeed(type);
        int xp = MonsterData.GetXp(type, currentWave);
        int radius = MonsterData.GetRadius(type);

        var entity = World.CreateEntity();

        entity.Add(new TransformComponent
        {
            Position = GetRandomEdgePosition(),
            Rotation = 0f,
        });

        entity.Add(new VelocityComponent
        {
            Speed = speed,
        });

        entity.Add(new HealthComponent
        {
            Hp = hp,
            MaxHp = hp,
        });

        entity.Add(new MonsterComponent
        {
            Type = type,
            Reward = xp,
        });

        entity.Add(new ColliderComponent
        {
            Radius = radius,
            Layer = CollisionLayers.Monster,
            Mask = CollisionLayers.Arrow | CollisionLayers.Player,
        });

        // Boss gets phase AI component
        if (type == MonsterType.Boss)
        {
            entity.Add(new BossPhaseComponent());
        }

        wave.SpawnCountInEntry++;
        wave.TotalSpawned++;
        wave.AliveMonsters++;

        if (wave.SpawnCountInEntry >= entry.Count)
        {
            wave.SpawnIndex++;
            wave.SpawnCountInEntry = 0;
        }

        if (wave.SpawnIndex >= wave.SpawnList.Length)
        {
            wave.IsSpawning = false;
        }
    }

    private static Vec2 GetRandomEdgePosition()
    {
        int edge = (int)(GameRandom.Randf() * 4f) % 4;

        float x, y;
        switch (edge)
        {
            case 0: // Top
                x = (float)GameRandom.RandRange(0, ArenaData.Size.X);
                y = -SpawnOffset;
                break;
            case 1: // Bottom
                x = (float)GameRandom.RandRange(0, ArenaData.Size.X);
                y = ArenaData.Size.Y + SpawnOffset;
                break;
            case 2: // Left
                x = -SpawnOffset;
                y = (float)GameRandom.RandRange(0, ArenaData.Size.Y);
                break;
            default: // Right
                x = ArenaData.Size.X + SpawnOffset;
                y = (float)GameRandom.RandRange(0, ArenaData.Size.Y);
                break;
        }

        return new Vec2(x, y);
    }

    /// <summary>
    /// Wave clear: attract all ExpOrb pickups toward the nearest player
    /// at high speed. PickupSystem handles the actual collection on arrival.
    /// </summary>
    private void CollectAllExpOrbs()
    {
        var players = World.GetEntitiesWith<PlayerComponent, TransformComponent>();
        if (players.Count == 0) return;

        var pickups = World.GetEntitiesWith<PickupComponent, TransformComponent, VelocityComponent>();

        foreach (var pickupEntity in pickups)
        {
            if (!pickupEntity.IsAlive) continue;

            var pickup = pickupEntity.Get<PickupComponent>();
            if (pickup.Type != PickupType.ExpOrb) continue;

            var pickupTransform = pickupEntity.Get<TransformComponent>();

            // Find nearest player
            float nearestDist = float.MaxValue;
            Vec2 nearestPos = Vec2.Zero;
            foreach (var player in players)
            {
                var pt = player.Get<TransformComponent>();
                float d = pickupTransform.Position.DistanceTo(pt.Position);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearestPos = pt.Position;
                }
            }

            // Set high-speed fly toward player (2x normal attract speed)
            var vel = pickupEntity.Get<VelocityComponent>();
            Vec2 dir = (nearestPos - pickupTransform.Position).Normalized();
            vel.Velocity = dir * PickupData.ExpOrbFlySpeed * 2f;
        }
    }
}
