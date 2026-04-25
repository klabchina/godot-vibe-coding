using System.Text;
using System.Text.Json;
using Server.Proto;
using Server.Session;
using Server.Room;
using Server.Match;
using Server.Game;

namespace Server.Network;

/// <summary>
/// 消息路由器
/// </summary>
public sealed class MessageRouter
{
    private readonly ILogger<MessageRouter> _logger;
    private readonly SessionManager _sessionManager;
    private readonly RoomManager _roomManager;
    private readonly MatchService _matchService;
    private readonly Action<string, uint, object>? _onSendMessage;  // (connectionId, msgId, message)

    public MessageRouter(
        ILogger<MessageRouter> logger,
        SessionManager sessionManager,
        RoomManager roomManager,
        MatchService matchService,
        Action<string, uint, object>? onSendMessage = null)
    {
        _logger = logger;
        _sessionManager = sessionManager;
        _roomManager = roomManager;
        _matchService = matchService;
        _onSendMessage = onSendMessage;
    }

    /// <summary>
    /// 路由消息
    /// </summary>
    public async Task RouteAsync(string connectionId, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        try
        {
            // 解析 Envelope
            var envelope = ParseEnvelope(payload);
            if (envelope == null)
            {
                _logger.LogWarning("Failed to parse envelope from connection {ConnectionId}", connectionId);
                return;
            }

            // 根据 MsgId 路由
            switch (envelope.Value.msgId)
            {
                case MsgIds.MatchRequest:
                    await HandleMatchRequest(connectionId, envelope.Value.payload);
                    break;
                    
                case MsgIds.MatchCancel:
                    await HandleMatchCancel(connectionId, envelope.Value.payload);
                    break;
                    
                case MsgIds.PlayerReady:
                    await HandlePlayerReady(connectionId, envelope.Value.payload);
                    break;
                    
                case MsgIds.PlayerInput:
                    await HandlePlayerInput(connectionId, envelope.Value.payload);
                    break;
                    
                case MsgIds.Heartbeat:
                    await HandleHeartbeat(connectionId);
                    break;
                    
                default:
                    _logger.LogWarning("Unknown msgId: {MsgId}", envelope.Value.msgId);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error routing message from {ConnectionId}", connectionId);
        }
    }

    private (uint msgId, byte[] payload)? ParseEnvelope(ReadOnlyMemory<byte> payload)
    {
        try
        {
            // 简化实现：前4字节是 msgId，后面是 payload
            if (payload.Length < 4) return null;
            
            var reader = new BinaryReader(new MemoryStream(payload.ToArray()));
            var msgId = reader.ReadUInt32();
            var remaining = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
            
            return (msgId, remaining);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 处理匹配请求
    /// </summary>
    private async Task HandleMatchRequest(string connectionId, byte[] payload)
    {
        try
        {
            var request = JsonSerializer.Deserialize<MatchRequest>(payload);
            if (request == null) return;

            // 创建或更新会话
            var session = _sessionManager.GetOrCreate(connectionId, request.PlayerId);
            session.PlayerName = request.PlayerName;
            session.State = SessionState.Matching;

            // 加入匹配队列
            _matchService.Enqueue(request.PlayerId, request.PlayerName);

            // 发送等待状态
            await SendAsync(connectionId, MsgIds.MatchUpdate, new MatchUpdate
            {
                Status = MatchUpdate.MatchStatus.Waiting,
                WaitTime = 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MatchRequest");
        }
    }

    /// <summary>
    /// 处理取消匹配
    /// </summary>
    private Task HandleMatchCancel(string connectionId, byte[] payload)
    {
        if (_sessionManager.TryGetByConnection(connectionId, out var session))
        {
            _matchService.Dequeue(session.PlayerId);
            session.State = SessionState.Idle;
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// 处理玩家准备
    /// </summary>
    private Task HandlePlayerReady(string connectionId, byte[] payload)
    {
        try
        {
            var ready = JsonSerializer.Deserialize<PlayerReady>(payload);
            if (ready == null) return Task.CompletedTask;

            if (_sessionManager.TryGetByConnection(connectionId, out var session) && 
                session.RoomId != null)
            {
                var room = _roomManager.GetRoom(session.RoomId);
                room?.OnPlayerReady(session.PlayerId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling PlayerReady");
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// 处理玩家输入
    /// </summary>
    private Task HandlePlayerInput(string connectionId, byte[] payload)
    {
        try
        {
            var input = JsonSerializer.Deserialize<PlayerInputMsg>(payload);
            if (input == null) return Task.CompletedTask;

            if (_sessionManager.TryGetByConnection(connectionId, out var session) && 
                session.RoomId != null)
            {
                var room = _roomManager.GetRoom(session.RoomId);
                room?.OnPlayerInput(session.PlayerId, input);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling PlayerInput");
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// 处理心跳
    /// </summary>
    private Task HandleHeartbeat(string connectionId)
    {
        if (_sessionManager.TryGetByConnection(connectionId, out var session))
        {
            session.LastHeartbeat = DateTime.UtcNow;
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// 发送消息给客户端
    /// </summary>
    private async Task SendAsync(string connectionId, uint msgId, object message)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message);
        var envelope = BuildEnvelope(msgId, payload);
        
        _onSendMessage?.Invoke(connectionId, msgId, message);
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// 构建消息信封
    /// </summary>
    private byte[] BuildEnvelope(uint msgId, byte[] payload)
    {
        using var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);
        writer.Write(msgId);
        writer.Write(payload);
        return ms.ToArray();
    }
}
