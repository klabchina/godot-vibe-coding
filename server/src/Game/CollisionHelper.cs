namespace Server.Game;

/// <summary>
/// 碰撞检测辅助类
/// </summary>
public static class CollisionHelper
{
    /// <summary>
    /// 检测两个圆形是否碰撞
    /// </summary>
    public static bool CircleCollision(
        float x1, float y1, float r1,
        float x2, float y2, float r2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        var distSq = dx * dx + dy * dy;
        var radiusSum = r1 + r2;
        return distSq <= radiusSum * radiusSum;
    }
    
    /// <summary>
    /// 检测点是否在圆内
    /// </summary>
    public static bool PointInCircle(float px, float py, float cx, float cy, float r)
    {
        var dx = px - cx;
        var dy = py - cy;
        return dx * dx + dy * dy <= r * r;
    }
    
    /// <summary>
    /// 计算两点距离
    /// </summary>
    public static float Distance(float x1, float y1, float x2, float y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
    
    /// <summary>
    /// 计算距离平方（避免开方）
    /// </summary>
    public static float DistanceSq(float x1, float y1, float x2, float y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return dx * dx + dy * dy;
    }
    
    /// <summary>
    /// 获取方向向量
    /// </summary>
    public static (float X, float Y) Normalize(float x, float y)
    {
        var len = MathF.Sqrt(x * x + y * y);
        if (len < 0.0001f) return (0, 0);
        return (x / len, y / len);
    }
    
    /// <summary>
    /// 限制向量长度
    /// </summary>
    public static (float X, float Y) ClampLength(float x, float y, float maxLen)
    {
        var lenSq = x * x + y * y;
        if (lenSq > maxLen * maxLen)
        {
            var len = MathF.Sqrt(lenSq);
            return (x / len * maxLen, y / len * maxLen);
        }
        return (x, y);
    }
    
    /// <summary>
    /// 边界检测：限制位置在矩形区域内
    /// </summary>
    public static (float X, float Y) ClampToRect(
        float x, float y,
        float minX, float minY,
        float maxX, float maxY)
    {
        return (
            Math.Clamp(x, minX, maxX),
            Math.Clamp(y, minY, maxY)
        );
    }
    
    /// <summary>
    /// 边界检测：限制位置在矩形区域内（带半径）
    /// </summary>
    public static (float X, float Y) ClampToRectWithRadius(
        float x, float y, float radius,
        float minX, float minY,
        float maxX, float maxY)
    {
        return (
            Math.Clamp(x, minX + radius, maxX - radius),
            Math.Clamp(y, minY + radius, maxY - radius)
        );
    }
    
    /// <summary>
    /// 计算角度（弧度）
    /// </summary>
    public static float Angle(float x, float y) => MathF.Atan2(y, x);
    
    /// <summary>
    /// 从角度计算方向向量
    /// </summary>
    public static (float X, float Y) DirFromAngle(float angle) => 
        (MathF.Cos(angle), MathF.Sin(angle));
}
