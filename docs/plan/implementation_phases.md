# 客户端分阶段实现计划

> 基于 project_client.md 架构、battle-gameplay-design.md 玩法设计、player_level_upgrade_stats.md 和 monster_growth_stats.md 数值文档

**项目现状**: 仅有 Godot 4.6 C# 项目骨架（project.godot + client.csproj），无任何代码或场景文件。

---

## Phase 1：基础架构层

**目标**: 搭建项目目录结构、ECS 框架核心、全局管理器、所有静态数值配置类。本阶段完成后，项目可编译通过，数值可被单元测试验证。

### 1.1 目录结构创建

按照 `project_client.md` 中的目录规范创建：

```
client/
├── Scenes/           # .tscn 场景文件
├── Scripts/
│   ├── Ecs/
│   │   ├── Components/
│   │   └── Systems/
│   ├── Net/
│   ├── UI/
│   ├── Game/
│   └── Data/
└── Assets/
    ├── Sprites/
    ├── Audio/
    └── UI/
```

### 1.2 枚举与数据层

| 文件 | 内容 | 依据文档 |
|------|------|----------|
| `Scripts/Data/Enums.cs` | MonsterType, PickupType, BuffType, UpgradeId, UpgradeCategory, BossPhase | project_client.md §枚举定义 |
| `Scripts/Data/MonsterData.cs` | 怪物基础属性表 + 成长公式 + Boss 阶段数据 + Orc/Skeleton 特殊参数 | monster_growth_stats.md §1-2 |
| `Scripts/Data/PlayerData.cs` | 玩家基础属性常量（HP=100, 移速=200, 射击间隔=0.8s 等） | player_level_upgrade_stats.md §1 |
| `Scripts/Data/WaveData.cs` | 8 波固定配置 + 波次间歇/刷怪间隔常量 | project_client.md §WaveData |
| `Scripts/Data/LevelData.cs` | 经验等级表（8 级）+ GetLevel() | player_level_upgrade_stats.md §2.2 |
| `Scripts/Data/UpgradeData.cs` | 14 种升级定义 + 类别权重 + 所有效果计算公式 | player_level_upgrade_stats.md §3-5, §8 |
| `Scripts/Data/PickupData.cs` | 道具掉落概率表 + 效果数值 + 经验球物理属性 | monster_growth_stats.md §4, player_level_upgrade_stats.md §9 |

### 1.3 ECS 框架核心

| 文件 | 内容 |
|------|------|
| `Scripts/Ecs/Entity.cs` | Entity 基础定义（int ID + Component 字典） |
| `Scripts/Ecs/World.cs` | ECS World 管理器：Entity 创建/销毁、Component 查询、System 注册与按顺序执行 |

设计要点：
- Entity 使用 int ID，Component 通过泛型字典存储
- World 维护 Entity 池 + Component 索引，支持 `GetEntitiesWith<T1, T2>()` 查询
- System 为抽象基类，含 `Update(float delta)` 方法
- World.Update() 按预定义顺序执行所有 System

### 1.4 全局管理器（Autoload）

| 文件 | 内容 |
|------|------|
| `Scripts/Game/GameManager.cs` | 全局游戏状态：当前场景阶段、玩家数据（继承 Node） |
| `Scripts/Game/SceneManager.cs` | 场景切换管理（继承 Node） |

在 `project.godot` 中注册为 Autoload。

### 1.5 验收标准

- [ ] 项目可编译通过（`dotnet build`）
- [ ] 所有 Data 类的公式方法可通过手动测试验证（如 `MonsterData.GetHp(MonsterType.Slime, 3)` 返回 24）
- [ ] ECS World 可创建 Entity、添加/获取 Component、注册/执行 System

---

## Phase 2：单人核心战斗循环

**目标**: 实现最基础的单人战斗体验——玩家可移动、弓箭自动射击最近怪物、怪物按波次刷新并追踪玩家、箭矢命中怪物造成伤害并死亡清理。本阶段完成后可以单人跑通 Wave 1-7（不含 Boss）。

