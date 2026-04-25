using Server.Proto;

namespace Server.Game;

/// <summary>
/// 游戏状态快照（每 tick 广播给客户端）
/// </summary>
public sealed class GameStateSnapshot
{
    public int ServerTick { get; set; }
    public List<PlayerStateMsg> Players { get; } = new();
    public List<ArrowStateMsg> Arrows { get; } = new();
    public List<MonsterStateMsg> Monsters { get; } = new();
    public WaveInfoMsg? WaveInfo { get; set; }
    
    public GameStateSnapshotMsg ToMsg()
    {
        return new GameStateSnapshotMsg
        {
            ServerTick = ServerTick,
            Players = Players,
            Arrows = Arrows,
            Monsters = Monsters,
            WaveInfo = WaveInfo
        };
    }
}
