using Game.Ecs.Core;
using Game.Ecs.Components;
using Game.Data;
using Game.Net;

namespace Game.Ecs.Systems;

/// <summary>
/// Receives server state and applies it to the ECS world.
/// Runs early in the system pipeline (execution order #2).
///
/// Processes queued messages from SyncClient:
///   - GameState  → reconcile entity positions/HP
///   - SpawnArrow → create arrow entities
///   - SpawnWave  → trigger wave start
///   - EntityDeath → mark entities for removal
///   - PickupSpawn / PickupCollect
///   - BuffApply
///   - BossPhaseChange
/// </summary>
public class NetworkRecvSystem : GameSystem
{
    public SyncClient Sync { get; set; }

    public override void Update(float delta)
    {
        if (Sync == null) return;

        ProcessGameStates();
        ProcessSpawnWaves();
        ProcessSpawnArrows();
        ProcessEntityDeaths();
        ProcessPickupSpawns();
        ProcessPickupCollects();
        ProcessBuffApplies();
        ProcessBossPhaseChanges();
    }

    private void ProcessGameStates()
    {
        while (Sync.GameStateQueue.Count > 0)
        {
            var state = Sync.GameStateQueue.Dequeue();

            // Reconcile player positions
            foreach (var ps in state.Players)
            {
                var entity = FindEntityByNetId(ps.Id);
                if (entity == null) continue;

                var net = entity.Get<NetworkSyncComponent>();
                if (net != null && net.IsLocal)
                {
                    // Local player: only reconcile if significantly diverged (client prediction)
                    var transform = entity.Get<TransformComponent>();
                    if (transform != null)
                    {
                        var serverPos = new Vec2(ps.X, ps.Y);
                        float drift = (transform.Position - serverPos).Length();
                        if (drift > 5f) // correction threshold
                        {
                            transform.Position = serverPos;
                        }
                    }
                }
                else
                {
                    // Remote player: directly apply server state
                    var transform = entity.Get<TransformComponent>();
                    if (transform != null)
                    {
                        transform.Position = new Vec2(ps.X, ps.Y);
                    }
                }

                // Always sync HP/stats for all players
                var health = entity.Get<HealthComponent>();
                if (health != null)
                {
                    health.Hp = ps.Hp;
                    health.MaxHp = ps.MaxHp;
                }

                var player = entity.Get<PlayerComponent>();
                if (player != null)
                {
                    player.TotalXp = ps.Xp;
                    player.CurrentLevel = ps.Level;
                    player.KillCount = ps.Kills;
                    player.TotalDamageDealt = ps.TotalDamage;
                }
            }

            // Reconcile monster positions
            foreach (var ms in state.Monsters)
            {
                var entity = FindEntityByNetId(ms.Id);
                if (entity == null) continue;

                var transform = entity.Get<TransformComponent>();
                if (transform != null)
                {
                    transform.Position = new Vec2(ms.X, ms.Y);
                }

                var health = entity.Get<HealthComponent>();
                if (health != null)
                {
                    health.Hp = ms.Hp;
                }
            }
        }
    }

    private void ProcessSpawnWaves()
    {
        while (Sync.SpawnWaveQueue.Count > 0)
        {
            var wave = Sync.SpawnWaveQueue.Dequeue();
            var waveEntities = World.GetEntitiesWith<WaveComponent>();
            if (waveEntities.Count > 0)
            {
                var wc = waveEntities[0].Get<WaveComponent>();
                wc.CurrentWave = wave.WaveNumber;
                wc.IsSpawning = true;
            }
        }
    }

    private void ProcessSpawnArrows()
    {
        while (Sync.SpawnArrowQueue.Count > 0)
        {
            var arrow = Sync.SpawnArrowQueue.Dequeue();
            var entity = World.CreateEntity();
            entity.Add(new NetworkSyncComponent { NetId = arrow.Id, Owner = -1, IsLocal = false });
            entity.Add(new TransformComponent { Position = new Vec2(arrow.X, arrow.Y) });
            entity.Add(new VelocityComponent { Velocity = new Vec2(arrow.Vx, arrow.Vy) });
            entity.Add(new ArrowComponent
            {
                Damage = arrow.Damage,
                OwnerId = arrow.OwnerId,
                PierceCount = arrow.Pierce,
                Bouncing = arrow.Bouncing,
                Explosive = arrow.Explosive,
                Freezing = arrow.Freezing,
                Burning = arrow.Burning,
            });
            entity.Add(new ColliderComponent
            {
                Radius = 5f,
                Layer = CollisionLayers.Arrow,
                Mask = CollisionLayers.Monster,
            });
        }
    }

    private void ProcessEntityDeaths()
    {
        while (Sync.EntityDeathQueue.Count > 0)
        {
            var death = Sync.EntityDeathQueue.Dequeue();
            var entity = FindEntityByNetId(death.Id);
            if (entity != null)
            {
                var health = entity.Get<HealthComponent>();
                if (health != null)
                    health.Hp = 0; // DeathSystem will clean up
            }
        }
    }

    private void ProcessPickupSpawns()
    {
        while (Sync.PickupSpawnQueue.Count > 0)
        {
            var pickup = Sync.PickupSpawnQueue.Dequeue();
            var entity = World.CreateEntity();
            entity.Add(new NetworkSyncComponent { NetId = pickup.Id, Owner = -1, IsLocal = false });
            entity.Add(new TransformComponent { Position = new Vec2(pickup.X, pickup.Y) });
            entity.Add(new PickupComponent
            {
                Type = pickup.Type,
                Value = pickup.Value,
                LifeTime = PickupData.ExpOrbLifeTime,
            });
            entity.Add(new ColliderComponent
            {
                Radius = 8f,
                Layer = CollisionLayers.Pickup,
                Mask = CollisionLayers.Player,
            });
        }
    }

    private void ProcessPickupCollects()
    {
        while (Sync.PickupCollectQueue.Count > 0)
        {
            var collect = Sync.PickupCollectQueue.Dequeue();
            var pickup = FindEntityByNetId(collect.PickupId);
            if (pickup != null)
            {
                World.DestroyEntity(pickup.Id);
            }
        }
    }

    private void ProcessBuffApplies()
    {
        while (Sync.BuffApplyQueue.Count > 0)
        {
            var buff = Sync.BuffApplyQueue.Dequeue();
            var player = FindEntityByNetId(buff.PlayerId);
            if (player == null) continue;

            var bc = player.Get<BuffComponent>();
            if (bc == null) continue;

            if (buff.Remove)
            {
                bc.ActiveTimedBuff = null;
                bc.TimedBuffRemaining = 0;
            }
            else
            {
                bc.ActiveTimedBuff = buff.BuffType;
                bc.TimedBuffRemaining = buff.Duration;
            }
        }
    }

    private void ProcessBossPhaseChanges()
    {
        while (Sync.BossPhaseChangeQueue.Count > 0)
        {
            var phase = Sync.BossPhaseChangeQueue.Dequeue();
            var bosses = World.GetEntitiesWith<BossPhaseComponent>();
            foreach (var boss in bosses)
            {
                var bp = boss.Get<BossPhaseComponent>();
                if (bp != null)
                {
                    bp.Phase = phase.Phase;
                }
            }
        }
    }

    private Entity FindEntityByNetId(int netId)
    {
        var entities = World.GetEntitiesWith<NetworkSyncComponent>();
        foreach (var e in entities)
        {
            if (e.Get<NetworkSyncComponent>().NetId == netId)
                return e;
        }
        return null;
    }
}
