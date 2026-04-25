using Server.Proto;

namespace Server.Game;

/// <summary>
/// 箭矢状态
/// </summary>
public sealed class ArrowState
{
    private static int _nextId = 1;
    
    public int Id { get; init; }
    public string OwnerId { get; init; } = "";
    public bool IsPlayerArrow { get; init; }
    
    // 位置与速度
    public float X { get; set; }
    public float Y { get; set; }
    public float VX { get; set; }
    public float VY { get; set; }
    public float Rotation { get; set; }
    
    // 战斗属性
    public int Damage { get; set; }
    
    // 高级箭矢属性（怪物使用）
    public float Acceleration { get; set; }  // 加速度，非零表示追踪箭
    public float MaxSpeed { get; set; }
    
    // 生命周期
    public float LifeTime { get; set; }
    public float MaxLifeTime { get; set; } = 10f;  // 10秒后自动销毁
    public bool IsAlive => LifeTime < MaxLifeTime;
    
    public Vec2 Position => new() { X = X, Y = Y };
    public Vec2 Velocity => new() { X = VX, Y = VY };
    
    public ArrowState(string ownerId, bool isPlayerArrow, float x, float y, float vx, float vy)
    {
        Id = Interlocked.Increment(ref _nextId);
        OwnerId = ownerId;
        IsPlayerArrow = isPlayerArrow;
        X = x;
        Y = y;
        VX = vx;
        VY = vy;
        
        // 计算初始旋转角度
        Rotation = MathF.Atan2(vy, vx);
    }
    
    /// <summary>
    /// 更新位置
    /// </summary>
    public void Update(float dt)
    {
        LifeTime += dt;
        
        if (Acceleration > 0)
        {
            // 追踪箭：加速向目标方向
            var speed = MathF.Sqrt(VX * VX + VY * VY);
            if (speed < MaxSpeed)
            {
                speed += Acceleration * dt;
                speed = Math.Min(speed, MaxSpeed);
                var currentSpeed = MathF.Sqrt(VX * VX + VY * VY);
                if (currentSpeed > 0)
                {
                    VX = (VX / currentSpeed) * speed;
                    VY = (VY / currentSpeed) * speed;
                }
            }
        }
        
        X += VX * dt;
        Y += VY * dt;
        
        // 更新旋转角度
        if (VX != 0 || VY != 0)
        {
            Rotation = MathF.Atan2(VY, VX);
        }
    }
    
    public ArrowStateMsg ToMsg()
    {
        return new ArrowStateMsg
        {
            ArrowId = Id,
            OwnerId = OwnerId,
            Position = Position,
            Velocity = Velocity,
            Rotation = Rotation,
            Damage = Damage,
            IsPlayerArrow = IsPlayerArrow
        };
    }
    
    /// <summary>
    /// 重置 ID 计数器
    /// </summary>
    public static void ResetIdCounter() => _nextId = 0;
}
