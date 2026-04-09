using Godot;

namespace Game.UI;

/// <summary>
/// Result screen: displays battle stats and S/A/B/C grade.
/// Reads stats from GameManager.
/// </summary>
public partial class ResultUI : Control
{
    public override void _Ready()
    {
        var bg = new ColorRect();
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.Color = new Color(0.05f, 0.05f, 0.08f, 1f);
        AddChild(bg);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(LayoutPreset.Center);
        vbox.GrowHorizontal = GrowDirection.Both;
        vbox.GrowVertical = GrowDirection.Both;
        vbox.AddThemeConstantOverride("separation", 16);
        vbox.Position = new Vector2(-220, -220);
        AddChild(vbox);

        var gm = GameManager.Instance;

        // Title
        var title = new Label();
        title.Text = "BATTLE RESULT";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 40);
        vbox.AddChild(title);

        // Grade
        string grade = CalculateGrade(gm);
        var gradeLabel = new Label();
        gradeLabel.Text = $"Grade: {grade}";
        gradeLabel.HorizontalAlignment = HorizontalAlignment.Center;
        gradeLabel.AddThemeFontSizeOverride("font_size", 56);
        gradeLabel.AddThemeColorOverride("font_color", GetGradeColor(grade));
        vbox.AddChild(gradeLabel);

        // Stats
        AddStat(vbox, "Waves Completed", $"{gm.WavesCompleted} / 8");
        AddStat(vbox, "Kills", $"{gm.KillCount}");
        AddStat(vbox, "Total Damage", $"{gm.TotalDamage}");
        AddStat(vbox, "XP Collected", $"{gm.TotalXpCollected}");
        AddStat(vbox, "Remaining HP", $"{(gm.RemainingHpPercent * 100):F0}%");

        // Return button
        var returnBtn = new Button();
        returnBtn.Text = "Return to Menu";
        returnBtn.CustomMinimumSize = new Vector2(400, 60);
        returnBtn.Pressed += () => SceneManager.Instance.GoToMainMenu();
        vbox.AddChild(returnBtn);
    }

    private void AddStat(VBoxContainer parent, string label, string value)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 20);

        var nameLabel = new Label();
        nameLabel.Text = label;
        nameLabel.CustomMinimumSize = new Vector2(250, 0);
        hbox.AddChild(nameLabel);

        var valLabel = new Label();
        valLabel.Text = value;
        hbox.AddChild(valLabel);

        parent.AddChild(hbox);
    }

    private string CalculateGrade(GameManager gm)
    {
        // Scoring: wave progress (40) + kills (20) + damage (20) + survival (10) + xp (10) = 100
        float score = 0;

        // Wave progress: 40 pts for clearing all 8 waves
        score += (gm.WavesCompleted / 8f) * 40f;

        // Kills: up to 20 pts (100+ kills = max)
        score += Mathf.Min(gm.KillCount / 100f, 1f) * 20f;

        // Damage: up to 20 pts (5000+ = max)
        score += Mathf.Min(gm.TotalDamage / 5000f, 1f) * 20f;

        // Survival: 10 pts based on remaining HP
        score += gm.RemainingHpPercent * 10f;

        // XP collected: 10 pts (2000+ = max)
        score += Mathf.Min(gm.TotalXpCollected / 2000f, 1f) * 10f;

        if (score >= 85) return "S";
        if (score >= 65) return "A";
        if (score >= 40) return "B";
        return "C";
    }

    private Color GetGradeColor(string grade)
    {
        return grade switch
        {
            "S" => new Color(1f, 0.85f, 0f),   // gold
            "A" => new Color(0.2f, 0.8f, 1f),  // cyan
            "B" => new Color(0.4f, 0.8f, 0.4f),// green
            _ => new Color(0.7f, 0.7f, 0.7f),  // gray
        };
    }
}