### 2.1 基础 Component

| Component | 关键字段 |
|-----------|----------|
| `TransformComponent` | Position(Vector2), Rotation(float) |
| `VelocityComponent` | Velocity(Vector2), Speed(float) |
| `HealthComponent` | Hp(int), MaxHp(int) |
| `BowComponent` | Damage(int), Cooldown(float), CooldownTimer(float), ArrowCount(int), SpreadAngle(float) |
| `ArrowComponent` | Damage(int), OwnerId(int), PierceCount(int) |
| `MonsterComponent` | Type(MonsterType), Reward(int) |
| `ColliderComponent` | Radius(float), Layer(int), Mask(int) — 简化为圆形碰撞 |
| `SpriteComponent` | Texture(Texture2D), Animation(string) |
| `AutoAimComponent` | TargetId(int), SearchRadius(float) |
| `WaveComponent` | WaveNum(int), SpawnList, Interval(float) |

### 2.2 核心 System（按执行顺序）

| System | 职责 | 依据 |
|--------|------|------|
| `InputSystem` | 读取 WASD/摇杆输入，设置玩家 VelocityComponent | project_client.md §System 列表 #1 |
| `WaveSpawnSystem` | 波次推进、从四边随机生成怪物（0.3-0.5s 间隔）、波次结束检测 | project_client.md §波次系统 |
| `AutoAimSystem` | 搜索最近怪物、按冷却时间自动发射箭矢 Entity | battle-gameplay-design.md §2 |
| `MovementSystem` | 根据 Velocity 更新 Transform，处理边界碰撞 | project_client.md §System 列表 #6 |
| `CollisionSystem` | 箭矢 vs 怪物的圆形碰撞检测，命中时标记 | project_client.md §System 列表 #8 |
| `DamageSystem` | 处理碰撞结果，扣减怪物 HP | project_client.md §System 列表 #10 |
| `DeathSystem` | 清理 HP≤0 的怪物 Entity，生成经验球（暂不处理拾取） | project_client.md §System 列表 #13 |
| `RenderSystem` | 将 ECS 数据同步到 Godot 节点进行渲染 | project_client.md §System 列表 #15 |

### 2.3 Battle 场景

| 文件 | 内容 |
|------|------|
| `Scenes/Battle.tscn` | 战斗场景：2D 竞技场（固定大小矩形）、摄像机、基础背景 |
| `Scripts/UI/BattleHud.cs` | 最小战斗 HUD：当前波次、HP 条 |

### 2.4 渲染桥接

RenderSystem 负责 ECS 世界与 Godot 节点树的同步：
- ECS Entity 创建时，RenderSystem 实例化对应 Godot 节点（Sprite2D）
- 每帧同步 TransformComponent → Node2D.Position/Rotation
- Entity 销毁时，移除对应节点

可先用简单几何图形（ColorRect/Circle）代替精灵资源。

### 2.5 怪物 AI（基础版）

- **Slime**: 直线追踪最近玩家
- **Skeleton**: 直线追踪（闪避行为延后实现）
- **Orc**: 直线追踪（冲锋行为延后实现）
- **Elite**: 直线追踪（更高属性）

玩家接触怪物扣血逻辑也在 CollisionSystem 中处理。

### 2.6 验收标准

- [ ] 玩家可 WASD 移动，弓箭自动射击最近怪物
- [ ] 怪物从四边刷新，追踪玩家，被箭射死后消失
- [ ] 波次自动推进（Wave 1 → Wave 7），每波间歇 5 秒
- [ ] 玩家被怪物接触会扣血，HP 降至 0 游戏结束
- [ ] BattleHud 显示波次和 HP

---

## Phase 3：经验与升级系统

**目标**: 实现完整的经验收集、等级提升、升级面板 3 选 1 机制。本阶段完成后，玩家可通过击杀怪物收集经验球升级，选择可叠加的攻击类升级能感受到明显变强。

### 3.1 新增 Component

