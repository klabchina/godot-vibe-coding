using Server.Proto;
using Server.Game;
using System.Text.Json;

namespace Server.Room;

/// <summary>
/// 波次状态
/// </summary>
public enum WaveState
{
    Interval,    // 波次间歇
    Spawning,    // 正在生成怪物
    InProgress,  // 怪物存活中，等待清完
}

/// <summary>
/// 波次配置
/// </summary>
public class WaveConfig
{
    public int WaveNumber { get; set; }
    public float PreWaveDelay { get; set; }
    public float SpawnInterval { get; set; }
    public List<SpawnEntry> Spawns { get; set; } = new();
}

public class SpawnEntry
{
    public int Type { get; set; }
    public int Count { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }
}

/// <summary>
/// 波次控制器
/// </summary>
public sealed class WaveController
{
    private readonly string _waveConfigPath;
    
    public int CurrentWave { get; private set; }
    public WaveState State { get; private set; } = WaveState.Interval;
    public float IntervalTimer { get; private set; }
    public int TotalWaves => _waveConfigs.Count;
    public int MonstersRemaining { get; private set; }
    
    private List<WaveConfig> _waveConfigs = new();
    private int _spawnIndex;  // 当前波次内的生成索引
    private float _spawnTimer;
    private List<SpawnEntry> _pendingSpawns = new();  // 待生成的怪物列表
    
    // 事件
    public event Action<int, int>? OnWaveStart;  // (waveNumber, monsterCount)
    public event Action<MonsterType, float, float>? OnSpawnMonster;  // (type, x, y)
    public event Action? OnAllWavesCompleted;
    
    public WaveController(string? waveConfigPath = null)
    {
        _waveConfigPath = waveConfigPath ?? "config/wave_config.json";
        LoadWaveConfigs();
    }
    
