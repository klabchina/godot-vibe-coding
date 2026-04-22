using Game.Ecs;
using Game.Ecs.Components;
using Game.Server;

// 加载关卡配置（对应 BattleScene._Ready 里的 StageLoader.Load）
Game.StageLoader.Load("stage_2");

var gm = ServerGameManager.Instance;
gm.Initialize();

// 生成玩家
gm.SpawnPlayer(playerIndex: 0, x: 990f, y: 640f);

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
            Console.WriteLine($"[Tick {tickCount,6} | {elapsed,6:F1}s] Wave {currentWave} | Alive monsters: {aliveMonsters} | Entities: {gm.World.Entities.Count}");
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
        var divider = new string('=', 50);

        Console.WriteLine();
        Console.WriteLine(divider);
        Console.WriteLine($"  GAME OVER — {(result == GameOverType.Victory ? "VICTORY" : "DEFEAT")}");
        Console.WriteLine(divider);

        // 玩家统计
        var players = gm.World.GetEntitiesWith<PlayerComponent>();
        foreach (var player in players)
        {
            var pc = player.Get<PlayerComponent>();
            var hp = player.Get<HealthComponent>();

            Console.WriteLine($"  Player {pc.PlayerIndex}");
            Console.WriteLine($"    Kill Count  : {pc.KillCount}");
            Console.WriteLine($"    HP          : {hp.Hp} / {hp.MaxHp}");
        }

        // 波次信息
        var waveEntities = gm.World.GetEntitiesWith<WaveComponent>();
        foreach (var waveEntity in waveEntities)
        {
            var wave = waveEntity.Get<WaveComponent>();
            Console.WriteLine($"  Waves Cleared: {wave.CurrentWave}");
        }

        // 存活实体
        var aliveCount = gm.World.Entities.Count;
        Console.WriteLine($"  Entities Left: {aliveCount}");
        Console.WriteLine($"  Total Ticks   : {tickCount}");
        Console.WriteLine($"  Total Time    : {totalSeconds:F1}s");

        Console.WriteLine(divider);
    }
}
