using Game.Ecs;
using Game.Ecs.Components;
using Game.Ecs.Systems;
using Game.Data;

namespace Game.Server;

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
    public int KillCount       { get; private set; }
    public int TotalDamage     { get; private set; }
    public int WavesCompleted  { get; private set; }

    private ServerGameManager() { }

    /// <summary>初始化 ECS World 并注册所有服务端系统。</summary>
    public void Initialize()
    {
        World = new World();

        // 顺序决定每帧执行次序
        World.AddSystem(new MonsterAISystem());
        World.AddSystem(new BossAISystem());
        World.AddSystem(new AutoAimSystem());
        World.AddSystem(new BuffSystem());
        World.AddSystem(new MovementSystem());
        World.AddSystem(new CollisionSystem());
        World.AddSystem(new DamageSystem());
        World.AddSystem(new EffectSystem());
        World.AddSystem(new OrbitSystem());
        World.AddSystem(new PickupSystem());
        World.AddSystem(new WaveSpawnSystem());
        World.AddSystem(new DeathSystem());

        Console.WriteLine("[ServerGameManager] World initialized with all server systems.");
    }

    /// <summary>创建玩家实体并添加到 World。</summary>
    public Entity SpawnPlayer(int playerIndex, float x, float y)
    {
        var player = World.CreateEntity();
        player.Add(new TransformComponent { Position = new(x, y) });
        player.Add(new VelocityComponent  { Speed = PlayerData.BaseMoveSpeed });
        player.Add(new HealthComponent    { Hp = PlayerData.BaseHp, MaxHp = PlayerData.BaseHp });
        player.Add(new PlayerComponent    { PlayerIndex = playerIndex, IsLocal = false });
        player.Add(new BowComponent
        {
            Damage      = PlayerData.BaseArrowDamage,
            Cooldown    = PlayerData.BaseCooldown,
            ArrowCount  = PlayerData.BaseArrowCount,
        });
        player.Add(new BuffComponent());
        player.Add(new UpgradeComponent());
        player.Add(new AutoAimComponent());
        player.Add(new ColliderComponent
        {
            Radius = PlayerData.PlayerRadius,
            Layer  = CollisionLayers.Player,
            Mask   = CollisionLayers.Monster | CollisionLayers.MonsterArrow,
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
        IsRunning     = false;
        KillCount     = 0;
        TotalDamage   = 0;
        WavesCompleted = 0;
        World?.Clear();
    }
}
