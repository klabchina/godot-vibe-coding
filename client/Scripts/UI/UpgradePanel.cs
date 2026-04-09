using System.Collections.Generic;
using Godot;
using Game.Data;
using Game.Ecs;
using Game.Ecs.Components;

namespace Game.UI;

/// <summary>
/// 3-choice upgrade panel with 5-second countdown.
/// Game does NOT pause while panel is open.
/// </summary>
public partial class UpgradePanel : Control
{
    private readonly List<Button> _buttons = new();
    private Label _timerLabel;
    private List<UpgradeId> _options;
    private Entity _playerEntity;
    private float _countdown;
    private bool _isActive;

    public override void _Ready()
    {
        // Semi-transparent dark background covering full screen
        var bg = new ColorRect();
        bg.Color = new Color(0, 0, 0, 0.6f);
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        // Build UI dynamically
        var vbox = new VBoxContainer();
        vbox.Name = "VBox";
        vbox.SetAnchorsPreset(LayoutPreset.Center);
        vbox.GrowHorizontal = GrowDirection.Both;
        vbox.GrowVertical = GrowDirection.Both;
        vbox.AddThemeConstantOverride("separation", 10);
        AddChild(vbox);

        var title = new Label();
        title.Text = "LEVEL UP!";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        for (int i = 0; i < UpgradeData.ChoiceCount; i++)
        {
            var btn = new Button();
            btn.CustomMinimumSize = new Vector2(280, 50);
            int index = i;
            btn.Pressed += () => OnChoiceSelected(index);
            vbox.AddChild(btn);
            _buttons.Add(btn);
        }

        _timerLabel = new Label();
        _timerLabel.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(_timerLabel);

        // Center the VBox
        vbox.Position = new Vector2(-150, -120);

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
            // Auto-select first option
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

        for (int i = 0; i < _buttons.Count; i++)
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

    private void OnChoiceSelected(int index)
    {
        if (!_isActive || _options == null || index >= _options.Count) return;

        _isActive = false;
        Visible = false;

        var upgrade = _playerEntity?.Get<UpgradeComponent>();
        if (upgrade == null) return;

        var chosen = _options[index];
        upgrade.Apply(chosen);

        // Apply immediate effects for defense upgrades
        ApplyImmediateEffects(chosen);
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
        }
    }

    public bool IsActive => _isActive;
}
