using System.Collections.Generic;
using Godot;
using Game.Data;
using Game.Ecs.Components;

namespace Game.UI;

/// <summary>
/// Displays acquired permanent upgrades as a row of colored icons in the HUD.
/// </summary>
public partial class UpgradeBar : HBoxContainer
{
    private readonly List<ColorRect> _icons = new();

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 4);
    }

    public void UpdateUpgrades(UpgradeComponent upgrade)
    {
        if (upgrade == null) return;

        // Collect active upgrade IDs with levels
        var active = new List<(UpgradeId id, int level)>();
        foreach (var (id, def) in UpgradeData.Definitions)
        {
            int level = upgrade.GetLevel(id);
            if (level > 0)
                active.Add((id, level));
        }

        // Sync icon count
        while (_icons.Count < active.Count)
        {
            var icon = new ColorRect();
            icon.CustomMinimumSize = new Vector2(20, 20);
            AddChild(icon);
            _icons.Add(icon);
        }
        while (_icons.Count > active.Count)
        {
            var last = _icons[^1];
            last.QueueFree();
            _icons.RemoveAt(_icons.Count - 1);
        }

        // Update colors
        for (int i = 0; i < active.Count; i++)
        {
            _icons[i].Color = GetUpgradeColor(active[i].id);
            _icons[i].TooltipText = $"{UpgradeData.Definitions[active[i].id].Name} Lv{active[i].level}";
        }
    }

    private static Color GetUpgradeColor(UpgradeId id)
    {
        return UpgradeData.Definitions[id].Category switch
        {
            UpgradeCategory.Attack => new Color(1f, 0.3f, 0.3f),   // red
            UpgradeCategory.Defense => new Color(0.3f, 0.7f, 1f),  // blue
            UpgradeCategory.Special => new Color(0.3f, 1f, 0.5f),  // green
            _ => Colors.White
        };
    }
}
