using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Server.Match;
using Server.Network;
using Server.Room;
using Server.Session;
using Xunit;

namespace Server.Tests;

public sealed class MatchServiceTests
{
    [Fact]
    public void Enqueue_should_use_gameroom_slot_in_match_success()
    {
        var (firstPlayerId, secondPlayerId) = FindReversedEnumerationPair();

        var loggerFactory = LoggerFactory.Create(builder => { });
        var sessionManager = new SessionManager();
        var connectionManager = new ConnectionManager();
        var wsHandler = new WebSocketHandler(
            loggerFactory.CreateLogger<WebSocketHandler>(),
            connectionManager);
        var roomManager = new RoomManager(wsHandler);
        var matchService = new MatchService(sessionManager, roomManager);

        sessionManager.Create("conn-first", firstPlayerId, "First");
        sessionManager.Create("conn-second", secondPlayerId, "Second");

        var firstResult = matchService.Enqueue(firstPlayerId, "First");
        Assert.Null(firstResult);

        var matchResult = matchService.Enqueue(secondPlayerId, "Second");
        Assert.NotNull(matchResult);

        var slots = matchResult!.Players.ToDictionary(p => p.PlayerId, p => p.Slot);
        Assert.Equal(0, slots[firstPlayerId]);
        Assert.Equal(1, slots[secondPlayerId]);
    }

    private static (string first, string second) FindReversedEnumerationPair()
    {
        for (var i = 0; i < 2000; i++)
        {
            var first = $"pA_{i}";
            var second = $"pB_{i}";

            var manager = new SessionManager();
            manager.Create($"c1_{i}", first, "A");
            manager.Create($"c2_{i}", second, "B");

            var order = manager.GetAllSessions().Select(s => s.PlayerId).ToArray();
            if (order.Length == 2 && order[0] == second && order[1] == first)
            {
                return (first, second);
            }
        }

        throw new InvalidOperationException("No reversed enumeration pair found for SessionManager.");
    }
}
