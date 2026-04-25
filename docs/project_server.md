# 对战服务器架构设计

## 关联实现计划

- `docs/superpowers/server/2026-04-24-server-minimal-api-skeleton.md`（Server Minimal API + 原生 WS 骨架，已完成）
- `docs/superpowers/server/2026-04-25-server-full-implementation.md`（Server 游戏功能实现，进行中）

## 技术选型

| 项目 | 选择 | 说明 |
|------|------|------|
| **语言** | C# (.NET 8+) | 与 Godot C# 客户端统一语言，可共享协议和数据结构 |
| **应用框架** | ASP.NET Core Minimal API | 轻量 Host，提供 DI / 配置 / 日志 / 生命周期管理 |
| **网络** | 原生 WebSocket (System.Net.WebSockets) | 通过 `app.UseWebSockets()` + 自定义 Handler 处理连接与消息 |
| **序列化** | Protobuf (Google.Protobuf) | 高效紧凑，客户端服务端共享 .proto 定义 |
| **架构** | 单进程多房间 | 每个房间独立 Game Loop，适合 2 人小规模对战 |
| **部署** | Docker / 单二进制 | `dotnet publish` 产出独立可执行文件 |

---

## 整体架构

```
                    ┌─────────────────────────────────────────────┐
                    │              Game Server (.NET)             │
                    │                                             │
 Player A ──WS──▶  │  ┌───────────┐     ┌──────────────────┐    │
                    │  │ Connection │────▶│  Session Manager  │    │
 Player B ──WS──▶  │  │  Manager  │     └────────┬─────────┘    │
                    │  └───────────┘              │              │
                    │                             ▼              │
                    │                    ┌─────────────────┐     │
                    │                    │  Match Service   │     │
                    │                    └────────┬────────┘     │
                    │                             │ 匹配成功     │
                    │                             ▼              │
                    │                    ┌─────────────────┐     │
                    │                    │  Room Manager    │     │
                    │                    └────────┬────────┘     │
                    │                             │              │
                    │               ┌─────────────┼──────────┐   │
                    │               ▼             ▼          ▼   │
                    │           ┌───────┐   ┌───────┐  ┌───────┐│
                    │           │Room 1 │   │Room 2 │  │Room N ││
                    │           │GameLoop│   │GameLoop│  │GameLoop││
                    │           └───────┘   └───────┘  └───────┘│
                    └─────────────────────────────────────────────┘
```

---

## Program.cs 最小启动模板（Minimal API + 原生 WS）

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Server.Hosting;
using Server.Match;
using Server.Network;
using Server.Room;
using Server.Session;

var builder = WebApplication.CreateBuilder(args);

// 核心服务注册
builder.Services.AddSingleton<ConnectionManager>();
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<MatchService>();
builder.Services.AddSingleton<RoomManager>();
builder.Services.AddHostedService<GameLoopService>();

var app = builder.Build();

// 启用原生 WebSocket
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(10)
});

// 连接入口（示例）
app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var socket = await context.WebSockets.AcceptWebSocketAsync();
    var connectionManager = context.RequestServices.GetRequiredService<ConnectionManager>();
    var handler = context.RequestServices.GetRequiredService<WebSocketHandler>();

    var connectionId = connectionManager.Add(socket);
    await handler.RunAsync(connectionId, socket, context.RequestAborted);
});

app.Run();
```

> 说明：`Program.cs` 负责 Host 与中间件装配；匹配与房间逻辑由 `GameLoopService` 定时驱动。

---

## 核心模块

### 1. ConnectionManager — 连接管理

负责 WebSocket 连接的接入、维护和断开处理。

```csharp
// 职责
- 接受 WebSocket 握手
- 为每个连接分配唯一 ConnectionId
- 维护 ConnectionId → WebSocket 映射
- 心跳检测（30s 超时）
- 断线处理与通知
```

**连接生命周期：**

```
Client Connect
      │
      ▼
WebSocket Handshake
      │
      ▼
分配 ConnectionId ──▶ 加入 ConnectionPool
      │
      ▼
等待 LoginRequest
      │
      ▼
验证通过 ──▶ 创建 Session ──▶ 可进行匹配
      │
   (断线)
      ▼
通知 Room/MatchService ──▶ 移出 ConnectionPool
```

### 2. SessionManager — 会话管理

将连接与玩家身份绑定。

```csharp
public class Session
{
    public string ConnectionId { get; set; }
    public string PlayerId { get; set; }
    public string PlayerName { get; set; }
    public SessionState State { get; set; }  // Idle, Matching, InRoom, InBattle
    public string? RoomId { get; set; }
}

