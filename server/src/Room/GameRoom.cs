using Server.Game;
using Server.Proto;
using Server.Session;

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
/// 单局游戏房间（服务器权威）
/// </summary>
public sealed class GameRoom
{
    private static int _tickCounter = 0;
    
    public string RoomId { get; }
    public RoomState State { get; private set; } = RoomState.WaitingReady;
    public GameResult? Result { get; private set; }
    
    // 玩家
    private readonly Dictionary<string, PlayerState> _players = new();
    private readonly HashSet<string> _readyPlayers = new();
    
    // 游戏实体
    private readonly List<MonsterState> _monsters = new();
    private readonly List<ArrowState> _arrows = new();
    
    // 波次控制器
    private readonly WaveController _waveController;
    
    // 战斗统计
    public int TotalKills => _players.Values.Sum(p => p.KillCount);
    
    // 事件
    public event Action<GameStateSnapshot>? OnBroadcast;  // 广播状态给客户端
    public event Action<EntityDeathMsg>? OnEntityDeath;     // 实体死亡
    public event Action<WaveStartMsg>? OnWaveStart;        // 波次开始
    public event Action<GameOverMsg>? OnGameOver;          // 游戏结束
    public event Action<string, int>? OnPlayerLevelUp;     // 玩家升级 (playerId, newLevel)
    
    public GameRoom(string roomId, params string[] playerIds)
    {
        RoomId = roomId;
        
        // 初始化玩家
        for (int i = 0; i < playerIds.Length; i++)
        {
            var playerId = playerIds[i];
            var player = new PlayerState
            {
                PlayerId = playerId,
                PlayerName = $"Player{i + 1}",
                Slot = i,
                X = 400 + i * 400,  // 初始位置
                Y = 450,
                MaxHp = GameConfig.PlayerMaxHp,
                Hp = GameConfig.PlayerMaxHp,
                OwnerId = playerId
            };
            _players[playerId] = player;
        }
        
        // 初始化波次控制器
        _waveController = new WaveController();
        _waveController.OnWaveStart += HandleWaveStart;
        _waveController.OnSpawnMonster += HandleSpawnMonster;
        _waveController.OnAllWavesCompleted += HandleAllWavesCompleted;
    }
    
    /// <summary>
    /// 添加玩家
    /// </summary>
    public void AddPlayer(string playerId, string playerName)
    {
        if (_players.ContainsKey(playerId)) return;
        
        var player = new PlayerState
        {
            PlayerId = playerId,
            PlayerName = playerName,
            Slot = _players.Count,
            X = 400 + _players.Count * 400,
            Y = 450,
            MaxHp = GameConfig.PlayerMaxHp,
            Hp = GameConfig.PlayerMaxHp,
            OwnerId = playerId
        };
        _players[playerId] = player;
    }
    
    /// <summary>
    /// 玩家准备
    /// </summary>
    public void OnPlayerReady(string playerId)
    {
        if (State != RoomState.WaitingReady) return;
        
        _readyPlayers.Add(playerId);
        Console.WriteLine($"Player {playerId} ready. {_readyPlayers.Count}/{_players.Count}");
        
        // 双方都准备好后开始游戏
        if (_readyPlayers.Count >= 2 || _players.Count == 1)
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
        _waveController.Reset();
        Console.WriteLine($"Room {RoomId} started!");
    }
    
    /// <summary>
    /// 处理玩家输入
    /// </summary>
    public void OnPlayerInput(string playerId, PlayerInputMsg input)
    {
        if (!_players.TryGetValue(playerId, out var player)) return;
        
        player.LastInput = input;
        player.MoveDirX = input.MoveDir.X;
        player.MoveDirY = input.MoveDir.Y;
        player.AimAngle = input.AimAngle;
        player.ChargePower = input.ChargePower;
        
        // 处理射击
        if (input.Shoot && !player.IsCharging)
        {
            ShootArrow(player);
        }
        
        player.IsCharging = input.ChargePower > 0.1f;
    }
    
