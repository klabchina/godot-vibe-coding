# 对战服务器架构设计

## 同步模式：帧同步（Lockstep）

服务器**不运行任何游戏逻辑**，职责仅为：

1. 接受客户端连接与匹配
2. 每帧收集所有玩家的输入
3. 将输入打包成 `LockstepFrameMsg` 广播给房间内所有客户端

所有游戏模拟（怪物 AI、碰撞、伤害、波次推进）由客户端本地 ECS 确定性执行。

---

## 技术选型

| 项目 | 选择 | 说明 |
|------|------|------|
| **语言** | C# (.NET 8+) | 与 Godot C# 客户端统一语言，可共享协议和数据结构 |
| **应用框架** | ASP.NET Core Minimal API | 轻量 Host，提供 DI / 配置 / 日志 / 生命周期管理 |
| **网络** | 原生 WebSocket (System.Net.WebSockets) | 通过 `app.UseWebSockets()` + 自定义 Handler 处理连接与消息 |
| **序列化** | System.Text.Json | 轻量，无需 Protobuf 代码生成 |
| **架构** | 帧同步 + 单进程多房间 | 服务器只转发输入帧，客户端负责确定性模拟 |
| **部署** | Docker / 单二进制 | `dotnet publish` 产出独立可执行文件 |

---

## 整体架构

```
                    ┌──────────────────────────────────────────────┐
                    │              Game Server (.NET)              │
                    │                                              │
 Player A ──WS──▶  │  ┌────────────┐    ┌──────────────────┐     │
                    │  │ Connection  │───▶│  Session Manager  │     │
 Player B ──WS──▶  │  │  Manager   │    └────────┬─────────┘     │
                    │  └────────────┘             │               │
                    │                             ▼               │
                    │                    ┌─────────────────┐      │
                    │                    │  Match Service   │      │
                    │                    └────────┬────────┘      │
                    │                             │ 匹配成功      │
                    │                             ▼               │
                    │                    ┌─────────────────┐      │
                    │                    │  Room Manager    │      │
                    │                    └────────┬────────┘      │
                    │                             │               │
                    │              ┌──────────────┼──────────┐    │
                    │              ▼              ▼          ▼    │
                    │          ┌───────┐    ┌───────┐  ┌───────┐  │
                    │          │Room 1 │    │Room 2 │  │Room N │  │
                    │          │(转发帧)│    │(转发帧)│  │(转发帧)│  │
                    │          └───────┘    └───────┘  └───────┘  │
                    └──────────────────────────────────────────────┘
```

---

## 核心模块

### 1. ConnectionManager — 连接管理

负责 WebSocket 连接的接入、维护和断开处理。

```
职责：
- 接受 WebSocket 握手，分配唯一 ConnectionId
- 维护 ConnectionId → WebSocket 映射
- 心跳超时检测
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
等待 MatchRequest（含 PlayerId / PlayerName）
      │
      ▼
创建/更新 Session ──▶ 进入匹配队列
      │
   (断线)
      ▼
通知 Room.OnPlayerDisconnect ──▶ 移出 ConnectionPool
```

### 2. SessionManager — 会话管理

将连接与玩家身份绑定。

```csharp
public sealed class Session
{
    public string ConnectionId { get; init; }
    public string PlayerId     { get; init; }
    public string PlayerName   { get; set; }
    public SessionState State  { get; set; }  // Idle, Matching, InRoom, InBattle
    public string? RoomId      { get; set; }
    public bool IsDisconnected { get; set; }
}
```

### 3. MatchService — 匹配服务

简单 FIFO 队列，满 2 人即建房。

```
匹配流程：

Player A ──MatchRequest──▶ ┌──────────────┐
                           │ Match Queue  │
Player B ──MatchRequest──▶ │  [A]  [B]    │
                           └──────┬───────┘
                                  │ 队列 >= 2
                                  ▼
                     RoomManager.CreateRoom(A, B)
                     room.SetConnection(A, connIdA)
                     room.SetConnection(B, connIdB)
                     Session[A].RoomId = room.RoomId
                     Session[B].RoomId = room.RoomId
```

### 4. RoomManager — 房间管理

创建房间并将 `OnBroadcastFrame` / `OnGameOver` 事件与 `WebSocketHandler` 对接。

```csharp
public GameRoom CreateRoom(params string[] playerIds)
{
    var room = new GameRoom(..., playerIds);

    room.OnBroadcastFrame += (connIds, frame) =>
        _ = _wsHandler.BroadcastAsync(connIds, MsgIds.LockstepFrame, frame);

    room.OnGameOver += msg =>
    {
        foreach (var connId in room.GetConnectionIds())
            _ = _wsHandler.SendAsync(connId, MsgIds.GameOver, msg);
        _rooms.TryRemove(room.RoomId, out _);
    };

    _rooms[room.RoomId] = room;
    return room;
}
```

### 5. GameRoom — 帧同步房间

```csharp
// 职责：收集输入缓冲 → 每 Tick 打包帧广播
public sealed class GameRoom
{
    // 收到玩家输入，缓存到当前帧
    public void OnPlayerInput(string playerId, PlayerInputMsg input);

    // 每 tick 由 RoomManager 驱动
    public void Tick(float dt);
        // → _frame++
        // → BroadcastFrame()   // 打包所有玩家输入 → OnBroadcastFrame 事件
        // → _frameInputs.Clear()

    public event Action<IReadOnlyList<string>, LockstepFrameMsg>? OnBroadcastFrame;
    public event Action<GameOverMsg>? OnGameOver;
}
```

缺席（本帧未发送输入）的玩家自动填充空 `PlayerInputMsg`，客户端不会因缺帧断流。

---

## 游戏循环