| Component | 关键字段 |
|-----------|----------|
| `PickupComponent` | Type(PickupType), Value(int), LifeTime(float) |
| `UpgradeComponent` | MultiShotLevel, AttackSpeedLevel, DamageLevel, PierceLevel, HasBounce, HasExplosion, MaxHpLevel, MoveSpeedLevel, HasShield, HasRegen, MagnetLevel, HasFreeze, HasBurn, OrbitCount |

### 3.2 新增/修改 System

| System | 职责 |
|--------|------|
| `PickupSystem`（新增） | 检测玩家与经验球碰撞，拾取后累加经验，检查升级触发 |
| `DeathSystem`（修改） | 怪物死亡后生成经验球 Entity（双人模式下为双方各生成一份） |
| `AutoAimSystem`（修改） | 读取 UpgradeComponent 计算实际箭矢数、冷却时间、伤害 |
| `CollisionSystem`（修改） | 增加穿透判定逻辑（PierceCount 递减，不立即销毁箭矢） |

### 3.3 经验球机制

依据 monster_growth_stats.md §3 + §5：
- 怪物死亡掉落经验球，XP 值由 `MonsterData.GetXp()` 计算
- 经验球物理属性：拾取半径 50px、飞行速度 300px/s、存活 30 秒、最后 5 秒闪烁
- 进入拾取半径后自动飞向玩家

### 3.4 升级面板

| 文件 | 内容 |
|------|------|
| `Scripts/UI/UpgradePanel.cs` | 3 选 1 升级面板 + 5 秒倒计时 + 超时自动选第一个 |

依据 player_level_upgrade_stats.md §2.5 + §8：
- 经验达到阈值（LevelData）时立即弹出
- 按类别权重抽取（攻击 50% / 防御 30% / 特殊 20%）
- 已满级的项不再出现
- 前 2 次升级保底 1 个攻击类
- 游戏不暂停
- 支持连续升级（极端情况下可连续弹出多次）

### 3.5 可叠加攻击类升级实现

本阶段实现效果立竿见影的 4 种可叠加攻击升级：

| 升级 | 实现方式 | 数值依据 |
|------|----------|----------|
| 多重射击 | AutoAimSystem 读取 MultiShotLevel，发射 1+Lv 支箭矢，扇形展开 | player_level_upgrade_stats.md §3.1 |
| 射速提升 | BowComponent.Cooldown = `0.80 × 0.85^Lv` | player_level_upgrade_stats.md §3.2 |
| 伤害提升 | ArrowComponent.Damage = `10 × (1 + 0.3 × Lv)` | player_level_upgrade_stats.md §3.3 |
| 穿透箭 | ArrowComponent.PierceCount = Lv，CollisionSystem 穿透逻辑 | player_level_upgrade_stats.md §3.4 |

### 3.6 验收标准

- [ ] 怪物死亡掉落绿色经验球，玩家靠近自动吸附拾取
- [ ] 经验达到阈值弹出 3 选 1 升级面板
- [ ] 选择多重射击后可见箭矢数增加
- [ ] 选择射速/伤害/穿透后效果生效
- [ ] 升级面板 5 秒倒计时 + 超时自动选择
- [ ] 单局最高 Lv8，满级后不再弹出面板

---

## Phase 4：高级战斗机制

**目标**: 实现所有剩余升级效果、Boss 战、怪物高级 AI、道具掉落与 Buff 系统。本阶段完成后，完整的 8 波单人战斗体验可完整体验。

### 4.1 新增 Component

| Component | 关键字段 |
|-----------|----------|
| `EffectComponent` | AoeRadius, AoeDamage, SlowPercent, SlowDuration, DotDamage, DotDuration, BounceRadius |
| `BuffComponent` | ActiveBuffs: List\<Buff\>（含 Type + RemainingTime） |
| `OrbitComponent` | Count, RotationSpeed(180°/s), Radius(80px), Damage(8), CurrentAngle |
| `BossPhaseComponent` | Phase(1/2/3), PhaseTimer, SummonCooldown |

### 4.2 新增 System

