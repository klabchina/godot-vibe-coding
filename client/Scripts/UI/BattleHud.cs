using Godot;

namespace Game.UI;

/// <summary>
/// Battle HUD: wave counter, HP bar, level and XP display.
/// </summary>
public partial class BattleHud : Control
{
    private Label _waveLabel;
    private ProgressBar _hpBar;
    private Label _hpLabel;
    private Label _levelLabel;
    private Label _xpLabel;

    public override void _Ready()
    {
        _waveLabel = GetNodeOrNull<Label>("WaveLabel");
        _hpBar = GetNodeOrNull<ProgressBar>("HpBar");
        _hpLabel = GetNodeOrNull<Label>("HpLabel");
        _levelLabel = GetNodeOrNull<Label>("LevelLabel");
        _xpLabel = GetNodeOrNull<Label>("XpLabel");
    }

    public void UpdateWave(int current, int total)
    {
        if (_waveLabel != null)
            _waveLabel.Text = $"Wave {current}/{total}";
    }

    public void UpdateHp(int hp, int maxHp)
    {
        if (_hpBar != null)
        {
            _hpBar.MaxValue = maxHp;
            _hpBar.Value = hp;
        }
        if (_hpLabel != null)
            _hpLabel.Text = $"{hp}/{maxHp}";
    }

    public void UpdateLevel(int level)
    {
        if (_levelLabel != null)
            _levelLabel.Text = $"Lv.{level}";
    }

    public void UpdateXp(int xp)
    {
        if (_xpLabel != null)
            _xpLabel.Text = $"XP: {xp}";
    }
}
