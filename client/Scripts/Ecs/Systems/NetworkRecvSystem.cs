using Game.Net;

namespace Game.Ecs.Systems;

/// <summary>
/// Receives server lockstep frames.
/// Runs early in the system pipeline (execution order #2).
///
/// 当前协议为帧同步：服务器下发 LockstepFrame（输入帧），
/// 客户端不再接收/回放 GameState、SpawnWave、SpawnArrow 等旧状态快照消息。
/// </summary>
public class NetworkRecvSystem : GameSystem
{
    public SyncClient Sync { get; set; }

    public override void Update(float delta)
    {
        if (Sync == null) return;

        ProcessLockstepFrames();
    }

    private void ProcessLockstepFrames()
    {
        while (Sync.LockstepFrameQueue.Count > 0)
        {
            var _ = Sync.LockstepFrameQueue.Dequeue();
            // TODO: 在后续完整锁步改造中，将输入帧应用到本地模拟。
        }
    }
}
