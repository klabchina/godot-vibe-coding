# Server Minimal API + 原生 WS 骨架 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 `server/src` 搭建 ASP.NET Core Minimal API + 原生 WebSocket + 20 tick/s 后台主循环的最小可运行服务端骨架。

**Architecture:** 使用 Minimal API 作为宿主，`Program.cs` 仅负责 DI、中间件与路由装配；`GameLoopService` 负责固定 Tick 驱动；`Network/Session/Match/Room` 保持职责分层但先做最小实现，保证可编译、可启动、可握手、可优雅停机。

**Tech Stack:** .NET 8, ASP.NET Core Minimal API, System.Net.WebSockets, BackgroundService, ConcurrentDictionary/ConcurrentQueue

**Spec:** `docs/project_server.md`

---

### Task 1: 创建 server/src 项目骨架与解决方案

**Files:**
- Create: `server/Server.sln`
- Create: `server/src/Server.csproj`
- Create: `server/src/appsettings.json`
- Create: `server/src/appsettings.Development.json`

- [ ] **Step 1: 创建目录结构**

Run:
```bash
mkdir -p server/src/{Hosting,Network,Session,Match,Room,Proto}
```

Expected: `server/src` 下出现模块目录。

- [ ] **Step 2: 创建 Server.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>Server</RootNamespace>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: 创建配置文件**

`server/src/appsettings.json`:
```json
{
  "Server": {
    "Port": 8080,
    "TickRate": 20,
    "HeartbeatIntervalSec": 10,
    "HeartbeatTimeoutSec": 30
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

`server/src/appsettings.Development.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

- [ ] **Step 4: 创建解决方案并加入项目**

Run:
```bash
cd server
dotnet new sln -n Server
dotnet sln Server.sln add src/Server.csproj
```

Expected: `Server.sln` 包含 `src/Server.csproj`。

- [ ] **Step 5: 提交**

Run:
```bash
git add server/Server.sln server/src/Server.csproj server/src/appsettings.json server/src/appsettings.Development.json
git commit -m "chore: scaffold server minimal api project structure"
```

---

### Task 2: 实现网络最小层（ConnectionManager / WebSocketHandler / MessageRouter）

**Files:**
- Create: `server/src/Network/ConnectionManager.cs`
- Create: `server/src/Network/WebSocketHandler.cs`
- Create: `server/src/Network/MessageRouter.cs`

- [ ] **Step 1: 实现 ConnectionManager（连接登记与移除）**

```csharp
using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace Server.Network;

public sealed class ConnectionManager
{
    private readonly ConcurrentDictionary<string, WebSocket> _connections = new();

    public string Add(WebSocket socket)
    {
        var id = Guid.NewGuid().ToString("N");
        _connections[id] = socket;
        return id;
    }

    public bool TryGet(string connectionId, out WebSocket? socket)
        => _connections.TryGetValue(connectionId, out socket);

    public bool Remove(string connectionId)
        => _connections.TryRemove(connectionId, out _);

    public int Count => _connections.Count;
}
```

- [ ] **Step 2: 实现 MessageRouter（占位路由）**

```csharp
namespace Server.Network;

public sealed class MessageRouter
{
    private readonly ILogger<MessageRouter> _logger;

    public MessageRouter(ILogger<MessageRouter> logger)
    {
        _logger = logger;
    }

    public Task RouteAsync(string connectionId, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        _logger.LogDebug("Route message: conn={ConnectionId}, bytes={Length}", connectionId, payload.Length);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 3: 实现 WebSocketHandler（读循环 + 回声确认）**

```csharp
using System.Net.WebSockets;
using System.Text;

namespace Server.Network;

public sealed class WebSocketHandler
{
    private readonly ILogger<WebSocketHandler> _logger;
    private readonly MessageRouter _router;
    private readonly ConnectionManager _connections;

    public WebSocketHandler(
        ILogger<WebSocketHandler> logger,
        MessageRouter router,
        ConnectionManager connections)
    {
        _logger = logger;
        _router = router;
        _connections = connections;
    }

