using System;
using Game.Data;

namespace Game.Net;

/// <summary>
/// Handles the matchmaking flow: MatchRequest → MatchUpdate → MatchSuccess → PlayerReady → BattleStart.
/// Communicates with the server through NetManager and exposes events for the UI.
/// </summary>
public class MatchClient
{
    // ── Events for UI binding ──
    public event Action<MatchUpdateMsg> OnMatchUpdate;
    public event Action<MatchSuccessMsg> OnMatchSuccess;
    public event Action<BattleStartMsg> OnBattleStart;

    // ── State ──
    public bool IsMatching { get; private set; }
    public string RoomId { get; private set; }
    public string LocalPlayerId { get; private set; }
    public int LocalPlayerIndex { get; private set; }
    public int Seed { get; private set; }

    public MatchClient()
    {
        NetManager.Instance.OnMessageReceived += HandleMessage;
    }

    /// <summary>Start matchmaking — send MatchRequest to server.</summary>
    public void StartMatch()
    {
        if (IsMatching) return;
        IsMatching = true;

        NetManager.Instance.Send(MessageType.MatchRequest, new MatchRequestMsg());
    }

    /// <summary>Cancel matchmaking.</summary>
    public void CancelMatch()
    {
        if (!IsMatching) return;
        IsMatching = false;

        NetManager.Instance.Send(MessageType.MatchCancel, new { });
    }

    /// <summary>Signal readiness after match found.</summary>
    public void SendReady()
    {
        NetManager.Instance.Send(MessageType.PlayerReady, new PlayerReadyMsg());
    }

    /// <summary>Stop listening to NetManager events (cleanup).</summary>
    public void Dispose()
    {
        if (NetManager.Instance != null)
            NetManager.Instance.OnMessageReceived -= HandleMessage;
    }

    private void HandleMessage(NetMessage msg)
    {
        switch (msg.Type)
        {
            case MessageType.MatchUpdate:
                var update = msg.GetPayload<MatchUpdateMsg>();
                OnMatchUpdate?.Invoke(update);
                break;

            case MessageType.MatchSuccess:
                var success = msg.GetPayload<MatchSuccessMsg>();
                RoomId = success.RoomId;
                Seed = success.Seed;

                // Determine local player index
                foreach (var p in success.Players)
                {
                    if (p.Id == LocalPlayerId)
                    {
                        LocalPlayerIndex = p.Index;
                        break;
                    }
                }

                IsMatching = false;
                OnMatchSuccess?.Invoke(success);

                // Auto-send ready
                SendReady();
                break;

            case MessageType.BattleStart:
                var start = msg.GetPayload<BattleStartMsg>();
                Seed = start.Seed;
                OnBattleStart?.Invoke(start);
                break;
        }
    }
}
