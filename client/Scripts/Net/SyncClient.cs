using System;
using System.Collections.Generic;
using CoreVec2 = Game.Ecs.Core.Vec2;
using Server.Proto;

namespace Game.Net;

public class SyncClient
{
    public readonly Queue<LockstepFrame> LockstepFrameQueue = new();

    // 兼容现有 ECS 接收系统所需队列（当前协议以 LockstepFrame 为主）
    public readonly Queue<GameStateMsg> GameStateQueue = new();
    public readonly Queue<SpawnWaveMsg> SpawnWaveQueue = new();
    public readonly Queue<SpawnArrowMsg> SpawnArrowQueue = new();
    public readonly Queue<EntityDeathMsg> EntityDeathQueue = new();
    public readonly Queue<PickupSpawnMsg> PickupSpawnQueue = new();
    public readonly Queue<PickupCollectMsg> PickupCollectQueue = new();
    public readonly Queue<BuffApplyMsg> BuffApplyQueue = new();
    public readonly Queue<BossPhaseChangeMsg> BossPhaseChangeQueue = new();

    public event Action<GameOver> OnGameOver;

    private int _inputTick;

    public SyncClient()
    {
        NetManager.Instance.OnMessageReceived += HandleMessage;
    }

    public void SendInput(CoreVec2 moveDir)
    {
        _inputTick++;
        NetManager.Instance.Send(MsgIds.PlayerMove, new PlayerMove
        {
            Tick = _inputTick,
            MoveDir = new Server.Proto.Vec2
            {
                X = moveDir.X,
                Y = moveDir.Y,
            }
        });
    }

    public void SendSkillChoice(string skillId)
    {
        NetManager.Instance.Send(MsgIds.SkillChoice, new SkillChoice
        {
            Tick = _inputTick,
            SkillId = skillId ?? string.Empty,
        });
    }

    public void SendGameEndSubmit(string reason)
    {
        NetManager.Instance.Send(MsgIds.GameEndSubmit, new GameEndSubmit
        {
            Tick = _inputTick,
            Reason = reason ?? string.Empty,
        });
    }

    public void Dispose()
    {
        if (NetManager.Instance != null)
            NetManager.Instance.OnMessageReceived -= HandleMessage;
    }

    private void HandleMessage(uint msgId, Google.Protobuf.IMessage msg)
    {
        switch (msgId)
        {
            case MsgIds.LockstepFrame:
                if (msg is LockstepFrame frame)
                {
                    LockstepFrameQueue.Enqueue(frame);
                }
                break;

            case MsgIds.GameOver:
                if (msg is GameOver over)
                {
                    OnGameOver?.Invoke(over);
                }
                break;
        }
    }
}
