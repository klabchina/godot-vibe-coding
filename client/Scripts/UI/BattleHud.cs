using Godot;
using Game.Ecs.Components;

namespace Game.UI;

/// <summary>
/// Battle HUD: wave counter, HP bar, level, XP, kills, BuffBar, UpgradeBar.
/// </summary>
public partial class BattleHud : Control
{
    private Label _waveLabel;
    private ProgressBar _hpBar;
    private Label _hpLabel;
    private Label _levelLabel;
    private Label _xpLabel;
    private Label _killLabel;
    private BuffBar _buffBar;
    private UpgradeBar _upgradeBar;

    public override void _Ready()
    {
        _waveLabel = GetNodeOrNull<Label>("WaveLabel");
        _hpBar = GetNodeOrNull<ProgressBar>("HpBar");
        _hpLabel = GetNodeOrNull<Label>("HpLabel");
        _levelLabel = GetNodeOrNull<Label>("LevelLabel");
        _xpLabel = GetNodeOrNull<Label>("XpLabel");
        _killLabel = GetNodeOrNull<Label>("KillLabel");

        // Create BuffBar dynamically (top-right area)
        _buffBar = new BuffBar();
        _buffBar.Position = new Vector2(800, 10);
        AddChild(_buffBar);

        // Create UpgradeBar dynamically (bottom-left area)
        _upgradeBar = new UpgradeBar();
        _upgradeBar.Position = new Vector2(20, 100);
        AddChild(_upgradeBar);
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

    public void UpdateKills(int kills)
    {
        if (_killLabel != null)
            _killLabel.Text = $"Kills: {kills}";
    }

    public void UpdateBuffs(BuffComponent buff)
    {
        _buffBar?.UpdateBuff(buff);
        if (buff != null)
            _buffBar?.UpdateShield(buff.ShieldActive);
    }

    public void UpdateUpgradeIcons(UpgradeComponent upgrade)
    {
        _upgradeBar?.UpdateUpgrades(upgrade);
    }
}
