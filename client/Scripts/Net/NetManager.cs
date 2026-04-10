using System;
using System.Collections.Concurrent;
using Godot;

namespace Game.Net;

/// <summary>
/// WebSocket connection manager (Autoload-ready singleton).
/// Handles connecting, disconnecting, heartbeat, reconnection,
/// and dispatching incoming messages to registered handlers.
/// </summary>
public partial class NetManager : Node
{
    public static NetManager Instance { get; private set; }

    // ── Configuration ──
    private const float HeartbeatInterval = 5f;
    private const float ReconnectDelay = 3f;
    private const int MaxReconnectAttempts = 5;

    // ── Connection state ──
    public enum ConnState { Disconnected, Connecting, Connected }
    public ConnState State { get; private set; } = ConnState.Disconnected;

    private WebSocketPeer _ws;
    private string _serverUrl = "";
    private float _heartbeatTimer;
    private float _reconnectTimer;
    private int _reconnectAttempts;
    private bool _autoReconnect;

    // ── Events ──
    public event Action OnConnected;
    public event Action<string> OnDisconnected; // reason
    public event Action<NetMessage> OnMessageReceived;

    // ── Thread-safe message queue (messages from polling thread) ──
    private readonly ConcurrentQueue<NetMessage> _incomingQueue = new();

    public override void _Ready()
    {
        Instance = this;
    }

    // ──────────────────── Public API ────────────────────

    /// <summary>Connect to the game server.</summary>
    public void Connect(string url)
    {
        if (State != ConnState.Disconnected) return;

        _serverUrl = url;
        _reconnectAttempts = 0;
        _autoReconnect = true;
        StartConnection();
    }

    /// <summary>Gracefully disconnect.</summary>
    public void Disconnect()
    {
        _autoReconnect = false;
        if (_ws != null)
        {
            Send(NetMessage.Create(MessageType.Disconnect, new { }));
            _ws.Close();
        }
        SetState(ConnState.Disconnected, "client requested");
    }

    /// <summary>Send a NetMessage to the server.</summary>
    public void Send(NetMessage msg)
    {
        if (State != ConnState.Connected || _ws == null) return;
        _ws.Send(msg.Serialize().ToUtf8Buffer());
    }

    /// <summary>Send a typed payload message.</summary>
    public void Send<T>(MessageType type, T payload)
    {
        Send(NetMessage.Create(type, payload));
    }

    // ──────────────────── Godot Lifecycle ────────────────────

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        if (_ws != null)
        {
            _ws.Poll();
            var state = _ws.GetReadyState();

            switch (state)
            {
                case WebSocketPeer.State.Open:
                    if (State == ConnState.Connecting)
                    {
                        SetState(ConnState.Connected, null);
                        _reconnectAttempts = 0;
                    }
                    PollIncoming();
                    TickHeartbeat(dt);
                    break;

                case WebSocketPeer.State.Closing:
                    break;

                case WebSocketPeer.State.Closed:
                    HandleClosed();
                    break;

                case WebSocketPeer.State.Connecting:
                    break;
            }
        }

        // Reconnect timer
        if (State == ConnState.Disconnected && _autoReconnect && _reconnectAttempts < MaxReconnectAttempts)
        {
            _reconnectTimer -= dt;
            if (_reconnectTimer <= 0)
            {
                _reconnectAttempts++;
                StartConnection();
            }
        }

        // Dispatch queued messages on main thread
        while (_incomingQueue.TryDequeue(out var msg))
        {
            OnMessageReceived?.Invoke(msg);
        }
    }

    // ──────────────────── Internal ────────────────────

    private void StartConnection()
    {
        _ws = new WebSocketPeer();
        var err = _ws.ConnectToUrl(_serverUrl);
        if (err != Error.Ok)
        {
            GD.PrintErr($"[NetManager] Failed to connect to {_serverUrl}: {err}");
            _reconnectTimer = ReconnectDelay;
            return;
        }
        SetState(ConnState.Connecting, null);
    }

    private void PollIncoming()
    {
        while (_ws.GetAvailablePacketCount() > 0)
        {
            var packet = _ws.GetPacket();
            var json = packet.GetStringFromUtf8();
            try
            {
                var msg = NetMessage.Deserialize(json);
                if (msg != null)
                {
                    if (msg.Type == MessageType.Heartbeat)
                    {
                        // Heartbeat response — just reset timer
                        continue;
                    }
                    _incomingQueue.Enqueue(msg);
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[NetManager] Failed to parse message: {e.Message}");
            }
        }
    }

    private void TickHeartbeat(float dt)
    {
        _heartbeatTimer -= dt;
        if (_heartbeatTimer <= 0)
        {
            _heartbeatTimer = HeartbeatInterval;
            Send(MessageType.Heartbeat, new HeartbeatMsg
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }
    }

    private void HandleClosed()
    {
        var reason = _ws.GetCloseReason();
        _ws = null;

        if (State != ConnState.Disconnected)
        {
            SetState(ConnState.Disconnected, reason);
            if (_autoReconnect)
            {
                _reconnectTimer = ReconnectDelay;
            }
        }
    }

    private void SetState(ConnState newState, string reason)
    {
        var prev = State;
        State = newState;

        if (newState == ConnState.Connected && prev != ConnState.Connected)
        {
            _heartbeatTimer = HeartbeatInterval;
            OnConnected?.Invoke();
        }
        else if (newState == ConnState.Disconnected && prev != ConnState.Disconnected)
        {
            OnDisconnected?.Invoke(reason ?? "unknown");
        }
    }
}
