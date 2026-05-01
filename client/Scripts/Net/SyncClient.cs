using System;
using System.Collections.Generic;
using CoreVec2 = Game.Ecs.Core.Vec2;
using Server.Proto;

namespace Game.Net;

public class SyncClient
{
    public readonly Queue<LockstepFrame> LockstepFrameQueue = new();
    public readonly Queue<SkillChoice> SkillChoiceQueue = new();

    public event Action<GameOver> OnGameOver;

    private int _inputTick;
    public int LocalPlayerSlot { get; set; }

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

            case MsgIds.SkillChoice:
                if (msg is SkillChoice choice)
                {
                    SkillChoiceQueue.Enqueue(choice);
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
