using System;

namespace Game.Ecs.Core;

/// <summary>
/// BlRNGUtil 包装类 - 适配 BlRNGUtil 提供与原 GameRandom 相同的 API 接口
/// 基于 MasonFramework 的 BlRNGUtil 随机数生成器，使用 hash-based 算法保证确定性
/// </summary>
public static class BlRNGUtil
{
    private static BlRNGUtilImpl _impl = new BlRNGUtilImpl(0);
    private static int _callCount = 0;

    /// <summary>获取随机数调用次数（用于调试）</summary>
    public static int CallCount => _callCount;

    /// <summary>重置调用计数</summary>
    public static void ResetCallCount() => _callCount = 0;

    /// <summary>
    /// Sets the random seed for deterministic simulation.
    /// Must be called on both server and client with the same seed before game starts.
    /// </summary>
    public static void SetSeed(int seed) => _impl.ResetSeed((uint)seed, 0);

    /// <summary>Returns a random float in [0, 1).</summary>
    public static float Randf() { _callCount++; return _impl.NextFloat(); }

    /// <summary>Returns a random double in [min, max].</summary>
    public static double RandRange(double min, double max) { _callCount++; return min + _impl.NextDouble() * (max - min); }

    /// <summary>Returns a random float in [min, max].</summary>
    public static float RandRangef(float min, float max) { _callCount++; return min + _impl.NextFloat() * (max - min); }

    /// <summary>Returns a random int in [0, max).</summary>
    public static int Next(int max) { _callCount++; return (int)_impl.RollRandomIntLessThan((uint)max); }

    /// <summary>Returns a random double in [0, 1).</summary>
    public static double NextDouble() { _callCount++; return _impl.NextDouble(); }

    /// <summary>
    /// 内部实现类 - 基于 MasonFramework BlRNGUtil 的 hash-based 随机数生成器
    /// </summary>
    private class BlRNGUtilImpl
    {
        private uint m_seed;
        private int m_position;

        public BlRNGUtilImpl(uint seed)
        {
            m_seed = seed;
            m_position = 0;
        }

        public void ResetSeed(uint seed, int position)
        {
            m_seed = seed;
            m_position = position;
        }

        public uint GetSeed() => m_seed;

        public int GetCurPos() => m_position;

        /// <summary>
        /// [0, 1) double - 使用 32-bit 整数转换，与 System.Random.NextDouble 兼容
        /// </summary>
        public double NextDouble()
        {
            uint val = RollRandomUInt32();
            return val / (double)0xFFFFFFFF;
        }

        /// <summary>
        /// [0, 1) float
        /// </summary>
        public float NextFloat()
        {
            uint val = RollRandomUInt32();
            return val / (float)0xFFFFFFFF;
        }

        /// <summary>
        /// 32-bit 无符号整数
        /// </summary>
        public uint RollRandomUInt32()
        {
            return (uint)GetNoiseHash((ulong)m_position++, m_seed);
        }

        /// <summary>
        /// [0, max) 整数
        /// </summary>
        public uint RollRandomIntLessThan(uint maxValueNotInclusive)
        {
            uint v = RollRandomUInt32();
            v = v % maxValueNotInclusive;
            return v;
        }

        private ulong GetNoiseHash(ulong position, ulong seed = 0)
        {
            const ulong BIT_NOISE1 = 0xB5297A4D;
            const ulong BIT_NOISE2 = 0x68E31DA4;
            const ulong BIT_NOISE3 = 0x1B56C4E9;
            ulong mangled = position;
            mangled *= BIT_NOISE1;
            mangled += seed;
            mangled ^= (mangled >> 8);
            mangled += BIT_NOISE2;
            mangled ^= (mangled << 8);
            mangled *= BIT_NOISE3;
            mangled ^= (mangled >> 8);
            return mangled;
        }
    }
}
