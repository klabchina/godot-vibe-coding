using Game.Ecs;
using Game.Ecs.Components;
using Game.Ecs.Core;
using Game.Ecs.Systems;
using Game.Data;
using Game;
using Server.Proto;
using Server.Game;

// 类型别名，解决 Vec2 二义性
using ProtoVec2 = Server.Proto.Vec2;
using GameVec2 = Game.Ecs.Core.Vec2;

namespace Server.Room;

/// <summary>
/// 房间状态
/// </summary>
public enum RoomState
{
    WaitingReady,   // 等待双方 Ready
    Playing,       // 战斗中
    Finished       // 已结束
}

/// <summary>
/// 游戏结果
/// </summary>
public enum GameResult
{
    Victory,
    Defeat,
    Disconnect
}

/// <summary>
/// 玩家出生数据结构体
/// </summary>
public readonly struct PlayerSpawnData
{
    public readonly int PlayerIndex;
    public readonly GameVec2 Position;

    public PlayerSpawnData(int playerIndex, GameVec2 position)
    {
        PlayerIndex = playerIndex;
        Position = position;
    }

    public static PlayerSpawnData Default(int playerIndex = 0) =>
        new(playerIndex, ArenaData.Size / 2);
}

/// <summary>
/// 单局游戏房间（服务器权威）
/// 使用 ECS 架构，与 ServerGameManager 保持一致
/// </summary>
public sealed class GameRoom
{
    private static int _tickCounter = 0;
    
    public string RoomId { get; }
    public RoomState State { get; private set; } = RoomState.WaitingReady;
    public GameResult? Result { get; private set; }
    
    // ECS World
    public World World { get; private set; }
    
    // 玩家映射：PlayerId -> EntityId
    private readonly Dictionary<string, int> _playerEntityMap = new();
    
    // 战局统计
    public int KillCount { get; private set; }
    public int TotalDamage { get; private set; }
    public int WavesCompleted { get; private set; }
    
    // 事件
    public event Action<GameStateSnapshot>? OnBroadcast;  // 广播状态给客户端
    public event Action<string, int>? OnPlayerLevelUp;   // 玩家升级 (playerId, newLevel)
    
    public GameRoom(string roomId, params string[] playerIds)
    {
        RoomId = roomId;
        World = new World();
        
        // 初始化玩家
        for (int i = 0; i < playerIds.Length; i++)
        {
            var playerId = playerIds[i];
            SpawnPlayer(i, playerId, 400 + i * 400, 450);
        }
        
        // 注册服务端系统
        RegisterSystems();
    }
    
    /// <summary>
    /// 注册所有服务端 ECS 系统
    /// </summary>
    private void RegisterSystems()
    {
        var waveSpawnSystem = new WaveSpawnSystem();
        World.AddSystem(waveSpawnSystem);

        var bossAISystem = new BossAISystem();
        World.AddSystem(bossAISystem);

        World.AddSystem(new MonsterAISystem());
        World.AddSystem(new MeleeAttackSystem());
        World.AddSystem(new AutoAimSystem());
        World.AddSystem(new MovementSystem());
        World.AddSystem(new CollisionSystem());
        World.AddSystem(new OrbitSystem());

        var pickupSystem = new PickupSystem();
        pickupSystem.OnLevelUp = OnPlayerLevelUpInternal;
        World.AddSystem(pickupSystem);

        World.AddSystem(new DamageSystem());
        World.AddSystem(new EffectSystem());
        World.AddSystem(new BuffSystem());
        World.AddSystem(new DeathSystem());
        
        Console.WriteLine($"[GameRoom:{RoomId}] ECS systems registered.");
    }
    
    /// <summary>
    /// 创建玩家实体
    /// </summary>
    public Entity SpawnPlayer(int playerIndex, string playerId, float x, float y)
    {
        var player = World.CreateEntity();
        _playerEntityMap[playerId] = player.Id;
        
        player.Add(new TransformComponent { Position = new GameVec2(x, y) });
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

        Console.WriteLine($"[GameRoom:{RoomId}] Player {playerIndex} ({playerId}) spawned at ({x}, {y}), EntityId={player.Id}");
        return player;
    }
    
