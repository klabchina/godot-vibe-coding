using System.Collections.Generic;
using Game.Ecs.Components;
using Game.Ecs.Core;
using Game.Net;

namespace Game.Ecs.ClientSystems;

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
            var frame = Sync.LockstepFrameQueue.Dequeue();
            ApplyFrameInputs(frame);
        }
    }

    private void ApplyFrameInputs(Server.Proto.LockstepFrame frame)
    {
        var players = World.GetEntitiesWith<PlayerComponent, VelocityComponent>();
        if (players.Count == 0) return;

        var bySlot = new Dictionary<int, Entity>(players.Count);
        foreach (var playerEntity in players)
        {
            var player = playerEntity.Get<PlayerComponent>();
            bySlot[player.PlayerIndex] = playerEntity;
        }

        foreach (var input in frame.Inputs)
        {
            if (!bySlot.TryGetValue(input.Slot, out var playerEntity))
                continue;

            var velocity = playerEntity.Get<VelocityComponent>();
            var moveDir = new Vec2(input.MoveDir.X, input.MoveDir.Y);
            if (moveDir.LengthSquared() > 0.0001f)
                moveDir = moveDir.Normalized();
            else
                moveDir = Vec2.Zero;

            velocity.LogicVelocity = moveDir * velocity.Speed;
        }
    }
}
