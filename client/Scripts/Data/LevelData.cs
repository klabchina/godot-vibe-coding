namespace Game.Data;

public static class LevelData
{
    public const int MaxLevel = 8;

    // XP required for each level (index 0 = Lv1)
    private static readonly int[] XpThresholds = { 40, 60, 90, 120, 160, 200, 260, 320 };

    // Cumulative XP for each level
    private static readonly int[] CumulativeXp = { 40, 100, 190, 310, 470, 670, 930, 1250 };

    /// <summary>Get XP needed for a specific level (1-based).</summary>
    public static int GetXpForLevel(int level)
    {
        if (level < 1)
        {
            return 0;
        }

        return XpThresholds[level - 1];
    }

    /// <summary>Get cumulative XP needed to reach a specific level (1-based).</summary>
    public static int GetCumulativeXp(int level)
    {
        if (level < 1)
        {
            return 0;
        }

        return CumulativeXp[level - 1];
    }

    /// <summary>Given total accumulated XP, return the current level (0 = not yet Lv1).</summary>
    public static int GetLevel(int totalXp)
    {
        for (int i = MaxLevel - 1; i >= 0; i--)
        {
            if (totalXp >= CumulativeXp[i])
                return i + 1;
        }
        return 0;
    }
}