    /// <summary>
    /// 创建 WaveComponent 实体并启动第一波
    /// </summary>
    public void StartWaves()
    {
        var waveEntity = World.CreateEntity();
        waveEntity.Add(new WaveComponent());
        World.GetSystem<WaveSpawnSystem>()!.StartNextWave(waveEntity.Get<WaveComponent>());
        
        Console.WriteLine($"[GameRoom:{RoomId}] Waves started.");
    }
    
    /// <summary>
    /// 添加玩家
    /// </summary>
    public void AddPlayer(string playerId, string playerName)
    {
        if (_playerEntityMap.ContainsKey(playerId)) return;
        
        int slot = _playerEntityMap.Count;
        SpawnPlayer(slot, playerId, 400 + slot * 400, 450);
    }
    
    /// <summary>
    /// 玩家准备
    /// </summary>
    public void OnPlayerReady(string playerId)
    {
        if (State != RoomState.WaitingReady) return;
        
        Console.WriteLine($"Player {playerId} ready. {_playerEntityMap.Count} players");
        
        if (_playerEntityMap.Count >= 1)
        {
            StartGame();
        }
    }
    
    /// <summary>
    /// 开始游戏
    /// </summary>
    public void StartGame()
    {
        State = RoomState.Playing;
        StartWaves();
        Console.WriteLine($"[GameRoom:{RoomId}] Game started!");
    }
    
    /// <summary>
    /// 处理玩家输入
    /// </summary>
    public void OnPlayerInput(string playerId, PlayerInputMsg input)
    {
        if (!_playerEntityMap.TryGetValue(playerId, out int entityId)) return;
        
        var player = World.GetEntity(entityId);
        if (player == null) return;
        
        var transform = player.Get<TransformComponent>();
        var velocity = player.Get<VelocityComponent>();
        var upgrade = player.Get<UpgradeComponent>();
        
        if (transform == null || velocity == null) return;

        // 更新瞄准角度
        transform.Rotation = input.AimAngle;

        // 根据升级等级更新速度基准值
        if (upgrade != null)
            velocity.Speed = UpgradeData.GetMoveSpeed(upgrade.MoveSpeedLevel);

        // 将输入方向转为速度向量，由 MovementSystem 负责位移
        var dir = new GameVec2(input.MoveDir.X, input.MoveDir.Y);
        velocity.Velocity = dir.Length() > 0.01f ? dir.Normalized() * velocity.Speed : GameVec2.Zero;
    }
    
    /// <summary>
    /// 游戏主循环 Tick
    /// </summary>
    public void Tick(float dt)
    {
        if (State != RoomState.Playing) return;
        
        _tickCounter++;
        
        // 使用 ECS World 更新
        World.Update(dt);
        
        // 更新战局统计
        UpdateStats();
        
        // 检查游戏结束
        var gameOver = CheckGameOver();
        if (gameOver.HasValue)
        {
            EndGame(gameOver.Value);
            return;
        }
        
        // 广播状态
        BroadcastState();
    }
    
    /// <summary>
    /// 更新战局统计
    /// </summary>
    private void UpdateStats()
    {
        KillCount = 0;
        TotalDamage = 0;
        WavesCompleted = 0;
        
        var waveEntities = World.GetEntitiesWith<WaveComponent>();
        foreach (var waveEntity in waveEntities)
        {
            var wave = waveEntity.Get<WaveComponent>();
            if (wave.AllWavesComplete && wave.AliveMonsters <= 0)
            {
                WavesCompleted = wave.CurrentWave;
            }
            break;
        }
        
        var players = World.GetEntitiesWith<PlayerComponent>();
        foreach (var player in players)
        {
            var pc = player.Get<PlayerComponent>();
            KillCount += pc.KillCount;
            TotalDamage += pc.TotalDamageDealt;
        }
    }
    
