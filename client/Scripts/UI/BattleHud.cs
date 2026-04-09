using Godot;

namespace Game.UI;

/// <summary>
/// Minimal battle HUD: wave counter and HP bar.
/// </summary>
public partial class BattleHud : Control
{
    private Label _waveLabel;
    private ProgressBar _hpBar;
    private Label _hpLabel;

    public override void _Ready()
    {
        _waveLabel = GetNode<Label>("WaveLabel");
        _hpBar = GetNode<ProgressBar>("HpBar");
        _hpLabel = GetNode<Label>("HpLabel");
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
}
