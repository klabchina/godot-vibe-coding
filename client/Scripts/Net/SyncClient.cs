using System;
using System.Collections.Generic;
using Game.Data;
using Game.Ecs.Core;

namespace Game.Net;

/// <summary>
/// Battle synchronization client.
/// Sends local player input to the server and processes incoming game-state
/// updates, spawn events, deaths, upgrades, pickups, buffs, and boss phases.
/// The received data is queued for consumption by NetworkRecvSystem.
/// </summary>
public class SyncClient
{
    // ── Incoming queues (consumed by NetworkRecvSystem each tick) ──
    public readonly Queue<GameStateMsg> GameStateQueue = new();
    public readonly Queue<SpawnArrowMsg> SpawnArrowQueue = new();
    public readonly Queue<SpawnWaveMsg> SpawnWaveQueue = new();
    public readonly Queue<EntityDeathMsg> EntityDeathQueue = new();
    public readonly Queue<UpgradeOptionsMsg> UpgradeOptionsQueue = new();
    public readonly Queue<PickupSpawnMsg> PickupSpawnQueue = new();
    public readonly Queue<PickupCollectMsg> PickupCollectQueue = new();
    public readonly Queue<BuffApplyMsg> BuffApplyQueue = new();
    public readonly Queue<BossPhaseChangeMsg> BossPhaseChangeQueue = new();

    // ── Events for direct UI notification ──
    public event Action<GameOverMsg> OnGameOver;
    public event Action<UpgradeOptionsMsg> OnUpgradeOptions;

    // ── Input sequence counter ──
    private int _inputSeq;

    public SyncClient()
    {
        NetManager.Instance.OnMessageReceived += HandleMessage;
    }

    // ──────────────────── Outgoing ────────────────────

    /// <summary>Send local player input (movement direction).</summary>
    public void SendInput(Vec2 moveDir)
    {
        _inputSeq++;
        NetManager.Instance.Send(MessageType.PlayerInput, new PlayerInputMsg
        {
            Dx = moveDir.X,
            Dy = moveDir.Y,
            Seq = _inputSeq,
        });
    }

    /// <summary>Send the player's upgrade choice.</summary>
    public void SendUpgradeChoice(UpgradeId id)
    {
        NetManager.Instance.Send(MessageType.UpgradeChoice, new UpgradeChoiceMsg
        {
            UpgradeId = id,
        });
    }

    /// <summary>Stop listening to NetManager events (cleanup).</summary>
    public void Dispose()
    {
        if (NetManager.Instance != null)
            NetManager.Instance.OnMessageReceived -= HandleMessage;
    }

    // ──────────────────── Incoming ────────────────────

    private void HandleMessage(NetMessage msg)
    {
        switch (msg.Type)
        {
            case MessageType.GameState:
                GameStateQueue.Enqueue(msg.GetPayload<GameStateMsg>());
                break;

            case MessageType.SpawnArrow:
                SpawnArrowQueue.Enqueue(msg.GetPayload<SpawnArrowMsg>());
                break;

            case MessageType.SpawnWave:
                SpawnWaveQueue.Enqueue(msg.GetPayload<SpawnWaveMsg>());
                break;

            case MessageType.EntityDeath:
                EntityDeathQueue.Enqueue(msg.GetPayload<EntityDeathMsg>());
                break;

            case MessageType.UpgradeOptions:
                var opts = msg.GetPayload<UpgradeOptionsMsg>();
                UpgradeOptionsQueue.Enqueue(opts);
                OnUpgradeOptions?.Invoke(opts);
                break;

            case MessageType.PickupSpawn:
                PickupSpawnQueue.Enqueue(msg.GetPayload<PickupSpawnMsg>());
                break;

            case MessageType.PickupCollect:
                PickupCollectQueue.Enqueue(msg.GetPayload<PickupCollectMsg>());
                break;

            case MessageType.BuffApply:
                BuffApplyQueue.Enqueue(msg.GetPayload<BuffApplyMsg>());
                break;

            case MessageType.BossPhaseChange:
                BossPhaseChangeQueue.Enqueue(msg.GetPayload<BossPhaseChangeMsg>());
                break;

            case MessageType.GameOver:
                OnGameOver?.Invoke(msg.GetPayload<GameOverMsg>());
                break;
        }
    }
}
