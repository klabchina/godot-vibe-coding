using Godot;
using Game.Net;
using Server.Proto;

namespace Game.UI;

/// <summary>
/// Multiplayer matching UI: connects to server, sends MatchRequest,
/// handles cancel/timeout/disconnect and enters battle after GameStart.
/// </summary>
public partial class MatchingUI : Control
{
    [Export]
    public string ServerUrl { get; set; } = "ws://127.0.0.1:8085/ws";

    private const float MatchTimeoutSec = 20f;

    private Label _statusLabel;
    private Label _countdownLabel;
    private Button _cancelBtn;

    private MatchClient _matchClient;
    private float _elapsed;
    private bool _cancelled;
    private bool _leaving;

    public override void _Ready()
    {
        _statusLabel = GetNode<Label>("ContentBox/StatusLabel");
        _countdownLabel = GetNode<Label>("ContentBox/CountdownLabel");
        _cancelBtn = GetNode<Button>("CancelButton");

        _matchClient = new MatchClient();
        _matchClient.OnMatchSuccess += OnMatchSuccess;
        _matchClient.OnBattleStart += OnBattleStart;

        NetManager.Instance.OnConnected += OnConnected;
        NetManager.Instance.OnDisconnected += OnDisconnected;

        StartOrConnect();
    }

    public override void _ExitTree()
    {
        if (_matchClient != null)
        {
            _matchClient.OnMatchSuccess -= OnMatchSuccess;
            _matchClient.OnBattleStart -= OnBattleStart;
            _matchClient.Dispose();
        }

        if (NetManager.Instance != null)
        {
            NetManager.Instance.OnConnected -= OnConnected;
            NetManager.Instance.OnDisconnected -= OnDisconnected;
        }
    }

    public override void _Process(double delta)
    {
        if (_cancelled || _leaving || _matchClient == null || !_matchClient.IsMatching)
            return;

        _elapsed += (float)delta;
        var remain = Mathf.Max(0, MatchTimeoutSec - _elapsed);
        _countdownLabel.Text = $"{Mathf.CeilToInt(remain)}s";

        if (_elapsed >= MatchTimeoutSec)
        {
            _matchClient.CancelMatch();
            _statusLabel.Text = "Match timeout";
            _countdownLabel.Text = "Returning...";
            _cancelBtn.Disabled = true;
            _leaving = true;
            _ = ReturnToMainMenuDelayed(1.0f);
        }
    }

    private void StartOrConnect()
    {
        if (NetManager.Instance.State == NetManager.ConnState.Connected)
        {
            StartMatch();
            return;
        }

        _statusLabel.Text = "Connecting to server...";
        _countdownLabel.Text = string.Empty;

        if (NetManager.Instance.State == NetManager.ConnState.Disconnected)
        {
            NetManager.Instance.Connect(ServerUrl);
        }
    }

    private void StartMatch()
    {
        if (_cancelled || _leaving) return;

        var playerId = $"guest_{(long)Time.GetUnixTimeFromSystem()}";
        _matchClient.StartMatch(playerId, "Guest");

        _elapsed = 0f;
        _statusLabel.Text = "Searching for opponent...";
        _countdownLabel.Text = $"{Mathf.CeilToInt(MatchTimeoutSec)}s";
        _cancelBtn.Visible = true;
        _cancelBtn.Disabled = false;
    }

    private void OnConnected()
    {
        if (_cancelled || _leaving) return;
        StartMatch();
    }

    private void OnMatchSuccess(MatchSuccess matchSuccess)
    {
        if (_cancelled || _leaving) return;

        GameManager.Instance.CurrentRandomSeed = matchSuccess.RandomSeed;
        _statusLabel.Text = "Opponent found!";
        _countdownLabel.Text = "Waiting game start...";
        _cancelBtn.Disabled = true;
        _cancelBtn.Visible = false;
    }

    private void OnBattleStart(GameStart gameStart)
    {
        if (_cancelled || _leaving) return;

        _leaving = true;
        _statusLabel.Text = "Starting battle...";
        _countdownLabel.Text = string.Empty;
        SceneManager.Instance.GoToBattle();
    }

    private void OnDisconnected(string reason)
    {
        if (_cancelled || _leaving) return;

        if (_matchClient != null && _matchClient.IsMatching)
            _matchClient.CancelMatch();

        _statusLabel.Text = "Disconnected from server";
        _countdownLabel.Text = "Returning...";
        _cancelBtn.Disabled = true;
        _leaving = true;
        _ = ReturnToMainMenuDelayed(1.0f);
    }

    private void OnCancelPressed()
    {
        if (_cancelled || _leaving) return;

        _cancelled = true;
        if (_matchClient != null && _matchClient.IsMatching)
            _matchClient.CancelMatch();

        SceneManager.Instance.GoToMainMenu();
    }

    private async System.Threading.Tasks.Task ReturnToMainMenuDelayed(float delaySec)
    {
        await ToSignal(GetTree().CreateTimer(delaySec), SceneTreeTimer.SignalName.Timeout);
        if (IsInsideTree())
            SceneManager.Instance.GoToMainMenu();
    }
}
