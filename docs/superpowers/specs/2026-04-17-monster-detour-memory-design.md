# 怪物绕行记忆（Detour Memory）设计

## 问题

当怪物和玩家之间有障碍物时，当前 `AdjustForObstacles` 只做单帧预判：检测下一帧是否撞障碍物，撞了就尝试 ±90° 滑动。但每帧都重新计算 toPlayer 方向，导致怪物反复尝试直线→被挡→转向→下一帧又直线，原地震荡无法绕过障碍物。

## 方案：绕行记忆

怪物检测到直线路径被障碍物遮挡时进入"绕行模式"，记住选择的绕行方向并持续执行，直到路径畅通。

## 数据结构变更

`MonsterAIState` 新增字段：

```csharp
public bool IsDetouring;         // 是否在绕行中
public Vec2 DetourDir;           // 绕行方向（持续使用，不每帧重算）
public float DetourTimer;        // 绕行持续时间（防止无限绕行，超时重选）
```

## 算法流程

```
每帧更新:
1. 计算 toPlayer 方向
2. 用 AABB 线段相交检测 toPlayer 方向是否被障碍物遮挡
   - 从怪物位置沿 toPlayer 方向到玩家位置，检查是否穿过任何障碍物扩展 AABB
3. 如果路径畅通:
   - 清除绕行状态 (IsDetouring = false)
   - 正常朝玩家移动（原有逻辑）
4. 如果路径被挡:
   a. 如果未在绕行中 → 进入绕行模式:
      - 选择 ±90° 中更优的方向（障碍物偏左→往右绕，反之往左）
      - 记录 DetourDir, DetourTimer = 2s
   b. 如果已在绕行中:
      - DetourTimer -= delta
      - 如果超时 → 重新选择绕行方向（可能换方向）
      - 否则继续沿 DetourDir 移动
5. 最终移动方向仍通过 AdjustForObstacles 做单帧碰撞安全检查
```

## 遮挡检测：AABB 线段相交

```csharp
/// 检测从 start 到 end 的线段是否与任何障碍物扩展 AABB 相交
bool IsPathBlockedByObstacles(Vec2 start, Vec2 end, float entityRadius)
```

对每个障碍物，将其 AABB 扩展 entityRadius（Minkowski 和），然后用标准 AABB 线段相交算法检测。

## 智能选向

选择绕行方向时：
- 计算阻挡障碍物中心相对于 怪物→玩家 连线的位置（叉积符号）
- 障碍物偏左 → 怪物往右绕（toPlayer 逆时针旋转 90°）
- 障碍物偏右 → 怪物往左绕（toPlayer 顺时针旋转 90°）
- 两个方向都被挡 → 尝试更大角度（±135°），都不行则停止

## 影响的怪物类型

| 类型 | 追踪阶段 | 是否使用绕行记忆 |
|------|----------|-----------------|
| Slime | 直线追踪 | 是 |
| Boss | 直线追踪 | 是 |
| Orc | 追踪 + 冲锋 | 追踪和冲锋阶段都使用 |
| Skeleton | 游荡 + 远程攻击 | 游荡阶段不需要（不追玩家） |
| Elite | 游荡 + 远程攻击 | 游荡阶段不需要（不追玩家） |

## 超时保护

- DetourTimer 默认 2 秒
- 超时后重新评估路径，选择新的绕行方向
- 防止怪物绕着一个方向兜死圈

## 修改范围

| 文件 | 变更 |
|------|------|
| `MonsterAIState.cs` | 新增 IsDetouring, DetourDir, DetourTimer 字段 |
| `MonsterAISystem.cs` | 新增 `IsPathBlockedByObstacles` 方法 |
| `MonsterAISystem.cs` | 新增 `GetDetourDirection` 选向方法 |
| `MonsterAISystem.cs` | 修改 Slime/Boss/Orc 追踪逻辑，插入绕行记忆判断 |
| `MonsterAISystem.cs` | 保留原有 `AdjustForObstacles` 作为最终碰撞安全检查 |
