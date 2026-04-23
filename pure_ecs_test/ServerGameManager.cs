using Game.Ecs;
using Game.Ecs.Components;
using Game.Ecs.Core;
using Game.Ecs.Systems;
using Game.Data;

namespace Game.Server;

/// <summary>
/// 玩家出生数据结构体，对应客户端 BattleScene 中创建玩家时的数据。
/// </summary>
public readonly struct PlayerSpawnData
{
    public readonly int PlayerIndex;
    public readonly Vec2 Position;

    public PlayerSpawnData(int playerIndex, Vec2 position)
    {
        PlayerIndex = playerIndex;
        Position = position;
    }

    public static PlayerSpawnData Default(int playerIndex = 0) =>
        new(playerIndex, ArenaData.Size / 2);
}

/// <summary>
/// 服务端游戏实例管理器，对应客户端的 GameManager。
/// 维护一个独立的 ECS World，包含全部服务端系统（不含 ClientSystems）。
/// </summary>
public class ServerGameManager
{
    public static ServerGameManager Instance { get; private set; } = new();

    public World World { get; private set; }
    public bool IsRunning { get; private set; }

    // 战局统计
    public int KillCount { get; private set; }
    public int TotalDamage { get; private set; }
    public int WavesCompleted { get; private set; }

    private ServerGameManager() { }

    /// <summary>
    /// 初始化 ECS World 并注册所有服务端系统。
    /// 创建顺序与 BattleScene.InitializeWorld 保持一致：先玩家 → obstacles → 系统注册。
    /// </summary>
    public void Initialize(MapConfig map, PlayerSpawnData playerData)
    {
        World = new World();

        // 创建玩家实体（与 BattleScene.InitializeWorld 第 66-92 行保持一致）
        SpawnPlayer(playerData.PlayerIndex, playerData.Position.X, playerData.Position.Y);

        // Spawn map obstacles（对应 BattleScene 第 99 行）
        Game.MapLoader.SpawnObstacles(map, World);

        // 顺序与 BattleScene.InitializeWorld 保持一致（去掉客户端专用的 InputSystem / RenderSystem）
        var waveSpawnSystem = new WaveSpawnSystem();
        World.AddSystem(waveSpawnSystem);

        var bossAISystem = new BossAISystem();
        bossAISystem.OnBossPhaseChange = OnBossPhaseChange;
        World.AddSystem(bossAISystem);

        World.AddSystem(new MonsterAISystem());
        World.AddSystem(new MeleeAttackSystem());
        World.AddSystem(new AutoAimSystem());
        World.AddSystem(new MovementSystem());
        World.AddSystem(new CollisionSystem());
        World.AddSystem(new OrbitSystem());

        var pickupSystem = new PickupSystem();
        pickupSystem.OnLevelUp = OnPlayerLevelUp;
        World.AddSystem(pickupSystem);

        World.AddSystem(new DamageSystem());
        World.AddSystem(new EffectSystem());
        World.AddSystem(new BuffSystem());
        World.AddSystem(new DeathSystem());

        Console.WriteLine("[ServerGameManager] World initialized with all server systems.");
    }

    /// <summary>创建玩家实体并添加到 World。</summary>
    public Entity SpawnPlayer(int playerIndex, float x, float y)
    {
        var player = World.CreateEntity();
        player.Add(new TransformComponent { Position = new(x, y) });
        player.Add(new VelocityComponent { Speed = PlayerData.BaseMoveSpeed });
        player.Add(new HealthComponent { Hp = PlayerData.BaseHp, MaxHp = PlayerData.BaseHp });
        player.Add(new PlayerComponent { PlayerIndex = playerIndex, IsLocal = false });
        player.Add(new BowComponent
        {
            Damage = PlayerData.BaseArrowDamage,
            Cooldown = PlayerData.BaseCooldown,
            CooldownTimer = 0,
            ArrowCount = PlayerData.BaseArrowCount,
            SpreadAngle = 0,
        });
        player.Add(new AutoAimComponent { TargetId = -1, SearchRadius = 0 });
        player.Add(new BuffComponent());
        player.Add(new UpgradeComponent());
        player.Add(new OrbitComponent());
        player.Add(new ColliderComponent
        {
            Shape = ColliderShape.Box,
            Radius = PlayerData.PlayerRadius,
            Layer = CollisionLayers.Player,
            Mask = CollisionLayers.Monster | CollisionLayers.Pickup,
            HalfWidth = PlayerData.HalfWidth,
            HalfHeight = PlayerData.HalfHeight,
        });

        Console.WriteLine($"[ServerGameManager] Player {playerIndex} spawned at ({x}, {y}).");
        return player;
    }

