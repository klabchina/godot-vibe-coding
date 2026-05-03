using Server.Match;
using Server.Room;
using Server.Network;
using Server.Session;
using Server.Game;

namespace Server.Hosting;

/// <summary>
/// 游戏主循环服务（20 tick/s）
/// </summary>
public sealed class GameLoopService : BackgroundService
{
    private readonly MatchService _match;
    private readonly RoomManager _rooms;
    private readonly SessionManager _sessions;
    private readonly ConnectionManager _connections;
    private readonly ILogger<GameLoopService> _logger;

    public GameLoopService(
        MatchService match,
        RoomManager rooms,
        SessionManager sessions,
        ConnectionManager connections,
        ILogger<GameLoopService> logger)
    {
        _match = match;
        _rooms = rooms;
        _sessions = sessions;
        _connections = connections;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        _logger.LogInformation("GameLoop started at 20 tick/s");

        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            const float dt = 0.05f;
            
            try
            {
                // 1. 处理匹配
                _match.Tick();
                
                // 2. 更新所有房间
                _rooms.Tick(dt);
                
                // 3. 检查心跳超时
                CheckHeartbeatTimeout();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in game loop tick");
            }
        }
    }

    /// <summary>
    /// 检查心跳超时
    /// </summary>
    private void CheckHeartbeatTimeout()
    {
        var timeout = TimeSpan.FromSeconds(GameConfig.HeartbeatTimeoutSec);
        foreach (var connectionId in _connections.GetTimedOutConnections(timeout))
        {
            _logger.LogWarning("Connection heartbeat timeout: {ConnectionId}", connectionId);
            
            // 不支持重连：断线即踢
            HandleDisconnect(connectionId);
            _connections.Remove(connectionId);
        }
    }

    /// <summary>
    /// 处理断线（不支持重连：断线即踢）
    /// </summary>
    private void HandleDisconnect(string connectionId)
    {
        DisconnectPolicy.KickByConnection(_sessions, _rooms, connectionId);
    }
}
