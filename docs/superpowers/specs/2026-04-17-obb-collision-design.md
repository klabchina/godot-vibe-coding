# OBB 矩形碰撞支持设计

日期: 2026-04-17

## 背景

当前碰撞系统仅支持圆形碰撞（ColliderComponent.Radius），需要增加 OBB（定向包围盒）矩形碰撞支持，主要用于怪物弹幕/激光等有方向的投射物，以及所有怪物可选使用矩形碰撞体。

## 设计

### 1. ColliderComponent 增加碰撞类型

```csharp
public enum ColliderShape { Circle, Box }

public class ColliderComponent
{
    public ColliderShape Shape = ColliderShape.Circle;
    public float Radius;          // Circle 用
    public float HalfWidth;       // Box 用 (宽度的一半)
    public float HalfHeight;      // Box 用 (高度的一半)
    public int Layer;
    public int Mask;
}
```

Box 碰撞使用 OBB，旋转角度从 TransformComponent.Rotation 获取。

### 2. CollisionSystem 碰撞判定

提取静态方法 `Overlaps(ColliderComponent a, TransformComponent ta, ColliderComponent b, TransformComponent tb)` 处理四种组合：
- Circle vs Circle → 原有距离判定
- Circle vs Box → 圆与 OBB 最近点距离
- Box vs Box → SAT（分离轴定理）
- Box vs Circle → 对称调用

三个 Check 方法统一调用 `Overlaps` 替代手写距离判定。

### 3. MonsterData 扩展

MonsterBase record 增加可选参数：
- `ColliderShape Shape = ColliderShape.Circle`
- `int HalfWidth = 0`
- `int HalfHeight = 0`

默认圆形，向后兼容。需要矩形的怪物/弹幕创建时填上 Box 参数。

### 4. 影响文件

| 文件 | 改动 |
|------|------|
| `ColliderComponent.cs` | 增加 ColliderShape 枚举和 HalfWidth/HalfHeight 字段 |
| `CollisionSystem.cs` | 统一 Overlaps 判定函数，替换三处距离判定 |
| `MonsterData.cs` | MonsterBase record 增加可选碰撞形状字段 |
