# Map Loading Module Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add map loading to BattleScene — each map has a background image + AABB obstacle rectangles that block players, monsters, and projectiles.

**Architecture:** JSON config files define maps (background + obstacles). MapLoader reads them, picks randomly, spawns obstacle entities. MovementSystem pushes entities out of obstacles. CollisionSystem destroys projectiles on obstacle contact. MonsterAISystem adjusts directions to avoid obstacles.

**Tech Stack:** Godot 4.6 C#, custom ECS, System.Text.Json

**Spec:** `docs/superpowers/specs/2026-04-17-map-loading-design.md`

---

### Task 1: ObstacleComponent + CollisionLayers.Obstacle

**Files:**
- Create: `client/Scripts/Ecs/Components/ObstacleComponent.cs`
- Modify: `client/Scripts/Ecs/Components/ColliderComponent.cs`

- [ ] **Step 1: Create ObstacleComponent**

```csharp
// client/Scripts/Ecs/Components/ObstacleComponent.cs
namespace Game.Ecs.Components;

/// <summary>Marker: entity is a static obstacle (impassable AABB).</summary>
public class ObstacleComponent { }
```

- [ ] **Step 2: Add Obstacle to CollisionLayers**

In `client/Scripts/Ecs/Components/ColliderComponent.cs`, add after `MonsterArrow = 16;`:

```csharp
public const int Obstacle    = 32; // Static obstacles; blocks players, monsters, and projectiles
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build client/client.csproj`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add client/Scripts/Ecs/Components/ObstacleComponent.cs client/Scripts/Ecs/Components/ColliderComponent.cs
git commit -m "feat: add ObstacleComponent and Obstacle collision layer"
```

---

### Task 2: MapConfig data model

**Files:**
- Create: `client/Scripts/Data/MapConfig.cs`

- [ ] **Step 1: Create MapConfig and ObstacleConfig classes**

```csharp
// client/Scripts/Data/MapConfig.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Game.Data;

public class ObstacleConfig
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("w")]
    public float W { get; set; }

    [JsonPropertyName("h")]
    public float H { get; set; }

    [JsonPropertyName("sprite")]
    public string Sprite { get; set; } = "";
}

public class MapConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("background")]
    public string Background { get; set; } = "";

    [JsonPropertyName("obstacles")]
    public List<ObstacleConfig> Obstacles { get; set; } = new();
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build client/client.csproj`
Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add client/Scripts/Data/MapConfig.cs
git commit -m "feat: add MapConfig and ObstacleConfig data models"
```

---

### Task 3: JSON map config files

**Files:**
- Create: `client/Data/Maps/plain.json`
- Create: `client/Data/Maps/mountain.json`
- Create: `client/Data/Maps/grassland.json`

- [ ] **Step 1: Create plain.json**

Arena is 1980×1280. Place a few rocks/obstacles. Obstacles should NOT overlap spawn edges (monsters spawn from edges).

```json
{
  "id": "plain",
  "background": "res://Assets/Sprites/Scenes/battle_bg_plain.png",
  "obstacles": [
    { "x": 500, "y": 400, "w": 120, "h": 80, "sprite": "" },
    { "x": 1300, "y": 350, "w": 100, "h": 100, "sprite": "" },
    { "x": 900, "y": 800, "w": 150, "h": 60, "sprite": "" }
  ]
}
```

- [ ] **Step 2: Create mountain.json**

```json
{
  "id": "mountain",
  "background": "res://Assets/Sprites/Scenes/battle_bg_mountain.png",
  "obstacles": [
    { "x": 300, "y": 500, "w": 140, "h": 100, "sprite": "" },
    { "x": 800, "y": 200, "w": 100, "h": 120, "sprite": "" },
    { "x": 1400, "y": 700, "w": 180, "h": 80, "sprite": "" },
    { "x": 1000, "y": 600, "w": 80, "h": 80, "sprite": "" }
  ]
}
```

- [ ] **Step 3: Create grassland.json**

```json
{
  "id": "grassland",
  "background": "res://Assets/Sprites/Scenes/battle_bg_grassland.png",
  "obstacles": [
    { "x": 600, "y": 300, "w": 100, "h": 80, "sprite": "" },
    { "x": 1100, "y": 500, "w": 120, "h": 90, "sprite": "" },
    { "x": 400, "y": 800, "w": 80, "h": 120, "sprite": "" },
    { "x": 1500, "y": 400, "w": 110, "h": 70, "sprite": "" },
    { "x": 950, "y": 950, "w": 90, "h": 90, "sprite": "" }
  ]
}
```