public enum SessionState
{
    Idle,       // 空闲，在主界面
    Matching,   // 匹配中
    InRoom,     // 已进入房间，等待开始
    InBattle,   // 战斗中
}
```

### 3. MatchService — 匹配服务

简单队列匹配，2 人即开。

```
匹配流程：

Player A ──MatchRequest──▶ ┌──────────────┐
                           │ Match Queue  │
Player B ──MatchRequest──▶ │  [A]  [B]    │
                           └──────┬───────┘
                                  │ 队列人数 >= 2
                                  ▼
                           ┌──────────────┐
                           │ 创建 Room    │
                           │ 通知双方     │
                           │ MatchSuccess │
                           └──────────────┘
```

```csharp
public class MatchService
{
    private readonly ConcurrentQueue<string> _queue;  // SessionId 队列

    // 定时检查（每 500ms）
    public void Tick()
    {
        while (_queue.Count >= 2)
        {
            _queue.TryDequeue(out var playerA);
            _queue.TryDequeue(out var playerB);
            RoomManager.CreateRoom(playerA, playerB);
        }
    }

    public void Enqueue(string sessionId) { ... }
    public void Dequeue(string sessionId) { ... }  // 取消匹配
}
```

### 4. RoomManager — 房间管理

管理所有活跃房间的创建、查找和销毁。

```csharp
public class RoomManager
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms;

    public GameRoom CreateRoom(string playerA, string playerB);
    public GameRoom? GetRoom(string roomId);
    public void DestroyRoom(string roomId);
    public void Tick(float deltaTime);  // 驱动所有房间的 GameLoop
}
```

### 5. GameRoom — 单局游戏房间

每个房间包含独立的游戏状态和循环逻辑，是战斗的核心。

```csharp
public class GameRoom
{
    public string RoomId { get; }
    public RoomState State { get; set; }
    public List<PlayerState> Players { get; }
    public WaveController WaveCtrl { get; }
    public List<MonsterState> Monsters { get; }
    public List<ArrowState> Arrows { get; }

    public int CurrentWave { get; }
    public int TotalKills { get; }
    public long TickCount { get; }

    public void OnPlayerReady(string playerId);
    public void OnPlayerInput(string playerId, PlayerInputMsg input);
    public void OnPlayerDisconnect(string playerId);
    public void Tick(float deltaTime);  // 单房间游戏循环
}

public enum RoomState
{
    WaitingReady,   // 等待双方 Ready
    Playing,        // 战斗中
    Finished,       // 已结束
}
```

---

## 游戏循环 (Game Loop)

主循环由 ASP.NET Core `BackgroundService` 驱动，以固定频率（20 tick/s，每 tick 50ms）更新匹配与房间。

```
┌───────────────────────────────────────────────────┐
│                Server Main Loop                   │
│                (20 ticks/sec)                      │
│                                                   │
│  foreach room in activeRooms:                     │
│  ┌─────────────────────────────────────────────┐  │
│  │            Room.Tick(deltaTime)              │  │
│  │                                             │  │
│  │  1. ProcessInputQueue()   处理玩家输入缓冲   │  │
│  │  2. UpdateArrows()        更新箭矢位移       │  │
│  │  3. UpdateMonsters()      更新怪物 AI/移动   │  │
│  │  4. CheckCollisions()     碰撞检测           │  │
│  │  5. ApplyDamage()         结算伤害           │  │
│  │  6. CleanupDead()         清除死亡实体       │  │
│  │  7. CheckWaveProgress()   检查波次推进       │  │
│  │  8. CheckGameOver()       检查游戏结束条件   │  │
│  │  9. BroadcastState()      广播状态给客户端   │  │
│  └─────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────┘
```

```csharp
// 主循环驱动（GameLoopService : BackgroundService）
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
    while (!stoppingToken.IsCancellationRequested &&
           await timer.WaitForNextTickAsync(stoppingToken))
    {
        var dt = 0.05f; // 50ms
        _matchService.Tick();
        _roomManager.Tick(dt);
    }
}
```

---

## 波次控制

```csharp
public class WaveController
{
    public int CurrentWave { get; private set; }
    public WaveState State { get; private set; }
    public float IntervalTimer { get; private set; }

    private List<WaveConfig> _waveConfigs;

    public enum WaveState
    {
        Interval,    // 波次间歇
        Spawning,    // 正在生成怪物
        InProgress,  // 怪物存活中，等待清完
    }
}
```

**波次配置数据结构：**

```csharp
public class WaveConfig
{
    public int WaveNumber { get; set; }
    public List<SpawnEntry> Spawns { get; set; }
    public float SpawnInterval { get; set; }     // 同波怪物生成间隔
    public float PreWaveDelay { get; set; }      // 波次开始前等待时间
}