    /// <summary>
    /// 加载波次配置
    /// </summary>
    private void LoadWaveConfigs()
    {
        try
        {
            if (File.Exists(_waveConfigPath))
            {
                var json = File.ReadAllText(_waveConfigPath);
                _waveConfigs = JsonSerializer.Deserialize<List<WaveConfig>>(json) ?? new();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load wave config: {ex.Message}");
        }
        
        // 如果加载失败，使用默认配置
        if (_waveConfigs.Count == 0)
        {
            _waveConfigs = CreateDefaultWaveConfigs();
        }
        
        Console.WriteLine($"Loaded {TotalWaves} wave configs");
    }
    
    private List<WaveConfig> CreateDefaultWaveConfigs()
    {
        var configs = new List<WaveConfig>();
        
        // 波次1: 只有史莱姆
        configs.Add(new WaveConfig
        {
            WaveNumber = 1,
            PreWaveDelay = 3f,
            SpawnInterval = 1f,
            Spawns = new List<SpawnEntry> { new() { Type = 1, Count = 5 } }
        });
        
        // 波次2: 史莱姆 + 骷髅
        configs.Add(new WaveConfig
        {
            WaveNumber = 2,
            PreWaveDelay = 5f,
            SpawnInterval = 0.8f,
            Spawns = new List<SpawnEntry>
            {
                new() { Type = 1, Count = 8 },
                new() { Type = 2, Count = 2 }
            }
        });
        
        // 波次3: 更多敌人
        configs.Add(new WaveConfig
        {
            WaveNumber = 3,
            PreWaveDelay = 5f,
            SpawnInterval = 0.6f,
            Spawns = new List<SpawnEntry>
            {
                new() { Type = 1, Count = 3 },
                new() { Type = 2, Count = 5 },
                new() { Type = 3, Count = 2 }
            }
        });
        
        // 波次4-7: 逐渐增加难度
        configs.Add(new WaveConfig
        {
            WaveNumber = 4,
            PreWaveDelay = 5f,
            SpawnInterval = 0.5f,
            Spawns = new List<SpawnEntry>
            {
                new() { Type = 2, Count = 6 },
                new() { Type = 3, Count = 3 }
            }
        });
        
        configs.Add(new WaveConfig
        {
            WaveNumber = 5,
            PreWaveDelay = 8f,
            SpawnInterval = 0.5f,
            Spawns = new List<SpawnEntry>
            {
                new() { Type = 4, Count = 2 },
                new() { Type = 2, Count = 5 }
            }
        });
        
        configs.Add(new WaveConfig
        {
            WaveNumber = 6,
            PreWaveDelay = 5f,
            SpawnInterval = 0.4f,
            Spawns = new List<SpawnEntry>
            {
                new() { Type = 2, Count = 5 },
                new() { Type = 3, Count = 4 },
                new() { Type = 4, Count = 1 }
            }
        });
        
        configs.Add(new WaveConfig
        {
            WaveNumber = 7,
            PreWaveDelay = 5f,
            SpawnInterval = 0.3f,
            Spawns = new List<SpawnEntry>
            {
                new() { Type = 3, Count = 5 },
                new() { Type = 4, Count = 3 }
            }
        });
        
        // 波次8: Boss
        configs.Add(new WaveConfig
        {
            WaveNumber = 8,
            PreWaveDelay = 10f,
            SpawnInterval = 0.5f,
            Spawns = new List<SpawnEntry>
            {
                new() { Type = 5, Count = 1 },
                new() { Type = 2, Count = 8 },
                new() { Type = 4, Count = 2 }
            }
        });
        
        return configs;
    }
    
    /// <summary>
    /// 重置波次控制器
    /// </summary>
    public void Reset()
    {
        CurrentWave = 0;
        State = WaveState.Interval;
        IntervalTimer = 1f;  // 短暂延迟后开始
        _spawnIndex = 0;
        _spawnTimer = 0;
        _pendingSpawns.Clear();
        MonstersRemaining = 0;
    }
    
    /// <summary>
    /// 开始下一波
    /// </summary>
    public void StartNextWave()
    {
        if (CurrentWave >= TotalWaves)
        {
            OnAllWavesCompleted?.Invoke();
            return;
        }
        
        CurrentWave++;
        var config = _waveConfigs[CurrentWave - 1];
        
        // 构建待生成列表
        _pendingSpawns.Clear();
        foreach (var spawn in config.Spawns)
        {
            for (int i = 0; i < spawn.Count; i++)
            {
                _pendingSpawns.Add(spawn);
            }
        }
        
        // 打乱顺序
        ShuffleSpawns();
        
        _spawnIndex = 0;
        _spawnTimer = 0;
        State = WaveState.Spawning;
        
        var totalMonsters = _pendingSpawns.Count;
        MonstersRemaining = totalMonsters;
        
        Console.WriteLine($"Wave {CurrentWave}: Starting with {totalMonsters} monsters");
        OnWaveStart?.Invoke(CurrentWave, totalMonsters);
    }
    
    private void ShuffleSpawns()
    {
        var random = new Random();
        for (int i = _pendingSpawns.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (_pendingSpawns[i], _pendingSpawns[j]) = (_pendingSpawns[j], _pendingSpawns[i]);
        }
    }
    
    /// <summary>
    /// 更新波次
    /// </summary>
    public void Update(float dt)
    {
        switch (State)
        {
            case WaveState.Interval:
                IntervalTimer -= dt;
                if (IntervalTimer <= 0)
                {
                    StartNextWave();
                }
                break;
                
            case WaveState.Spawning:
                UpdateSpawning(dt);
                break;
                
            case WaveState.InProgress:
                // 等待所有怪物死亡
                break;
        }
    }
    
    private void UpdateSpawning(float dt)
    {
        if (_pendingSpawns.Count == 0)
        {
            State = WaveState.InProgress;
            return;
        }
        
        _spawnTimer -= dt;
        if (_spawnTimer <= 0)
        {
            var config = _waveConfigs[CurrentWave - 1];
            _spawnTimer = config.SpawnInterval;
            
            // 生成一个怪物
            var spawn = _pendingSpawns[_spawnIndex++];
            var (x, y) = GetRandomSpawnPosition();
            OnSpawnMonster?.Invoke((MonsterType)spawn.Type, x, y);
        }
    }
    
    /// <summary>
    /// 获取随机生成位置（地图边缘）
    /// </summary>
    private (float, float) GetRandomSpawnPosition()
    {
        var random = new Random();
        var side = random.Next(4);
        float x, y;
        
        const float margin = 50f;
        const float width = GameConfig.ArenaWidth;
        const float height = GameConfig.ArenaHeight;
        
        switch (side)
        {
            case 0: // 上
                x = random.NextSingle() * width;
                y = margin;
                break;
            case 1: // 下
                x = random.NextSingle() * width;
                y = height - margin;
                break;
            case 2: // 左
                x = margin;
                y = random.NextSingle() * height;
                break;
            default: // 右
                x = width - margin;
                y = random.NextSingle() * height;
                break;
        }
        
        return (x, y);
    }
    
    /// <summary>
    /// 怪物死亡时调用
    /// </summary>
    public void OnMonsterDied()
    {
        MonstersRemaining--;
        
        if (MonstersRemaining <= 0 && State == WaveState.InProgress)
        {
            // 波次完成
            Console.WriteLine($"Wave {CurrentWave} cleared!");
            
            if (CurrentWave >= TotalWaves)
            {
                OnAllWavesCompleted?.Invoke();
            }
            else
            {
                // 进入下一波间歇
                State = WaveState.Interval;
                var config = _waveConfigs[Math.Min(CurrentWave, _waveConfigs.Count - 1)];
                IntervalTimer = config.PreWaveDelay;
            }
        }
    }
    
    /// <summary>
    /// 获取波次信息
    /// </summary>
    public WaveInfoMsg GetWaveInfo()
    {
        return new WaveInfoMsg
        {
            CurrentWave = CurrentWave,
            TotalWaves = TotalWaves,
            MonstersRemaining = MonstersRemaining,
            IntervalCountdown = State == WaveState.Interval ? IntervalTimer : 0
        };
    }
    
    /// <summary>
    /// 是否已完成所有波次
    /// </summary>
    public bool IsCompleted => CurrentWave >= TotalWaves && State == WaveState.InProgress && MonstersRemaining <= 0;
}
