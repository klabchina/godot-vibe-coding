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
        _statusLabel = GetNode<Label>("ContentBox/StatusLabel");
        _countdownLabel = GetNode<Label>("ContentBox/CountdownLabel");
        _cancelBtn = GetNode<Button>("CancelButton");
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