public class SpawnEntry
{
    public MonsterType Type { get; set; }
    public int Count { get; set; }
    public SpawnPosition Position { get; set; }  // 生成位置/区域
}
```

**波次推进逻辑：**

```
Wave N 所有怪物死亡
        │
        ▼
State = Interval
IntervalTimer = PreWaveDelay (如 5 秒)
        │
        ▼ 倒计时结束
State = Spawning
按 SpawnInterval 逐个生成怪物
        │
        ▼ 全部生成完毕
State = InProgress
等待所有怪物死亡 ──▶ 进入 Wave N+1
        │
        ▼ 最终波完成
GameOver(Victory)
```

---

## Protobuf 协议定义

### 消息总线

所有消息通过统一信封包装，通过 WebSocket Binary Frame 传输：

```protobuf
syntax = "proto3";
package summer;

// 消息信封 — 所有消息的外层包装
message Envelope {
    uint32 msg_id = 1;      // 消息类型 ID
    bytes  payload = 2;      // 具体消息的序列化字节
    uint64 timestamp = 3;    // 服务器时间戳
}
```

### 匹配相关

```protobuf
// Client → Server
message MatchRequest {
    string player_id = 1;
}

// Client → Server
message MatchCancel {
    string player_id = 1;
}

// Server → Client
message MatchUpdate {
    MatchStatus status = 1;
    float wait_time = 2;

    enum MatchStatus {
        WAITING = 0;
        FOUND = 1;
        CANCELLED = 2;
        TIMEOUT = 3;
    }
}

// Server → Client
message MatchSuccess {
    string room_id = 1;
    repeated PlayerInfo players = 2;
}

message PlayerInfo {
    string player_id = 1;
    string player_name = 2;
    int32  slot = 3;          // 0 或 1
}
```

### 房间 & 准备

```protobuf
// Client → Server
message PlayerReady {
    string room_id = 1;
}

// Server → Client
message BattleStart {
    string room_id = 1;
    int32  random_seed = 2;
    repeated WaveConfigMsg waves = 3;
}

message WaveConfigMsg {
    int32 wave_number = 1;
    repeated SpawnEntryMsg spawns = 2;
    float spawn_interval = 3;
    float pre_wave_delay = 4;
}

message SpawnEntryMsg {
    int32 monster_type = 1;
    int32 count = 2;
    Vec2  position = 3;
}

message Vec2 {
    float x = 1;
    float y = 2;
}
```

### 战斗同步

```protobuf
// Client → Server — 玩家操作（高频，每帧）
message PlayerInput {
    int32  tick = 1;            // 客户端帧号
    Vec2   move_dir = 2;        // 移动方向
    float  aim_angle = 3;       // 瞄准角度
    bool   shoot = 4;           // 是否射击
    float  charge_power = 5;    // 蓄力值 (0~1)
}

// Server → Client — 游戏状态快照（每 tick 广播）
message GameStateSnapshot {
    int32 server_tick = 1;
    repeated PlayerState players = 2;
    repeated ArrowState arrows = 3;
    repeated MonsterState monsters = 4;
    WaveInfo wave_info = 5;
}

message PlayerState {
    string player_id = 1;
    Vec2   position = 2;
    float  aim_angle = 3;
    int32  hp = 4;
    int32  max_hp = 5;
    PlayerAction action = 6;

    enum PlayerAction {
        IDLE = 0;
        MOVING = 1;
        CHARGING = 2;
        SHOOTING = 3;
    }
}

message ArrowState {
    int32  arrow_id = 1;
    string owner_id = 2;
    Vec2   position = 3;
    Vec2   velocity = 4;
    int32  damage = 5;
}

message MonsterState {
    int32       monster_id = 1;
    int32       monster_type = 2;
    Vec2        position = 3;
    Vec2        velocity = 4;
    int32       hp = 5;
    int32       max_hp = 6;
}

message WaveInfo {
    int32 current_wave = 1;
    int32 total_waves = 2;
    int32 monsters_remaining = 3;
    float interval_countdown = 4;  // 波次间歇倒计时，<=0 表示战斗中
}
```

### 事件通知

```protobuf
// Server → Client
message EntityDeath {
    EntityType type = 1;
    int32 entity_id = 2;
    Vec2  position = 3;         // 死亡位置，用于播放特效

    enum EntityType {
        MONSTER = 0;
        ARROW = 1;
        PLAYER = 2;
    }
}

// Server → Client
message WaveStart {
    int32 wave_number = 1;
    int32 monster_count = 2;
}

