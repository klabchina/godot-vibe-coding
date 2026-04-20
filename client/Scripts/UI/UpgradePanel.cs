using System.Collections.Generic;
using Godot;
using Game.Data;
using Game.Ecs;
using Game.Ecs.Components;

namespace Game.UI;

/// <summary>
/// 3-choice upgrade panel with 5-second countdown.
/// Scene: res://Scenes/UpgradePanel.tscn
/// </summary>
public partial class UpgradePanel : Control
{
    [Export] private TextureRect _cardBg = null!;
    [Export] private Button _button0 = null!;
    [Export] private Button _button1 = null!;
    [Export] private Button _button2 = null!;
    [Export] private Label _timerLabel = null!;

    private Button[] _buttons = null!;
    private List<UpgradeId> _options = null!;
    private Entity _playerEntity;
    private float _countdown;
    private bool _isActive;

    /// <summary>Fires when player selects an upgrade (or timer expires). Entity may be null if dismissed.</summary>
    public event System.Action<Entity, UpgradeId> OnUpgradeSelected;

    public override void _Ready()
    {
        _buttons = new[] { _button0, _button1, _button2 };

        foreach (var btn in _buttons)
            btn.Visible = false;

        Visible = false;
        _isActive = false;
    }

    public override void _Process(double delta)
    {
        if (!_isActive) return;

        _countdown -= (float)delta;
        _timerLabel.Text = $"{Mathf.Max(0, _countdown):F1}s";

        if (_countdown <= 0)
        {
            OnChoiceSelected(0);
        }
    }

    /// <summary>
    /// Show the upgrade panel with the given options for the given player.
    /// </summary>
    public void Show(Entity playerEntity, List<UpgradeId> options)
    {
        if (options == null || options.Count == 0) return;

        _playerEntity = playerEntity;
        _options = options;
        _countdown = UpgradeData.ChoiceTimeoutSec;
        _isActive = true;

        for (int i = 0; i < _buttons.Length; i++)
        {
            if (i < options.Count)
            {
                var def = UpgradeData.Definitions[options[i]];
                var currentLevel = playerEntity.Get<UpgradeComponent>()?.GetLevel(options[i]) ?? 0;
                string levelText = def.MaxLevel == 1 ? "" : $" (Lv{currentLevel + 1})";
                _buttons[i].Text = $"{def.Name}{levelText}";
                _buttons[i].Visible = true;
            }
            else
            {
                _buttons[i].Visible = false;
            }
        }

        Visible = true;
    }

    private void OnButton0Pressed() => OnChoiceSelected(0);
    private void OnButton1Pressed() => OnChoiceSelected(1);
    private void OnButton2Pressed() => OnChoiceSelected(2);

    private void OnChoiceSelected(int index)
    {
        if (!_isActive || _options == null || index >= _options.Count) return;

        _isActive = false;
        Visible = false;

        var upgrade = _playerEntity?.Get<UpgradeComponent>();
        if (upgrade == null) return;

        var chosen = _options[index];
        upgrade.Apply(chosen);
        ApplyImmediateEffects(chosen);

        OnUpgradeSelected?.Invoke(_playerEntity, chosen);
    }

    private void ApplyImmediateEffects(UpgradeId chosen)
    {
        if (_playerEntity == null) return;

        switch (chosen)
        {
            case UpgradeId.MaxHpUp:
                {
                    var health = _playerEntity.Get<HealthComponent>();
                    var upgrade = _playerEntity.Get<UpgradeComponent>();
                    if (health != null && upgrade != null)
                    {
                        health.MaxHp = UpgradeData.GetMaxHp(upgrade.MaxHpLevel);
                        health.Hp = Mathf.Min(health.Hp + UpgradeData.HpHealPerUpgrade, health.MaxHp);
                    }
                    break;
                }
            case UpgradeId.MoveSpeedUp:
                {
                    var vel = _playerEntity.Get<VelocityComponent>();
                    var upgrade = _playerEntity.Get<UpgradeComponent>();
                    if (vel != null && upgrade != null)
                    {
                        vel.Speed = UpgradeData.GetMoveSpeed(upgrade.MoveSpeedLevel);
                    }
                    break;
                }
            case UpgradeId.Shield:
                {
                    var buff = _playerEntity.Get<BuffComponent>();
                    if (buff != null)
                    {
                        buff.ShieldActive = true;
                        buff.ShieldCooldown = UpgradeData.ShieldRegenInterval;
                    }
                    break;
                }
            case UpgradeId.OrbitGuard:
                {
                    var orbit = _playerEntity.Get<OrbitComponent>();
                    if (orbit != null)
                    {
                        orbit.Count = _playerEntity.Get<UpgradeComponent>()?.OrbitCount ?? 1;
                    }
                    break;
                }
        }
    }

    public bool IsActive => _isActive;
}