    public async Task RunAsync(string connectionId, WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[8 * 1024];

        try
        {
            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", ct);
                    break;
                }

                var bytes = buffer.AsMemory(0, result.Count);
                await _router.RouteAsync(connectionId, bytes, ct);

                var ack = Encoding.UTF8.GetBytes("ack");
                await socket.SendAsync(ack, WebSocketMessageType.Text, true, ct);
            }
        }
        finally
        {
            _connections.Remove(connectionId);
            _logger.LogInformation("Connection closed: {ConnectionId}", connectionId);
        }
    }
}
```

- [ ] **Step 4: 编译验证**

Run:
```bash
dotnet build server/src/Server.csproj
```

Expected: `Build succeeded`。

- [ ] **Step 5: 提交**

Run:
```bash
git add server/src/Network/ConnectionManager.cs server/src/Network/WebSocketHandler.cs server/src/Network/MessageRouter.cs
git commit -m "feat: add minimal websocket network layer"
```

---

### Task 3: 实现最小业务层（Session/Match/Room + Tick 驱动）

**Files:**
- Create: `server/src/Session/Session.cs`
- Create: `server/src/Session/SessionManager.cs`
- Create: `server/src/Match/MatchService.cs`
- Create: `server/src/Room/GameRoom.cs`
- Create: `server/src/Room/RoomManager.cs`
- Create: `server/src/Hosting/GameLoopService.cs`

- [ ] **Step 1: 实现 Session 模型与管理器**

```csharp
namespace Server.Session;

public enum SessionState
{
    Idle,
    Matching,
    InRoom,
    InBattle
}

public sealed class Session
{
    public required string ConnectionId { get; init; }
    public required string PlayerId { get; init; }
    public string PlayerName { get; set; } = "";
    public SessionState State { get; set; } = SessionState.Idle;
    public string? RoomId { get; set; }
}
```

```csharp
using System.Collections.Concurrent;

namespace Server.Session;

public sealed class SessionManager
{
    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    public Session Create(string connectionId, string playerId)
    {
        var session = new Session { ConnectionId = connectionId, PlayerId = playerId };
        _sessions[playerId] = session;
        return session;
    }

    public bool TryGet(string playerId, out Session? session)
        => _sessions.TryGetValue(playerId, out session);
}
```

- [ ] **Step 2: 实现 MatchService 最小队列**

```csharp
using System.Collections.Concurrent;
using Server.Room;

namespace Server.Match;

public sealed class MatchService
{
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly RoomManager _roomManager;

    public MatchService(RoomManager roomManager)
    {
        _roomManager = roomManager;
    }

    public void Enqueue(string playerId) => _queue.Enqueue(playerId);

    public void Tick()
    {
        while (_queue.Count >= 2)
        {
            _queue.TryDequeue(out var p1);
            _queue.TryDequeue(out var p2);

            if (!string.IsNullOrWhiteSpace(p1) && !string.IsNullOrWhiteSpace(p2))
            {
                _roomManager.CreateRoom(p1!, p2!);
            }
        }
    }
}
```

- [ ] **Step 3: 实现 RoomManager / GameRoom 最小实现**

```csharp
namespace Server.Room;

public sealed class GameRoom
{
    public string RoomId { get; }
    public IReadOnlyList<string> Players { get; }

    public GameRoom(string roomId, params string[] players)
    {
        RoomId = roomId;
        Players = players;
    }

    public void Tick(float dt)
    {
        _ = dt;
    }
}
```

```csharp
using System.Collections.Concurrent;

namespace Server.Room;

public sealed class RoomManager
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();

    public GameRoom CreateRoom(string playerA, string playerB)
    {
        var room = new GameRoom(Guid.NewGuid().ToString("N"), playerA, playerB);
        _rooms[room.RoomId] = room;
        return room;
    }

    public void Tick(float dt)
    {
        foreach (var room in _rooms.Values)
        {
            room.Tick(dt);
        }
    }
}
```

- [ ] **Step 4: 实现 GameLoopService**

```csharp
using Server.Match;
using Server.Room;

namespace Server.Hosting;