- [ ] **Step 4: Commit**

```bash
git add client/Data/Maps/
git commit -m "feat: add map JSON config files (plain, mountain, grassland)"
```

---

### Task 4: MapLoader — load, pick, spawn

**Files:**
- Create: `client/Scripts/Game/MapLoader.cs`

- [ ] **Step 1: Implement MapLoader**

```csharp
// client/Scripts/Game/MapLoader.cs
using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using Game.Data;
using Game.Ecs;
using Game.Ecs.Core;
using Game.Ecs.Components;

namespace Game;

/// <summary>
/// Loads map JSON configs, picks a random map, and spawns obstacle entities.
/// </summary>
public static class MapLoader
{
    private static readonly List<MapConfig> _maps = new();
    private static readonly Random _rng = new();

    private static readonly string[] MapFiles = { "plain", "mountain", "grassland" };

    public static void LoadAll()
    {
        _maps.Clear();
        foreach (var name in MapFiles)
        {
            var file = FileAccess.Open($"res://Data/Maps/{name}.json", FileAccess.ModeFlags.Read);
            if (file == null) continue;
            var json = file.GetAsText();
            file.Close();
            var config = JsonSerializer.Deserialize<MapConfig>(json);
            if (config != null)
                _maps.Add(config);
        }
    }

    public static MapConfig PickRandom()
    {
        if (_maps.Count == 0)
            throw new InvalidOperationException("No maps loaded. Call LoadAll() first.");
        return _maps[_rng.Next(_maps.Count)];
    }

    /// <summary>
    /// Replace the Background ColorRect with a Sprite2D showing the map background.
    /// </summary>
    public static void ApplyBackground(MapConfig map, Node2D sceneRoot)
    {
        var bg = sceneRoot.GetNodeOrNull<ColorRect>("Background");
        if (bg != null)
        {
            bg.Visible = false;
        }

        var bgSprite = new Sprite2D();
        bgSprite.Texture = GD.Load<Texture2D>(map.Background);
        bgSprite.Centered = false;
        bgSprite.Name = "BackgroundSprite";
        // Insert at index 0 so it renders behind everything
        sceneRoot.AddChild(bgSprite);
        sceneRoot.MoveChild(bgSprite, 0);
    }

    /// <summary>
    /// Create an ECS entity for each obstacle in the map config.
    /// </summary>
    public static void SpawnObstacles(MapConfig map, World world)
    {
        foreach (var obs in map.Obstacles)
        {
            var entity = world.CreateEntity();
            entity.Add(new TransformComponent
            {
                Position = new Vec2(obs.X + obs.W / 2f, obs.Y + obs.H / 2f)
            });
            entity.Add(new ColliderComponent
            {
                Shape = ColliderShape.Box,
                HalfWidth = obs.W / 2f,
                HalfHeight = obs.H / 2f,
                Layer = CollisionLayers.Obstacle,
                Mask = 0
            });
            entity.Add(new ObstacleComponent());
        }
    }
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build client/client.csproj`
Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add client/Scripts/Game/MapLoader.cs
git commit -m "feat: implement MapLoader (load JSON, random pick, spawn obstacles)"
```

---

### Task 5: BattleScene integration

**Files:**
- Modify: `client/Scripts/UI/BattleScene.cs`

- [ ] **Step 1: Add map loading to _Ready()**

Add a `MapConfig _currentMap;` field. In `_Ready()`, before `InitializeWorld()`:

```csharp
MapLoader.LoadAll();
_currentMap = MapLoader.PickRandom();
MapLoader.ApplyBackground(_currentMap, this);
```

- [ ] **Step 2: Spawn obstacles in InitializeWorld()**

After creating the wave spawner entity and before registering systems, add:

```csharp
MapLoader.SpawnObstacles(_currentMap, _world);
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build client/client.csproj`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add client/Scripts/UI/BattleScene.cs
git commit -m "feat: integrate MapLoader into BattleScene"
```

---

### Task 6: MovementSystem — obstacle pushback

**Files:**
- Modify: `client/Scripts/Ecs/Systems/MovementSystem.cs`

- [ ] **Step 1: Add obstacle pushback after movement**

After the existing per-type post-move rules (player clamp, arrow destroy, etc.), add an obstacle pushback pass for players and monsters. Add this method to MovementSystem:

