# Server 游戏功能实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 实现完整的对战服务器，包括 Protobuf 协议、游戏逻辑（碰撞/伤害/波次）、断线重连。

**Spec:** `docs/project_server.md`

---

## Task 1: 创建 Proto 协议定义与 MsgIds

**Files:**
- Create: `server/proto/common.proto`
- Create: `server/proto/match.proto`
- Create: `server/proto/room.proto`
- Create: `server/proto/battle.proto`
- Create: `server/proto/event.proto`
- Create: `server/src/Proto/MsgIds.cs`

- [ ] **Step 1: 创建 proto 文件目录**
```bash
mkdir -p server/proto
```

- [ ] **Step 2: 创建 common.proto（Envelope, Vec2）**

- [ ] **Step 3: 创建 match.proto（匹配相关消息）**

- [ ] **Step 4: 创建 room.proto（房间/准备消息）**

- [ ] **Step 5: 创建 battle.proto（战斗同步消息）**

- [ ] **Step 6: 创建 event.proto（事件通知消息）**

- [ ] **Step 7: 创建 MsgIds.cs（消息 ID 常量）**

- [ ] **Step 8: 编译验证**

- [ ] **Step 9: 提交**

---

## Task 2: 实现 Game 模块（游戏状态核心）

**Files:**
- Create: `server/src/Game/PlayerState.cs`
- Create: `server/src/Game/MonsterState.cs`
- Create: `server/src/Game/ArrowState.cs`
- Create: `server/src/Game/MonsterType.cs`
- Create: `server/src/Game/CollisionHelper.cs`
- Create: `server/src/Game/GameConfig.cs`
- Create: `server/src/Game/GameStateSnapshot.cs`

- [ ] **Step 1: 创建 Game 目录**

- [ ] **Step 2: 实现 PlayerState**

- [ ] **Step 3: 实现 MonsterState / MonsterType**

- [ ] **Step 4: 实现 ArrowState**

- [ ] **Step 5: 实现 CollisionHelper**

- [ ] **Step 6: 实现 GameConfig**

- [ ] **Step 7: 实现 GameStateSnapshot**

- [ ] **Step 8: 编译验证**

- [ ] **Step 9: 提交**

---

## Task 3: 实现 WaveController 波次控制

**Files:**
- Create: `server/src/Room/WaveController.cs`
- Create: `server/config/wave_config.json`

- [ ] **Step 1: 创建 config 目录**

- [ ] **Step 2: 创建 wave_config.json**

- [ ] **Step 3: 实现 WaveController.cs**

- [ ] **Step 4: 编译验证**

- [ ] **Step 5: 提交**

---

## Task 4: 完善 GameRoom 游戏循环

**Files:**
- Modify: `server/src/Room/GameRoom.cs`

- [ ] **Step 1: 实现 GameRoom 完整 Tick（处理输入、更新怪物、更新箭矢、碰撞检测、伤害结算、清理死亡、检查波次、检查游戏结束、广播状态）**

- [ ] **Step 2: 实现 ProcessInputQueue**

- [ ] **Step 3: 实现 UpdateArrows**

- [ ] **Step 4: 实现 UpdateMonsters（含简单 AI）**

- [ ] **Step 5: 实现 CheckCollisions**

- [ ] **Step 6: 实现 CleanupDead**

- [ ] **Step 7: 实现 CheckWaveProgress**

- [ ] **Step 8: 实现 CheckGameOver**

- [ ] **Step 9: 实现 BroadcastState**

- [ ] **Step 10: 编译验证**

- [ ] **Step 11: 提交**

---

## Task 5: 实现 MessageRouter 完整路由

**Files:**
- Modify: `server/src/Network/MessageRouter.cs`

- [ ] **Step 1: 实现消息反序列化（读取 Envelope + payload）**

- [ ] **Step 2: 实现 MatchRequest / MatchCancel 处理器**

- [ ] **Step 3: 实现 PlayerInput 处理器**

- [ ] **Step 4: 实现 Heartbeat 处理器**

- [ ] **Step 5: 编译验证**

- [ ] **Step 6: 提交**

---

## Task 6: 完善 WebSocketHandler 消息发送

**Files:**
- Modify: `server/src/Network/WebSocketHandler.cs`

- [ ] **Step 1: 添加 SendMessageAsync 方法（发送 Protobuf）**

- [ ] **Step 2: 修复当前发送 ack 的逻辑**

- [ ] **Step 3: 添加广播方法供 GameRoom 调用**

- [ ] **Step 4: 编译验证**

- [ ] **Step 5: 提交**

---

## Task 7: 实现断线重连

**Files:**
- Modify: `server/src/Session/Session.cs`
- Modify: `server/src/Network/ConnectionManager.cs`
- Modify: `server/src/Network/WebSocketHandler.cs`
- Modify: `server/src/Room/GameRoom.cs`

- [ ] **Step 1: 添加 Session 断线/重连字段（IsDisconnected, DisconnectTime, ReconnectTimeoutSec）**

- [ ] **Step 2: 实现 ConnectionManager 心跳超时检测**

- [ ] **Step 3: 实现 GameRoom 断线处理（30s 重连倒计时）**

- [ ] **Step 4: 实现重连后恢复（发送 GameStateSnapshot）**

- [ ] **Step 5: 编译验证**

- [ ] **Step 6: 提交**

---

## Task 8: 创建配置文件

**Files:**
- Create: `server/config/server_config.json`
- Create: `server/config/monster_config.json`

- [ ] **Step 1: 创建 server_config.json**

- [ ] **Step 2: 创建 monster_config.json**

- [ ] **Step 3: 提交**

---

## Task 9: 最终验证

- [ ] **Step 1: 完整编译**
```bash
dotnet build server/src/Server.csproj
```

- [ ] **Step 2: 运行测试（模拟客户端连接）**

- [ ] **Step 3: 提交**