    /// <summary>
    /// 发射箭矢
    /// </summary>
    private void ShootArrow(PlayerState player)
    {
        var (dirX, dirY) = CollisionHelper.Normalize(
            MathF.Cos(player.AimAngle),
            MathF.Sin(player.AimAngle)
        );
        
        var arrow = new ArrowState(
            player.PlayerId,
            true,
            player.X,
            player.Y,
            dirX * GameConfig.ArrowSpeed,
            dirY * GameConfig.ArrowSpeed
        );
        arrow.Damage = (int)(GameConfig.PlayerBaseDamage * UpgradeConfig.DamageMultiplier(player.Level));
        
        _arrows.Add(arrow);
        player.ArrowsFired++;
        
        player.Action = PlayerAction.Shooting;
    }
    
    /// <summary>
    /// 游戏主循环 Tick
    /// </summary>
    public void Tick(float dt)
    {
        if (State != RoomState.Playing) return;
        
        _tickCounter++;
        
        // 1. 处理输入
        ProcessInputQueue(dt);
        
        // 2. 更新波次
        _waveController.Update(dt);
        
        // 3. 更新怪物 AI
        UpdateMonsters(dt);
        
        // 4. 更新箭矢
        UpdateArrows(dt);
        
        // 5. 碰撞检测
        CheckCollisions();
        
        // 6. 清理死亡实体
        CleanupDead();
        
        // 7. 检查游戏结束
        CheckGameOver();
        
        // 8. 广播状态
        BroadcastState();
    }
    
    /// <summary>
    /// 处理输入队列
    /// </summary>
    private void ProcessInputQueue(float dt)
    {
        foreach (var player in _players.Values)
        {
            if (player.LastInput == null) continue;
            
            // 移动
            var speed = GameConfig.PlayerSpeed * UpgradeConfig.SpeedMultiplier(player.Level);
            var newX = player.X + player.MoveDirX * speed * dt;
            var newY = player.Y + player.MoveDirY * speed * dt;
            
            // 边界检测
            var (clampedX, clampedY) = CollisionHelper.ClampToRectWithRadius(
                newX, newY, GameConfig.PlayerCollisionRadius,
                0, 0,
                GameConfig.ArenaWidth, GameConfig.ArenaHeight
            );
            
            player.X = clampedX;
            player.Y = clampedY;
            
            // 更新状态
            if (MathF.Abs(player.MoveDirX) > 0.1f || MathF.Abs(player.MoveDirY) > 0.1f)
            {
                player.Action = PlayerAction.Moving;
            }
            else if (player.IsCharging)
            {
                player.Action = PlayerAction.Charging;
            }
            else
            {
                player.Action = PlayerAction.Idle;
            }
        }
    }
    
    /// <summary>
    /// 更新怪物 AI
    /// </summary>
    private void UpdateMonsters(float dt)
    {
        foreach (var monster in _monsters)
        {
            if (!monster.IsAlive) continue;
            
            monster.StateTimer += dt;
            
            // 简单 AI：朝最近玩家移动
            var target = FindClosestPlayer(monster);
            if (target != null)
            {
                var dirX = target.X - monster.X;
                var dirY = target.Y - monster.Y;
                var (nx, ny) = CollisionHelper.Normalize(dirX, dirY);
                
                monster.VX = nx * monster.Speed;
                monster.VY = ny * monster.Speed;
                monster.X += monster.VX * dt;
                monster.Y += monster.VY * dt;
                
                // 更新朝向
                monster.Rotation = MathF.Atan2(ny, nx);
                
                // 攻击冷却
                monster.AttackCooldown -= dt;
                if (monster.AttackCooldown <= 0 && DistanceToPlayer(monster, target) <= monster.AttackRange)
                {
                    // 怪物攻击
                    target.Hp -= monster.AttackDamage;
                    monster.AttackCooldown = 1f;
                    monster.State = MonsterStateType.Attack;
                    
                    if (target.Hp <= 0)
                    {
                        target.Hp = 0;
                        OnEntityDeath?.Invoke(new EntityDeathMsg
                        {
                            Type = EntityDeathMsg.EntityType.Player,
                            EntityId = 0,
                            Position = new Vec2 { X = target.X, Y = target.Y },
                            KillerId = monster.Id
                        });
                    }
                }
                else
                {
                    monster.State = MonsterStateType.Walk;
                }
            }
            
            // 边界检测
            var (cx, cy) = CollisionHelper.ClampToRectWithRadius(
                monster.X, monster.Y, GameConfig.MonsterCollisionRadius,
                0, 0,
                GameConfig.ArenaWidth, GameConfig.ArenaHeight
            );
            monster.X = cx;
            monster.Y = cy;
        }
    }
    
