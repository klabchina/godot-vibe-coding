using Google.Protobuf;
using Server.Proto;
using Server.Session;
using Server.Room;
using Server.Match;

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
    private readonly Action<string, uint, IMessage>? _onSendMessage;  // (connectionId, msgId, message)

    public MessageRouter(
        ILogger<MessageRouter> logger,
        SessionManager sessionManager,
        RoomManager roomManager,
        MatchService matchService,
        Action<string, uint, IMessage>? onSendMessage = null)
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
            var envelope = ParseEnvelope(payload);
            if (envelope == null)
            {
                _logger.LogWarning("Failed to parse envelope from connection {ConnectionId}", connectionId);
                return;
            }

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

                case MsgIds.PlayerMove:
                    await HandlePlayerMove(connectionId, envelope.Value.payload);
                    break;

                case MsgIds.SkillChoice:
                    await HandleSkillChoice(connectionId, envelope.Value.payload);
                    break;

                case MsgIds.GameEndSubmit:
                    await HandleGameEndSubmit(connectionId, envelope.Value.payload);
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
            if (payload.Length < 4) return null;

            using var reader = new BinaryReader(new MemoryStream(payload.ToArray()));
            var msgId = reader.ReadUInt32();
            var remaining = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));

            return (msgId, remaining);
        }
        catch
        {
            return null;
        }
    }

    private async Task HandleMatchRequest(string connectionId, byte[] payload)
    {
        try
        {
            var request = MatchRequest.Parser.ParseFrom(payload);

            var session = _sessionManager.GetOrCreate(connectionId, request.PlayerId);
            session.PlayerName = request.PlayerName;
            session.State = SessionState.Matching;

            var match = _matchService.Enqueue(request.PlayerId, request.PlayerName);
            if (match != null && _sessionManager.TryGetByRoom(match.RoomId, out var sessions))
            {
                foreach (var s in sessions)
                {
                    await SendAsync(s.ConnectionId, MsgIds.MatchSuccess, match);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MatchRequest");
        }
    }

    private Task HandleMatchCancel(string connectionId, byte[] payload)
    {
        try
        {
            _ = MatchCancel.Parser.ParseFrom(payload);

            if (_sessionManager.TryGetByConnection(connectionId, out var session) && session != null)
            {
                var cancelled = _matchService.Cancel(session.PlayerId);
                if (cancelled)
                {
                    session.State = SessionState.Idle;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MatchCancel");
        }

        return Task.CompletedTask;
    }

    private Task HandlePlayerReady(string connectionId, byte[] payload)
    {
        try
        {
            _ = PlayerReady.Parser.ParseFrom(payload);

            if (_sessionManager.TryGetByConnection(connectionId, out var session) &&
                session != null &&
                session.RoomId != null)
            {
                var room = _roomManager.GetRoom(session.RoomId);
                room?.OnPlayerReady(session.PlayerId);

                if (room != null && room.State == RoomState.InGame &&
                    _sessionManager.TryGetByRoom(session.RoomId, out var sessions))
                {
                    foreach (var s in sessions)
                    {
                        s.State = SessionState.InBattle;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling PlayerReady");
        }

        return Task.CompletedTask;
    }

    private Task HandlePlayerMove(string connectionId, byte[] payload)
    {
        try
        {
            var input = PlayerMove.Parser.ParseFrom(payload);

            if (_sessionManager.TryGetByConnection(connectionId, out var session) &&
                session != null &&
                session.RoomId != null)
            {
                var room = _roomManager.GetRoom(session.RoomId);
                room?.OnPlayerMove(session.PlayerId, input);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling PlayerMove");
        }

        return Task.CompletedTask;
    }

    private Task HandleSkillChoice(string connectionId, byte[] payload)
    {
        try
        {
            var choice = SkillChoice.Parser.ParseFrom(payload);

            if (_sessionManager.TryGetByConnection(connectionId, out var session) &&
                session != null &&
                session.RoomId != null)
            {
                var room = _roomManager.GetRoom(session.RoomId);
                room?.OnSkillChoice(session.PlayerId, choice);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling SkillChoice");
        }

        return Task.CompletedTask;
    }

    private Task HandleGameEndSubmit(string connectionId, byte[] payload)
    {
        try
        {
            var submit = GameEndSubmit.Parser.ParseFrom(payload);

            if (_sessionManager.TryGetByConnection(connectionId, out var session) &&
                session != null &&
                session.RoomId != null)
            {
                var room = _roomManager.GetRoom(session.RoomId);
                room?.OnGameEndSubmit(session.PlayerId, submit);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling GameEndSubmit");
        }

        return Task.CompletedTask;
    }

    private Task HandleGameOver(string connectionId, byte[] payload)
    {
        try
        {
            var gameOver = GameOver.Parser.ParseFrom(payload);

            if (_sessionManager.TryGetByConnection(connectionId, out var session) &&
                session != null &&
                session.RoomId != null)
            {
                var room = _roomManager.GetRoom(session.RoomId);
                room?.OnGameOverSubmit(session.PlayerId, gameOver);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling GameOver");
        }

        return Task.CompletedTask;
    }

    private Task HandleHeartbeat(string connectionId)
    {
        if (_sessionManager.TryGetByConnection(connectionId, out var session) && session != null)
        {
            session.LastHeartbeat = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    private Task SendAsync(string connectionId, uint msgId, IMessage message)
    {
        _onSendMessage?.Invoke(connectionId, msgId, message);
        return Task.CompletedTask;
    }
}
