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
///   2. 打包成 LockstepFrameMsg 广播给房间内所有客户端
/// </summary>
public sealed class GameRoom
{
    private int _frame = 0;

    public string RoomId { get; }
    public RoomState State { get; private set; } = RoomState.Waiting;
    public int PlayerCount => _players.Count;

    // playerId -> (connectionId, slot index)
    private readonly Dictionary<string, (string ConnectionId, int Slot)> _players = new();

    // 当前帧缓冲的输入，Tick 结束后清空
    private readonly Dictionary<string, PlayerMoveMsg> _frameInputs = new();

    // 已发送 Ready 的玩家
    private readonly HashSet<string> _readyPlayers = new();

    // 已提交结束状态的玩家
    private readonly Dictionary<string, GameEndSubmitMsg> _endSubmits = new();

    /// <summary>广播帧数据 — (connectionIds, frame消息)</summary>
    public event Action<IReadOnlyList<string>, LockstepFrameMsg>? OnBroadcastFrame;

    /// <summary>游戏开始事件</summary>
    public event Action<IReadOnlyList<string>, GameStartMsg>? OnGameStart;

    /// <summary>游戏结束事件</summary>
    public event Action<GameOverMsg>? OnGameOver;

    public GameRoom(string roomId, params string[] playerIds)
    {
        RoomId = roomId;
        for (int i = 0; i < playerIds.Length; i++)
            _players[playerIds[i]] = (string.Empty, i);
    }

    public void AddPlayer(string playerId)
    {
        if (_players.ContainsKey(playerId))
        {
            return;
        }

        var nextSlot = _players.Count;
        _players[playerId] = (string.Empty, nextSlot);
    }

    /// <summary>
    /// 关联玩家的 WebSocket 连接 ID（匹配成功后或断线重连时调用）
    /// </summary>
    public void SetConnection(string playerId, string connectionId)
    {
        if (_players.TryGetValue(playerId, out var info))
            _players[playerId] = (connectionId, info.Slot);
    }

    /// <summary>
    /// 玩家发送 Ready，全员就绪后开始游戏
    /// </summary>
    public void OnPlayerReady(string playerId)
    {
        if (State != RoomState.Waiting) return;

        _readyPlayers.Add(playerId);
        Console.WriteLine($"[GameRoom:{RoomId}] Player {playerId} ready ({_readyPlayers.Count}/{_players.Count})");

        if (_readyPlayers.Count >= _players.Count && _players.Count > 0)
            StartGame();
    }

    /// <summary>
    /// 收到玩家输入 — 缓存到当前帧，等待 Tick 打包广播
    /// </summary>
    public void OnPlayerMove(string playerId, PlayerMoveMsg input)
    {
        if (State != RoomState.InGame) return;
        _frameInputs[playerId] = input;
    }

    public void OnGameEndSubmit(string playerId, GameEndSubmitMsg submit)
    {
        if (State != RoomState.InGame) return;

        _endSubmits[playerId] = submit;
        if (_endSubmits.Count >= _players.Count && _players.Count > 0)
        {
            var reason = submit.Reason;
            EndGame(reason);
        }
    }

    /// <summary>
    /// 玩家断线
    /// </summary>
    public void OnPlayerDisconnect(string playerId)
    {
        if (_players.TryGetValue(playerId, out var info))
            _players[playerId] = (string.Empty, info.Slot);
    }

    /// <summary>
    /// 每 tick 由 RoomManager 驱动：打包输入帧并广播
    /// </summary>
    public void Tick(float dt)
    {
        if (State != RoomState.InGame) return;

        _frame++;
        BroadcastFrame();
        _frameInputs.Clear();
    }

    public IEnumerable<string> GetConnectionIds() =>
        _players.Values
            .Select(v => v.ConnectionId)
            .Where(id => !string.IsNullOrEmpty(id));

    public void Reset()
    {
        State = RoomState.Waiting;
        _frame = 0;
        _frameInputs.Clear();
        _readyPlayers.Clear();
        _endSubmits.Clear();
        Console.WriteLine($"[GameRoom:{RoomId}] Reset.");
    }

    // ── 私有 ─────────────────────────────────────────────────────────

    private void StartGame()
    {
        State = RoomState.InGame;
        _frame = 0;
        _endSubmits.Clear();

        var connectionIds = _players.Values
            .Where(v => !string.IsNullOrEmpty(v.ConnectionId))
            .Select(v => v.ConnectionId)
            .ToList();

        OnGameStart?.Invoke(connectionIds, new GameStartMsg
        {
            RoomId = RoomId,
            RandomSeed = Random.Shared.Next(1, int.MaxValue),
        });

        Console.WriteLine($"[GameRoom:{RoomId}] Game started!");
    }

    private void BroadcastFrame()
    {
        var msg = new LockstepFrameMsg { Frame = _frame };

        foreach (var (playerId, (_, slot)) in _players)
        {
            var input = _frameInputs.TryGetValue(playerId, out var inp) ? inp : new PlayerMoveMsg();
            msg.Inputs.Add(new PlayerFrameInput
            {
                PlayerId = playerId,
                Slot = slot,
                MoveDir = input.MoveDir,
            });
        }

        var connectionIds = _players.Values
            .Where(v => !string.IsNullOrEmpty(v.ConnectionId))
            .Select(v => v.ConnectionId)
            .ToList();

        OnBroadcastFrame?.Invoke(connectionIds, msg);
    }

    private void EndGame(string reason)
    {
        OnGameOver?.Invoke(new GameOverMsg
        {
            RoomId = RoomId,
            Reason = reason,
        });
        Console.WriteLine($"[GameRoom:{RoomId}] Game ended: {reason}");
    }
}