| System | 职责 | 执行位置 |
|--------|------|----------|
| `BossAISystem` | Boss 三阶段切换（追击→召唤→狂暴）、召唤 Slime | WaveSpawnSystem 之后 |
| `OrbitSystem` | 更新旋转护卫箭位置、碰撞伤害 | MovementSystem 之后 |
| `EffectSystem` | 箭矢附加效果：AOE 爆炸、弹射生成新箭矢、冰冻减速、灼烧 DoT | DamageSystem 之后 |
| `BuffSystem` | 更新 Buff 计时、应用/移除临时增益 | EffectSystem 之后 |

### 4.3 一次性攻击升级

| 升级 | 实现 | 数值 |
|------|------|------|
| 弹射箭 | 箭矢命中后在 120px 内找最近未命中敌人生成弹射箭（伤害 ×70%） | player_level_upgrade_stats.md §3.5 |
| 爆炸箭 | 命中时对 60px 半径内敌人造成 ×50% AOE 伤害 | player_level_upgrade_stats.md §3.6 |

### 4.4 防御类升级

| 升级 | 实现 | 数值 |
|------|------|------|
| 生命提升 | MaxHP = `100 × (1 + 0.2 × Lv)`，每次恢复 20 HP | player_level_upgrade_stats.md §4.1 |
| 移速提升 | Speed = `200 × (1 + 0.15 × Lv)`，最多 3 级 | player_level_upgrade_stats.md §4.2 |
| 护盾 | 每 15 秒生成 1 层，吸收 1 次伤害，获得后立即生效 | player_level_upgrade_stats.md §4.3 |
| 生命恢复 | 每秒恢复 1% MaxHP | player_level_upgrade_stats.md §4.4 |

### 4.5 特殊类升级

| 升级 | 实现 | 数值 |
|------|------|------|
| 磁铁 | 拾取半径 = `50 × (1 + 0.5 × Lv)` | player_level_upgrade_stats.md §5.1 |
| 冰冻箭 | 减速 30%（Boss 15%），持续 2 秒，不叠加不刷新 | player_level_upgrade_stats.md §5.2 |
| 火焰箭 | DoT 3/s × 3 秒，不叠加但命中刷新持续时间 | player_level_upgrade_stats.md §5.3 |
| 旋转护卫 | +1 护卫箭，180°/s 旋转，80px 半径，伤害 8，间隔 0.5s | player_level_upgrade_stats.md §5.4 |

### 4.6 怪物高级 AI

| 怪物 | 行为 | 数值依据 |
|------|------|----------|
| Skeleton 闪避 | 每 3 秒随机侧移 0.5 秒 | monster_growth_stats.md §1 |
| Orc 冲锋 | 进入 150px 后加速到 150px/s，冲锋后眩晕 1 秒 | monster_growth_stats.md §1, project_client.md §MonsterData |
| Boss 阶段 1 | 缓慢追踪（速度 40），HP > 2/3 | monster_growth_stats.md §2.7 |
| Boss 阶段 2 | 原地不动，每 3 秒召唤 3 个 Slime，持续 10 秒 | monster_growth_stats.md §2.7 |
| Boss 阶段 3 | 速度翻倍（80），伤害 +20%（30），体型缩小至半径 30px | monster_growth_stats.md §2.7 |

Boss 阶段切换给予 30 XP 奖励。

### 4.7 道具掉落与 Buff

依据 monster_growth_stats.md §4 + player_level_upgrade_stats.md §9：
- 怪物死亡 5% 掉落道具（药水 2.0%、狂暴 1.5%、无敌 0.5%、炸弹 1.0%）
- Boss 必掉 1 个随机道具
- 增益类 Buff 同时只生效 1 个（新覆盖旧）
- 药水（恢复 25% MaxHP）和炸弹（全屏 50 伤害）为即时效果

### 4.8 玩家死亡与复活

依据 battle-gameplay-design.md §1.4：
- 两名玩家独立血量
- HP 降至 0 后 10 秒自动复活（每局限 1 次）
- 任一玩家存活则游戏继续
- 双方均死亡则失败

