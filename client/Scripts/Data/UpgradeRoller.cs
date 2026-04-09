using System;
using System.Collections.Generic;
using System.Linq;
using Game.Ecs.Components;

namespace Game.Data;

/// <summary>
/// Rolls 3 random upgrade options per the weighted category system.
/// </summary>
public static class UpgradeRoller
{
    private static readonly Random _rng = new();

    /// <summary>
    /// Roll 3 unique upgrade options for the given player's current upgrade state.
    /// </summary>
    public static List<UpgradeId> Roll(UpgradeComponent upgrades, int playerLevel)
    {
        var available = GetAvailableUpgrades(upgrades);
        if (available.Count == 0) return new List<UpgradeId>();

        var result = new List<UpgradeId>();
        bool guaranteeAttack = playerLevel <= UpgradeData.GuaranteeAttackUntilLevel;

        for (int i = 0; i < UpgradeData.ChoiceCount && available.Count > 0; i++)
        {
            UpgradeId pick;

            // Guarantee at least 1 attack upgrade in first slot for early levels
            if (i == 0 && guaranteeAttack)
            {
                var attackOptions = available
                    .Where(id => UpgradeData.Definitions[id].Category == UpgradeCategory.Attack)
                    .ToList();

                if (attackOptions.Count > 0)
                {
                    pick = attackOptions[_rng.Next(attackOptions.Count)];
                }
                else
                {
                    pick = PickWeighted(available);
                }
            }
            else
            {
                pick = PickWeighted(available);
            }

            result.Add(pick);
            available.Remove(pick);
        }

        return result;
    }

    private static UpgradeId PickWeighted(List<UpgradeId> available)
    {
        // Group by category and assign weights
        var attackIds  = available.Where(id => UpgradeData.Definitions[id].Category == UpgradeCategory.Attack).ToList();
        var defenseIds = available.Where(id => UpgradeData.Definitions[id].Category == UpgradeCategory.Defense).ToList();
        var specialIds = available.Where(id => UpgradeData.Definitions[id].Category == UpgradeCategory.Special).ToList();

        // Build weighted list: category weight / items in category = per-item weight
        var weighted = new List<(UpgradeId id, float weight)>();
        if (attackIds.Count > 0)
            foreach (var id in attackIds)
                weighted.Add((id, UpgradeData.AttackWeight / attackIds.Count));
        if (defenseIds.Count > 0)
            foreach (var id in defenseIds)
                weighted.Add((id, UpgradeData.DefenseWeight / defenseIds.Count));
        if (specialIds.Count > 0)
            foreach (var id in specialIds)
                weighted.Add((id, UpgradeData.SpecialWeight / specialIds.Count));

        float totalWeight = weighted.Sum(w => w.weight);
        float roll = (float)(_rng.NextDouble() * totalWeight);
        float cumulative = 0;

        foreach (var (id, weight) in weighted)
        {
            cumulative += weight;
            if (roll <= cumulative) return id;
        }

        return weighted.Last().id;
    }

    private static List<UpgradeId> GetAvailableUpgrades(UpgradeComponent upgrades)
    {
        var available = new List<UpgradeId>();
        foreach (var (id, def) in UpgradeData.Definitions)
        {
            if (upgrades.GetLevel(id) < def.MaxLevel)
                available.Add(id);
        }
        return available;
    }
}