    /// <summary>创建 WaveComponent 实体并启动第一波。</summary>
    public Entity StartWaves()
    {
        var waveEntity = World.CreateEntity();
        waveEntity.Add(new WaveComponent());
        World.GetSystem<WaveSpawnSystem>()!.StartNextWave(waveEntity.Get<WaveComponent>());

        Console.WriteLine("[ServerGameManager] Wave 1 started.");
        return waveEntity;
    }

    /// <summary>推进一帧（delta 单位：秒）。</summary>
    public void Tick(float delta)
    {
        World.Update(delta);
    }

    /// <summary>重置战局数据并清空 World。</summary>
    public void Reset()
    {
        IsRunning = false;
        KillCount = 0;
        TotalDamage = 0;
        WavesCompleted = 0;
        World?.Clear();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Boss 阶段切换回调
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Boss 阶段切换时给所有玩家发放 XP 并触发升级检测。
    /// 与 BattleScene.OnBossPhaseChange 保持一致。
    /// </summary>
    private void OnBossPhaseChange(int xpReward)
    {
        var players = World.GetEntitiesWith<PlayerComponent>();
        foreach (var player in players)
        {
            var playerComp = player.Get<PlayerComponent>();
            playerComp.TotalXp += xpReward;

            int newLevel = LevelData.GetLevel(playerComp.TotalXp);
            if (newLevel > playerComp.CurrentLevel)
            {
                playerComp.CurrentLevel = newLevel;
                OnPlayerLevelUp(player, newLevel);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 升级回调（服务端模式：无 UI，自动处理）
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 玩家升级回调。服务端模式下使用 UpgradeRoller 随机抽取 3 个选项，
    /// 自动选择第 1 项并应用，同时触发即时效果（血量上限/移速/护盾/轨道卫）。
    /// </summary>
    private void OnPlayerLevelUp(Entity playerEntity, int newLevel)
    {
        return;
        var player = playerEntity.Get<PlayerComponent>();
        if (player == null) return;

        var upgrade = playerEntity.Get<UpgradeComponent>();
        if (upgrade == null) return;

        var options = UpgradeRoller.Roll(upgrade, newLevel);
        if (options.Count == 0) return;

        var chosen = options[0];
        upgrade.Apply(chosen);

        // 即时效果（与 UpgradePanel.ApplyImmediateEffects 保持一致）
        switch (chosen)
        {
            case UpgradeId.MaxHpUp:
                {
                    var health = playerEntity.Get<HealthComponent>();
                    if (health != null)
                    {
                        health.MaxHp = UpgradeData.GetMaxHp(upgrade.MaxHpLevel);
                        health.Hp = GMath.Min(health.Hp + UpgradeData.HpHealPerUpgrade, health.MaxHp);
                    }
                    break;
                }
            case UpgradeId.MoveSpeedUp:
                {
                    var vel = playerEntity.Get<VelocityComponent>();
                    if (vel != null)
                    {
                        vel.Speed = UpgradeData.GetMoveSpeed(upgrade.MoveSpeedLevel);
                    }
                    break;
                }
            case UpgradeId.Shield:
                {
                    var buff = playerEntity.Get<BuffComponent>();
                    if (buff != null)
                    {
                        buff.ShieldActive = true;
                        buff.ShieldCooldown = UpgradeData.ShieldRegenInterval;
                    }
                    break;
                }
            case UpgradeId.OrbitGuard:
                {
                    var orbit = playerEntity.Get<OrbitComponent>();
                    if (orbit != null)
                    {
                        orbit.Count = upgrade.OrbitCount;
                    }
                    break;
                }
        }

        var def = UpgradeData.Definitions[chosen];
        Console.WriteLine($"[LevelUp] Player {player.PlayerIndex} leveled up to Lv.{newLevel} → {def.Name}");
    }
}
