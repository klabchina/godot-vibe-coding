using System.Collections.Concurrent;
using Server.Session;

namespace Server.Match;

/// <summary>
/// 匹配服务
/// </summary>
public sealed class MatchService
{
    private readonly ConcurrentQueue<MatchRequest> _queue = new();
    private readonly SessionManager _sessionManager;
    private readonly Action<string, string, string>? _onMatchSuccess;  // (roomId, player1Id, player2Id)

    public MatchService(SessionManager sessionManager, Action<string, string, string>? onMatchSuccess = null)
    {
        _sessionManager = sessionManager;
        _onMatchSuccess = onMatchSuccess;
    }

    public void Enqueue(string playerId, string playerName)
    {
        _queue.Enqueue(new MatchRequest { PlayerId = playerId, PlayerName = playerName });
        Console.WriteLine($"Player {playerId} ({playerName}) joined match queue. Queue size: {_queue.Count}");
    }

    public void Dequeue(string playerId)
    {
        // 简单实现：无法从队列中移除，只能通过遍历
        // 实际生产环境需要更复杂的队列管理
        Console.WriteLine($"Player {playerId} cancelled match");
    }

    public void Tick()
    {
        if (_queue.Count < 2) return;

        // 取出两个玩家
        if (!_queue.TryDequeue(out var request1)) return;
        if (!_queue.TryDequeue(out var request2)) 
        {
            // 如果第二个出队失败，把第一个放回去
            _queue.Enqueue(request1);
            return;
        }

        // 验证玩家会话
        if (!_sessionManager.TryGet(request1.PlayerId, out var session1) ||
            !_sessionManager.TryGet(request2.PlayerId, out var session2))
        {
            Console.WriteLine("Match failed: player session not found");
            return;
        }

        // 更新会话状态
        session1.State = SessionState.InRoom;
        session2.State = SessionState.InRoom;

        // 通知匹配成功
        Console.WriteLine($"Match success: {request1.PlayerId} vs {request2.PlayerId}");
        _onMatchSuccess?.Invoke("", request1.PlayerId, request2.PlayerId);
    }

    private class MatchRequest
    {
        public string PlayerId { get; init; } = "";
        public string PlayerName { get; init; } = "";
    }
}
