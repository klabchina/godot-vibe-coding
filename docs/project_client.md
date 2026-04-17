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
| **Obstacle** | 不可通行障碍物（AABB 矩形），阻挡玩家/怪物/箭矢 |

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
│   │   │   ├── BossPhaseComponent.cs
│   │   │   └── ObstacleComponent.cs    # 障碍物标记
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
│   │   ├── MapLoader.cs           # 地图加载（随机选图、创建障碍物 Entity）
│   │   ├── WaveConfig.cs          # 波次配置数据
│   │   └── UpgradeConfig.cs       # 升级池配置数据
│   │
│   └── Data/
│       ├── MonsterData.cs         # 怪物属性配置
│       ├── WeaponData.cs          # 弓箭属性配置
│       ├── UpgradeData.cs         # 升级项定义（名称、效果、叠加上限）
│       ├── PickupData.cs          # 掉落物配置（概率、效果）
│       └── MapConfig.cs           # 地图数据模型（JSON 反序列化）
│
└── Assets/
    ├── Sprites/
    ├── Audio/
    └── UI/

Data/
└── Maps/                          # 地图 JSON 配置文件
    ├── plain.json
    ├── mountain.json
    └── grassland.json
```

---

## 数值配置系统

### 设计原则

两份策划数值表（`docs/plan/monster_growth_stats.md` + `docs/plan/player_level_upgrade_stats.md`）中的数值采用 **静态配置类 + 枚举 + 公式函数** 的方式在程序中表现：

- **公式型数值**（成长系数、升级效果）→ 写成静态方法，入参为等级/波次，返回计算结果
- **表格型数值**（怪物基础属性、波次配置、经验阈值）→ 写成只读字典 / 数组常量
- **开关型数值**（一次性升级、Boss 阶段）→ 用枚举 + bool 表达

不使用外部 JSON / .tres 文件，原因：数据量小（5 种怪物、8 波、14 种升级）且公式固定，静态类更简洁、类型安全、无反序列化开销。

### 枚举定义

```csharp
// Enums.cs
public enum MonsterType { Slime, Skeleton, Orc, Elite, Boss }
public enum PickupType  { ExpOrb, HealthPotion, Frenzy, Invincible, Bomb }
public enum BuffType    { Frenzy, Invincible, Shield }

public enum UpgradeId
{
    // 攻击类 (6)
    MultiShot, AttackSpeed, DamageUp, Pierce, Bounce, Explosion,
    // 防御类 (4)
    MaxHpUp, MoveSpeedUp, Shield, Regen,
    // 特殊类 (4)
    Magnet, FreezeArrow, BurnArrow, OrbitGuard
}

public enum UpgradeCategory { Attack, Defense, Special }
public enum BossPhase { Chase, Summon, Frenzy }
```

### MonsterData.cs — 怪物属性 + 成长

```csharp
public static class MonsterData
{
    // ── 基础属性表（Wave 1 基准） ──
    public record MonsterBase(int Hp, float Speed, float Damage, int Radius, int BaseXp, int FirstWave);

    public static readonly Dictionary<MonsterType, MonsterBase> Base = new()
    {
        [MonsterType.Slime]    = new(Hp: 20,  Speed: 60,  Damage: 5,  Radius: 15, BaseXp: 5,   FirstWave: 1),
        [MonsterType.Skeleton] = new(Hp: 40,  Speed: 90,  Damage: 8,  Radius: 18, BaseXp: 8,   FirstWave: 2),
        [MonsterType.Orc]      = new(Hp: 80,  Speed: 50,  Damage: 15, Radius: 22, BaseXp: 15,  FirstWave: 3),
        [MonsterType.Elite]    = new(Hp: 150, Speed: 70,  Damage: 12, Radius: 25, BaseXp: 30,  FirstWave: 4),
        [MonsterType.Boss]     = new(Hp: 500, Speed: 40,  Damage: 25, Radius: 40, BaseXp: 100, FirstWave: 8),
    };

