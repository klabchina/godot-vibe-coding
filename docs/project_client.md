# 客户端架构设计

## 项目概述

双人合作弓箭塔防游戏。玩家通过匹配系统组队（最多 2 人），进入战斗后使用弓箭协力击杀一波波来袭的怪物。

- **引擎**: Godot 4.x (C#)
- **战斗逻辑**: ECS (Entity-Component-System)
- **网络**: 多人在线对战，最多 2 人

---

## 整体架构

```
┌─────────────────────────────────────────────────────┐
│                     Godot Client                    │
│                                                     │
│  ┌───────────┐   ┌───────────┐   ┌──────────────┐  │
│  │  UI Layer  │   │ Game Loop │   │  Net Layer   │  │
│  │           │   │  (ECS)    │   │  (WebSocket) │  │
│  └─────┬─────┘   └─────┬─────┘   └──────┬───────┘  │
│        │               │                │           │
│        ▼               ▼                ▼           │
│  ┌──────────────────────────────────────────────┐   │
│  │              SceneManager                    │   │
│  │  MainMenu → Matching → Battle → Result       │   │
│  └──────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────┘
                         │
                         ▼
                ┌─────────────────┐
                │   Game Server   │
                └─────────────────┘
```

---

## 场景流程

```
┌────────────┐     ┌────────────┐     ┌────────────┐     ┌────────────┐
│  主界面    │────▶│  匹配中    │────▶│   战斗     │────▶│  结算      │
│ MainMenu   │     │ Matching   │     │  Battle    │     │  Result    │
└────────────┘     └─────┬──────┘     └────────────┘     └──────┬─────┘
                         │                                      │
                         │ 超时/取消                             │
                         ▼                                      ▼
                   返回主界面                              返回主界面
```

| 场景 | 说明 |
|------|------|
| **MainMenu** | 主界面，包含「开始匹配」按钮，显示玩家信息 |
| **Matching** | 匹配等待界面，显示匹配状态、倒计时，支持取消 |
| **Battle** | 核心战斗场景，ECS 驱动，双人协作弓箭射击 |
| **Result** | 结算界面，显示波次数、击杀数、评分，可返回主界面 |

---

## ECS 战斗架构

### 核心设计原则

将游戏对象拆分为 Entity + Component，逻辑由 System 驱动，保持数据与行为分离。

### Entity 类型

| Entity | 说明 |
|--------|------|
| **Player** | 玩家角色，持弓射箭 |
| **Arrow** | 弓箭投射物 |
| **Monster** | 每波刷新的敌人 |
| **WaveSpawner** | 波次控制器 |

### Component 定义

```csharp
// TransformComponent.cs
public class TransformComponent
{
    public Vector2 Position;
    public float Rotation;
}

// VelocityComponent.cs
public class VelocityComponent
{
    public Vector2 Velocity;
    public float Speed;
}

// HealthComponent.cs
public class HealthComponent
{
    public int Hp;
    public int MaxHp;
}

// BowComponent.cs
public class BowComponent
{
    public float ChargeTime;
    public int Damage;
    public float Cooldown;
}

// ArrowComponent.cs
public class ArrowComponent
{
    public int Damage;
    public int OwnerId;
    public bool Piercing;
}

// MonsterComponent.cs
public class MonsterComponent
{
    public MonsterType Type;
    public int Reward;
}

// ColliderComponent.cs
public class ColliderComponent
{
    public Shape2D Shape;
    public int Layer;
    public int Mask;
}

// SpriteComponent.cs
public class SpriteComponent
{
    public Texture2D Texture;
    public string Animation;
}

// NetworkSyncComponent.cs
public class NetworkSyncComponent
{
    public int NetId;
    public int Owner;
    public bool IsLocal;
}

// WaveComponent.cs
public class WaveComponent
{
    public int WaveNum;
    public Array<SpawnEntry> SpawnList;
    public float Interval;
}
```

### System 列表

```
执行顺序 (每帧):
 ┌──────────────────────┐
 │ 1. InputSystem        │  处理本地玩家输入（瞄准、蓄力、释放）
 ├──────────────────────┤
 │ 2. NetworkRecvSystem  │  接收远端玩家操作和服务器状态
 ├──────────────────────┤
 │ 3. WaveSpawnSystem    │  判定波次推进，生成怪物
 ├──────────────────────┤
 │ 4. MovementSystem     │  根据 VelocityComponent 更新 TransformComponent
 ├──────────────────────┤
 │ 5. BowSystem          │  处理蓄力、发射箭矢逻辑
 ├──────────────────────┤
 │ 6. CollisionSystem    │  碰撞检测（箭矢 vs 怪物）
 ├──────────────────────┤
 │ 7. DamageSystem       │  计算伤害，更新 HealthComponent
 ├──────────────────────┤
 │ 8. DeathSystem        │  清理 Hp<=0 的实体，触发掉落/特效
 ├──────────────────────┤
 │ 9. NetworkSendSystem  │  将本地状态变更同步给对方
 ├──────────────────────┤
 │ 10. RenderSystem      │  同步 ECS 数据到 Godot 节点进行渲染
 └──────────────────────┘
```

---

## 网络架构

### 连接方式

```
Player A (Host)  ◄───WebSocket───►  Game Server  ◄───WebSocket───►  Player B (Guest)
```

- 使用 WebSocket 长连接与游戏服务器通信
- 服务器负责匹配、状态仲裁、帧同步
- 客户端做本地预测 + 服务器校验

### 匹配流程

```
Client                          Server
  │                               │
  │──── MatchRequest ────────────▶│
  │                               │  加入匹配队列
  │                               │  等待另一位玩家
  │◀─── MatchUpdate(waiting) ─────│
  │                               │
  │                               │  匹配成功
  │◀─── MatchSuccess ────────────│
  │     { RoomId, Players[] }     │
  │                               │
  │──── PlayerReady ─────────────▶│
  │                               │
  │◀─── BattleStart ─────────────│  双方 Ready 后开始
  │     { WaveConfig, Seed }      │
  │                               │
```

### 战斗同步协议

| 消息类型 | 方向 | 说明 |
|----------|------|------|
| `PlayerInput` | Client → Server | 玩家操作（移动、瞄准、射击） |
| `GameState` | Server → Client | 权威游戏状态快照 |
| `SpawnArrow` | Server → Client | 箭矢生成事件 |
| `SpawnWave` | Server → Client | 新一波怪物刷新 |
| `EntityDeath` | Server → Client | 实体死亡通知 |
| `GameOver` | Server → Client | 游戏结束（胜利/失败） |

---

## 波次系统

### 设计

```
Wave 1          Wave 2          Wave 3         ...    Boss Wave
──────────      ──────────      ──────────            ──────────
5 x Slime       8 x Slime      3 x Slime             1 x Boss
                2 x Skeleton    5 x Skeleton          5 x Skeleton
                                2 x Orc
```

- 每波之间有间歇时间，供玩家调整
- 波次配置由服务器下发（`WaveConfig`）
- 当前波所有怪物死亡后，倒计时进入下一波
- 怪物从场景边缘生成，朝玩家方向移动

### 怪物类型

| 类型 | 血量 | 速度 | 特性 |
|------|------|------|------|
| Slime | 低 | 慢 | 基础小怪 |
| Skeleton | 中 | 中 | 会闪避 |
| Orc | 高 | 快 | 冲锋攻击 |
| Boss | 极高 | 慢 | 特殊技能，每 N 波出现 |

---

## 目录结构

```
client/
├── Scenes/
│   ├── MainMenu.tscn              # 主界面场景
│   ├── Matching.tscn              # 匹配界面场景
│   ├── Battle.tscn                # 战斗场景
│   └── Result.tscn                # 结算界面场景
│
├── Scripts/
│   ├── Ecs/
│   │   ├── World.cs               # ECS World，管理所有 Entity/Component
│   │   ├── Entity.cs              # Entity 基础定义
│   │   ├── Components/
│   │   │   ├── TransformComponent.cs
│   │   │   ├── VelocityComponent.cs
│   │   │   ├── HealthComponent.cs
│   │   │   ├── BowComponent.cs
│   │   │   ├── ArrowComponent.cs
│   │   │   ├── MonsterComponent.cs
│   │   │   ├── ColliderComponent.cs
│   │   │   ├── SpriteComponent.cs
│   │   │   ├── NetworkSyncComponent.cs
│   │   │   └── WaveComponent.cs
│   │   └── Systems/
│   │       ├── InputSystem.cs
│   │       ├── NetworkRecvSystem.cs
│   │       ├── WaveSpawnSystem.cs
│   │       ├── MovementSystem.cs
│   │       ├── BowSystem.cs
│   │       ├── CollisionSystem.cs
│   │       ├── DamageSystem.cs
│   │       ├── DeathSystem.cs
│   │       ├── NetworkSendSystem.cs
│   │       └── RenderSystem.cs
│   │
│   ├── Net/
│   │   ├── NetManager.cs          # WebSocket 连接管理
│   │   ├── MatchClient.cs         # 匹配逻辑
│   │   ├── SyncClient.cs          # 战斗同步
│   │   └── Protocol.cs            # 消息序列化/反序列化
│   │
│   ├── UI/
│   │   ├── MainMenuUI.cs
│   │   ├── MatchingUI.cs
│   │   ├── BattleHud.cs           # 战斗 HUD（血量、波次、击杀数）
│   │   └── ResultUI.cs
│   │
│   ├── Game/
│   │   ├── GameManager.cs         # 全局游戏状态管理（Autoload）
│   │   ├── SceneManager.cs        # 场景切换管理（Autoload）
│   │   └── WaveConfig.cs          # 波次配置数据
│   │
│   └── Data/
│       ├── MonsterData.cs         # 怪物属性配置
│       └── WeaponData.cs          # 弓箭属性配置
│
└── Assets/
    ├── Sprites/
    ├── Audio/
    └── UI/
```

---

## Autoload（全局单例）

| 名称 | 职责 |
|------|------|
| **GameManager** | 管理全局游戏状态（当前场景阶段、玩家数据） |
| **SceneManager** | 场景切换、过渡动画 |
| **NetManager** | WebSocket 连接生命周期、心跳、重连 |

---

## 关键流程时序

### 完整游戏流程

```
1. 启动 → MainMenu
2. 点击「开始匹配」→ NetManager 连接服务器 → 发送 MatchRequest
3. 进入 Matching 界面 → 等待服务器 MatchSuccess
4. 匹配成功 → 双方发送 PlayerReady
5. 收到 BattleStart → SceneManager 切换到 Battle 场景
6. ECS World 初始化 → 加载 WaveConfig → 开始 Wave 1
7. 每帧: InputSystem → ... → RenderSystem 循环执行
8. 所有波次完成 / 玩家死亡 → 服务器发送 GameOver
9. 切换到 Result 场景 → 显示结算
10. 点击返回 → 回到 MainMenu
```