// Server → Client
message GameOver {
    GameResult result = 1;
    int32 waves_cleared = 2;
    int32 total_kills = 3;
    repeated PlayerScore scores = 4;

    enum GameResult {
        VICTORY = 0;
        DEFEAT = 1;
        DISCONNECT = 2;
    }
}

message PlayerScore {
    string player_id = 1;
    string player_name = 2;
    int32  kills = 3;
    int32  damage_dealt = 4;
    int32  arrows_fired = 5;
}
```

### 消息 ID 映射

```csharp
public static class MsgIds
{
    // 匹配
    public const uint MatchRequest  = 1001;
    public const uint MatchCancel   = 1002;
    public const uint MatchUpdate   = 1003;
    public const uint MatchSuccess  = 1004;

    // 房间
    public const uint PlayerReady   = 2001;
    public const uint BattleStart   = 2002;

    // 战斗
    public const uint PlayerInput       = 3001;
    public const uint GameStateSnapshot = 3002;
    public const uint EntityDeath       = 3003;
    public const uint WaveStart         = 3004;
    public const uint GameOver          = 3005;

    // 系统
    public const uint Heartbeat     = 9001;
    public const uint Disconnect    = 9002;
}
```

---

## 服务端游戏逻辑

### 怪物 AI

```csharp
public class MonsterState
{
    public int MonsterId;
    public MonsterType Type;
    public Vector2 Position;
    public Vector2 Velocity;
    public int Hp;
    public int MaxHp;
    public float Speed;
    public float AttackRange;
    public int AttackDamage;

    // 简单 AI：朝最近玩家移动
    public void UpdateAI(List<PlayerState> players, float dt)
    {
        var target = FindClosestPlayer(players);
        if (target == null) return;

        var dir = (target.Position - Position).Normalized();
        Velocity = dir * Speed;
        Position += Velocity * dt;

        // 进入攻击范围则造成伤害
        if (DistanceTo(target) <= AttackRange)
        {
            target.TakeDamage(AttackDamage);
        }
    }
}
```

### 碰撞检测

```csharp
// 箭矢 vs 怪物 — 圆形碰撞
public void CheckCollisions()
{
    foreach (var arrow in _arrows.ToList())
    {
        foreach (var monster in _monsters)
        {
            float dist = (arrow.Position - monster.Position).Length();
            if (dist <= CollisionRadius)
            {
                monster.Hp -= arrow.Damage;
                _arrows.Remove(arrow);  // 箭矢命中后消失
                BroadcastEntityDeath(EntityType.Arrow, arrow.ArrowId, arrow.Position);

                if (monster.Hp <= 0)
                {
                    BroadcastEntityDeath(EntityType.Monster, monster.MonsterId, monster.Position);
                }
                break;
            }
        }
    }
}
```

### 游戏结束条件

```
Victory: 所有波次清除完毕
Defeat:  所有玩家 HP <= 0
Disconnect: 任一玩家断线超过 30 秒未重连
```

---

## 断线重连

```
Player 断线
      │
      ▼
Server 标记 Session 为 Disconnected
Room 保持运行，怪物继续移动
启动 30s 重连倒计时
      │
      ├── 30s 内重连 ──▶ 恢复 Session，发送全量 GameStateSnapshot
      │
      └── 超时未重连 ──▶ GameOver(DISCONNECT)，通知另一玩家
```

```csharp
public class Session
{
    public bool IsDisconnected { get; set; }
    public DateTime DisconnectTime { get; set; }
    public const int ReconnectTimeoutSec = 30;

