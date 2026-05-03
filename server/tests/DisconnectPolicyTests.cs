using Microsoft.Extensions.Logging;
using Server.Network;
using Server.Room;
using Server.Session;
using Xunit;

namespace Server.Tests;

public sealed class DisconnectPolicyTests
{
    [Fact]
    public void KickByConnection_should_remove_session_and_destroy_room_immediately()
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
        Assert.Null(roomManager.GetRoom(room.RoomId));

        Assert.True(sessionManager.TryGet("p2", out var survivor));
        Assert.NotNull(survivor);
        Assert.Null(survivor!.RoomId);
        Assert.Equal(SessionState.Idle, survivor.State);
    }
}
