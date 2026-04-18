using System;

namespace Game.Ecs.Core;

/// <summary>
/// Engine-independent random number generator. Replaces GD.RandRange / GD.Randf().
/// Thread-safe via [ThreadStatic].
/// </summary>
public static class GameRandom
{
    [ThreadStatic] private static Random _rng;

    private static Random Rng => _rng ??= new Random();

    /// <summary>
    /// Sets the random seed for deterministic simulation.
    /// Must be called on both server and client with the same seed before game starts.
    /// </summary>
    public static void SetSeed(int seed) => _rng = new Random(seed);

    /// <summary>Returns a random float in [0, 1).</summary>
    public static float Randf() => (float)Rng.NextDouble();

    /// <summary>Returns a random double in [min, max].</summary>
    public static double RandRange(double min, double max) => min + Rng.NextDouble() * (max - min);

    /// <summary>Returns a random float in [min, max].</summary>
    public static float RandRangef(float min, float max) => min + (float)Rng.NextDouble() * (max - min);

    /// <summary>Returns a random int in [0, max).</summary>
    public static int Next(int max) => Rng.Next(max);

    /// <summary>Returns a random double in [0, 1).</summary>
    public static double NextDouble() => Rng.NextDouble();
}