### 4.9 验收标准

- [ ] 所有 14 种升级效果均可正常生效
- [ ] Boss 三阶段切换正常（追击→召唤→狂暴）
- [ ] Skeleton 闪避、Orc 冲锋行为正常
- [ ] 道具掉落并拾取生效
- [ ] Buff 图标显示、计时正确、新覆盖旧
- [ ] 完整单人 8 波战斗可通关

---

## Phase 5：UI 与场景流转

**目标**: 实现完整的 4 场景流转（MainMenu → Matching → Battle → Result）和所有 UI 元素。本阶段完成后，单机版完整游戏体验可从头到尾走通。

### 5.1 场景

| 文件 | 内容 |
|------|------|
| `Scenes/MainMenu.tscn` | 主界面：开始匹配按钮、玩家信息 |
| `Scenes/Matching.tscn` | 匹配等待界面：匹配状态、倒计时、取消按钮（单机版模拟跳过） |
| `Scenes/Result.tscn` | 结算界面：波次数、击杀数、伤害、S/A/B/C 评分、返回按钮 |

### 5.2 UI 脚本

| 文件 | 内容 |
|------|------|
| `Scripts/UI/MainMenuUI.cs` | 主界面逻辑 |
| `Scripts/UI/MatchingUI.cs` | 匹配界面逻辑 |
| `Scripts/UI/ResultUI.cs` | 结算界面：评分计算（波次进度、击杀、伤害、存活、经验收集） |
| `Scripts/UI/BattleHud.cs`（增强） | 完整 HUD：HP 条、波次进度、击杀数 |
| `Scripts/UI/BuffBar.cs` | 当前生效 Buff 图标 + 剩余时间 |
| `Scripts/UI/UpgradeBar.cs` | 已获得永久升级图标列表 |
| `Scripts/UI/DamageNumber.cs` | 伤害飘字（浮动上升 + 渐隐） |

### 5.3 结算评分

依据 battle-gameplay-design.md §5.3：

| 评分维度 | 权重 |
|----------|------|
| 波次进度 | 清完所有波次得满分 |
| 击杀数 | 个人击杀数量 |
| 伤害输出 | 个人总伤害 |
| 存活能力 | 剩余 HP 比例 |
| 经验球收集 | 收集总量 |

汇总为 S/A/B/C 四个等级。

### 5.4 场景切换

SceneManager 实现场景流转：
```
MainMenu → (点击开始) → Matching → (匹配成功/单机模拟) → Battle → (游戏结束) → Result → (返回) → MainMenu
```

支持超时/取消从 Matching 返回 MainMenu。

### 5.5 验收标准

- [ ] 可从 MainMenu 开始，经完整 8 波战斗，到 Result 结算，再返回 MainMenu
- [ ] 结算界面正确显示波次、击杀、伤害、评分
- [ ] 战斗 HUD 显示 BuffBar、UpgradeBar
- [ ] 伤害飘字效果正常

---

## Phase 6：网络多人对战

**目标**: 实现 WebSocket 网络层、匹配系统、战斗状态同步，支持两人在线合作对战。

### 6.1 网络基础

| 文件 | 内容 |
|------|------|
| `Scripts/Net/NetManager.cs` | WebSocket 连接管理：连接、断开、心跳、重连 |
| `Scripts/Net/Protocol.cs` | 消息定义与序列化/反序列化（所有消息类型） |
| `Scripts/Net/MatchClient.cs` | 匹配逻辑：MatchRequest → MatchUpdate → MatchSuccess → PlayerReady → BattleStart |
| `Scripts/Net/SyncClient.cs` | 战斗同步：发送 PlayerInput、接收 GameState/事件 |

### 6.2 网络协议消息

依据 project_client.md §网络架构/战斗同步协议：

**匹配阶段**: MatchRequest, MatchUpdate, MatchSuccess, PlayerReady, BattleStart

