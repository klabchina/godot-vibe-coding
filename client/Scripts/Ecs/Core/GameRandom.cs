namespace Game.Ecs.Core;

/// <summary>
/// Engine-independent random number generator. Replaces GD.RandRange / GD.Randf().
/// Single shared instance for deterministic simulation across runs.
/// Delegates to BlRNGUtil for hash-based deterministic random generation.
/// </summary>
public static class GameRandom
{
    /// <summary>获取随机数调用次数（用于调试）</summary>
    public static int CallCount => BlRNGUtil.CallCount;

    /// <summary>重置调用计数</summary>
    public static void ResetCallCount() => BlRNGUtil.ResetCallCount();

    /// <summary>
    /// Sets the random seed for deterministic simulation.
    /// Must be called on both server and client with the same seed before game starts.
    /// </summary>
    public static void SetSeed(int seed) => BlRNGUtil.SetSeed(seed);

    /// <summary>Returns a random float in [0, 1).</summary>
    public static float Randf() => BlRNGUtil.Randf();

    /// <summary>Returns a random double in [min, max].</summary>
    public static double RandRange(double min, double max) => BlRNGUtil.RandRange(min, max);

    /// <summary>Returns a random float in [min, max].</summary>
    public static float RandRangef(float min, float max) => BlRNGUtil.RandRangef(min, max);

    /// <summary>Returns a random int in [0, max).</summary>
    public static int Next(int max) => BlRNGUtil.Next(max);

    /// <summary>Returns a random double in [0, 1).</summary>
    public static double NextDouble() => BlRNGUtil.NextDouble();
}