    /// <summary>
    /// 检查游戏结束
    /// </summary>
    private GameResult? CheckGameOver()
    {
        var waveEntities = World.GetEntitiesWith<WaveComponent>();
        foreach (var waveEntity in waveEntities)
        {
            var wave = waveEntity.Get<WaveComponent>();
            if (wave.AllWavesComplete && wave.AliveMonsters <= 0)
            {
                return GameResult.Victory;
            }
        }
        
        var players = World.GetEntitiesWith<PlayerComponent, HealthComponent>();
        bool allDead = true;
        foreach (var player in players)
        {
            var hp = player.Get<HealthComponent>();
            if (hp.Hp > 0)
            {
                allDead = false;
                break;
            }
            var revive = player.Get<ReviveComponent>();
            if (revive == null || !revive.HasRevived)
            {
                allDead = false;
                break;
            }
        }
        if (allDead && players.Count > 0)
        {
            return GameResult.Defeat;
        }
        
        return null;
    }
    
    private void EndGame(GameResult result)
    {
        State = RoomState.Finished;
        Result = result;
        
        var scores = new List<PlayerScoreMsg>();
        var players = World.GetEntitiesWith<PlayerComponent>();
        foreach (var player in players)
        {
            var pc = player.Get<PlayerComponent>();
            var hp = player.Get<HealthComponent>();
            
            scores.Add(new PlayerScoreMsg
            {
                PlayerId = GetPlayerIdByEntityId(player.Id) ?? $"Player{pc.PlayerIndex}",
                PlayerName = $"Player{pc.PlayerIndex}",
                Kills = pc.KillCount,
                DamageDealt = pc.TotalDamageDealt,
                ArrowsFired = 0,  // ECS 不追踪此字段
                Level = pc.CurrentLevel
            });
        }
        
        OnGameOver?.Invoke(new GameOverMsg
        {
            Result = (GameOverMsg.GameResult)result,
            WavesCleared = WavesCompleted,
            TotalKills = KillCount,
            Scores = scores
        });
        
        Console.WriteLine($"[GameRoom:{RoomId}] Game ended: {result}");
    }
    
    /// <summary>
    /// 根据 EntityId 获取 PlayerId
    /// </summary>
    private string? GetPlayerIdByEntityId(int entityId)
    {
        foreach (var kvp in _playerEntityMap)
        {
            if (kvp.Value == entityId)
                return kvp.Key;
        }
        return null;
    }
    
    /// <summary>
    /// 玩家升级回调
    /// </summary>
    private void OnPlayerLevelUpInternal(Entity playerEntity, int newLevel)
    {
        var player = playerEntity.Get<PlayerComponent>();
        if (player == null) return;

        var upgrade = playerEntity.Get<UpgradeComponent>();
        if (upgrade == null) return;

        var options = UpgradeRoller.Roll(upgrade, newLevel);
        if (options.Count == 0) return;

        var chosen = options[0];
        upgrade.Apply(chosen);

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
        Console.WriteLine($"[LevelUp] Player {player.PlayerIndex} ({GetPlayerIdByEntityId(playerEntity.Id)}) leveled up to Lv.{newLevel} → {def.Name}");
        
        var playerId = GetPlayerIdByEntityId(playerEntity.Id);
        if (playerId != null)
        {
            OnPlayerLevelUp?.Invoke(playerId, newLevel);
        }
    }
    
