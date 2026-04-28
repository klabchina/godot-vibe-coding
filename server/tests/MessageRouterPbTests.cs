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

    private static byte[] BuildEnvelope(uint msgId, byte[] payload)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(msgId);
        writer.Write(payload);
        return ms.ToArray();
    }
}