    // ── 成长公式 ──
    // 实际 HP  = 基础 HP  × (1 + 0.10 × (当前波次 - 首次出场波次))
    // 实际伤害 = 基础伤害 × (1 + 0.08 × (当前波次 - 首次出场波次))
    // 实际 XP  = 基础 XP  × (1 + 0.10 × (当前波次 - 首次出场波次))  向上取整
    // 速度、体型不随波次变化

    private const float HpGrowthRate     = 0.10f;
    private const float DamageGrowthRate = 0.08f;
    private const float XpGrowthRate     = 0.10f;

    public static int   GetHp(MonsterType type, int wave)     => (int)(Base[type].Hp * (1 + HpGrowthRate * Math.Max(0, wave - Base[type].FirstWave)));
    public static float GetDamage(MonsterType type, int wave)  => Base[type].Damage * (1 + DamageGrowthRate * Math.Max(0, wave - Base[type].FirstWave));
    public static int   GetXp(MonsterType type, int wave)      => (int)Math.Ceiling(Base[type].BaseXp * (1 + XpGrowthRate * Math.Max(0, wave - Base[type].FirstWave)));
    public static float GetSpeed(MonsterType type)             => Base[type].Speed;
    public static int   GetRadius(MonsterType type)            => Base[type].Radius;

    // ── Boss 不使用成长公式，阶段属性固定 ──
    public record BossPhaseData(float HpThresholdPercent, float Speed, float Damage, int Radius);

    public static readonly Dictionary<BossPhase, BossPhaseData> BossPhases = new()
    {
        [BossPhase.Chase]   = new(HpThresholdPercent: 1.0f,       Speed: 40, Damage: 25, Radius: 40),
        [BossPhase.Summon]  = new(HpThresholdPercent: 2f / 3f,    Speed: 0,  Damage: 0,  Radius: 40),
        [BossPhase.Frenzy]  = new(HpThresholdPercent: 1f / 3f,    Speed: 80, Damage: 30, Radius: 30),
    };

    public const int   BossSummonCount     = 3;          // 每次召唤 Slime 数量
    public const float BossSummonCooldown  = 3.0f;       // 召唤间隔（秒）
    public const float BossSummonDuration  = 10.0f;      // 召唤阶段持续时间
    public const int   BossPhaseChangeXp   = 30;         // 阶段切换额外经验

    // ── Orc 冲锋参数 ──
    public const float OrcChargeRange      = 150f;       // 冲锋触发距离 (px)
    public const float OrcChargeSpeed      = 150f;       // 冲锋速度
    public const float OrcStunDuration     = 1.0f;       // 冲锋后眩晕时间

    // ── Skeleton 闪避参数 ──
    public const float SkeletonDodgeInterval = 3.0f;     // 闪避间隔（秒）
    public const float SkeletonDodgeDuration = 0.5f;     // 闪避持续时间
}
```

### WaveData.cs — 波次配置

```csharp
public static class WaveData
{
    public record SpawnEntry(MonsterType Type, int Count);

    // 8 波固定配置（索引 0 = Wave 1）
    public static readonly SpawnEntry[][] Waves =
    {
        new[] { new SpawnEntry(MonsterType.Slime, 10) },                                                                               // Wave 1
        new[] { new SpawnEntry(MonsterType.Slime, 8),  new SpawnEntry(MonsterType.Skeleton, 4) },                                       // Wave 2
        new[] { new SpawnEntry(MonsterType.Slime, 5),  new SpawnEntry(MonsterType.Skeleton, 8), new SpawnEntry(MonsterType.Orc, 2) },   // Wave 3
        new[] { new SpawnEntry(MonsterType.Skeleton, 8), new SpawnEntry(MonsterType.Orc, 4), new SpawnEntry(MonsterType.Elite, 1) },    // Wave 4
        new[] { new SpawnEntry(MonsterType.Skeleton, 6), new SpawnEntry(MonsterType.Orc, 6), new SpawnEntry(MonsterType.Elite, 3) },    // Wave 5
        new[] { new SpawnEntry(MonsterType.Orc, 8), new SpawnEntry(MonsterType.Elite, 5), new SpawnEntry(MonsterType.Skeleton, 5) },    // Wave 6
        new[] { new SpawnEntry(MonsterType.Orc, 10), new SpawnEntry(MonsterType.Elite, 8), new SpawnEntry(MonsterType.Skeleton, 8) },   // Wave 7
        new[] { new SpawnEntry(MonsterType.Boss, 1), new SpawnEntry(MonsterType.Orc, 5), new SpawnEntry(MonsterType.Elite, 3), new SpawnEntry(MonsterType.Slime, 10) }, // Wave 8
    };

