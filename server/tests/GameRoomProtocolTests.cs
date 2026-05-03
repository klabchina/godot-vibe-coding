using System.Collections.Generic;
using Server.Proto;
using Server.Room;
using Xunit;

namespace Server.Tests;

public sealed class GameRoomProtocolTests
{
    [Fact]
    public void Room_should_broadcast_lockstep_frame_every_tick_even_without_player_input()
    {
        var room = new GameRoom("r1", "p1", "p2");
        room.SetConnection("p1", "c1");
        room.SetConnection("p2", "c2");
        room.OnPlayerReady("p1");
        room.OnPlayerReady("p2");

        var frames = new List<LockstepFrame>();
        room.OnBroadcastFrame += (_, frame) => frames.Add(frame);

        room.Tick(0.05f);
        room.Tick(0.05f);

        Assert.Equal(2, frames.Count);
        Assert.Equal(1, frames[0].Frame);
        Assert.Equal(2, frames[1].Frame);

        foreach (var frame in frames)
        {
            Assert.Equal(2, frame.Inputs.Count);
            foreach (var input in frame.Inputs)
            {
                Assert.NotNull(input.MoveDir);
                Assert.Equal(0f, input.MoveDir.X);
                Assert.Equal(0f, input.MoveDir.Y);
            }
        }
    }

    [Fact]
    public void Room_should_merge_skill_choice_into_next_lockstep_frame()
    {
        var room = new GameRoom("r1", "p1", "p2");
        room.SetConnection("p1", "c1");
        room.SetConnection("p2", "c2");
        room.OnPlayerReady("p1");
        room.OnPlayerReady("p2");

        LockstepFrame? frame = null;
        room.OnBroadcastFrame += (_, msg) => frame = msg;

        room.OnSkillChoice("p1", new SkillChoice
        {
            Tick = 11,
            SkillId = "MoveSpeedUp",
            Slot = -1,
        });

        room.Tick(0.05f);

        Assert.NotNull(frame);
        var choice = Assert.Single(frame!.SkillChoices);
        Assert.Equal(11, choice.Tick);
        Assert.Equal("MoveSpeedUp", choice.SkillId);
        Assert.Equal(0, choice.Slot);
    }

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
