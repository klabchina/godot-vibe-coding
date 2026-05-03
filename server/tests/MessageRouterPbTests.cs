using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Server.Match;
using Server.Network;
using Server.Proto;
using Server.Room;
using Server.Session;
using Xunit;

namespace Server.Tests;

public sealed class MessageRouterPbTests
{
    [Fact]
    public async Task RouteAsync_should_accept_protobuf_payload_for_match_request()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var sessionManager = new SessionManager();
        var wsHandler = new WebSocketHandler(
            loggerFactory.CreateLogger<WebSocketHandler>(),
            new ConnectionManager());
        var roomManager = new RoomManager(wsHandler);
        var matchService = new MatchService(sessionManager, roomManager);
        var router = new MessageRouter(
            loggerFactory.CreateLogger<MessageRouter>(),
            sessionManager,
            roomManager,
            matchService,
            null);

        var req = new MatchRequest { PlayerId = "p1", PlayerName = "Alice" };
        var payload = BuildEnvelope(MsgIds.MatchRequest, req.ToByteArray());

        await router.RouteAsync("conn-1", payload, CancellationToken.None);

        Assert.True(sessionManager.TryGet("p1", out var session));
        Assert.NotNull(session);
        Assert.Equal("Alice", session!.PlayerName);
    }

    [Fact]
    public async Task RouteAsync_match_cancel_should_reset_session_when_waiting_room_cancelled()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var sessionManager = new SessionManager();
        var wsHandler = new WebSocketHandler(
            loggerFactory.CreateLogger<WebSocketHandler>(),
            new ConnectionManager());
        var roomManager = new RoomManager(wsHandler);
        var matchService = new MatchService(sessionManager, roomManager);
        var router = new MessageRouter(
            loggerFactory.CreateLogger<MessageRouter>(),
            sessionManager,
            roomManager,
            matchService,
            null);

        var session = sessionManager.Create("conn-1", "p1", "Alice");
        var room = roomManager.CreateRoom("p1");
        session.RoomId = room.RoomId;
        session.State = SessionState.InRoom;

        var payload = BuildEnvelope(MsgIds.MatchCancel, new MatchCancel { PlayerId = "p1" }.ToByteArray());
        await router.RouteAsync("conn-1", payload, CancellationToken.None);

        Assert.Equal(SessionState.Idle, session.State);
        Assert.Null(roomManager.GetRoom(room.RoomId));
    }

    [Fact]
    public async Task RouteAsync_match_cancel_should_be_rejected_when_room_is_ingame()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var sessionManager = new SessionManager();
        var wsHandler = new WebSocketHandler(
            loggerFactory.CreateLogger<WebSocketHandler>(),
            new ConnectionManager());
        var roomManager = new RoomManager(wsHandler);
        var matchService = new MatchService(sessionManager, roomManager);
        var router = new MessageRouter(
            loggerFactory.CreateLogger<MessageRouter>(),
            sessionManager,
            roomManager,
            matchService,
            null);

        var session = sessionManager.Create("conn-1", "p1", "Alice");
        var room = roomManager.CreateRoom("p1", "p2");
        session.RoomId = room.RoomId;
        session.State = SessionState.InBattle;

        room.OnPlayerReady("p1");
        room.OnPlayerReady("p2");

        var payload = BuildEnvelope(MsgIds.MatchCancel, new MatchCancel { PlayerId = "p1" }.ToByteArray());
        await router.RouteAsync("conn-1", payload, CancellationToken.None);

        Assert.Equal(SessionState.InBattle, session.State);
        Assert.NotNull(roomManager.GetRoom(room.RoomId));
    }

    [Fact]
    public async Task RouteAsync_player_ready_should_mark_all_room_sessions_in_battle_after_game_start()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var sessionManager = new SessionManager();
        var wsHandler = new WebSocketHandler(
            loggerFactory.CreateLogger<WebSocketHandler>(),
            new ConnectionManager());
        var roomManager = new RoomManager(wsHandler);
        var matchService = new MatchService(sessionManager, roomManager);
        var router = new MessageRouter(
            loggerFactory.CreateLogger<MessageRouter>(),
            sessionManager,
            roomManager,
            matchService,
            null);

        var s1 = sessionManager.Create("conn-1", "p1", "Alice");
        var s2 = sessionManager.Create("conn-2", "p2", "Bob");
        var room = roomManager.CreateRoom("p1", "p2");
        room.SetConnection("p1", "conn-1");
        room.SetConnection("p2", "conn-2");

        s1.RoomId = room.RoomId;
        s2.RoomId = room.RoomId;
        s1.State = SessionState.InRoom;
        s2.State = SessionState.InRoom;

        await router.RouteAsync("conn-1", BuildEnvelope(MsgIds.PlayerReady, new PlayerReady { RoomId = room.RoomId }.ToByteArray()), CancellationToken.None);
        await router.RouteAsync("conn-2", BuildEnvelope(MsgIds.PlayerReady, new PlayerReady { RoomId = room.RoomId }.ToByteArray()), CancellationToken.None);

        Assert.Equal(RoomState.InGame, room.State);
        Assert.Equal(SessionState.InBattle, s1.State);
        Assert.Equal(SessionState.InBattle, s2.State);
    }

    [Fact]
    public async Task RouteAsync_game_end_submit_should_end_game_after_all_players_submit()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var sessionManager = new SessionManager();
        var wsHandler = new WebSocketHandler(
            loggerFactory.CreateLogger<WebSocketHandler>(),
            new ConnectionManager());
        var roomManager = new RoomManager(wsHandler);
        var matchService = new MatchService(sessionManager, roomManager);
        var router = new MessageRouter(
            loggerFactory.CreateLogger<MessageRouter>(),
            sessionManager,
            roomManager,
            matchService,
            null);

        var s1 = sessionManager.Create("conn-1", "p1", "Alice");
        var s2 = sessionManager.Create("conn-2", "p2", "Bob");
        var room = roomManager.CreateRoom("p1", "p2");

        s1.RoomId = room.RoomId;
        s2.RoomId = room.RoomId;
        room.OnPlayerReady("p1");
        room.OnPlayerReady("p2");

        GameEndSubmit? gameEnd = null;
        room.OnGameEnd += msg => gameEnd = msg;

        await router.RouteAsync("conn-1", BuildEnvelope(MsgIds.GameEndSubmit, new GameEndSubmit { Reason = "Win" }.ToByteArray()), CancellationToken.None);
        Assert.Null(gameEnd);

        await router.RouteAsync("conn-2", BuildEnvelope(MsgIds.GameEndSubmit, new GameEndSubmit { Reason = "Win" }.ToByteArray()), CancellationToken.None);
        Assert.NotNull(gameEnd);
        Assert.Equal("Win", gameEnd!.Reason);
    }

    [Fact]
    public async Task RouteAsync_heartbeat_should_refresh_session_last_heartbeat()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var sessionManager = new SessionManager();
        var wsHandler = new WebSocketHandler(
            loggerFactory.CreateLogger<WebSocketHandler>(),
            new ConnectionManager());
        var roomManager = new RoomManager(wsHandler);
        var matchService = new MatchService(sessionManager, roomManager);
        var router = new MessageRouter(
            loggerFactory.CreateLogger<MessageRouter>(),
            sessionManager,
            roomManager,
            matchService,
            null);

        var session = sessionManager.Create("conn-1", "p1", "Alice");
        session.LastHeartbeat = DateTime.UtcNow.AddMinutes(-10);
        var before = session.LastHeartbeat;

        await router.RouteAsync("conn-1", BuildEnvelope(MsgIds.Heartbeat, Array.Empty<byte>()), CancellationToken.None);

        Assert.True(session.LastHeartbeat > before);
    }

    [Fact]
    public async Task RouteAsync_invalid_envelope_should_not_create_session()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var sessionManager = new SessionManager();
        var wsHandler = new WebSocketHandler(
            loggerFactory.CreateLogger<WebSocketHandler>(),
            new ConnectionManager());
        var roomManager = new RoomManager(wsHandler);
        var matchService = new MatchService(sessionManager, roomManager);
        var router = new MessageRouter(
            loggerFactory.CreateLogger<MessageRouter>(),
            sessionManager,
            roomManager,
            matchService,
            null);

        await router.RouteAsync("conn-1", new byte[] { 0x01, 0x02, 0x03 }, CancellationToken.None);

        Assert.False(sessionManager.TryGetByConnection("conn-1", out _));
    }

    [Fact]
    public async Task RouteAsync_unknown_msgid_should_not_create_session()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var sessionManager = new SessionManager();
        var wsHandler = new WebSocketHandler(
            loggerFactory.CreateLogger<WebSocketHandler>(),
            new ConnectionManager());
        var roomManager = new RoomManager(wsHandler);
        var matchService = new MatchService(sessionManager, roomManager);
        var router = new MessageRouter(
            loggerFactory.CreateLogger<MessageRouter>(),
            sessionManager,
            roomManager,
            matchService,
            null);

        await router.RouteAsync("conn-1", BuildEnvelope(777777u, Array.Empty<byte>()), CancellationToken.None);

        Assert.False(sessionManager.TryGetByConnection("conn-1", out _));
    }

    [Fact]
    public async Task RouteAsync_invalid_match_request_payload_should_not_create_session()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var sessionManager = new SessionManager();
        var wsHandler = new WebSocketHandler(
            loggerFactory.CreateLogger<WebSocketHandler>(),
            new ConnectionManager());
        var roomManager = new RoomManager(wsHandler);
        var matchService = new MatchService(sessionManager, roomManager);
        var router = new MessageRouter(
            loggerFactory.CreateLogger<MessageRouter>(),
            sessionManager,
            roomManager,
            matchService,
            null);

        var invalidPb = new byte[] { 0x0A, 0xFF, 0xFF, 0xFF, 0xFF };
        await router.RouteAsync("conn-1", BuildEnvelope(MsgIds.MatchRequest, invalidPb), CancellationToken.None);

        Assert.False(sessionManager.TryGetByConnection("conn-1", out _));
    }

    [Fact]
    public async Task RouteAsync_invalid_player_ready_payload_should_not_break_followup_valid_ready()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var sessionManager = new SessionManager();
        var wsHandler = new WebSocketHandler(
            loggerFactory.CreateLogger<WebSocketHandler>(),
            new ConnectionManager());
        var roomManager = new RoomManager(wsHandler);
        var matchService = new MatchService(sessionManager, roomManager);
        var router = new MessageRouter(
            loggerFactory.CreateLogger<MessageRouter>(),
            sessionManager,
            roomManager,
            matchService,
            null);

        var s1 = sessionManager.Create("conn-1", "p1", "Alice");
        var s2 = sessionManager.Create("conn-2", "p2", "Bob");
        var room = roomManager.CreateRoom("p1", "p2");
        s1.RoomId = room.RoomId;
        s2.RoomId = room.RoomId;

        var invalidPb = new byte[] { 0x0A, 0xFF, 0xFF };
        await router.RouteAsync("conn-1", BuildEnvelope(MsgIds.PlayerReady, invalidPb), CancellationToken.None);
        await router.RouteAsync("conn-1", BuildEnvelope(MsgIds.PlayerReady, new PlayerReady { RoomId = room.RoomId }.ToByteArray()), CancellationToken.None);
        await router.RouteAsync("conn-2", BuildEnvelope(MsgIds.PlayerReady, new PlayerReady { RoomId = room.RoomId }.ToByteArray()), CancellationToken.None);

        Assert.Equal(RoomState.InGame, room.State);
    }

    [Fact]
    public async Task RouteAsync_player_move_should_update_lockstep_frame_input_after_room_started()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var sessionManager = new SessionManager();
        var wsHandler = new WebSocketHandler(
            loggerFactory.CreateLogger<WebSocketHandler>(),
            new ConnectionManager());
        var roomManager = new RoomManager(wsHandler);
        var matchService = new MatchService(sessionManager, roomManager);
        var router = new MessageRouter(
            loggerFactory.CreateLogger<MessageRouter>(),
            sessionManager,
            roomManager,
            matchService,
            null);

        var s1 = sessionManager.Create("conn-1", "p1", "Alice");
        var s2 = sessionManager.Create("conn-2", "p2", "Bob");
        var room = roomManager.CreateRoom("p1", "p2");
        room.SetConnection("p1", "conn-1");
        room.SetConnection("p2", "conn-2");
        s1.RoomId = room.RoomId;
        s2.RoomId = room.RoomId;

        await router.RouteAsync("conn-1", BuildEnvelope(MsgIds.PlayerReady, new PlayerReady { RoomId = room.RoomId }.ToByteArray()), CancellationToken.None);
        await router.RouteAsync("conn-2", BuildEnvelope(MsgIds.PlayerReady, new PlayerReady { RoomId = room.RoomId }.ToByteArray()), CancellationToken.None);

        LockstepFrame? frame = null;
        room.OnBroadcastFrame += (_, msg) => frame = msg;

        var move = new PlayerMove
        {
            Tick = 1,
            MoveDir = new Vec2 { X = 2.5f, Y = -1.25f }
        };

        await router.RouteAsync("conn-1", BuildEnvelope(MsgIds.PlayerMove, move.ToByteArray()), CancellationToken.None);
        room.Tick(0.05f);

        Assert.NotNull(frame);
        var input = Assert.Single(frame!.Inputs, i => i.PlayerId == "p1");
        Assert.Equal(2.5f, input.MoveDir.X);
        Assert.Equal(-1.25f, input.MoveDir.Y);
    }

    [Fact]
    public async Task RouteAsync_invalid_player_move_payload_should_not_break_followup_valid_move()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var sessionManager = new SessionManager();
        var wsHandler = new WebSocketHandler(
            loggerFactory.CreateLogger<WebSocketHandler>(),
            new ConnectionManager());
        var roomManager = new RoomManager(wsHandler);
        var matchService = new MatchService(sessionManager, roomManager);
        var router = new MessageRouter(
            loggerFactory.CreateLogger<MessageRouter>(),
            sessionManager,
            roomManager,
            matchService,
            null);

        var s1 = sessionManager.Create("conn-1", "p1", "Alice");
        var s2 = sessionManager.Create("conn-2", "p2", "Bob");
        var room = roomManager.CreateRoom("p1", "p2");
        room.SetConnection("p1", "conn-1");
        room.SetConnection("p2", "conn-2");
        s1.RoomId = room.RoomId;
        s2.RoomId = room.RoomId;

        await router.RouteAsync("conn-1", BuildEnvelope(MsgIds.PlayerReady, new PlayerReady { RoomId = room.RoomId }.ToByteArray()), CancellationToken.None);
        await router.RouteAsync("conn-2", BuildEnvelope(MsgIds.PlayerReady, new PlayerReady { RoomId = room.RoomId }.ToByteArray()), CancellationToken.None);

        var invalidPb = new byte[] { 0x0A, 0xFF, 0xFF };
        await router.RouteAsync("conn-1", BuildEnvelope(MsgIds.PlayerMove, invalidPb), CancellationToken.None);

        LockstepFrame? frame = null;
        room.OnBroadcastFrame += (_, msg) => frame = msg;

        var validMove = new PlayerMove
        {
            Tick = 2,
            MoveDir = new Vec2 { X = 0.5f, Y = 0.25f }
        };
        await router.RouteAsync("conn-1", BuildEnvelope(MsgIds.PlayerMove, validMove.ToByteArray()), CancellationToken.None);
        room.Tick(0.05f);

        Assert.NotNull(frame);
        var input = Assert.Single(frame!.Inputs, i => i.PlayerId == "p1");
        Assert.Equal(0.5f, input.MoveDir.X);
        Assert.Equal(0.25f, input.MoveDir.Y);
    }

    [Fact]
    public async Task RouteAsync_invalid_skill_choice_payload_should_not_break_followup_heartbeat()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var sessionManager = new SessionManager();
        var wsHandler = new WebSocketHandler(
            loggerFactory.CreateLogger<WebSocketHandler>(),
            new ConnectionManager());
        var roomManager = new RoomManager(wsHandler);
        var matchService = new MatchService(sessionManager, roomManager);
        var router = new MessageRouter(
            loggerFactory.CreateLogger<MessageRouter>(),
            sessionManager,
            roomManager,
            matchService,
            null);

        var session = sessionManager.Create("conn-1", "p1", "Alice");
        session.LastHeartbeat = DateTime.UtcNow.AddMinutes(-10);
        var before = session.LastHeartbeat;

        var invalidPb = new byte[] { 0x0A, 0xFF, 0xFF };
        await router.RouteAsync("conn-1", BuildEnvelope(MsgIds.SkillChoice, invalidPb), CancellationToken.None);
        await router.RouteAsync("conn-1", BuildEnvelope(MsgIds.Heartbeat, Array.Empty<byte>()), CancellationToken.None);

        Assert.True(session.LastHeartbeat > before);
    }

    private static byte[] BuildEnvelope(uint msgId, byte[] payload)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(msgId);
        writer.Write(payload);
        return ms.ToArray();
    }
}
