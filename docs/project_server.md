# 对战服务器架构设计

## 同步模式：帧同步（Lockstep）

服务器**不运行任何游戏逻辑**，职责仅为：

1. 接受客户端连接与匹配
2. 每帧收集所有玩家的输入
3. 将输入打包成 `LockstepFrameMsg` 广播给房间内所有客户端

所有游戏模拟（怪物 AI、碰撞、伤害、波次推进）由客户端本地 ECS 确定性执行。

## 房间状态与关闭规则

### 房间状态

| 状态 | 说明 |
|------|------|
| `Waiting` | 房间等待另一名玩家加入，或等待玩家完成 `PlayerReady` |
| `InGame` | 匹配成功后进入游戏中阶段 |

### 房间关闭条件

1. 房间内**所有玩家**连续 2 分钟无心跳，自动关闭房间
2. 房间内所有玩家提交 `GameEndSubmit` 后，关闭房间并广播 `GameOver`
3. 玩家在 `Waiting` 状态发送 `MatchCancel`，关闭房间

> 约束：`MatchCancel` 仅在 `Waiting` 状态有效，`InGame` 状态收到后应拒绝。

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

按房间复用匹配：优先查找仅 1 人的 `Waiting` 房间；找不到则创建新房间。

```
匹配流程：

Player A ──MatchRequest──▶ 查找 Waiting 且人数=1 的房间
                           └── 无：创建 Room(A)，状态 Waiting

Player B ──MatchRequest──▶ 查找 Waiting 且人数=1 的房间
                           └── 有：加入 Room(A,B)

Room(A,B) ──广播 MatchSuccess(RoomId, Players[])──▶ A,B
Room(A,B) 状态切换：Waiting → InGame（准备阶段）
```

取消匹配规则：
- `MatchCancel` 仅在房间状态为 `Waiting` 时有效
- 有玩家在 `Waiting` 状态取消匹配时，房间立即关闭
- `InGame` 状态收到 `MatchCancel` 应拒绝（不触发关房）

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
│     └── 查找 Waiting 且人数=1 房间；无则新建│
│                                            │
│  2. RoomManager.Tick(dt)                   │
│     └── foreach room:                      │
│           room._frame++                    │
│           打包所有玩家输入                 │
│           OnBroadcastFrame → WS广播        │
│           _frameInputs.Clear()             │
│                                            │
│  3. 心跳超时检查（仅所有玩家超时才关房）   │
│  4. 结束提交检查（全员 GameEndSubmit 关房）│
└────────────────────────────────────────────┘
```

---

## 消息协议

所有消息通过 WebSocket Binary Frame 传输，格式为：

```
[4字节 uint MsgId][N字节 JSON payload]
```

### 消息 ID（协议统一口径）

```csharp
// 匹配
MatchRequest   = 1001   // C→S
MatchCancel    = 1002   // C→S（仅 Waiting 有效）
MatchSuccess   = 1004   // S→C

// 开局准备
PlayerReady    = 2001   // C→S
GameStart      = 2002   // S→C（全员 Ready 后广播，含随机种子）

// 游戏操作
PlayerMove     = 3001   // C→S 玩家移动输入
SkillChoice    = 3002   // C→S 玩家技能/升级选择
GameEndSubmit  = 3003   // C→S 玩家提交结束状态
LockstepFrame  = 3008   // S→C 服务器按 Tick 广播输入帧

// 结算
GameOver       = 3005   // S→C 房间结束广播

// 系统
Heartbeat      = 9001   // C↔S
```

### 关键消息结构

```csharp
// Client → Server：玩家移动操作
public class PlayerMoveMsg
{
    public int  Tick    { get; set; }   // 客户端本地帧号
    public Vec2 MoveDir { get; set; }   // 移动方向（归一化）
}

// Client → Server：玩家技能选择
public class SkillChoiceMsg
{
    public int    Tick      { get; set; }
    public string SkillId   { get; set; } = string.Empty;
}

// Client → Server：玩家提交结束状态
public class GameEndSubmitMsg
{
    public int    Tick      { get; set; }
    public string Reason    { get; set; } = string.Empty; // Win / Lose / Surrender
}

// Server → Client：全员就绪后
public class GameStartMsg
{
    public string RoomId     { get; set; } = string.Empty;
    public int    RandomSeed { get; set; } // 客户端据此初始化确定性随机
}

// Server → Client：房间结束
public class GameOverMsg
{
    public string RoomId   { get; set; } = string.Empty;
    public string Reason   { get; set; } = string.Empty;
}
```

### 协议消息总览

| 消息类型 | 方向 | 房间状态约束 | 说明 |
|----------|------|--------------|------|
| `MatchRequest` | Client → Server | 无 | 发起匹配 |
| `MatchCancel` | Client → Server | `Waiting` | 取消匹配并关闭房间 |
| `MatchSuccess` | Server → Client | `Waiting` → `InGame` | 匹配成功广播 |
| `PlayerReady` | Client → Server | `InGame` | 客户端加载完成并上报 |
| `GameStart` | Server → Client | `InGame` | 全员 Ready 后广播开局 |
| `PlayerMove` | Client → Server | `InGame` | 玩家移动输入 |
| `SkillChoice` | Client → Server | `InGame` | 玩家技能/升级选择 |
| `LockstepFrame` | Server → Client | `InGame` | 服务器按 Tick 广播输入帧 |
| `GameEndSubmit` | Client → Server | `InGame` | 玩家提交结束状态 |
| `GameOver` | Server → Client | `InGame` / 关闭前 | 房间结束广播 |
| `Heartbeat` | Client ↔ Server | 全阶段 | 心跳保活 |

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
连接从 ConnectionPool 移除
      │
      └── 房间是否关闭由“房间关闭条件”统一判定
```

> 当前版本：不再采用“单玩家断线立即 GameOver 并销毁房间”的规则。房间生命周期按心跳与结束提交条件处理。

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
│   │   └── MatchService.cs           # 复用 Waiting 房间 → 创建房间 → 绑定连接
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