public sealed class GameLoopService : BackgroundService
{
    private readonly MatchService _match;
    private readonly RoomManager _rooms;
    private readonly ILogger<GameLoopService> _logger;

    public GameLoopService(MatchService match, RoomManager rooms, ILogger<GameLoopService> logger)
    {
        _match = match;
        _rooms = rooms;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        _logger.LogInformation("GameLoop started at 20 tick/s");

        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            const float dt = 0.05f;
            _match.Tick();
            _rooms.Tick(dt);
        }
    }
}
```

- [ ] **Step 5: 编译验证**

Run:
```bash
dotnet build server/src/Server.csproj
```

Expected: `Build succeeded`。

- [ ] **Step 6: 提交**

Run:
```bash
git add server/src/Session/Session.cs server/src/Session/SessionManager.cs server/src/Match/MatchService.cs server/src/Room/GameRoom.cs server/src/Room/RoomManager.cs server/src/Hosting/GameLoopService.cs
git commit -m "feat: add session match room and game loop skeleton"
```

---

### Task 4: 装配 Program.cs（DI + WS + healthz）

**Files:**
- Create: `server/src/Program.cs`

- [ ] **Step 1: 创建 Program.cs 并完成装配**

```csharp
using Microsoft.AspNetCore.Builder;
using Server.Hosting;
using Server.Match;
using Server.Network;
using Server.Room;
using Server.Session;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ConnectionManager>();
builder.Services.AddSingleton<MessageRouter>();
builder.Services.AddSingleton<WebSocketHandler>();

builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<RoomManager>();
builder.Services.AddSingleton<MatchService>();

builder.Services.AddHostedService<GameLoopService>();

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(10)
});

app.MapGet("/healthz", () => Results.Ok(new { ok = true }));

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket request required");
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

- [ ] **Step 2: 编译验证**

Run:
```bash
dotnet build server/src/Server.csproj
```

Expected: `Build succeeded`。

- [ ] **Step 3: 启动验证（最小）**

Run:
```bash
dotnet run --project server/src/Server.csproj
```

Expected:
- 控制台出现 `GameLoop started at 20 tick/s`
- 出现 ASP.NET Core listening 日志（默认 `http://localhost:5000` 或配置端口）

- [ ] **Step 4: 健康检查验证**

新终端运行：
```bash
curl -s http://localhost:5000/healthz
```

Expected: 返回 `{"ok":true}`。

- [ ] **Step 5: 提交**

Run:
```bash
git add server/src/Program.cs
git commit -m "feat: wire minimal api host with websocket endpoint"
```

---

### Task 5: 对齐文档与运行说明

**Files:**
- Modify: `docs/project_server.md`

- [ ] **Step 1: 同步目录结构与启动命令**

将运行示例改为：
```bash
cd server
dotnet run --project src/Server.csproj
```

并确保目录树包含：
- `src/Program.cs`
- `src/Hosting/GameLoopService.cs`
- `src/Network/*`
- `src/Session/*`
- `src/Match/*`
- `src/Room/*`

- [ ] **Step 2: 验证文档与代码一致**

Run:
```bash
grep -n "dotnet run" docs/project_server.md
```

Expected: 出现 `dotnet run --project src/Server.csproj`。

- [ ] **Step 3: 提交**

Run:
```bash
git add docs/project_server.md
git commit -m "docs: align server architecture doc with minimal api skeleton"
```

---

### Task 6: 最终验证（完成门槛）

**Files:**
- Modify: 无

- [ ] **Step 1: 完整编译**

Run:
```bash
dotnet build server/src/Server.csproj
```

Expected: 0 errors。

- [ ] **Step 2: 启动与健康检查复验**

Run:
```bash
dotnet run --project server/src/Server.csproj
```

另开终端：
```bash
curl -s http://localhost:5000/healthz
```

Expected: 服务正常响应，主循环日志持续输出。

- [ ] **Step 3: 记录当前状态（不提交额外代码）**

Run:
```bash
git status --short
```

Expected: 仅显示本次任务相关变更或工作区干净。