主循环由 `GameLoopService`（`BackgroundService`）以 **20 tick/s**（每 tick 50ms）驱动。

```
┌────────────────────────────────────────────┐
│            Server Tick (50ms)              │
│                                            │
│  1. MatchService.Tick()                    │
│     └── 队列 >= 2 → CreateRoom + 绑定连接 │
│                                            │
│  2. RoomManager.Tick(dt)                   │
│     └── foreach room:                      │
│           room._frame++                    │
│           打包所有玩家输入                 │
│           OnBroadcastFrame → WS广播        │
│           _frameInputs.Clear()             │
│                                            │
│  3. 心跳超时检查                           │
│  4. 断线重连超时检查                       │
└────────────────────────────────────────────┘
```

---

## 消息协议

所有消息通过 WebSocket Binary Frame 传输，格式为：

```
[4字节 uint MsgId][N字节 JSON payload]
```

### 消息 ID

```csharp
// 匹配
MatchRequest  = 1001   // C→S
MatchCancel   = 1002   // C→S
MatchUpdate   = 1003   // S→C
MatchSuccess  = 1004   // S→C

// 房间
PlayerReady   = 2001   // C→S
BattleStart   = 2002   // S→C  (含随机种子，客户端用于初始化确定性模拟)

// 战斗（帧同步）
PlayerInput   = 3001   // C→S  每帧上报本地操作
LockstepFrame = 3008   // S→C  打包所有玩家输入后广播

// 结算
GameOver      = 3005   // S→C

// 系统
Heartbeat     = 9001   // C↔S
Disconnect    = 9002   // S→C
```

### 关键消息结构

```csharp
// Client → Server（每帧，高频）
public class PlayerInputMsg
{
    public int   Tick        { get; set; }  // 客户端本地帧号
    public Vec2  MoveDir     { get; set; }  // 移动方向 (归一化)
    public float AimAngle    { get; set; }  // 瞄准角度
    public bool  Shoot       { get; set; }  // 是否射击
    public float ChargePower { get; set; }  // 蓄力值 0~1
}

// Server → Client（每服务器帧广播）
public class LockstepFrameMsg
{
    public int Frame { get; set; }
    public List<PlayerFrameInput> Inputs { get; set; }
}

public class PlayerFrameInput
{
    public string PlayerId   { get; set; }
    public int    Slot       { get; set; }  // 0 or 1
    public Vec2   MoveDir    { get; set; }
    public float  AimAngle   { get; set; }
    public bool   Shoot      { get; set; }
    public float  ChargePower { get; set; }
}

// Server → Client（全员就绪后）
public class BattleStart
{
    public string RoomId     { get; set; }
    public int    RandomSeed { get; set; }  // 客户端用此种子初始化 RNG，保证确定性
}
```

---

## 客户端确定性要求

帧同步要求所有客户端对相同输入序列产生完全相同的模拟结果：

| 要求 | 实现方式 |
|------|----------|
| 随机数一致 | `GameRandom` 以服务器下发的 `RandomSeed` 初始化，所有随机调用共享同一序列 |
| 浮点一致 | 使用定点数运算，或限定在相同平台（iOS / Android 同为 ARM） |
| 帧率解耦 | 逻辑以固定 `dt`（20 tick/s = 0.05s）推进，渲染插值独立 |
| 输入顺序 | `LockstepFrameMsg` 按 `Frame` 编号排序，客户端缓存后按序执行 |

---

## 断线处理

```
Player 断线
      │
      ▼
Session 标记 IsDisconnected = true
GameRoom.OnPlayerDisconnect() → 触发 GameOver(Disconnect)
通知房间内另一玩家
      │
      └── 房间销毁，GameOver 消息已发送
```

> 当前版本：断线直接结束房间。如需重连，可在此扩展：保留房间状态 30s，重连后客户端用 Frame 编号追帧。

---

## 目录结构

```
server/
├── Server.sln
├── src/
│   ├── Program.cs                    # 入口，Host / WS 中间件 / DI
│   ├── Hosting/
│   │   └── GameLoopService.cs        # BackgroundService 主循环（20 tick/s）
│   │
│   ├── Network/
│   │   ├── ConnectionManager.cs      # WebSocket 连接管理
│   │   ├── MessageRouter.cs          # 根据 MsgId 路由到处理器
│   │   └── WebSocketHandler.cs       # 收发字节工具（SendAsync / BroadcastAsync）
│   │
│   ├── Session/
│   │   ├── Session.cs                # 玩家会话
│   │   └── SessionManager.cs         # 会话管理
│   │
│   ├── Match/
│   │   └── MatchService.cs           # 匹配队列 → 创建房间 → 绑定连接
│   │
│   ├── Room/
│   │   ├── RoomManager.cs            # 房间生命周期 + 事件订阅
│   │   └── GameRoom.cs               # 帧同步房间（收集输入 → 广播帧）
│   │
│   └── Proto/
│       └── MsgIds.cs                 # 消息 ID 常量 + 消息类定义
│
├── config/
│   └── server_config.json
└── proto/                            # 协议参考定义（文档用，实际用 JSON）
```

---

## 配置

```json
// server_config.json
{
    "port": 8081,
    "tick_rate": 20,
    "max_rooms": 100,
    "heartbeat_timeout_sec": 30
}
```

---

## 性能预估

| 指标 | 预估值 |
|------|--------|
| 单房间内存 | < 1 KB（只存玩家列表和输入缓冲，无游戏状态） |
| 单 Tick CPU | < 0.01ms / 房间（仅序列化 + 发送） |
| 最大房间数 | 1000+（单核即可） |
| 网络带宽/房间 | ~1 KB/s（20 tick × ~50B/帧） |
| 启动内存 | ~30 MB（.NET 运行时） |