    public bool IsReconnectExpired()
        => IsDisconnected
        && (DateTime.UtcNow - DisconnectTime).TotalSeconds > ReconnectTimeoutSec;
}
```

---

## 目录结构

```
server/
├── Server.sln
├── src/
│   ├── Program.cs                    # 入口，启动 Minimal API Host / WS 中间件 / DI
│   ├── Hosting/
│   │   └── GameLoopService.cs        # BackgroundService 主循环（20 tick/s）
│   │
│   ├── Network/
│   │   ├── ConnectionManager.cs      # WebSocket 连接管理
│   │   ├── MessageRouter.cs          # 根据 MsgId 路由到处理器
│   │   └── WebSocketHandler.cs       # 单连接读写循环
│   │
│   ├── Session/
│   │   ├── Session.cs                # 玩家会话
│   │   └── SessionManager.cs         # 会话管理
│   │
│   ├── Match/
│   │   └── MatchService.cs           # 匹配队列逻辑
│   │
│   ├── Room/
│   │   ├── RoomManager.cs            # 房间生命周期管理
│   │   ├── GameRoom.cs               # 单局游戏房间 & Game Loop
│   │   └── WaveController.cs         # 波次控制器
│   │
│   ├── Game/
│   │   ├── PlayerState.cs            # 玩家状态
│   │   ├── MonsterState.cs           # 怪物状态 & AI
│   │   ├── ArrowState.cs             # 箭矢状态
│   │   ├── CollisionHelper.cs        # 碰撞检测工具
│   │   └── GameConfig.cs             # 游戏数值配置
│   │
│   └── Proto/
│       └── MsgIds.cs                 # 消息 ID 常量
│
├── proto/
│   ├── common.proto                  # Vec2, Envelope
│   ├── match.proto                   # 匹配相关消息
│   ├── room.proto                    # 房间 & 准备
│   ├── battle.proto                  # 战斗同步消息
│   └── event.proto                   # 事件通知
│
├── config/
│   ├── server_config.json            # 服务器配置（端口、tick率等）
│   ├── wave_config.json              # 波次配置
│   └── monster_config.json           # 怪物属性配置
│
├── Dockerfile
└── README.md
```

---

## 配置文件

### server_config.json

```json
{
    "port": 8080,
    "tick_rate": 20,
    "max_rooms": 100,
    "match_timeout_sec": 60,
    "reconnect_timeout_sec": 30,
    "heartbeat_interval_sec": 10,
    "heartbeat_timeout_sec": 30
}
```

### wave_config.json

```json
{
    "waves": [
        {
            "wave_number": 1,
            "pre_wave_delay": 3.0,
            "spawn_interval": 1.0,
            "spawns": [
                { "type": "Slime", "count": 5 }
            ]
        },
        {
            "wave_number": 2,
            "pre_wave_delay": 5.0,
            "spawn_interval": 0.8,
            "spawns": [
                { "type": "Slime", "count": 8 },
                { "type": "Skeleton", "count": 2 }
            ]
        },
        {
            "wave_number": 3,
            "pre_wave_delay": 5.0,
            "spawn_interval": 0.6,
            "spawns": [
                { "type": "Slime", "count": 3 },
                { "type": "Skeleton", "count": 5 },
                { "type": "Orc", "count": 2 }
            ]
        },
        {
            "wave_number": 5,
            "pre_wave_delay": 8.0,
            "spawn_interval": 0.5,
            "spawns": [
                { "type": "Boss", "count": 1 },
                { "type": "Skeleton", "count": 5 }
            ]
        }
    ]
}
```

### monster_config.json

```json
{
    "monsters": {
        "Slime":    { "hp": 30,  "speed": 50,  "attack_damage": 5,  "attack_range": 30  },
        "Skeleton": { "hp": 60,  "speed": 80,  "attack_damage": 10, "attack_range": 40  },
        "Orc":      { "hp": 120, "speed": 120, "attack_damage": 20, "attack_range": 35  },
        "Boss":     { "hp": 500, "speed": 40,  "attack_damage": 35, "attack_range": 50  }
    }
}
```

---

## 部署架构

```
                    ┌─────────────┐
                    │   Nginx     │
                    │  (WSS 代理) │
                    └──────┬──────┘
                           │ :443 → :8080
                           ▼
                    ┌─────────────┐
                    │ Game Server │
                    │  (.NET 8)   │
                    │  Port 8080  │
                    └─────────────┘

Docker 部署：
  docker build -t summer-server .
  docker run -p 8080:8080 summer-server
```

---

## 性能预估

| 指标 | 预估值 |
|------|--------|
| 单房间内存 | ~50 KB（2 玩家 + 50 怪物 + 20 箭矢） |
| 单 Tick CPU | < 0.1ms / 房间 |
| 最大房间数 | 100+（单核即可） |
| 网络带宽/房间 | ~2 KB/s（20 tick × ~100B/snapshot） |
| 启动内存 | ~30 MB（.NET 运行时） |

对于 2 人小规模对战游戏，单台低配服务器即可支撑数百同时对战。

---

# ECS 服务端测试程序

独立于 Godot 引擎的服务端 ECS 测试程序，链接客户端共享逻辑（`Systems/`、`Core/`、`Data/`），不含 `ClientSystems/` 和网络层。

---

## 项目结构

```
pure_ecs_test/
├── ServerTest.csproj      # 项目文件（OutputType=Exe，DefineConstants=SERVER）
├── ServerGameManager.cs   # ECS World 初始化与系统注册（对应客户端 GameManager）
└── Program.cs             # 入口：生成玩家、启动波次、20 tick/s 主循环
```


## 运行方式

```bash
cd server
dotnet run
```
