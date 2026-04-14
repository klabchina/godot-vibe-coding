# ASA Project Context — godot-ai-match-game

## 项目概述

双人合作弓箭幸存者游戏（吸血鬼幸存者-like），Godot 4.x C# 客户端 + .NET 8 服务端，ECS 架构，WebSocket 同步。

- **客户端**：Godot 4.6.2 / C# / net8.0，位于 `client/`
- **服务端**：.NET 8+ / System.Net.WebSockets，位于 `server/`
- **单局**：8 波制，4-6 分钟，双人合作自动射击 + 走位闪避

---

## 目录结构

```
client/
├── Scenes/          # Godot 场景 (.tscn)
├── Scripts/
│   ├── Ecs/
│   │   ├── World.cs              # ECS World
│   │   ├── Components/          # 18 个 Component
│   │   └── Systems/             # 15 个 System
│   ├── Net/                      # WebSocket 网络层
│   ├── UI/                       # HUD/面板/飘字
│   └── Game/                     # GameManager, SceneManager, 配置数据
└── Assets/Sprites/              # 精灵图

server/
├── src/
│   ├── Network/                  # WebSocket 连接管理
│   ├── Session/                  # 会话管理
│   ├── Match/                    # 匹配服务
│   ├── Room/                     # 房间 & Game Loop
│   └── Game/                     # 游戏逻辑 (Server authoritative)
└── proto/                        # Protobuf 消息定义
```

---

## 常用命令

```bash
# 客户端（Godot Editor）
cd client && godot --headless --build-solutions

# 服务端
cd server/src && dotnet run

# Protobuf 编译
cd server && dotnet run --project ProtoGen
```

---

## 核心约定

- **ECS 数据驱动**：所有游戏对象 = Entity + Component，逻辑由 System 驱动
- **服务器权威**：战斗逻辑以服务端为准，客户端做本地预测
- **Protobuf 通信**：客户端/服务端共享 `proto/` 目录，通过 MsgId 路由
- **固定帧率同步**：服务端 20 tick/s (50ms/tick)，广播 GameStateSnapshot
- **怪物 AI**：Skeleton/Elite 为远程攻击（详见 `docs/superpowers/specs/2026-04-07-battle-gameplay-design.md`）

---

## 关键文件索引

| 用途 | 路径 |
|------|------|
| 客户端架构 | `docs/project_client.md` |
| 服务端架构 | `docs/project_server.md` |
| 战斗玩法设计 | `docs/superpowers/specs/2026-04-07-battle-gameplay-design.md` |
| 远程敌人行为 | `docs/superpowers/plans/2026-04-10-ranged-enemy-behavior.md` |
| 怪物死亡动画延迟销毁设计 | `docs/superpowers/specs/2026-04-14-monster-death-animation-design.md` |
| 怪物 AI 源码 | `client/Scripts/Ecs/Systems/MonsterAISystem.cs` |
| 升级配置 | `client/Scripts/Data/UpgradeData.cs` |
| 消息 ID | `server/src/Proto/MsgIds.cs` |
