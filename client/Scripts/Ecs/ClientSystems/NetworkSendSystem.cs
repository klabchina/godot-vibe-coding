using Game.Ecs.Core;
using Game.Ecs.Components;
using Game.Net;

namespace Game.Ecs.ClientSystems;

/// <summary>
/// Sends local player input to the server each tick.
/// Runs late in the system pipeline (execution order #14, after DeathSystem).
///
/// In networked mode, the client only sends input intent (PlayerMove).
/// The server按 Tick 广播 LockstepFrame，客户端不再上传/下载完整状态快照。
/// </summary>
public class NetworkSendSystem : GameSystem
{
    public SyncClient Sync { get; set; }

    public override void Update(float delta)
    {
        if (Sync == null) return;

        // Find local player and send their current movement input
        var players = World.GetEntitiesWith<PlayerComponent, VelocityComponent, NetworkSyncComponent>();
        foreach (var player in players)
        {
            var net = player.Get<NetworkSyncComponent>();
            if (!net.IsLocal) continue;

            var velocity = player.Get<VelocityComponent>();
            if (velocity == null) continue;

            // Send normalized movement direction (not velocity magnitude)
            var dir = velocity.ClientVelocity;
            if (dir.LengthSquared() > 0.001f)
            {
                dir = dir.Normalized();
            }
            else
            {
                dir = Vec2.Zero;
            }

            Sync.SendInput(dir);
            break; // only one local player
        }
    }
}
