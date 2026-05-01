using System.Collections.Generic;
using Godot;
using Game.Data;
using Game.Ecs;
using Game.Ecs.Components;
using Game.Net;
using Game.Ecs.Systems;

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
    private SyncClient _sync;
    private UpgradeApplySystem _upgradeApplySystem;
    private int _localChoiceTick;

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

        var chosen = _options[index];

        if (_sync != null)
        {
            _sync.SendSkillChoice(chosen.ToString());
        }
        else
        {
            _localChoiceTick++;
            _upgradeApplySystem?.EnqueueChoice(0, chosen.ToString(), _localChoiceTick);
        }

        OnUpgradeSelected?.Invoke(_playerEntity, chosen);
    }

    public bool IsActive => _isActive;

    public void SetSyncClient(SyncClient sync)
    {
        _sync = sync;
    }

    public void SetUpgradeApplySystem(UpgradeApplySystem upgradeApplySystem)
    {
        _upgradeApplySystem = upgradeApplySystem;
    }
}
