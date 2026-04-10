using System;

namespace Game.Ecs.Core;

/// <summary>
/// Engine-independent 2D vector. Drop-in replacement for Godot.Vector2 in shared logic.
/// </summary>
public struct Vec2 : IEquatable<Vec2>
{
    public float X;
    public float Y;

    public Vec2(float x, float y) { X = x; Y = y; }

    public static readonly Vec2 Zero = new(0, 0);
    public static readonly Vec2 One  = new(1, 1);

    public float Length()        => MathF.Sqrt(X * X + Y * Y);
    public float LengthSquared() => X * X + Y * Y;

    public Vec2 Normalized()
    {
        float len = Length();
        return len > 1e-6f ? new Vec2(X / len, Y / len) : Zero;
    }

    public float DistanceTo(Vec2 other)
    {
        float dx = X - other.X;
        float dy = Y - other.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    public float Angle() => MathF.Atan2(Y, X);

    public Vec2 Rotated(float angleRad)
    {
        float cos = MathF.Cos(angleRad);
        float sin = MathF.Sin(angleRad);
        return new Vec2(X * cos - Y * sin, X * sin + Y * cos);
    }

    // Operators
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 v, float s) => new(v.X * s, v.Y * s);
    public static Vec2 operator *(float s, Vec2 v) => new(v.X * s, v.Y * s);
    public static Vec2 operator /(Vec2 v, float s) => new(v.X / s, v.Y / s);
    public static Vec2 operator -(Vec2 v)          => new(-v.X, -v.Y);

    public static bool operator ==(Vec2 a, Vec2 b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(Vec2 a, Vec2 b) => !(a == b);

    public bool Equals(Vec2 other) => X == other.X && Y == other.Y;
    public override bool Equals(object obj) => obj is Vec2 v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X:F2}, {Y:F2})";
}
