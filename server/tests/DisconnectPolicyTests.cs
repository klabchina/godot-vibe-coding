using Microsoft.Extensions.Logging;
using Server.Network;
using Server.Room;
using Server.Session;
using Xunit;

namespace Server.Tests;

public sealed class DisconnectPolicyTests
{
    [Fact]
    public void KickByConnection_should_keep_room_when_other_online_session_exists()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var sessionManager = new SessionManager();
        var wsHandler = new WebSocketHandler(
            loggerFactory.CreateLogger<WebSocketHandler>(),
            new ConnectionManager());
        var roomManager = new RoomManager(wsHandler);

        var s1 = sessionManager.Create("conn-1", "p1", "Alice");
        var s2 = sessionManager.Create("conn-2", "p2", "Bob");
        var room = roomManager.CreateRoom("p1", "p2");

        s1.RoomId = room.RoomId;
        s2.RoomId = room.RoomId;
        s1.State = SessionState.InBattle;
        s2.State = SessionState.InBattle;

        DisconnectPolicy.KickByConnection(sessionManager, roomManager, "conn-1");

        Assert.False(sessionManager.TryGet("p1", out _));
        Assert.NotNull(roomManager.GetRoom(room.RoomId));

        Assert.True(sessionManager.TryGet("p2", out var survivor));
        Assert.NotNull(survivor);
        Assert.Equal(room.RoomId, survivor!.RoomId);
        Assert.Equal(SessionState.InBattle, survivor.State);
    }

    [Fact]
    public void KickByConnection_should_destroy_room_when_no_session_left_in_room()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var sessionManager = new SessionManager();
        var wsHandler = new WebSocketHandler(
            loggerFactory.CreateLogger<WebSocketHandler>(),
            new ConnectionManager());
        var roomManager = new RoomManager(wsHandler);

        var s1 = sessionManager.Create("conn-1", "p1", "Alice");
        var room = roomManager.CreateRoom("p1");

        s1.RoomId = room.RoomId;
        s1.State = SessionState.InBattle;

        DisconnectPolicy.KickByConnection(sessionManager, roomManager, "conn-1");

        Assert.False(sessionManager.TryGet("p1", out _));
        Assert.Null(roomManager.GetRoom(room.RoomId));
    }
}