    public const int   TotalWaves        = 8;
    public const float WaveIntervalSec   = 5.0f;        // 波次间歇时间
    public const float SpawnIntervalMin  = 0.3f;        // 怪物刷出间隔下限
    public const float SpawnIntervalMax  = 0.5f;        // 怪物刷出间隔上限
}
```

### PlayerData.cs — 玩家基础属性

```csharp
public static class PlayerData
{
    // ── 基础属性（等级 0） ──
    public const int   BaseHp            = 100;
    public const float BaseMoveSpeed     = 200f;         // px/s
    public const int   BaseArrowCount    = 1;
    public const float BaseCooldown      = 0.80f;        // 射击间隔（秒）
    public const int   BaseArrowDamage   = 10;
    public const float ArrowSpeed        = 400f;         // 箭矢速度（固定不可升级）
    public const int   BasePierce        = 0;
    public const float BasePickupRadius  = 50f;          // px
    public const int   BaseOrbitCount    = 0;
}
```

### LevelData.cs — 经验等级表

```csharp
public static class LevelData
{
    public const int MaxLevel = 8;

    // 各等级所需累计 XP（索引 0 = Lv1）
    public static readonly int[] CumulativeXp = { 40, 100, 190, 310, 470, 670, 930, 1250 };

    // 各等级本级所需 XP
    public static readonly int[] LevelXp = { 40, 60, 90, 120, 160, 200, 260, 320 };

    /// <summary>根据累计经验返回当前等级（0 = 未升级，最大 8）</summary>
    public static int GetLevel(int totalXp)
    {
        for (int i = CumulativeXp.Length - 1; i >= 0; i--)
            if (totalXp >= CumulativeXp[i]) return i + 1;
        return 0;
    }
}
```

### UpgradeData.cs — 升级配置 + 效果公式

```csharp
public static class UpgradeData
{
    // ── 升级定义 ──
    public record UpgradeDef(
        UpgradeId Id,
        string Name,
        UpgradeCategory Category,
        int MaxLevel,                // 1 = 一次性，int.MaxValue = 无上限
        string Description
    );

    public static readonly UpgradeDef[] All =
    {
        // 攻击类 (6)
        new(UpgradeId.MultiShot,   "多重射击", UpgradeCategory.Attack,  7,              "+1 箭矢，扇形展开"),
        new(UpgradeId.AttackSpeed, "射速提升", UpgradeCategory.Attack,  5,              "射击间隔 ×0.85"),
        new(UpgradeId.DamageUp,    "伤害提升", UpgradeCategory.Attack,  5,              "基础伤害 +30%"),
        new(UpgradeId.Pierce,      "穿透箭",   UpgradeCategory.Attack,  3,              "+1 穿透次数"),
        new(UpgradeId.Bounce,      "弹射箭",   UpgradeCategory.Attack,  1,              "命中后弹射 1 次"),
        new(UpgradeId.Explosion,   "爆炸箭",   UpgradeCategory.Attack,  1,              "命中时 AOE 爆炸"),

        // 防御类 (4)
        new(UpgradeId.MaxHpUp,     "生命提升", UpgradeCategory.Defense, int.MaxValue,   "最大 HP +20%，立即恢复"),
        new(UpgradeId.MoveSpeedUp, "移速提升", UpgradeCategory.Defense, 3,              "移动速度 +15%"),
        new(UpgradeId.Shield,      "护盾",     UpgradeCategory.Defense, 1,              "每 15 秒生成 1 层护盾"),
        new(UpgradeId.Regen,       "生命恢复", UpgradeCategory.Defense, 1,              "每秒恢复 1% 最大 HP"),

        // 特殊类 (4)
        new(UpgradeId.Magnet,      "磁铁",     UpgradeCategory.Special, int.MaxValue,   "拾取半径 +50%"),
        new(UpgradeId.FreezeArrow, "冰冻箭",   UpgradeCategory.Special, 1,              "减速 30%，持续 2 秒"),
        new(UpgradeId.BurnArrow,   "火焰箭",   UpgradeCategory.Special, 1,              "DoT 3/s，持续 3 秒"),
        new(UpgradeId.OrbitGuard,  "旋转护卫", UpgradeCategory.Special, int.MaxValue,   "+1 环绕护卫箭"),
    };