    private PlayerState? FindClosestPlayer(MonsterState monster)
    {
        PlayerState? closest = null;
        float minDist = float.MaxValue;
        
        foreach (var player in _players.Values)
        {
            if (!player.IsAlive) continue;
            
            var dist = CollisionHelper.DistanceSq(monster.X, monster.Y, player.X, player.Y);
            if (dist < minDist)
            {
                minDist = dist;
                closest = player;
            }
        }
        
        return closest;
    }
    
    private float DistanceToPlayer(MonsterState monster, PlayerState player)
    {
        return CollisionHelper.Distance(monster.X, monster.Y, player.X, player.Y);
    }
    
    /// <summary>
    /// 更新箭矢
    /// </summary>
    private void UpdateArrows(float dt)
    {
        foreach (var arrow in _arrows)
        {
            if (!arrow.IsAlive) continue;
            arrow.Update(dt);
            
            // 边界检测
            if (arrow.X < -50 || arrow.X > GameConfig.ArenaWidth + 50 ||
                arrow.Y < -50 || arrow.Y > GameConfig.ArenaHeight + 50)
            {
                arrow.LifeTime = arrow.MaxLifeTime;  // 标记销毁
            }
        }
    }
    
    /// <summary>
    /// 碰撞检测
    /// </summary>
    private void CheckCollisions()
    {
        // 箭矢 vs 怪物
        foreach (var arrow in _arrows.ToList())
        {
            if (!arrow.IsAlive || !arrow.IsPlayerArrow) continue;
            
            foreach (var monster in _monsters)
            {
                if (!monster.IsAlive) continue;
                
                if (CollisionHelper.CircleCollision(
                    arrow.X, arrow.Y, GameConfig.ArrowCollisionRadius,
                    monster.X, monster.Y, GameConfig.MonsterCollisionRadius))
                {
                    // 命中
                    monster.TakeDamage(arrow.Damage, arrow.OwnerId);
                    arrow.LifeTime = arrow.MaxLifeTime;  // 标记销毁
                    
                    // 记录伤害
                    if (_players.TryGetValue(arrow.OwnerId, out var player))
                    {
                        player.TotalDamage += arrow.Damage;
                    }
                    
                    if (monster.IsDead)
                    {
                        // 怪物死亡
                        _waveController.OnMonsterDied();
                        
                        // 奖励经验
                        if (_players.TryGetValue(arrow.OwnerId, out var killer))
                        {
                            killer.KillCount++;
                            AwardXp(killer, 10);
                        }
                        
                        OnEntityDeath?.Invoke(new EntityDeathMsg
                        {
                            Type = EntityDeathMsg.EntityType.Monster,
                            EntityId = monster.Id,
                            Position = new Vec2 { X = monster.X, Y = monster.Y },
                            KillerId = killer?.KillCount ?? 0
                        });
                    }
                    
                    break;
                }
            }
        }
    }
    
