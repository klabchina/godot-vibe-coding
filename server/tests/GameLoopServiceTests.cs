using System;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Server.Hosting;
using Server.Match;
using Server.Network;
using Server.Room;
using Server.Session;
using Xunit;

namespace Server.Tests;

public sealed class GameLoopServiceTests
{
    [Fact]
    public void CheckReconnectTimeout_should_destroy_room_when_all_players_expired()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var sessionManager = new SessionManager();
        var connectionManager = new ConnectionManager();
        var wsHandler = new WebSocketHandler(
            loggerFactory.CreateLogger<WebSocketHandler>(),
            connectionManager);
        var roomManager = new RoomManager(wsHandler);
        var matchService = new MatchService(sessionManager, roomManager);

        var service = new GameLoopService(
            matchService,
            roomManager,
            sessionManager,
            connectionManager,
            loggerFactory.CreateLogger<GameLoopService>());

        var s1 = sessionManager.Create("conn-1", "p1", "Alice");
        var s2 = sessionManager.Create("conn-2", "p2", "Bob");
        var room = roomManager.CreateRoom("p1", "p2");

        s1.RoomId = room.RoomId;
        s2.RoomId = room.RoomId;
        s1.IsDisconnected = true;
        s2.IsDisconnected = true;
        s1.DisconnectTime = DateTime.UtcNow.AddSeconds(-31);
        s2.DisconnectTime = DateTime.UtcNow.AddSeconds(-31);

        var method = typeof(GameLoopService).GetMethod("CheckReconnectTimeout", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(service, null);

        Assert.Null(roomManager.GetRoom(room.RoomId));
    }
}
