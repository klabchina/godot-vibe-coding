using Game.Ecs;
using Game.Ecs.Components;
using Game.Ecs.Core;
using Game.Server;

Console.WriteLine($"[Debug] Pre-load GameRandom calls: {GameRandom.CallCount}");



// 设置固定随机种子，确保客户端和服务端行为一致
GameRandom.ResetCallCount();
GameRandom.SetSeed(42);
Console.WriteLine("[ECS] Random seed set to 42 for deterministic simulation.");

// 加载关卡配置（对应 BattleScene._Ready 里的 StageLoader.Load）
Game.StageLoader.Load("stage_2");
Console.WriteLine($"[Debug] After StageLoader.Load GameRandom calls: {GameRandom.CallCount}");

// 加载地图配置
Game.MapLoader.LoadAll();
var map = Game.MapLoader.PickRandom();
Console.WriteLine($"[ECS] Map loaded: {map.Id}");


// 在关键 tick 输出详细状态用于调试
const bool DebugTick = true;
int lastKillCount = 0, lastTick = 0;

var gm = ServerGameManager.Instance;
gm.Initialize(map);

// 生成玩家
gm.SpawnPlayer(playerIndex: 0, x: Game.Data.ArenaData.Size.X / 2, y: Game.Data.ArenaData.Size.Y / 2);

// 启动第一波怪物
gm.StartWaves();

const float DeltaTime = 0.05f;  // 50ms 每帧（服务器逻辑步长）
const int PrintIntervalTicks = 200;  // 每 200 tick 打印一次状态（约 10 秒）
int tickCount = 0;
DateTime startTime = DateTime.Now;

Console.WriteLine("[ECS] Game loop starting. Press Ctrl+C to stop.");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    while (!cts.IsCancellationRequested)
    {
        gm.Tick(DeltaTime);
        tickCount++;

        // 每 PrintIntervalTicks tick 打印一次状态（含当前波次）
        if (tickCount % PrintIntervalTicks == 0)
        {
            var elapsed = (DateTime.Now - startTime).TotalSeconds;
            var waveEntities = gm.World.GetEntitiesWith<WaveComponent>();
            int currentWave = 0, aliveMonsters = 0;
            foreach (var w in waveEntities)
            {
                var wave = w.Get<WaveComponent>();
                currentWave = wave.CurrentWave;
                aliveMonsters = wave.AliveMonsters;
                break;
            }
            // 注意：不要在这里调用 gm.World.Entities.Count 或 GetEntitiesWith，因为会消耗额外的随机数
            Console.WriteLine($"[Tick {tickCount,6} | {elapsed,6:F1}s] Wave {currentWave} | Alive monsters: {aliveMonsters}");
        }

        // 检测游戏结束
        var gameOverResult = GameOverHelper.CheckGameOver(gm);
        if (gameOverResult != null)
        {
            GameOverHelper.PrintGameOverStats(gm, gameOverResult.Value, tickCount, (DateTime.Now - startTime).TotalSeconds);
            break;
        }
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("[ECS] Interrupted.");
}

Console.WriteLine("[ECS] Shutting down.");
gm.Reset();

// ─────────────────────────────────────────────────────────────────────────────
// 游戏结束检测 & 统计打印
// ─────────────────────────────────────────────────────────────────────────────

static class GameOverHelper
{
    public enum GameOverType { Victory, Defeat }

    public static GameOverType? CheckGameOver(ServerGameManager gm)
    {
        // 检查 WaveComponent
        var waveEntities = gm.World.GetEntitiesWith<WaveComponent>();
        foreach (var waveEntity in waveEntities)
        {
            var wave = waveEntity.Get<WaveComponent>();

            // Victory: 所有波次已完成，且本波怪物全部清空
            if (wave.AllWavesComplete && wave.AliveMonsters <= 0)
            {
                return GameOverType.Victory;
            }
        }

        // Defeat: 所有玩家血量 <= 0
        var players = gm.World.GetEntitiesWith<PlayerComponent>();
        if (players.Count > 0)
        {
            bool allDead = true;
            foreach (var player in players)
            {
                if (player.Has<HealthComponent>())
                {
                    var hp = player.Get<HealthComponent>();
                    if (hp.Hp > 0)
                    {
                        allDead = false;
                        break;
                    }
                }
            }
            if (allDead)
            {
                return GameOverType.Defeat;
            }
        }

        return null;
    }

    public static void PrintGameOverStats(ServerGameManager gm, GameOverType result, int tickCount, double totalSeconds)
    {
        var waveEntities = gm.World.GetEntitiesWith<WaveComponent>();
        int wavesCompleted = 0;
        foreach (var w in waveEntities)
        {
            wavesCompleted = w.Get<WaveComponent>().CurrentWave;
            break;
        }

        var players = gm.World.GetEntitiesWith<PlayerComponent>();
        var up = players.Count > 0 ? players[0].Get<UpgradeComponent>() : null;

        // [ECS一致性日志] 输出战斗结算数据，用于对比客户端/服务端运行结果
        Console.WriteLine("========== ECS 战斗结算 ==========");
        Console.WriteLine($"结果: {(result == GameOverType.Victory ? "胜利" : "失败")}");
        Console.WriteLine($"完成波数: {wavesCompleted}");
        Console.WriteLine($"存活玩家数: {players.Count}");
        foreach (var player in players)
        {
            var pc = player.Get<PlayerComponent>();
            var hp = player.Get<HealthComponent>();
            Console.WriteLine($"  玩家: KillCount={pc.KillCount}, TotalDamage={pc.TotalDamageDealt}, " +
                $"Level={pc.CurrentLevel}, Xp={pc.TotalXp}, Hp={hp.Hp}/{hp.MaxHp}");
            if (up != null)
            {
                Console.WriteLine($"  升级: OrbitCount={up.OrbitCount}");
            }
        }
        Console.WriteLine($"存活怪物数: {gm.World.GetEntitiesWith<MonsterComponent>().Count}");
        Console.WriteLine($"Tick数: {tickCount}");
        Console.WriteLine($"总耗时: {totalSeconds:F1}s");
        Console.WriteLine("=================================");
        Console.WriteLine($"[Debug] GameRandom calls: {GameRandom.CallCount}");
    }
}
