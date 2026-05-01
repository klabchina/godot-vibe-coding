# 多人模式心跳超时误判修复设计

## 背景与现象

线上/联调日志出现：

- `Server.Hosting.GameLoopService[0] Connection heartbeat timeout: <connectionId>`

用户感知为“客户端多人模式没有发心跳”。

## 现状复核

### 客户端

客户端 `NetManager` 已实现定时心跳发送：

- `client/Scripts/Net/NetManager.cs:12` 定义 `HeartbeatInterval = 5f`
- `client/Scripts/Net/NetManager.cs:155-162` 每隔 5 秒触发 `SendHeartbeat()`
- `client/Scripts/Net/NetManager.cs:63-67` 发送 `MsgIds.Heartbeat (9001)`

结论：客户端侧并非“完全未发送心跳”。

### 服务端

服务端超时判定当前依赖连接活跃时间：

- `server/src/Hosting/GameLoopService.cs:70-73` 使用 `_connections.GetTimedOutConnections(timeout)` 判定超时
- `server/src/Network/ConnectionManager.cs:12` 活跃时间来源 `_lastActivity`

但当前 `/ws` 主消息循环未刷新该活跃时间：

- `server/src/Program.cs:102-106` 仅 `await router.RouteAsync(...)`，没有 `connectionManager.UpdateActivity(connectionId)`

因此即使客户端持续发消息（含心跳），也可能因 `_lastActivity` 不更新在约 30 秒后被误判超时。

## 目标

1. 消除多人模式连接的心跳超时误判。
2. 保持当前协议与客户端发送逻辑不变（最小改动）。
3. 不引入与本问题无关的重构。

## 非目标

- 不调整心跳协议字段。
- 不改客户端心跳频率（仍 5 秒）。
- 不重写整套会话超时机制。

## 方案对比

### 方案 A（推荐）：修复服务端活跃时间刷新

在 `server/src/Program.cs` 的二进制消息分支中，路由前增加：

```csharp
connectionManager.UpdateActivity(connectionId);
```

优点：
- 改动最小，风险最低。
- 与现有 `ConnectionManager` 超时口径一致。
- 立即修复误判。

代价：
- 超时判定继续基于“收到任意二进制消息”的活跃度，而非专门 `Heartbeat` 字段。

### 方案 B：切换为 Session.LastHeartbeat 口径

将 `GameLoopService` 改为按 `Session.LastHeartbeat` 判超时。

优点：语义更直接（真正心跳口径）。
代价：改动面较大，需要改遍历与映射逻辑，超出本次最小修复目标。

### 方案 C：双保险（A + B）

同时保留连接活跃时间并加 Session 心跳口径。

优点：鲁棒性最好。
代价：复杂度提升，不符合当前最小修复诉求。

## 最终设计（已选）

采用方案 A：

- 文件：`server/src/Program.cs`
- 位置：`if (result.MessageType == WebSocketMessageType.Binary)` 分支
- 修改：在 `router.RouteAsync(...)` 前调用 `connectionManager.UpdateActivity(connectionId)`

伪代码：

```csharp
if (result.MessageType == WebSocketMessageType.Binary)
{
    connectionManager.UpdateActivity(connectionId);
    var payload = buffer.AsMemory(0, result.Count);
    await router.RouteAsync(connectionId, payload, CancellationToken.None);
}
```

## 验证标准

1. 客户端保持在线且有网络消息时，不再在约 30 秒后出现误判超时日志。
2. 断线场景仍可触发超时清理逻辑。
3. `server/src` 构建通过。

## 影响文件

- `server/src/Program.cs`（唯一必要改动）
