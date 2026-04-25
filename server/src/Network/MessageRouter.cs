namespace Server.Network;

public sealed class MessageRouter
{
    private readonly ILogger<MessageRouter> _logger;

    public MessageRouter(ILogger<MessageRouter> logger)
    {
        _logger = logger;
    }

    public Task RouteAsync(string connectionId, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        _logger.LogDebug("Route message: conn={ConnectionId}, bytes={Length}", connectionId, payload.Length);
        return Task.CompletedTask;
    }
}
