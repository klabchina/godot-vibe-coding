using Server.Proto;

namespace Server.Room;

public enum RoomState
{
    Waiting,
    InGame
}

/// <summary>
/// 帧同步房间 — 服务器不跑任何游戏逻辑，只负责：
///   1. 收集每帧各玩家的输入
///   2. 打包成 LockstepFrame 广播给房间内所有客户端
/// </summary>
public sealed class GameRoom
{
    private int _frame = 0;

    public int RandomSeed { get; private set; }

    public string RoomId { get; }
    public RoomState State { get; private set; } = RoomState.Waiting;
    public int PlayerCount => _players.Count;

    private readonly Dictionary<string, (string ConnectionId, int Slot)> _players = new();
    private readonly Dictionary<string, PlayerMove> _frameInputs = new();
    private readonly List<SkillChoice> _pendingSkillChoices = new();
    private readonly HashSet<string> _readyPlayers = new();
    private readonly Dictionary<string, GameEndSubmit> _endSubmits = new();
    private readonly object _sync = new();

    public event Action<IReadOnlyList<string>, LockstepFrame>? OnBroadcastFrame;
    public event Action<IReadOnlyList<string>, GameStart>? OnGameStart;
    public event Action<GameEndSubmit>? OnGameEnd;

    public GameRoom(string roomId, params string[] playerIds)
    {
        RoomId = roomId;
        RandomSeed = Random.Shared.Next(1, int.MaxValue);
        for (int i = 0; i < playerIds.Length; i++)
            _players[playerIds[i]] = (string.Empty, i);
    }

    public void AddPlayer(string playerId)
    {
        lock (_sync)
        {
            if (_players.ContainsKey(playerId))
            {
                return;
            }

            var nextSlot = _players.Count;
            _players[playerId] = (string.Empty, nextSlot);
        }
    }

    public void SetConnection(string playerId, string connectionId)
    {
        lock (_sync)
        {
            if (_players.TryGetValue(playerId, out var info))
                _players[playerId] = (connectionId, info.Slot);
        }
    }

    public bool TryGetPlayerSlot(string playerId, out int slot)
    {
        lock (_sync)
        {
            if (_players.TryGetValue(playerId, out var info))
            {
                slot = info.Slot;
                return true;
            }

            slot = default;
            return false;
        }
    }

    public void OnPlayerReady(string playerId)
    {
        lock (_sync)
        {
            if (State != RoomState.Waiting) return;

            _readyPlayers.Add(playerId);
            Console.WriteLine($"[GameRoom:{RoomId}] Player {playerId} ready ({_readyPlayers.Count}/{_players.Count}) Needed player:2");

            if (_players.Count >= 2 && _readyPlayers.Count >= _players.Count)
                StartGame();
        }
    }

    public void OnPlayerMove(string playerId, PlayerMove input)
    {
        lock (_sync)
        {
            if (State != RoomState.InGame) return;
            _frameInputs[playerId] = input;
        }
    }

    public void OnSkillChoice(string playerId, SkillChoice choice)
    {
        lock (_sync)
        {
            if (State != RoomState.InGame) return;
            if (!_players.TryGetValue(playerId, out var info)) return;

            _pendingSkillChoices.Add(new SkillChoice
            {
                Tick = choice.Tick,
                SkillId = choice.SkillId,
                Slot = info.Slot,
            });
        }
    }

    public void OnGameEndSubmit(string playerId, GameEndSubmit submit)
    {
        lock (_sync)
        {
            if (State != RoomState.InGame) return;

            _endSubmits[playerId] = submit;
            if (_endSubmits.Count >= _players.Count && _players.Count > 0)
            {
                var reason = submit.Reason;
                EndGame(reason);
            }
        }
    }

    public void OnPlayerDisconnect(string playerId)
    {
        lock (_sync)
        {
            if (_players.TryGetValue(playerId, out var info))
                _players[playerId] = (string.Empty, info.Slot);
        }
    }

    public void Tick(float dt)
    {
        lock (_sync)
        {
            if (State != RoomState.InGame) return;

            _frame++;
            BroadcastFrame();
            _frameInputs.Clear();
            _pendingSkillChoices.Clear();
        }
    }

    public IEnumerable<string> GetConnectionIds()
    {
        lock (_sync)
        {
            return _players.Values
                .Select(v => v.ConnectionId)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            State = RoomState.Waiting;
            _frame = 0;
            _frameInputs.Clear();
            _pendingSkillChoices.Clear();
            _readyPlayers.Clear();
            _endSubmits.Clear();
            Console.WriteLine($"[GameRoom:{RoomId}] Reset.");
        }
    }

    private void StartGame()
    {
        State = RoomState.InGame;
        _frame = 0;
        _endSubmits.Clear();

        var connectionIds = _players.Values
            .Where(v => !string.IsNullOrEmpty(v.ConnectionId))
            .Select(v => v.ConnectionId)
            .ToList();

        OnGameStart?.Invoke(connectionIds, new GameStart
        {
            RoomId = RoomId,
            RandomSeed = RandomSeed,
        });

        Console.WriteLine($"[GameRoom:{RoomId}] Game started!");
    }

    private void BroadcastFrame()
    {
        var msg = new LockstepFrame { Frame = _frame };

        foreach (var (playerId, (_, slot)) in _players)
        {
            var input = _frameInputs.TryGetValue(playerId, out var inp) ? inp : new PlayerMove();
            msg.Inputs.Add(new PlayerFrameInput
            {
                PlayerId = playerId,
                Slot = slot,
                MoveDir = input.MoveDir ?? new Vec2(),
            });
        }

        foreach (var choice in _pendingSkillChoices)
        {
            msg.SkillChoices.Add(choice);
        }

        var connectionIds = _players.Values
            .Where(v => !string.IsNullOrEmpty(v.ConnectionId))
            .Select(v => v.ConnectionId)
            .ToList();

        OnBroadcastFrame?.Invoke(connectionIds, msg);
    }

    private void EndGame(string reason)
    {
        OnGameEnd?.Invoke(new GameEndSubmit
        {
            Tick = _frame,
            Reason = reason,
        });
        Console.WriteLine($"[GameRoom:{RoomId}] Game ended: {reason}");
    }
}
