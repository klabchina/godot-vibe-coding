# 客户端架构设计

## 项目概述

双人合作弓箭幸存者游戏。玩家通过匹配系统组队（最多 2 人），进入战斗后弓箭自动射击最近敌人，玩家专注走位闪避，每波结束后 3 选 1 局内升级，越打越爽。

- **引擎**: Godot 4.x (C#)
- **战斗逻辑**: ECS (Entity-Component-System)
- **网络**: 多人在线对战，最多 2 人
- **单局时长**: 4-6 分钟（8 波制）
- **核心体验**: 轻松休闲、爆爽清屏

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
| **Battle** | 核心战斗场景，ECS 驱动，自动射击 + 走位闪避 + 局内升级 |
| **Result** | 结算界面，显示波次数、击杀数、伤害、S/A/B/C 评分，可返回主界面 |

---

## ECS 战斗架构

### 核心设计原则

将游戏对象拆分为 Entity + Component，逻辑由 System 驱动，保持数据与行为分离。

### Entity 类型

| Entity | 说明 |
|--------|------|
| **Player** | 玩家角色，自动射箭，专注走位 |
| **Arrow** | 弓箭投射物（含普通箭、旋转护卫箭） |
| **Monster** | 每波刷新的敌人（Slime/Skeleton/Orc/Elite/Boss） |
| **Pickup** | 可拾取物（经验球、临时道具） |
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

// BowComponent.cs — 自动射击弓
public class BowComponent
{
    public int Damage;
    public float Cooldown;         // 射击间隔（初始 0.8s）
    public float CooldownTimer;    // 当前冷却计时
    public int ArrowCount;         // 同时发射箭矢数（初始 1，可升级）
    public float SpreadAngle;      // 多箭扇形展开角度
}

// ArrowComponent.cs
public class ArrowComponent
{
    public int Damage;
    public int OwnerId;
    public int PierceCount;        // 剩余穿透次数（0 = 命中即消失）
    public bool Bouncing;          // 是否弹射
    public bool Explosive;         // 是否爆炸
    public bool Freezing;          // 是否冰冻
    public bool Burning;           // 是否灼烧
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

// AutoAimComponent.cs — 自动瞄准
public class AutoAimComponent
{
    public int TargetId;           // 当前锁定目标 Entity ID（-1 = 无目标）
    public float SearchRadius;     // 搜索半径（默认无限，搜屏幕内）
}

// UpgradeComponent.cs — 玩家局内升级状态
public class UpgradeComponent
{
    public int MultiShotLevel;     // 多重射击等级（0-7，影响 ArrowCount）
    public int AttackSpeedLevel;   // 射速提升等级（0-5）
    public int DamageLevel;        // 伤害提升等级（0-5）
    public int PierceLevel;        // 穿透等级（0-3）
    public bool HasBounce;         // 弹射箭
    public bool HasExplosion;      // 爆炸箭
    public int MaxHpLevel;         // 生命提升等级
    public int MoveSpeedLevel;     // 移速提升等级（0-3）
    public bool HasShield;         // 护盾
    public bool HasRegen;          // 生命恢复
    public int MagnetLevel;        // 磁铁等级
    public bool HasFreeze;         // 冰冻箭
    public bool HasBurn;           // 火焰箭
    public int OrbitCount;         // 旋转护卫数量
}

// PickupComponent.cs — 可拾取物
public class PickupComponent
{
    public PickupType Type;        // ExpOrb, HealthPotion, Frenzy, Invincible, Bomb
    public int Value;              // 经验值 / 恢复量 / 伤害量
    public float LifeTime;         // 存活时间（超时消失）
}

// EffectComponent.cs — 箭矢附加效果（运行时）
public class EffectComponent
{
    public float AoeRadius;        // 爆炸范围（0 = 无 AOE）
    public int AoeDamage;          // AOE 伤害
    public float SlowPercent;      // 减速比例（0-1）
    public float SlowDuration;     // 减速持续时间
    public int DotDamage;          // DoT 每秒伤害
    public float DotDuration;      // DoT 持续时间
    public float BounceRadius;     // 弹射搜索半径
}

// BuffComponent.cs — 玩家临时增益
public class BuffComponent
{
    public List<Buff> ActiveBuffs;
}

public class Buff
{
    public BuffType Type;          // Frenzy, Invincible, Shield
    public float RemainingTime;
}

// OrbitComponent.cs — 旋转护卫箭
public class OrbitComponent
{
    public int Count;              // 护卫箭数量
    public float RotationSpeed;    // 旋转速度（弧度/秒）
    public float Radius;           // 旋转半径
    public int Damage;             // 每次碰撞伤害
    public float CurrentAngle;     // 当前旋转角度
}

// BossPhaseComponent.cs — Boss 阶段管理
public class BossPhaseComponent
{
    public int Phase;              // 当前阶段（1=追击, 2=召唤, 3=狂暴）
    public float PhaseTimer;       // 阶段内计时器
    public float SummonCooldown;   // 召唤冷却
}
```

### System 列表

```
执行顺序 (每帧):
 ┌──────────────────────────┐
 │ 1.  InputSystem           │  处理本地玩家移动输入（仅移动方向）
 ├──────────────────────────┤
 │ 2.  NetworkRecvSystem     │  接收远端玩家操作和服务器状态
 ├──────────────────────────┤
 │ 3.  WaveSpawnSystem       │  判定波次推进，生成怪物
 ├──────────────────────────┤
 │ 4.  BossAISystem          │  Boss 阶段切换、召唤小怪逻辑
 ├──────────────────────────┤
 │ 5.  AutoAimSystem         │  自动搜索最近目标，朝目标发射箭矢
 ├──────────────────────────┤
 │ 6.  MovementSystem        │  根据 VelocityComponent 更新 TransformComponent
 ├──────────────────────────┤
 │ 7.  OrbitSystem           │  更新旋转护卫箭位置
 ├──────────────────────────┤
 │ 8.  CollisionSystem       │  碰撞检测（箭矢 vs 怪物 + 玩家 vs 拾取物）含穿透判定
 ├──────────────────────────┤
 │ 9.  PickupSystem          │  处理经验球/道具拾取效果
 ├──────────────────────────┤
 │ 10. DamageSystem          │  计算伤害（含 AOE、DoT），更新 HealthComponent
 ├──────────────────────────┤
 │ 11. EffectSystem          │  处理箭矢附加效果（AOE 爆炸、弹射、冰冻减速、灼烧 DoT）
 ├──────────────────────────┤
 │ 12. BuffSystem            │  更新 Buff 剩余时间，应用/移除临时增益
 ├──────────────────────────┤
 │ 13. DeathSystem           │  清理 Hp<=0 的实体，生成掉落物（经验球/道具），触发特效
 ├──────────────────────────┤
 │ 14. NetworkSendSystem     │  将本地状态变更同步给对方
 ├──────────────────────────┤
 │ 15. RenderSystem          │  同步 ECS 数据到 Godot 节点进行渲染
 └──────────────────────────┘
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
| `PlayerInput` | Client → Server | 玩家操作（仅移动方向） |
| `GameState` | Server → Client | 权威游戏状态快照 |
| `SpawnArrow` | Server → Client | 箭矢生成事件 |
| `SpawnWave` | Server → Client | 新一波怪物刷新 |
| `EntityDeath` | Server → Client | 实体死亡通知 |
| `GameOver` | Server → Client | 游戏结束（胜利/失败） |
| `UpgradeOptions` | Server → Client | 波次结束时下发 3 个升级选项 |
| `UpgradeChoice` | Client → Server | 玩家选择的升级项 |
| `PickupSpawn` | Server → Client | 掉落物（经验球/道具）生成通知 |
| `PickupCollect` | Server → Client | 掉落物拾取通知 |
| `BuffApply` | Server → Client | 临时 Buff 应用/移除通知 |
| `BossPhaseChange` | Server → Client | Boss 阶段切换通知 |

---

## 波次系统

### 设计（固定 8 波制）

```
Wave 1       Wave 2            Wave 3                  Wave 4                    Wave 5
──────────   ──────────        ──────────              ──────────                ──────────
Slime ×10    Slime ×8          Slime ×5                Skeleton ×8               Skeleton ×6
             Skeleton ×4       Skeleton ×8             Orc ×4                    Orc ×6
                               Orc ×2                  Elite ×1                  Elite ×3

Wave 6                Wave 7                      Wave 8 (Boss)
──────────            ──────────                  ──────────
Orc ×8                Orc ×10                     Boss ×1
Elite ×5              Elite ×8                    Orc ×5
Skeleton ×5           Skeleton ×8                 Elite ×3
                                                  Slime ×10
```

- 总共 **8 波**，单局约 4-6 分钟
- 每波持续 20-35 秒
- 波次间歇 **5 秒**，期间弹出升级选择面板（3 选 1）
- 波次配置由服务器下发（`WaveConfig`）
- 当前波所有怪物死亡后，倒计时进入下一波
- 怪物从场景**四个边缘随机位置**生成，逐个快速刷出（0.3-0.5 秒间隔）

### 怪物类型

| 类型 | HP | 速度 | 伤害 | 特性 |
|------|-----|------|------|------|
| **Slime** | 20 | 60 | 5 | 基础小怪，直线追踪 |
| **Skeleton** | 40 | 90 | 8 | 间歇性变速闪避（短暂加速侧移） |
| **Orc** | 80 | 50→150 | 15 | 接近后冲锋（加速冲向玩家，冲锋后短暂眩晕） |
| **Elite** | 150 | 70 | 12 | 普通怪的强化版，体型更大，发光标识 |
| **Boss** | 500 | 40 | 25 | 三阶段：追击→召唤小怪→狂暴加速 |

### Boss 设计（第 8 波）

Boss 有三个阶段，每减少 1/3 血量切换：

1. **追击阶段**：缓慢追踪玩家，正常受伤
2. **召唤阶段**：停下来，每 3 秒召唤 3 个 Slime，持续 10 秒
3. **狂暴阶段**：速度翻倍，体型缩小（更难打），不再召唤

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
│   │   │   ├── WaveComponent.cs
│   │   │   ├── AutoAimComponent.cs
│   │   │   ├── UpgradeComponent.cs
│   │   │   ├── PickupComponent.cs
│   │   │   ├── EffectComponent.cs
│   │   │   ├── BuffComponent.cs
│   │   │   ├── OrbitComponent.cs
│   │   │   └── BossPhaseComponent.cs
│   │   └── Systems/
│   │       ├── InputSystem.cs         # 仅处理移动输入
│   │       ├── NetworkRecvSystem.cs
│   │       ├── WaveSpawnSystem.cs
│   │       ├── BossAISystem.cs        # Boss 阶段切换、召唤
│   │       ├── AutoAimSystem.cs       # 自动搜索最近目标、发射箭矢
│   │       ├── MovementSystem.cs
│   │       ├── OrbitSystem.cs         # 旋转护卫箭位置更新
│   │       ├── CollisionSystem.cs     # 含穿透判定、拾取物碰撞
│   │       ├── PickupSystem.cs        # 经验球/道具拾取处理
│   │       ├── DamageSystem.cs        # 含 AOE、DoT 伤害
│   │       ├── EffectSystem.cs        # 箭矢附加效果（爆炸/弹射/冰冻/灼烧）
│   │       ├── BuffSystem.cs          # Buff 更新与应用
│   │       ├── DeathSystem.cs         # 死亡清理 + 生成掉落物
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
│   │   ├── UpgradePanel.cs        # 波次间歇 3 选 1 升级面板 + 倒计时
│   │   ├── BuffBar.cs             # 临时 Buff 图标 + 剩余时间
│   │   ├── UpgradeBar.cs          # 已获得永久升级图标列表
│   │   ├── DamageNumber.cs        # 伤害飘字
│   │   └── ResultUI.cs
│   │
│   ├── Game/
│   │   ├── GameManager.cs         # 全局游戏状态管理（Autoload）
│   │   ├── SceneManager.cs        # 场景切换管理（Autoload）
│   │   ├── WaveConfig.cs          # 波次配置数据
│   │   └── UpgradeConfig.cs       # 升级池配置数据
│   │
│   └── Data/
│       ├── MonsterData.cs         # 怪物属性配置
│       ├── WeaponData.cs          # 弓箭属性配置
│       ├── UpgradeData.cs         # 升级项定义（名称、效果、叠加上限）
│       └── PickupData.cs          # 掉落物配置（概率、效果）
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
7. 每帧: InputSystem(移动) → AutoAimSystem(自动射击) → ... → RenderSystem 循环执行
8. 每波结束 → 收到 UpgradeOptions → 弹出 3 选 1 升级面板 → 发送 UpgradeChoice
9. 8 波全部完成（胜利） / 双人 HP 均为 0（失败）→ 服务器发送 GameOver
10. 切换到 Result 场景 → 显示 S/A/B/C 评分、击杀数、伤害
11. 点击返回 → 回到 MainMenu
```