    /// <summary>
    /// 广播游戏状态
    /// </summary>
    private void BroadcastState()
    {
        var snapshot = new GameStateSnapshot
        {
            ServerTick = _tickCounter
        };
        
        // 玩家状态
        var players = World.GetEntitiesWith<PlayerComponent>();
        foreach (var player in players)
        {
            var pc = player.Get<PlayerComponent>();
            var transform = player.Get<TransformComponent>();
            var hp = player.Get<HealthComponent>();
            
            if (transform == null || hp == null) continue;
            
            snapshot.Players.Add(new PlayerStateMsg
            {
                PlayerId = GetPlayerIdByEntityId(player.Id) ?? $"Player{pc.PlayerIndex}",
                Position = new ProtoVec2 { X = transform.Position.X, Y = transform.Position.Y },
                AimAngle = transform.Rotation,
                Hp = hp.Hp,
                MaxHp = hp.MaxHp,
                Action = (int)PlayerAction.Idle,
                Level = pc.CurrentLevel,
                Xp = pc.TotalXp
            });
        }
        
        // 箭矢状态
        var arrows = World.GetEntitiesWith<ArrowComponent>();
        foreach (var arrow in arrows)
        {
            var transform = arrow.Get<TransformComponent>();
            var velocity = arrow.Get<VelocityComponent>();
            var arrowComp = arrow.Get<ArrowComponent>();
            
            if (transform == null) continue;
            
            snapshot.Arrows.Add(new ArrowStateMsg
            {
                ArrowId = arrow.Id,
                OwnerId = arrowComp.OwnerId.ToString(),
                Position = new ProtoVec2 { X = transform.Position.X, Y = transform.Position.Y },
                Velocity = velocity != null ? new ProtoVec2 { X = velocity.Velocity.X, Y = velocity.Velocity.Y } : new ProtoVec2(),
                Rotation = transform.Rotation,
                Damage = arrowComp.Damage,
                IsPlayerArrow = arrowComp.OwnerId >= 0
            });
        }
        
        // 怪物状态
        var monsters = World.GetEntitiesWith<MonsterComponent>();
        foreach (var monster in monsters)
        {
            var transform = monster.Get<TransformComponent>();
            var velocity = monster.Get<VelocityComponent>();
            var hp = monster.Get<HealthComponent>();
            var monsterComp = monster.Get<MonsterComponent>();
            
            if (transform == null || hp == null) continue;
            
            snapshot.Monsters.Add(new MonsterStateMsg
            {
                MonsterId = monster.Id,
                MonsterType = (int)monsterComp.Type,
                Position = new ProtoVec2 { X = transform.Position.X, Y = transform.Position.Y },
                Velocity = velocity != null ? new ProtoVec2 { X = velocity.Velocity.X, Y = velocity.Velocity.Y } : new ProtoVec2(),
                Rotation = transform.Rotation,
                Hp = hp.Hp,
                MaxHp = hp.MaxHp,
                State = (int)MonsterStateType.Walk
            });
        }
        
        // 波次信息
        var waveEntities = World.GetEntitiesWith<WaveComponent>();
        foreach (var waveEntity in waveEntities)
        {
            var wave = waveEntity.Get<WaveComponent>();
            snapshot.WaveInfo = new WaveInfoMsg
            {
                CurrentWave = wave.CurrentWave,
                TotalWaves = StageLoader.GetTotalWaves(),
                MonstersRemaining = wave.AliveMonsters,
                IntervalCountdown = wave.WaveIntervalTimer > 0 ? wave.WaveIntervalTimer : 0
            };
            break;
        }
        
        OnBroadcast?.Invoke(snapshot);
    }
    
    /// <summary>
    /// 重置战局
    /// </summary>
    public void Reset()
    {
        State = RoomState.WaitingReady;
        Result = null;
        KillCount = 0;
        TotalDamage = 0;
        WavesCompleted = 0;
        _tickCounter = 0;
        World.Clear();
        
        RegisterSystems();
    }
    
    /// <summary>
    /// 获取玩家
    /// </summary>
    public Entity? GetPlayer(string playerId)
    {
        if (_playerEntityMap.TryGetValue(playerId, out int entityId))
        {
            return World.GetEntity(entityId);
        }
        return null;
    }
    
    /// <summary>
    /// 获取所有玩家 Entity
    /// </summary>
    public IEnumerable<Entity> GetPlayers() => World.GetEntitiesWith<PlayerComponent>();
    
    // 事件
    public event Action<GameOverMsg>? OnGameOver;
}
