using System;
using System.Collections.Generic;
using CoreVec2 = Game.Ecs.Core.Vec2;
using Server.Proto;

namespace Game.Net;

public class SyncClient
{
    public readonly Queue<LockstepFrame> LockstepFrameQueue = new();

    private int _inputTick;
    public int LocalPlayerSlot { get; set; }
    public int NextExpectedFrame { get; private set; } = 1;

    public SyncClient()
    {
        NetManager.Instance.OnMessageReceived += HandleMessage;
    }

    public void SendInput(CoreVec2 moveDir)
    {
        _inputTick = NextExpectedFrame;
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
            Slot = LocalPlayerSlot,
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

    /// <summary>
    /// 检查是否可以前进一帧
    /// </summary>
    /// <returns></returns>
    public bool CanAdvanceOneTick()
    {
        while (LockstepFrameQueue.Count > 0)
        {
            var peek = LockstepFrameQueue.Peek();
            if (peek.Frame < NextExpectedFrame)
            {
                LockstepFrameQueue.Dequeue();
                continue;
            }

            return peek.Frame == NextExpectedFrame;
        }

        return false;
    }

    public bool TryDequeueExpectedFrame(out LockstepFrame frame)
    {
        frame = null;
        if (!CanAdvanceOneTick())
            return false;

        frame = LockstepFrameQueue.Dequeue();
        NextExpectedFrame++;
        return true;
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
        }
    }
}