**战斗阶段**:
| 消息 | 方向 | 说明 |
|------|------|------|
| PlayerInput | C→S | 仅移动方向 (move_dir) |
| GameState | S→C | 权威游戏状态快照 |
| SpawnArrow | S→C | 箭矢生成事件 |
| SpawnWave | S→C | 新一波怪物刷新 |
| EntityDeath | S→C | 实体死亡通知 |
| UpgradeOptions | S→C | 3 个升级选项 |
| UpgradeChoice | C→S | 玩家选择 |
| PickupSpawn | S→C | 掉落物生成 |
| PickupCollect | S→C | 掉落物拾取 |
| BuffApply | S→C | Buff 应用/移除 |
| BossPhaseChange | S→C | Boss 阶段切换 |
| GameOver | S→C | 游戏结束 |

### 6.3 新增 Component & System

| 项目 | 内容 |
|------|------|
| `NetworkSyncComponent` | NetId, Owner, IsLocal |
| `NetworkRecvSystem` | 接收并应用服务器状态（执行顺序 #2） |
| `NetworkSendSystem` | 发送本地操作和状态变更（执行顺序 #14） |

### 6.4 同步策略

- **客户端**：仅发送移动输入（PlayerInput）
- **服务器**：权威状态，负责所有游戏逻辑判定
- **客户端预测**：本地玩家移动做预测，收到服务器状态后校验并修正
- **升级选择**：服务器下发 UpgradeOptions，客户端选择后发送 UpgradeChoice，两人独立选择

### 6.5 Matching 场景对接

- MatchingUI 显示真实匹配状态（等待中 / 匹配成功）
- 支持取消匹配
- 匹配成功后双方发送 PlayerReady
- 双方 Ready 后服务器发送 BattleStart，切换到 Battle 场景

### 6.6 验收标准

- [ ] 两个客户端可连接服务器并成功匹配
- [ ] 双人进入战斗后各自独立操控角色
- [ ] 怪物、箭矢、经验球、道具在双方屏幕上同步显示
- [ ] 各自独立升级选择
- [ ] 战斗结束后双方同时进入结算
- [ ] 断线重连正常工作

---

## 阶段依赖关系

```
Phase 1 (基础架构)
    │
    ▼
Phase 2 (核心战斗循环)
    │
    ├──────────────────────────┐
    ▼                          ▼
Phase 3 (经验升级)        Phase 5 (UI 场景流转 - 可并行开始 MainMenu/Result)
    │                          │
    ▼                          │
Phase 4 (高级战斗)             │
    │                          │
    ├──────────────────────────┘
    ▼
Phase 6 (网络多人)
```

- Phase 1 → 2 → 3 → 4 为严格顺序依赖
- Phase 5 的 MainMenu/Result 场景可在 Phase 3 完成后开始并行开发
- Phase 6 依赖 Phase 4 + 5 全部完成

---

## 各阶段关键文件清单

| Phase | 新增文件数 | 关键文件 |
|-------|-----------|----------|
| 1 | ~12 | Enums.cs, MonsterData.cs, PlayerData.cs, WaveData.cs, LevelData.cs, UpgradeData.cs, PickupData.cs, Entity.cs, World.cs, GameManager.cs, SceneManager.cs |
| 2 | ~14 | 10 个 Component + Battle.tscn + 8 个 System + BattleHud.cs |
| 3 | ~4 | PickupComponent.cs, UpgradeComponent.cs, PickupSystem.cs, UpgradePanel.cs |
| 4 | ~8 | EffectComponent.cs, BuffComponent.cs, OrbitComponent.cs, BossPhaseComponent.cs, BossAISystem.cs, OrbitSystem.cs, EffectSystem.cs, BuffSystem.cs |
| 5 | ~9 | MainMenu.tscn, Matching.tscn, Result.tscn, MainMenuUI.cs, MatchingUI.cs, ResultUI.cs, BuffBar.cs, UpgradeBar.cs, DamageNumber.cs |
| 6 | ~7 | NetManager.cs, Protocol.cs, MatchClient.cs, SyncClient.cs, NetworkSyncComponent.cs, NetworkRecvSystem.cs, NetworkSendSystem.cs |
