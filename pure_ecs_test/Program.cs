using Game.Ecs;
using Game.Ecs.Components;
using Game.Server;

var gm = ServerGameManager.Instance;
gm.Initialize();

// 生成玩家
gm.SpawnPlayer(playerIndex: 0, x: 990f, y: 640f);

// 启动第一波怪物
gm.StartWaves();

// 20 tick/s 主循环（每帧 50ms）
const float DeltaTime = 0.033f;
const int TargetHz = 30;
int tickCount = 0;

Console.WriteLine("[Server] Game loop starting at 20 tick/s. Press Ctrl+C to stop.");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000f / TargetHz));

while (!cts.IsCancellationRequested && await timer.WaitForNextTickAsync(cts.Token))
{
    gm.Tick(DeltaTime);
    tickCount++;

    // 每 200 tick（10 秒）打印一次状态
    if (tickCount % 200 == 0)
    {
        var entities = gm.World.Entities;
        Console.WriteLine($"[Tick {tickCount,6}] Entities alive: {entities.Count}");
    }

    // 检测游戏结束
    var gameOverResult = GameOverHelper.CheckGameOver(gm);
    if (gameOverResult != null)
    {
        GameOverHelper.PrintGameOverStats(gm, gameOverResult.Value, tickCount);
        break;
    }
}

Console.WriteLine("[Server] Shutting down.");
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

    public static void PrintGameOverStats(ServerGameManager gm, GameOverType result, int tickCount)
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
        Console.WriteLine($"  Total Time    : {tickCount * 0.05f:F1}s");

        Console.WriteLine(divider);
    }
}
