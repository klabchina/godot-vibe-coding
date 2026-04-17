using Godot;
using Game.Ecs.Components;
using Game.Data;

namespace Game.UI;

/// <summary>
/// Battle HUD: wave counter, HP bar, XP bar, kills, BuffBar, UpgradeBar.
/// </summary>
public partial class BattleHud : Control
{
    private Label _waveLabel;
    private ProgressBar _hpBar;
    private Label _hpLabel;
    private ProgressBar _xpBar;
    private Label _xpBarLabel;
    private Label _killLabel;
    private BuffBar _buffBar;
    private UpgradeBar _upgradeBar;

    public override void _Ready()
    {
        _waveLabel = GetNodeOrNull<Label>("WaveLabel");
        _hpBar = GetNodeOrNull<ProgressBar>("HpBar");
        _hpLabel = GetNodeOrNull<Label>("HpLabel");
        _xpBar = GetNodeOrNull<ProgressBar>("XpBar");
        _xpBarLabel = GetNodeOrNull<Label>("XpBarLabel");
        _killLabel = GetNodeOrNull<Label>("KillLabel");

        // Create BuffBar dynamically (top-right area)
        _buffBar = new BuffBar();
        _buffBar.Position = new Vector2(800, 10);
        AddChild(_buffBar);

        // Create UpgradeBar dynamically (bottom-left area)
        _upgradeBar = new UpgradeBar();
        _upgradeBar.Position = new Vector2(20, 140);
        AddChild(_upgradeBar);
    }

    public void UpdateWave(int current, int total)
    {
        if (_waveLabel != null)
            _waveLabel.Text = $"⚔ Wave {current}/{total}";
    }

    public void UpdateHp(int hp, int maxHp)
    {
        if (_hpBar != null)
        {
            _hpBar.MaxValue = maxHp;
            _hpBar.Value = hp;
        }
        if (_hpLabel != null)
            _hpLabel.Text = $"♥ {hp}/{maxHp}";
    }

    public void UpdateLevel(int level)
    {
        // Level is now shown inside XpBarLabel; no separate update needed.
    }

    public void UpdateXp(int totalXp)
    {
        int level = LevelData.GetLevel(totalXp);

        // 展示等级最低为 1（升级前也显示 Lv.1）
        int displayLevel = level + 1;

        // 本级起始累积 XP（Lv.1 之前起始为 0）
        int prevCumXp = LevelData.GetCumulativeXp(level);
        // 本级目标累积 XP
        int nextCumXp = level + 1 >= LevelData.MaxLevel
            ? LevelData.GetCumulativeXp(LevelData.MaxLevel)
            : LevelData.GetCumulativeXp(level + 1);

        int levelXp = nextCumXp - prevCumXp;          // 本级需要的 XP 段长
        int currentXp = totalXp - prevCumXp;            // 本级已积累的 XP

        if (_xpBar != null)
        {
            _xpBar.MaxValue = levelXp > 0 ? levelXp : 1;
            _xpBar.Value = level >= LevelData.MaxLevel ? levelXp : currentXp;
        }

        if (_xpBarLabel != null)
        {
            if (level >= LevelData.MaxLevel)
                _xpBarLabel.Text = $"Lv.{displayLevel}  MAX";
            else
                _xpBarLabel.Text = $"Lv.{displayLevel}  XP {currentXp}/{levelXp}";
        }
    }

    public void UpdateKills(int kills)
    {
        if (_killLabel != null)
            _killLabel.Text = $"⚔ Kills: {kills}";
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