```csharp
/// <summary>
/// Push entity out of any overlapping obstacles using minimum penetration axis.
/// entityHW/entityHH are the entity's half-extents (for Box) or radius used as half-extents.
/// </summary>
private void PushOutOfObstacles(TransformComponent transform, float entityHW, float entityHH)
{
    var obstacles = World.GetEntitiesWith<ObstacleComponent, TransformComponent, ColliderComponent>();
    foreach (var obs in obstacles)
    {
        var obsTransform = obs.Get<TransformComponent>();
        var obsCollider = obs.Get<ColliderComponent>();

        // AABB overlap test (obstacles have no rotation)
        float dx = transform.Position.X - obsTransform.Position.X;
        float dy = transform.Position.Y - obsTransform.Position.Y;
        float overlapX = entityHW + obsCollider.HalfWidth - GMath.Abs(dx);
        float overlapY = entityHH + obsCollider.HalfHeight - GMath.Abs(dy);

        if (overlapX <= 0 || overlapY <= 0) continue;

        // Push along the axis with least penetration
        if (overlapX < overlapY)
        {
            float sign = dx >= 0 ? 1f : -1f;
            transform.Position = new Vec2(transform.Position.X + overlapX * sign, transform.Position.Y);
        }
        else
        {
            float sign = dy >= 0 ? 1f : -1f;
            transform.Position = new Vec2(transform.Position.X, transform.Position.Y + overlapY * sign);
        }
    }
}
```

- [ ] **Step 2: Call pushback for players**

Inside the `if (entity.Has<PlayerComponent>())` block, after the arena clamp, add:

```csharp
var playerCollider = entity.Get<ColliderComponent>();
if (playerCollider != null)
    PushOutOfObstacles(transform, playerCollider.HalfWidth, playerCollider.HalfHeight);
```

- [ ] **Step 3: Call pushback for monsters**

After the existing `// Monsters: no clamping` comment, replace it with:

```csharp
else if (entity.Has<MonsterComponent>())
{
    var monsterCollider = entity.Get<ColliderComponent>();
    if (monsterCollider != null)
    {
        float hw = monsterCollider.Shape == ColliderShape.Circle ? monsterCollider.Radius : monsterCollider.HalfWidth;
        float hh = monsterCollider.Shape == ColliderShape.Circle ? monsterCollider.Radius : monsterCollider.HalfHeight;
        PushOutOfObstacles(transform, hw, hh);
    }
}
```

- [ ] **Step 4: Build to verify**

Run: `dotnet build client/client.csproj`
Expected: 0 errors

- [ ] **Step 5: Commit**

```bash
git add client/Scripts/Ecs/Systems/MovementSystem.cs
git commit -m "feat: add obstacle pushback for players and monsters in MovementSystem"
```

---

### Task 7: CollisionSystem — projectile vs obstacle

**Files:**
- Modify: `client/Scripts/Ecs/Systems/CollisionSystem.cs`

- [ ] **Step 1: Add CheckProjectileVsObstacle method**

```csharp
private void CheckProjectileVsObstacle()
{
    var obstacles = World.GetEntitiesWith<ObstacleComponent, TransformComponent, ColliderComponent>();
    if (obstacles.Count == 0) return;

    // Player arrows
    var arrows = World.GetEntitiesWith<ArrowComponent, TransformComponent, ColliderComponent>();
    foreach (var arrow in arrows)
    {
        if (!arrow.IsAlive) continue;
        var at = arrow.Get<TransformComponent>();
        var ac = arrow.Get<ColliderComponent>();
        foreach (var obs in obstacles)
        {
            var ot = obs.Get<TransformComponent>();
            var oc = obs.Get<ColliderComponent>();
            if (Overlaps(ac, at, oc, ot))
            {
                World.DestroyEntity(arrow.Id);
                break;
            }
        }
    }

    // Monster projectiles
    var projectiles = World.GetEntitiesWith<MonsterProjectileComponent, TransformComponent, ColliderComponent>();
    foreach (var proj in projectiles)
    {
        if (!proj.IsAlive) continue;
        var pt = proj.Get<TransformComponent>();
        var pc = proj.Get<ColliderComponent>();
        foreach (var obs in obstacles)
        {
            var ot = obs.Get<TransformComponent>();
            var oc = obs.Get<ColliderComponent>();
            if (Overlaps(pc, pt, oc, ot))
            {
                World.DestroyEntity(proj.Id);
                break;
            }
        }
    }
}
```

