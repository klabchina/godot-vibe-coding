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
                
                // 4. 检查断线重连超时
                CheckReconnectTimeout();
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
            
            // 通知断线
            if (_sessions.TryGetByConnection(connectionId, out var session))
            {
                HandleDisconnect(session);
            }
            
            _connections.Remove(connectionId);
        }
    }

    /// <summary>
    /// 检查断线重连超时
    /// </summary>
    private void CheckReconnectTimeout()
    {
        foreach (var session in _sessions.GetAllSessions())
        {
            if (session.IsDisconnected && session.IsReconnectExpired())
            {
                _logger.LogWarning("Player reconnect timeout: {PlayerId}", session.PlayerId);
                
                // 通知游戏结束（断线）
                if (session.RoomId != null)
                {
                    var room = _rooms.GetRoom(session.RoomId);
                    // 通知其他玩家有人断线
                }
                
                // 清理会话
                _sessions.Remove(session.PlayerId);
            }
        }
    }

    /// <summary>
    /// 处理断线
    /// </summary>
    private void HandleDisconnect(Server.Session.Session session)
    {
        session.IsDisconnected = true;
        session.DisconnectTime = DateTime.UtcNow;
        session.State = SessionState.Idle;
        
        if (session.RoomId != null)
        {
            var room = _rooms.GetRoom(session.RoomId);
            // TODO: 通知房间玩家断线
        }
    }
}
