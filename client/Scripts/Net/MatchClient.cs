using System;
using Server.Proto;

namespace Game.Net;

public class MatchClient
{
    public event Action<MatchSuccess> OnMatchSuccess;
    public event Action<GameStart> OnBattleStart;

    public bool IsMatching { get; private set; }
    public string RoomId { get; private set; }
    public string LocalPlayerId { get; private set; }
    public int LocalPlayerIndex { get; private set; }
    public int Seed { get; private set; }

    public MatchClient()
    {
        NetManager.Instance.OnMessageReceived += HandleMessage;
    }

    public void StartMatch(string playerId, string playerName)
    {
        if (IsMatching) return;
        IsMatching = true;
        LocalPlayerId = playerId;

        NetManager.Instance.Send(MsgIds.MatchRequest, new MatchRequest
        {
            PlayerId = playerId,
            PlayerName = playerName,
        });
    }

    public void CancelMatch()
    {
        if (!IsMatching) return;
        IsMatching = false;

        NetManager.Instance.Send(MsgIds.MatchCancel, new MatchCancel
        {
            PlayerId = LocalPlayerId ?? string.Empty,
        });
    }

    public void SendReady()
    {
        NetManager.Instance.Send(MsgIds.PlayerReady, new PlayerReady
        {
            RoomId = RoomId ?? string.Empty,
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
            case MsgIds.MatchSuccess:
                if (msg is not MatchSuccess success) break;

                RoomId = success.RoomId;
                Seed = success.RandomSeed;
                foreach (var p in success.Players)
                {
                    if (p.PlayerId == LocalPlayerId)
                    {
                        LocalPlayerIndex = p.Slot;
                        break;
                    }
                }

                IsMatching = false;
                OnMatchSuccess?.Invoke(success);
                SendReady();
                break;

            case MsgIds.GameStart:
                if (msg is not GameStart start) break;
                Seed = start.RandomSeed;
                OnBattleStart?.Invoke(start);
                break;
        }
    }
}
