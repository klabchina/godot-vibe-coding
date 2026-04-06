# 客户端架构设计

## 项目概述

双人合作弓箭塔防游戏。玩家通过匹配系统组队（最多 2 人），进入战斗后使用弓箭协力击杀一波波来袭的怪物。

- **引擎**: Godot 4.x (GDScript / C#)
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
│  │              Scene Manager                   │   │
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

```
┌─────────────────────────────────────────────────────────────┐
│                       Components                            │
├─────────────────┬───────────────────────────────────────────┤
│ TransformC      │ position: Vector2, rotation: float        │
│ VelocityC       │ velocity: Vector2, speed: float           │
│ HealthC         │ hp: int, max_hp: int                      │
│ BowC            │ charge_time: float, damage: int,          │
│                 │ cooldown: float                            │
│ ArrowC          │ damage: int, owner_id: int, piercing: bool│
│ MonsterC        │ type: MonsterType, reward: int             │
│ ColliderC       │ shape: Shape, layer: int, mask: int        │
│ SpriteC         │ texture: Texture, animation: String        │
│ NetworkSyncC    │ net_id: int, owner: int, is_local: bool    │
│ WaveC           │ wave_num: int, spawn_list: Array,          │
│                 │ interval: float                            │
└─────────────────┴───────────────────────────────────────────┘
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
 │ 4. MovementSystem     │  根据 VelocityC 更新 TransformC
 ├──────────────────────┤
 │ 5. BowSystem          │  处理蓄力、发射箭矢逻辑
 ├──────────────────────┤
 │ 6. CollisionSystem    │  碰撞检测（箭矢 vs 怪物）
 ├──────────────────────┤
 │ 7. DamageSystem       │  计算伤害，更新 HealthC
 ├──────────────────────┤
 │ 8. DeathSystem        │  清理 hp<=0 的实体，触发掉落/特效
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
  │     { room_id, players[] }    │
  │                               │
  │──── PlayerReady ─────────────▶│
  │                               │
  │◀─── BattleStart ─────────────│  双方 Ready 后开始
  │     { wave_config, seed }     │
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
- 波次配置由服务器下发（`wave_config`）
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
project/
├── scenes/
│   ├── main_menu.tscn          # 主界面场景
│   ├── matching.tscn           # 匹配界面场景
│   ├── battle.tscn             # 战斗场景
│   └── result.tscn             # 结算界面场景
│
├── scripts/
│   ├── ecs/
│   │   ├── world.gd            # ECS World，管理所有 Entity/Component
│   │   ├── entity.gd           # Entity 基础定义
│   │   ├── components/
│   │   │   ├── transform_c.gd
│   │   │   ├── velocity_c.gd
│   │   │   ├── health_c.gd
│   │   │   ├── bow_c.gd
│   │   │   ├── arrow_c.gd
│   │   │   ├── monster_c.gd
│   │   │   ├── collider_c.gd
│   │   │   ├── sprite_c.gd
│   │   │   ├── network_sync_c.gd
│   │   │   └── wave_c.gd
│   │   └── systems/
│   │       ├── input_system.gd
│   │       ├── network_recv_system.gd
│   │       ├── wave_spawn_system.gd
│   │       ├── movement_system.gd
│   │       ├── bow_system.gd
│   │       ├── collision_system.gd
│   │       ├── damage_system.gd
│   │       ├── death_system.gd
│   │       ├── network_send_system.gd
│   │       └── render_system.gd
│   │
│   ├── net/
│   │   ├── net_manager.gd      # WebSocket 连接管理
│   │   ├── match_client.gd     # 匹配逻辑
│   │   ├── sync_client.gd      # 战斗同步
│   │   └── protocol.gd         # 消息序列化/反序列化
│   │
│   ├── ui/
│   │   ├── main_menu_ui.gd
│   │   ├── matching_ui.gd
│   │   ├── battle_hud.gd       # 战斗 HUD（血量、波次、击杀数）
│   │   └── result_ui.gd
│   │
│   ├── game/
│   │   ├── game_manager.gd     # 全局游戏状态管理（Autoload）
│   │   ├── scene_manager.gd    # 场景切换管理（Autoload）
│   │   └── wave_config.gd      # 波次配置数据
│   │
│   └── data/
│       ├── monster_data.gd     # 怪物属性配置
│       └── weapon_data.gd      # 弓箭属性配置
│
└── assets/
    ├── sprites/
    ├── audio/
    └── ui/
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
6. ECS World 初始化 → 加载 wave_config → 开始 Wave 1
7. 每帧: InputSystem → ... → RenderSystem 循环执行
8. 所有波次完成 / 玩家死亡 → 服务器发送 GameOver
9. 切换到 Result 场景 → 显示结算
10. 点击返回 → 回到 MainMenu
```
