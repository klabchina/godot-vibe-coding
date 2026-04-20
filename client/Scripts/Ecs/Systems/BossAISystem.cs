using Game.Ecs.Core;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.Systems;

/// <summary>
/// Boss three-phase AI: Chase → Summon → Frenzy.
/// Phase transitions at 2/3 and 1/3 HP thresholds.
/// Each transition awards 30 XP to all players.
/// Boss also has a dash attack (Dash → cooldown) active in all non-summon phases.
/// </summary>
public class BossAISystem : GameSystem
{
    /// <summary>Called when boss changes phase, to award XP.</summary>
    public System.Action<int> OnBossPhaseChange;

    public override void Update(float delta)
    {
        var bosses = World.GetEntitiesWith<BossPhaseComponent, MonsterComponent, HealthComponent>();

        foreach (var boss in bosses)
        {
            if (!boss.IsAlive) continue;
            if (boss.Has<DeathPendingComponent>()) continue;

            var phase = boss.Get<BossPhaseComponent>();
            var health = boss.Get<HealthComponent>();
            var velocity = boss.Get<VelocityComponent>();
            if (velocity == null) continue;

            float hpPercent = (float)health.Hp / health.MaxHp;

            // Phase transitions
            if (!phase.Phase2Triggered && hpPercent <= 2f / 3f)
            {
                phase.Phase2Triggered = true;
                phase.Phase = BossPhase.Summon;
                phase.SummonDuration = MonsterData.BossSummonDuration;
                phase.SummonTimer = 0f; // summon immediately
                phase.IsDashing = false;
                OnBossPhaseChange?.Invoke(MonsterData.BossPhaseChangeXp);
            }
            else if (!phase.Phase3Triggered && hpPercent <= 1f / 3f)
            {
                phase.Phase3Triggered = true;
                phase.Phase = BossPhase.Frenzy;
                OnBossPhaseChange?.Invoke(MonsterData.BossPhaseChangeXp);
            }

            // Apply phase behavior
            switch (phase.Phase)
            {
                case BossPhase.Chase:
                    ApplyChase(boss, velocity, phase, delta);
                    break;
                case BossPhase.Summon:
                    ApplySummon(boss, velocity, phase, delta);
                    break;
                case BossPhase.Frenzy:
                    ApplyFrenzy(boss, velocity, phase, delta);
                    break;
            }
        }
    }

    private void ApplyChase(Entity boss, VelocityComponent velocity, BossPhaseComponent phase, float delta)
    {
        var phaseData = MonsterData.BossPhases[BossPhase.Chase];
        velocity.Speed = phaseData.Speed;
        ApplyDash(boss, velocity, phase, delta, phaseData.Speed);
    }

    private void ApplySummon(Entity boss, VelocityComponent velocity, BossPhaseComponent phase, float delta)
    {
        // Stop moving during summon phase
        velocity.Speed = 0;
        velocity.Velocity = Vec2.Zero;
        phase.IsDashing = false;

        phase.SummonDuration -= delta;
        phase.SummonTimer -= delta;

        if (phase.SummonTimer <= 0 && phase.SummonDuration > 0)
        {
            phase.SummonTimer = MonsterData.BossSummonCooldown;
            SpawnSlimes(boss);
        }

        // After summon duration, if phase 3 not triggered, return to chase
        if (phase.SummonDuration <= 0 && !phase.Phase3Triggered)
        {
            phase.Phase = BossPhase.Chase;
        }
    }

    private void ApplyFrenzy(Entity boss, VelocityComponent velocity, BossPhaseComponent phase, float delta)
    {
        var phaseData = MonsterData.BossPhases[BossPhase.Frenzy];
        velocity.Speed = phaseData.Speed;

        // Update collider for smaller hitbox
        var collider = boss.Get<ColliderComponent>();
        if (collider != null)
            collider.Radius = phaseData.Radius;

        ApplyDash(boss, velocity, phase, delta, phaseData.Speed);
    }