- [ ] **Step 2: Call it from Update()**

In the `Update(float delta)` method, add after `CheckMonsterProjectileVsPlayer();`:

```csharp
CheckProjectileVsObstacle();
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build client/client.csproj`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add client/Scripts/Ecs/Systems/CollisionSystem.cs
git commit -m "feat: destroy projectiles on obstacle collision"
```

---

### Task 8: MonsterAISystem — obstacle avoidance

**Files:**
- Modify: `client/Scripts/Ecs/Systems/MonsterAISystem.cs`

- [ ] **Step 1: Add AdjustForObstacles helper method**

Add this method to MonsterAISystem. It checks if moving in `desiredDir` would collide with an obstacle, and if so, tries ±90° rotations:

```csharp
/// <summary>
/// If moving in desiredDir would overlap an obstacle, try ±90° to slide around it.
/// Returns the adjusted (or original) direction, normalized.
/// </summary>
private Vec2 AdjustForObstacles(Vec2 pos, Vec2 desiredDir, float speed, float delta, float entityRadius)
{
    var obstacles = World.GetEntitiesWith<ObstacleComponent, TransformComponent, ColliderComponent>();
    if (obstacles.Count == 0) return desiredDir;

    Vec2 predictedPos = pos + desiredDir * speed * delta;

    foreach (var obs in obstacles)
    {
        var ot = obs.Get<TransformComponent>();
        var oc = obs.Get<ColliderComponent>();

        // Check AABB overlap at predicted position
        float dx = GMath.Abs(predictedPos.X - ot.Position.X);
        float dy = GMath.Abs(predictedPos.Y - ot.Position.Y);
        if (dx < entityRadius + oc.HalfWidth && dy < entityRadius + oc.HalfHeight)
        {
            // Try +90° and -90°, pick the one closer to desiredDir's target
            Vec2 left = desiredDir.Rotated(GMath.Pi * 0.5f);
            Vec2 right = desiredDir.Rotated(-GMath.Pi * 0.5f);

            Vec2 leftPos = pos + left * speed * delta;
            Vec2 rightPos = pos + right * speed * delta;

            // Check which alternative doesn't collide (prefer the one that doesn't)
            bool leftBlocked = IsBlockedByObstacle(leftPos, entityRadius, obstacles);
            bool rightBlocked = IsBlockedByObstacle(rightPos, entityRadius, obstacles);

            if (!leftBlocked && !rightBlocked)
            {
                // Both free — pick the one closer to original direction (dot product)
                return left.Dot(desiredDir) >= right.Dot(desiredDir) ? left : right;
            }
            if (!leftBlocked) return left;
            if (!rightBlocked) return right;
            // Both blocked — stop
            return Vec2.Zero;
        }
    }

    return desiredDir;
}

