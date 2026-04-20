using Godot;

namespace Game.UI;

public partial class ResultUI : Control
{
    public override void _Ready()
    {
        var gm = GameManager.Instance;
        string grade = CalculateGrade(gm);

        GetNode<Label>("ContentBox/GradeLabel").Text = grade;
        GetNode<Label>("ContentBox/GradeLabel").AddThemeColorOverride("font_color", GetGradeColor(grade));

        GetNode<Label>("ContentBox/WavesRow/WavesValue").Text = $"{gm.WavesCompleted} / {StageLoader.GetTotalWaves()}";
        GetNode<Label>("ContentBox/KillsRow/KillsValue").Text  = $"{gm.KillCount}";
        GetNode<Label>("ContentBox/DamageRow/DamageValue").Text = $"{gm.TotalDamage}";
        GetNode<Label>("ContentBox/XpRow/XpValue").Text        = $"{gm.TotalXpCollected}";
        GetNode<Label>("ContentBox/HpRow/HpValue").Text        = $"{(gm.RemainingHpPercent * 100):F0}%";
    }

    public void OnReturnPressed()
    {
        SceneManager.Instance.GoToMainMenu();
    }

    private static string CalculateGrade(GameManager gm)
    {
        float score = 0;
        score += (gm.WavesCompleted / 8f) * 40f;
        score += Mathf.Min(gm.KillCount / 100f, 1f) * 20f;
        score += Mathf.Min(gm.TotalDamage / 5000f, 1f) * 20f;
        score += gm.RemainingHpPercent * 10f;
        score += Mathf.Min(gm.TotalXpCollected / 2000f, 1f) * 10f;

        if (score >= 85) return "S";
        if (score >= 65) return "A";
        if (score >= 40) return "B";
        return "C";
    }

    private static Color GetGradeColor(string grade) => grade switch
    {
        "S" => new Color(1f, 0.85f, 0f),
        "A" => new Color(0.2f, 0.8f, 1f),
        "B" => new Color(0.4f, 0.8f, 0.4f),
        _   => new Color(0.7f, 0.7f, 0.7f),
    };
}
