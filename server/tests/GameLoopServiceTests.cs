using System.Reflection;
using Server.Hosting;
using Xunit;

namespace Server.Tests;

public sealed class GameLoopServiceTests
{
    [Fact]
    public void GameLoopService_should_not_keep_reconnect_timeout_path_when_reconnect_is_disabled()
    {
        var method = typeof(GameLoopService).GetMethod("CheckReconnectTimeout", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.Null(method);
    }
}