    private void ApplyDash(Entity boss, VelocityComponent velocity, BossPhaseComponent phase, float delta, float baseSpeed)
    {
        var transform = boss.Get<TransformComponent>();
        Vec2? nearestPos = FindNearestPlayer(transform.Position);
        if (nearestPos == null) return;

        Vec2 toPlayer = ((Vec2)nearestPos - transform.Position).Normalized();

        if (phase.IsDashing)
        {
            phase.DashTimer += delta;
            if (phase.DashTimer >= phase.DashInterval)
            {
                // End dash
                phase.IsDashing = false;
                phase.DashTimer = 0f;
                phase.DashInterval = MonsterData.BossDashIntervalMin
                    + GameRandom.Randf() * (MonsterData.BossDashIntervalMax - MonsterData.BossDashIntervalMin);
            }
            else
            {
                // Accelerating dash: speed ramps from 0 → peakSpeed over DashDuration
                float progress = phase.DashTimer / phase.DashInterval;
                float currentSpeed = MonsterData.BossDashSpeed * progress;
                float bossRadius = boss.Get<ColliderComponent>()?.Radius ?? 40f;
                Vec2 dashDir = AdjustForObstacles(transform.Position, toPlayer, currentSpeed, delta, bossRadius);
                velocity.Velocity = dashDir * currentSpeed;
            }
        }
        else
        {
            // Countdown to next dash
            phase.DashInterval -= delta;
            if (phase.DashInterval <= 0f)
            {
                // Start dash with random duration
                phase.IsDashing = true;
                phase.DashTimer = 0f;
                phase.DashInterval = MonsterData.BossDashDurationMin
                    + GameRandom.Randf() * (MonsterData.BossDashDurationMax - MonsterData.BossDashDurationMin);
            }
            else
            {
                // Normal chase at base speed
                float bossRadius = boss.Get<ColliderComponent>()?.Radius ?? 40f;
                Vec2 dir = AdjustForObstacles(transform.Position, toPlayer, baseSpeed, delta, bossRadius);
                velocity.Velocity = dir * baseSpeed;
            }
        }
    }

    private Vec2 AdjustForObstacles(Vec2 from, Vec2 desiredDir, float speed, float delta, float radius)
    {
        return desiredDir; // TODO: obstacle avoidance if needed
    }

    private Vec2? FindNearestPlayer(Vec2 bossPos)
    {
        var players = World.GetEntitiesWith<PlayerComponent, TransformComponent>();
        Vec2? nearest = null;
        float minDistSq = float.MaxValue;
        foreach (var p in players)
        {
            if (!p.IsAlive) continue;
            var t = p.Get<TransformComponent>();
            float d = (t.Position - bossPos).LengthSquared();
            if (d < minDistSq)
            {
                minDistSq = d;
                nearest = t.Position;
            }
        }
        return nearest;
    }

    private void SpawnSlimes(Entity boss)
    {
        var bossTransform = boss.Get<TransformComponent>();

        // Get current wave number for growth calculation
        int waveNum = 8; // Boss is always wave 8

        // Track spawned monsters in WaveComponent
        var waveEntities = World.GetEntitiesWith<WaveComponent>();
        WaveComponent waveComp = null;
        if (waveEntities.Count > 0)
            waveComp = waveEntities[0].Get<WaveComponent>();

        for (int i = 0; i < MonsterData.BossSummonCount; i++)
        {
            var slime = World.CreateEntity();

            // Spawn near boss with slight offset
            float angle = (float)GameRandom.RandRange(0, GMath.Tau);
            Vec2 offset = new Vec2(GMath.Cos(angle), GMath.Sin(angle)) * 50f;

            slime.Add(new TransformComponent
            {
                Position = bossTransform.Position + offset,
                Rotation = 0f
            });

            int hp = MonsterData.GetHp(MonsterType.Slime, waveNum);
            slime.Add(new HealthComponent { Hp = hp, MaxHp = hp });
            slime.Add(new VelocityComponent { Speed = MonsterData.GetSpeed(MonsterType.Slime) });
            slime.Add(new MonsterComponent
            {
                Type = MonsterType.Slime,
                Reward = MonsterData.GetXp(MonsterType.Slime, waveNum)
            });
            slime.Add(new ColliderComponent
            {
                Radius = MonsterData.GetRadius(MonsterType.Slime),
                Layer = CollisionLayers.Monster,
                Mask = CollisionLayers.Arrow | CollisionLayers.Player
            });

            if (waveComp != null)
                waveComp.AliveMonsters++;
        }
    }
}
