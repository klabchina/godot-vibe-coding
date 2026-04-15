using Game.Server;

var gm = ServerGameManager.Instance;
gm.Initialize();

// 生成玩家
gm.SpawnPlayer(playerIndex: 0, x: 990f, y: 640f);

// 启动第一波怪物
gm.StartWaves();

// 20 tick/s 主循环（每帧 50ms）
const float DeltaTime   = 0.05f;
const int   TargetHz    = 20;
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
}

Console.WriteLine("[Server] Shutting down.");
gm.Reset();
