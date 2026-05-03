namespace Server.Game;

/// <summary>
/// 服务端配置（不依赖 Godot 或 ECS Data）
/// </summary>
public static class GameConfig
{
    // 服务器设置
    public const int DefaultPort = 8080;
    public const int TickRate = 20;
    public const float TickInterval = 1f / TickRate;  // 50ms
    public const int MaxRooms = 100;
    
    // 匹配设置
    public const int MatchTimeoutSec = 60;
    
    // 心跳设置
    public const int HeartbeatIntervalSec = 10;
    public const int HeartbeatTimeoutSec = 30;
    
    // 地图和关卡加载器
    public static ServerMapLoader MapLoader => ServerMapLoader.Instance;
}
