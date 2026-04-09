using Godot;

namespace Game.UI;

/// <summary>
/// Matching screen: simulates matchmaking with a countdown, then enters battle.
/// Single-player mode: 3-second fake countdown → auto start.
/// </summary>
public partial class MatchingUI : Control
{
    private Label _statusLabel;
    private Label _countdownLabel;
    private Button _cancelBtn;
    private float _timer = 3.0f;
    private bool _cancelled;

    public override void _Ready()
    {
        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(LayoutPreset.Center);
        vbox.GrowHorizontal = GrowDirection.Both;
        vbox.GrowVertical = GrowDirection.Both;
        vbox.AddThemeConstantOverride("separation", 20);
        vbox.Position = new Vector2(0, -100);
        AddChild(vbox);

        _statusLabel = new Label();
        _statusLabel.Text = "Searching for opponent...";
        _statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _statusLabel.AddThemeFontSizeOverride("font_size", 28);
        vbox.AddChild(_statusLabel);

        _countdownLabel = new Label();
        _countdownLabel.Text = "";
        _countdownLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _countdownLabel.AddThemeFontSizeOverride("font_size", 40);
        vbox.AddChild(_countdownLabel);

        _cancelBtn = new Button();
        _cancelBtn.Text = "Cancel";
        _cancelBtn.CustomMinimumSize = new Vector2(200, 50);
        _cancelBtn.Pressed += OnCancelPressed;
        vbox.AddChild(_cancelBtn);
    }

    public override void _Process(double delta)
    {
        if (_cancelled) return;

        _timer -= (float)delta;
        int sec = Mathf.Max(0, Mathf.CeilToInt(_timer));

        if (_timer > 1.0f)
        {
            _countdownLabel.Text = $"{sec}";
        }
        else if (_timer > 0)
        {
            _statusLabel.Text = "Opponent found!";
            _countdownLabel.Text = "Starting...";
            _cancelBtn.Visible = false;
        }
        else
        {
            SceneManager.Instance.GoToBattle();
        }
    }

    private void OnCancelPressed()
    {
        _cancelled = true;
        SceneManager.Instance.GoToMainMenu();
    }
}