    // ── 类别抽取权重 ──
    public static readonly Dictionary<UpgradeCategory, float> CategoryWeight = new()
    {
        [UpgradeCategory.Attack]  = 0.50f,
        [UpgradeCategory.Defense] = 0.30f,
        [UpgradeCategory.Special] = 0.20f,
    };

    public const int ChoiceCount         = 3;            // 每次升级 3 选 1
    public const float ChoiceTimeoutSec  = 5.0f;         // 超时自动选第一个
    public const int GuaranteeAttackWave = 2;            // 前 N 次升级至少出现 1 个攻击类

    // ── 效果计算公式（全部静态方法） ──

    // 多重射击: 箭矢数 = 1 + Lv
    public static int GetArrowCount(int lv) => 1 + lv;
    // 多重射击: 扇形角度
    public static readonly float[] SpreadAngles = { 0, 15, 30, 45, 50, 55, 60, 65 };  // 索引 = Lv

    // 射速提升: 间隔 = 0.80 × 0.85^Lv
    public static float GetCooldown(int lv) => 0.80f * MathF.Pow(0.85f, lv);

    // 伤害提升: 伤害 = 10 × (1 + 0.3 × Lv)  加法叠加
    public static int GetArrowDamage(int lv) => (int)(PlayerData.BaseArrowDamage * (1 + 0.3f * lv));

    // 穿透: 穿透次数 = Lv（命中数 = Lv + 1）
    public static int GetPierceCount(int lv) => lv;

    // 弹射箭参数
    public const float BounceRadius       = 120f;        // 弹射搜索半径 (px)
    public const float BounceDamageRatio  = 0.70f;       // 弹射伤害 = 原始 ×70%

    // 爆炸箭参数
    public const float ExplosionRadius      = 60f;       // 爆炸半径 (px)
    public const float ExplosionDamageRatio = 0.50f;     // 爆炸伤害 = 原始 ×50%

    // 生命提升: MaxHP = 100 × (1 + 0.2 × Lv)，每次升级恢复 20 HP
    public static int GetMaxHp(int lv) => (int)(PlayerData.BaseHp * (1 + 0.2f * lv));
    public const int HpUpHealAmount = 20;

    // 移速提升: 速度 = 200 × (1 + 0.15 × Lv)
    public static float GetMoveSpeed(int lv) => PlayerData.BaseMoveSpeed * (1 + 0.15f * lv);

    // 护盾参数
    public const float ShieldRegenInterval = 15.0f;      // 护盾重生间隔（秒）

    // 生命恢复: 每秒恢复当前 MaxHP 的 1%
    public const float RegenPercent = 0.01f;

    // 磁铁: 拾取半径 = 50 × (1 + 0.5 × Lv)
    public static float GetPickupRadius(int lv) => PlayerData.BasePickupRadius * (1 + 0.5f * lv);

    // 冰冻箭参数
    public const float FreezeSlowPercent    = 0.30f;     // 减速 30%
    public const float FreezeDuration       = 2.0f;      // 持续 2 秒
    public const float FreezeBossResist     = 0.50f;     // Boss 抗性 50%（减速降为 15%）

