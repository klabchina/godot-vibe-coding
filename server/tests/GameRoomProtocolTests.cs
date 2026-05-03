using Server.Proto;
using Server.Room;
using Xunit;

namespace Server.Tests;

public sealed class GameRoomProtocolTests
{
    [Fact]
    public void Room_should_enter_ingame_after_all_players_ready()
    {
        var room = new GameRoom("r1", "p1");
        room.AddPlayer("p2");

        room.OnPlayerReady("p1");
        Assert.Equal(RoomState.Waiting, room.State);

        room.OnPlayerReady("p2");
        Assert.Equal(RoomState.InGame, room.State);
    }

    [Fact]
    public void Room_should_emit_game_end_when_all_players_submit_end()
    {
        var room = new GameRoom("r1", "p1", "p2");
        room.OnPlayerReady("p1");
        room.OnPlayerReady("p2");

        GameEndSubmit? gameEnd = null;
        room.OnGameEnd += msg => gameEnd = msg;

        room.OnGameEndSubmit("p1", new GameEndSubmit { Reason = "Win" });
        Assert.Null(gameEnd);

        room.OnGameEndSubmit("p2", new GameEndSubmit { Reason = "Win" });
        Assert.NotNull(gameEnd);
        Assert.Equal("Win", gameEnd!.Reason);
    }

    [Fact]
    public void Room_disconnect_should_not_end_game_immediately()
    {
        var room = new GameRoom("r1", "p1", "p2");
        room.OnPlayerReady("p1");
        room.OnPlayerReady("p2");

        var ended = false;
        room.OnGameEnd += _ => ended = true;

        room.OnPlayerDisconnect("p1");

        Assert.Equal(RoomState.InGame, room.State);
        Assert.False(ended);
    }
}