private static bool IsBlockedByObstacle(Vec2 pos, float radius, System.Collections.Generic.List<Entity> obstacles)
{
    foreach (var obs in obstacles)
    {
        var ot = obs.Get<TransformComponent>();
        var oc = obs.Get<ColliderComponent>();
        float dx = GMath.Abs(pos.X - ot.Position.X);
        float dy = GMath.Abs(pos.Y - ot.Position.Y);
        if (dx < radius + oc.HalfWidth && dy < radius + oc.HalfHeight)
            return true;
    }
    return false;
}
```

- [ ] **Step 2: Apply obstacle avoidance to Slime chase**

In the `default:` (Slime) case, change from:
```csharp
velocity.Velocity = toPlayer * baseSpeed * speedMultiplier;
```
to:
```csharp
float slimeRadius = monster.Get<ColliderComponent>()?.Radius ?? 15f;
Vec2 adjustedDir = AdjustForObstacles(monsterTransform.Position, toPlayer, baseSpeed * speedMultiplier, delta, slimeRadius);
velocity.Velocity = adjustedDir * baseSpeed * speedMultiplier;
```

- [ ] **Step 3: Apply to Boss chase**

In the `case MonsterType.Boss:` case, change from:
```csharp
velocity.Velocity = toPlayer * velocity.Speed * speedMultiplier;
```
to:
```csharp
float bossRadius = monster.Get<ColliderComponent>()?.Radius ?? 40f;
Vec2 bossDir = AdjustForObstacles(monsterTransform.Position, toPlayer, velocity.Speed * speedMultiplier, delta, bossRadius);
velocity.Velocity = bossDir * velocity.Speed * speedMultiplier;
```

- [ ] **Step 4: Apply to Orc chase and charge**

In `UpdateOrc`, for the normal chase line:
```csharp
velocity.Velocity = toPlayer * baseSpeed * speedMul;
```
change to:
```csharp
velocity.Velocity = AdjustForObstacles(/* need monsterPos */) ...
```

To achieve this, modify `UpdateOrc` signature to also accept `Entity monster` and `float delta`. Then:

Normal chase:
```csharp
float orcRadius = monster.Get<ColliderComponent>()?.Radius ?? 22f;
Vec2 orcPos = monster.Get<TransformComponent>().Position;
Vec2 orcDir = AdjustForObstacles(orcPos, toPlayer, baseSpeed * speedMul, delta, orcRadius);
velocity.Velocity = orcDir * baseSpeed * speedMul;
```

Charge:
```csharp
float orcRadius2 = monster.Get<ColliderComponent>()?.Radius ?? 22f;
Vec2 orcPos2 = monster.Get<TransformComponent>().Position;
Vec2 chargeDir = AdjustForObstacles(orcPos2, toPlayer, MonsterData.OrcChargeSpeed * speedMul, delta, orcRadius2);
velocity.Velocity = chargeDir * MonsterData.OrcChargeSpeed * speedMul;
```

- [ ] **Step 5: Apply to Skeleton/Elite wander**

In `UpdateSkeletonRanged`, the wander line:
```csharp
velocity.Velocity = ai.WanderDir * baseSpeed * speedMul;
```
change to:
```csharp
float skelRadius = monster.Get<ColliderComponent>()?.Radius ?? 18f;
Vec2 skelPos = monster.Get<TransformComponent>().Position;
Vec2 skelDir = AdjustForObstacles(skelPos, ai.WanderDir, baseSpeed * speedMul, delta, skelRadius);
velocity.Velocity = skelDir * baseSpeed * speedMul;
```

Same pattern for `UpdateEliteRanged` wander velocity line.

- [ ] **Step 6: Update method signatures**

`UpdateOrc` needs `Entity monster` and `float delta` parameters added. Update the call site at line ~89 to pass them.

`UpdateSkeletonRanged` and `UpdateEliteRanged` already receive `Entity monster` — just need `delta` added to their signatures and call sites.

- [ ] **Step 7: Build to verify**

Run: `dotnet build client/client.csproj`
Expected: 0 errors

- [ ] **Step 8: Commit**

```bash
git add client/Scripts/Ecs/Systems/MonsterAISystem.cs
git commit -m "feat: add obstacle avoidance to monster AI (slide around AABB obstacles)"
```

---

### Task 9: RenderSystem — obstacle visual rendering

**Files:**
- Modify: `client/Scripts/Ecs/ClientSystems/RenderSystem.cs`

- [ ] **Step 1: Add obstacle visual creation in CreateVisualNode**

Before the `return null;` at the end of `CreateVisualNode`, add:

```csharp
else if (entity.Has<ObstacleComponent>())
{
    var collider = entity.Get<ColliderComponent>();
    var rect = new ColorRect();
    rect.Color = new Color(0.3f, 0.25f, 0.2f, 0.8f); // dark brown, semi-transparent
    float w = collider.HalfWidth * 2;
    float h = collider.HalfHeight * 2;
    rect.Size = new Vector2(w, h);
    rect.Position = new Vector2(-collider.HalfWidth, -collider.HalfHeight);
    wrapper.AddChild(rect);
    return wrapper;
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build client/client.csproj`
Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add client/Scripts/Ecs/ClientSystems/RenderSystem.cs
git commit -m "feat: render obstacles as colored rectangles"
```

---

### Task 10: Final build and smoke test

- [ ] **Step 1: Full build**

Run: `dotnet build client/client.csproj`
Expected: 0 errors, 0 warnings related to new code

- [ ] **Step 2: Verify all files exist**

```bash
ls -la client/Scripts/Ecs/Components/ObstacleComponent.cs
ls -la client/Scripts/Data/MapConfig.cs
ls -la client/Scripts/Game/MapLoader.cs
ls -la client/Data/Maps/plain.json
ls -la client/Data/Maps/mountain.json
ls -la client/Data/Maps/grassland.json
```

- [ ] **Step 3: Final commit if any fixes needed**

```bash
git status
# If clean, done. Otherwise fix and commit.
```
