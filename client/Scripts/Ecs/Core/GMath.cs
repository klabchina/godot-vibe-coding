using System;

namespace Game.Ecs.Core;

/// <summary>
/// Engine-independent math utilities. Replaces Godot.Mathf in shared logic.
/// </summary>
public static class GMath
{
    public const float Pi  = MathF.PI;
    public const float Tau = MathF.PI * 2f;

    public static float Sin(float x) => MathF.Sin(x);
    public static float Cos(float x) => MathF.Cos(x);

    public static float Sqrt(float x) => MathF.Sqrt(x);
    public static float Abs(float x)  => MathF.Abs(x);

    public static float Min(float a, float b) => a < b ? a : b;
    public static float Max(float a, float b) => a > b ? a : b;
    public static int   Min(int a, int b)     => a < b ? a : b;
    public static int   Max(int a, int b)     => a > b ? a : b;

    public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    public static int   Clamp(int v, int min, int max)       => v < min ? min : v > max ? max : v;

    public static float DegToRad(float deg) => deg * (Pi / 180f);
    public static float RadToDeg(float rad) => rad * (180f / Pi);

    public static int CeilToInt(float x) => (int)MathF.Ceiling(x);
}
