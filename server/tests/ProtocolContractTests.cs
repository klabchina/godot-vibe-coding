using System;
using Server.Proto;
using Xunit;

namespace Server.Tests;

public sealed class ProtocolContractTests
{
    [Fact]
    public void MsgIds_should_match_new_protocol_contract()
    {
        Assert.Equal(1001u, MsgIds.MatchRequest);
        Assert.Equal(1002u, MsgIds.MatchCancel);
        Assert.Equal(1004u, MsgIds.MatchSuccess);

        Assert.Equal(2001u, MsgIds.PlayerReady);
        Assert.Equal(2002u, MsgIds.GameStart);

        Assert.Equal(3001u, MsgIds.PlayerMove);
        Assert.Equal(3002u, MsgIds.SkillChoice);
        Assert.Equal(3003u, MsgIds.GameEndSubmit);
        Assert.Equal(3005u, MsgIds.GameOver);
        Assert.Equal(3008u, MsgIds.LockstepFrame);

        Assert.Equal(9001u, MsgIds.Heartbeat);
    }

    [Fact]
    public void RoomState_should_only_include_waiting_and_ingame()
    {
        var names = Enum.GetNames(typeof(Server.Room.RoomState));
        Assert.Equal(new[] { "Waiting", "InGame" }, names);
    }
}