    // 火焰箭参数
    public const int   BurnDotDamage        = 3;         // 每秒 3 点
    public const float BurnDotDuration      = 3.0f;      // 持续 3 秒

    // 旋转护卫: 护卫数 = Lv
    public static int GetOrbitCount(int lv) => lv;
    public const float OrbitRotationSpeed   = 180f;      // 旋转速度 (°/s)
    public const float OrbitRadius          = 80f;       // 旋转半径 (px)
    public const int   OrbitDamage          = 8;         // 每次碰撞伤害
    public const float OrbitHitInterval     = 0.5f;      // 同一护卫对同一怪物最短打击间隔
}
```

### PickupData.cs — 掉落物配置

```csharp
public static class PickupData
{
    // ── 道具掉落概率（各概率之和 = 5%）──
    public record DropEntry(PickupType Type, float Probability);

    public static readonly DropEntry[] DropTable =
    {
        new(PickupType.HealthPotion, 0.020f),    // 2.0% 恢复 25% MaxHP
        new(PickupType.Frenzy,       0.015f),    // 1.5% 射速 ×2，持续 5 秒
        new(PickupType.Invincible,   0.005f),    // 0.5% 免疫伤害，持续 3 秒
        new(PickupType.Bomb,         0.010f),    // 1.0% 全屏 50 伤害
    };

    public const bool BossGuaranteeDrop = true;          // Boss 必掉 1 个随机道具

    // ── 道具效果数值 ──
    public const float HealthPotionPercent  = 0.25f;     // 恢复 25% MaxHP
    public const float FrenzyMultiplier     = 2.0f;      // 射速翻倍
    public const float FrenzyDuration       = 5.0f;      // 持续 5 秒
    public const float InvincibleDuration   = 3.0f;      // 持续 3 秒
    public const int   BombDamage           = 50;        // 全屏 50 伤害

    // ── 经验球物理属性 ──
    public const float ExpOrbPickupRadius   = 50f;       // 拾取吸附半径 (px)，受磁铁升级影响
    public const float ExpOrbFlySpeed       = 300f;      // 被吸附后飞向玩家的速度 (px/s)
    public const float ExpOrbLifeTime       = 30f;       // 存活时间（秒）
    public const float ExpOrbBlinkTime      = 5.0f;      // 最后 N 秒闪烁提示
}
```

### 数值引用方式示例

System 中使用配置数据的典型写法：

```csharp
// WaveSpawnSystem 中生成怪物
var entry = WaveData.Waves[currentWave - 1];
foreach (var spawn in entry)
{
    for (int i = 0; i < spawn.Count; i++)
    {
        var hp     = MonsterData.GetHp(spawn.Type, currentWave);
        var damage = MonsterData.GetDamage(spawn.Type, currentWave);
        var speed  = MonsterData.GetSpeed(spawn.Type);
        // ... 创建 Entity 并赋值 Component
    }
}

// DamageSystem 中计算箭矢伤害
var arrowDmg = UpgradeData.GetArrowDamage(upgrade.DamageLevel);
if (arrow.Explosive)
{
    var aoeDmg = (int)(arrowDmg * UpgradeData.ExplosionDamageRatio);
    // ... 在 ExplosionRadius 内对所有怪物造成 aoeDmg
}

// DeathSystem 中掉落经验
var xp = MonsterData.GetXp(monster.Type, currentWave);
// ... 生成经验球 Entity

// 升级面板中计算当前等级
var level = LevelData.GetLevel(player.TotalXp);
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

---

## 相关设计文档

| 主题 | 路径 |
|------|------|
| 战斗玩法设计 | `docs/superpowers/specs/2026-04-07-battle-gameplay-design.md` |
| 远程敌人行为 | `docs/superpowers/plans/2026-04-10-ranged-enemy-behavior.md` |
| 怪物死亡动画延迟销毁 | `docs/superpowers/specs/2026-04-14-monster-death-animation-design.md` |
| OBB 矩形碰撞支持 | `docs/superpowers/specs/2026-04-17-obb-collision-design.md` |
