# 地图加载模块设计

## 概述

为 BattleScene 增加地图加载功能。每张地图由**背景图 + 障碍物列表**组成，战斗前随机选图。障碍物为 AABB 矩形，对玩家、怪物、箭矢均生效（阻挡型碰撞）。

## 数据格式

每张地图一个 JSON 文件，存放于 `client/Data/Maps/` 目录：

```json
{
  "id": "plain",
  "background": "res://Assets/Sprites/Scenes/battle_bg_plain.png",
  "obstacles": [
    { "x": 400, "y": 300, "w": 120, "h": 80, "sprite": "res://Assets/Sprites/Props/rock_01.png" },
    { "x": 900, "y": 600, "w": 200, "h": 60, "sprite": "" }
  ]
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string | 地图唯一标识 |
| `background` | string | 背景图 Godot 资源路径 |
| `obstacles[]` | array | 障碍物列表 |
| `obstacles[].x` | float | 障碍物左上角 X 坐标 |
| `obstacles[].y` | float | 障碍物左上角 Y 坐标 |
| `obstacles[].w` | float | 障碍物宽度 |
| `obstacles[].h` | float | 障碍物高度 |
| `obstacles[].sprite` | string | 障碍物精灵图路径（空字符串 = 无可见精灵，仅碰撞） |

## 新增文件

### 1. `client/Scripts/Data/MapConfig.cs` — 数据模型

```csharp
namespace Game.Data;

public class ObstacleConfig
{
    public float X { get; set; }
    public float Y { get; set; }
    public float W { get; set; }
    public float H { get; set; }
    public string Sprite { get; set; } = "";
}

public class MapConfig
{
    public string Id { get; set; } = "";
    public string Background { get; set; } = "";
    public List<ObstacleConfig> Obstacles { get; set; } = new();
}
```

### 2. `client/Scripts/Game/MapLoader.cs` — 加载与初始化

职责：
- `LoadAll()` — 扫描 `res://Data/Maps/*.json`，反序列化为 `MapConfig` 列表
- `PickRandom()` — 随机选取一张地图
- `ApplyToScene(MapConfig, Node2D renderRoot)` — 替换背景图为 Sprite2D
- `SpawnObstacles(MapConfig, World)` — 为每个障碍物创建 ECS Entity

```csharp
namespace Game;

public static class MapLoader
{
    private static readonly List<MapConfig> _maps = new();
    private static readonly Random _rng = new();

    public static void LoadAll() { /* 读取 JSON 文件 */ }

    public static MapConfig PickRandom()
        => _maps[_rng.Next(_maps.Count)];

    public static void ApplyToScene(MapConfig map, Node2D sceneRoot)
    {
        // 1. 找到 Background 节点，替换为 Sprite2D + 加载纹理
        // 2. 为每个有 sprite 的障碍物创建 Sprite2D 子节点
    }

    public static void SpawnObstacles(MapConfig map, World world)
    {
        foreach (var obs in map.Obstacles)
        {
            var entity = world.CreateEntity();
            entity.Add(new TransformComponent
            {
                Position = new Vec2(obs.X + obs.W / 2, obs.Y + obs.H / 2)
            });
            entity.Add(new ColliderComponent
            {
                Shape = ColliderShape.Box,
                HalfWidth = obs.W / 2,
                HalfHeight = obs.H / 2,
                Layer = CollisionLayers.Obstacle,
                Mask = 0  // 障碍物不主动检测，被其他实体检测
            });
            entity.Add(new ObstacleComponent());
        }
    }
}
```

### 3. `client/Scripts/Ecs/Components/ObstacleComponent.cs` — ECS 标记

```csharp
namespace Game.Ecs.Components;

/// <summary>
/// 标记实体为障碍物（不可通行区域）。
/// </summary>
public class ObstacleComponent { }
```

## 碰撞层扩展

在 `CollisionLayers` 中新增 `Obstacle` 层：

```csharp
public static class CollisionLayers
{
    public const int Player   = 1 << 0;
    public const int Monster  = 1 << 1;
    public const int Arrow    = 1 << 2;
    public const int Pickup   = 1 << 3;
    public const int Obstacle = 1 << 4;  // 新增
}
```

## 系统改动

### MovementSystem — 障碍物推回

玩家和怪物移动后，检测与所有障碍物的 AABB 重叠。若重叠，沿**最小穿透轴**推回：

```
移动后位置 → 遍历障碍物 → 计算 AABB 重叠 → 取最小穿透方向 → 推回
```

算法：
1. 计算实体 AABB（中心 ± HalfWidth/HalfHeight 或 Radius）与障碍物 AABB 的重叠量
2. 取 X 和 Y 方向中穿透量较小的一个
3. 沿该方向推回穿透量

受影响实体：有 `PlayerComponent` 或 `MonsterComponent` 的实体。

### CollisionSystem — 箭矢拦截

在现有箭矢碰撞检测之后，增加箭矢/投射物 vs 障碍物检测：

- `ArrowComponent` 实体碰到障碍物 → 销毁（`World.DestroyEntity`）
- `MonsterProjectileComponent` 实体碰到障碍物 → 销毁

### RenderSystem — 障碍物渲染

为有 `ObstacleComponent` 且配置了精灵图的障碍物创建 Sprite2D 节点。无精灵的障碍物仅有碰撞体，不渲染。

## BattleScene 集成

在 `_Ready()` 中，`InitializeWorld()` 之前：

```csharp
// 加载地图
MapLoader.LoadAll();
var mapConfig = MapLoader.PickRandom();
MapLoader.ApplyToScene(mapConfig, this);
// ... InitializeWorld() 中调用 MapLoader.SpawnObstacles(mapConfig, _world)
```

## 文件布局

```
client/
├── Data/
│   └── Maps/
│       ├── plain.json
│       ├── mountain.json
│       └── grassland.json
├── Scripts/
│   ├── Data/
│   │   └── MapConfig.cs
│   ├── Game/
│   │   └── MapLoader.cs
│   └── Ecs/
│       └── Components/
│           └── ObstacleComponent.cs
└── Assets/
    └── Sprites/
        ├── Scenes/          # 背景图（已有）
        └── Props/           # 障碍物精灵图
```

## 设计决策

| 决策 | 选择 | 理由 |
|------|------|------|
| 配置格式 | JSON | 数据驱动，与 ECS 架构一致，服务端可复用 |
| 障碍物形状 | 仅 AABB 矩形 | 简单高效，当前需求足够 |
| 碰撞处理 | 最小穿透轴推回 | 标准 AABB 碰撞响应，实现简单 |
| 地图选择 | 随机 | 先做随机，后续可扩展玩家选择 |
| 障碍物影响 | 玩家+怪物+箭矢均阻挡 | 增加战术深度 |
