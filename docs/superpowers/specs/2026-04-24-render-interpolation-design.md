# 客户端渲染插值设计

## 概述

客户端以 60fps 渲染，但服务器逻辑以 20tick/s（50ms/帧）运行。为消除卡顿感，采用**帧间插值**技术——在两个逻辑帧之间线性插值平滑过渡。

## 核心变量

| 变量 | 类型 | 说明 |
|------|------|------|
| `_currentDelta` | float | 每帧 Update 开始后累加 delta，范围 [0, ServerFrameTime] |
| `_prevLogicPositions[id]` | Vector2 | 插值起点（上一逻辑帧的渲染位置） |
| `_renderPositions[id]` | Vector2 | 插值终点（目标逻辑位置） |
| `ServerFrameTime` | const float = 0.05f | 服务器帧率 20tick/s |

## 逻辑帧检测

每帧判断 `transform.Position` 是否等于 `_renderPositions[id]`：
- **不相等**：服务器发来了新逻辑帧，更新 `_prevLogicPositions` = `_renderPositions`，`_renderPositions` = target，重置 `_currentDelta = 0`
- **相等**：仍在当前逻辑帧内，执行插值

## 插值公式

```
t = clamp(_currentDelta / ServerFrameTime, 0, 1)
node.Position = lerp(_prevLogicPositions, _renderPositions, t)
```

即：
- t=0：渲染位置 = 起点（上一帧位置）
- t=0.5：渲染位置 = 起点和终点的中点
- t=1：渲染位置 = 终点（目标位置）

## 旋转插值

同样使用线性插值，并处理角度跨越 ±π 的情况（归一化到 [-π, π]）。

## 时序图

```
时间(ms):    0    16   32   48   64   80   96  112  128  144  160
客户端帧:     0    1    2    3    4    5    6    7    8    9    10
服务器帧:        0         1         2         3         4
_currentDelta: 0→16→32→48→0→16→32→48→0→16→32→48→0→16→32
t值:          0→.3→.6→1→0→.3→.6→1→0→.3→.6→1→0→.3→.6→1
```

## 性能考虑

- 插值计算在主循环中遍历所有实体，开销 O(n)
- `_currentDelta` 为单例，不随实体数量增加
- 无需额外内存分配，纯算术运算