    /// <summary>
    /// 奖励经验值
    /// </summary>
    private void AwardXp(PlayerState player, int xp)
    {
        player.Xp += xp;
        
        // 检查升级
        while (player.Xp >= player.XpToNextLevel)
        {
            player.Xp -= player.XpToNextLevel;
            player.Level++;
            player.XpToNextLevel = UpgradeConfig.XpForLevel(player.Level);
            
            Console.WriteLine($"Player {player.PlayerId} leveled up to {player.Level}!");
            OnPlayerLevelUp?.Invoke(player.PlayerId, player.Level);
        }
    }
    
    /// <summary>
    /// 清理死亡实体
    /// </summary>
    private void CleanupDead()
    {
        _arrows.RemoveAll(a => !a.IsAlive);
        _monsters.RemoveAll(m => m.IsDead && m.StateTimer > 2f);  // 延迟删除死亡怪物
    }
    
    /// <summary>
    /// 检查游戏结束
    /// </summary>
    private void CheckGameOver()
    {
        // 检查胜利
        if (_waveController.IsCompleted)
        {
            EndGame(GameResult.Victory);
            return;
        }
        
        // 检查失败（所有玩家死亡）
        if (_players.Values.All(p => !p.IsAlive))
        {
            EndGame(GameResult.Defeat);
        }
    }
    
    private void EndGame(GameResult result)
    {
        State = RoomState.Finished;
        Result = result;
        
        var scores = _players.Values.Select(p => new PlayerScoreMsg
        {
            PlayerId = p.PlayerId,
            PlayerName = p.PlayerName,
            Kills = p.KillCount,
            DamageDealt = p.TotalDamage,
            ArrowsFired = p.ArrowsFired,
            Level = p.Level
        }).ToList();
        
        OnGameOver?.Invoke(new GameOverMsg
        {
            Result = (GameOverMsg.GameResult)result,
            WavesCleared = _waveController.CurrentWave,
            TotalKills = TotalKills,
            Scores = scores
        });
        
        Console.WriteLine($"Room {RoomId} ended: {result}");
    }
    
    /// <summary>
    /// 广播游戏状态
    /// </summary>
    private void BroadcastState()
    {
        var snapshot = new GameStateSnapshot
        {
            ServerTick = _tickCounter,
            WaveInfo = _waveController.GetWaveInfo()
        };
        
        foreach (var player in _players.Values)
        {
            snapshot.Players.Add(player.ToMsg());
        }
        
        foreach (var arrow in _arrows)
        {
            if (arrow.IsAlive)
            {
                snapshot.Arrows.Add(arrow.ToMsg());
            }
        }
        
        foreach (var monster in _monsters)
        {
            if (monster.IsAlive)
            {
                snapshot.Monsters.Add(monster.ToMsg());
            }
        }
        
        OnBroadcast?.Invoke(snapshot);
    }
    
    /// <summary>
    /// 处理波次开始
    /// </summary>
    private void HandleWaveStart(int waveNumber, int monsterCount)
    {
        OnWaveStart?.Invoke(new WaveStartMsg
        {
            WaveNumber = waveNumber,
            MonsterCount = monsterCount
        });
    }
    
    /// <summary>
    /// 处理生成怪物
    /// </summary>
    private void HandleSpawnMonster(MonsterType type, float x, float y)
    {
        var monster = new MonsterState(type, x, y);
        _monsters.Add(monster);
    }
    
    /// <summary>
    /// 处理所有波次完成
    /// </summary>
    private void HandleAllWavesCompleted()
    {
        EndGame(GameResult.Victory);
    }
    
    /// <summary>
    /// 获取玩家
    /// </summary>
    public PlayerState? GetPlayer(string playerId)
    {
        return _players.TryGetValue(playerId, out var player) ? player : null;
    }
    
    /// <summary>
    /// 获取所有玩家
    /// </summary>
    public IEnumerable<PlayerState> GetPlayers() => _players.Values;
}
