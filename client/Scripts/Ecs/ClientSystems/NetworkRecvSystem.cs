using System.Collections.Generic;
using Game.Ecs.Components;
using Game.Ecs.Core;
using Game.Ecs.Systems;
using Game.Net;
using Game.Utils;

namespace Game.Ecs.ClientSystems;

/// <summary>
/// Receives server lockstep frames.
/// Runs early in the system pipeline (execution order #2).
///
/// 当前协议为帧同步：服务器下发 LockstepFrame（输入帧），
/// 客户端不再接收/回放 GameState、SpawnWave、SpawnArrow 等旧状态快照消息。
///
/// 追帧策略：每渲染帧最多处理 <see cref="MaxCatchupPerFrame"/> 个逻辑帧。
/// BattleScene 的 while 循环通过此常量限制追帧速度，将大积压分摊到多个渲染帧，
/// 避免 lag spike 后在单帧内爆发式执行造成卡顿。
/// </summary>
public class NetworkRecvSystem : GameSystem
{
    /// <summary>
    /// 每渲染帧最多追赶的逻辑帧数。
    /// 网络恢复后积压帧将以此速率逐步消化，而非一次性全部执行。
    /// </summary>
    public const int MaxCatchupPerFrame = 3;

    /// <summary>积压帧数超过此值时输出警告日志。</summary>
    private const int BacklogWarnThreshold = MaxCatchupPerFrame + 1;

    public SyncClient Sync { get; set; }

    public override void Update(float delta)
    {
        if (Sync == null) return;

        int stackedFrameCount = Sync.LockstepFrameQueue.Count;
        if (stackedFrameCount > BacklogWarnThreshold)
        {
            GameLogger.Print($"[NetworkRecvSystem] 帧积压: {stackedFrameCount} 帧待处理（每渲染帧上限 {MaxCatchupPerFrame}）");
        }

        ProcessLockstepFrames();
    }

    private void ProcessLockstepFrames()
    {
        // 一次 logic tick 只消费一帧，避免后续 MovementSystem 等系统只跑一次而吞掉中间帧的输入。
        // 队列中堆积的帧由 BattleScene 的逻辑循环按 CanAdvanceOneTick 多次推进消费。
        if (Sync.TryDequeueExpectedFrame(out var frame))
        {
            ApplyFrameInputs(frame);
            ApplyFrameSkillChoices(frame);
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

    private void ApplyFrameSkillChoices(Server.Proto.LockstepFrame frame)
    {
        if (frame.SkillChoices.Count == 0)
            return;

        var upgradeApplySystem = World.GetSystem<UpgradeApplySystem>();
        if (upgradeApplySystem == null)
            return;

        foreach (var choice in frame.SkillChoices)
        {
            upgradeApplySystem.EnqueueChoice(choice.Slot, choice.SkillId, choice.Tick);
        }
    }
}
