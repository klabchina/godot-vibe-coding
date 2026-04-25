using Server.Match;
using Server.Room;

namespace Server.Hosting;

public sealed class GameLoopService : BackgroundService
{
    private readonly MatchService _match;
    private readonly RoomManager _rooms;
    private readonly ILogger<GameLoopService> _logger;

    public GameLoopService(MatchService match, RoomManager rooms, ILogger<GameLoopService> logger)
    {
        _match = match;
        _rooms = rooms;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        _logger.LogInformation("GameLoop started at 20 tick/s");

        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            const float dt = 0.05f;
            _match.Tick();
            _rooms.Tick(dt);
        }
    }
}
