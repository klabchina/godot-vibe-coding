using System;
using System.Collections.Concurrent;
using Godot;
using Google.Protobuf;

namespace Game.Net;

public partial class NetManager : Node
{
    public static NetManager Instance { get; private set; }

    private const float HeartbeatInterval = 5f;
    private const float ReconnectDelay = 3f;
    private const int MaxReconnectAttempts = 5;

    public enum ConnState { Disconnected, Connecting, Connected }
    public ConnState State { get; private set; } = ConnState.Disconnected;

    private WebSocketPeer _ws;
    private string _serverUrl = "";
    private float _heartbeatTimer;
    private float _reconnectTimer;
    private int _reconnectAttempts;
    private bool _autoReconnect;

    public event Action OnConnected;
    public event Action<string> OnDisconnected;
    public event Action<uint, IMessage?> OnMessageReceived;

    private readonly ConcurrentQueue<(uint msgId, IMessage? message)> _incomingQueue = new();

    public override void _Ready()
    {
        Instance = this;
    }

    public void Connect(string url)
    {
        if (State != ConnState.Disconnected) return;

        _serverUrl = url;
        _reconnectAttempts = 0;
        _autoReconnect = true;
        StartConnection();
    }

    public void Disconnect()
    {
        _autoReconnect = false;
        if (_ws != null)
        {
            _ws.Close();
        }
        SetState(ConnState.Disconnected, "client requested");
    }

    public void Send(uint msgId, IMessage message)
    {
        if (State != ConnState.Connected || _ws == null) return;
        _ws.Send(Protocol.BuildEnvelope(msgId, message));
    }

    public void SendHeartbeat()
    {
        if (State != ConnState.Connected || _ws == null) return;
        _ws.Send(Protocol.BuildEnvelope(MsgIds.Heartbeat, Array.Empty<byte>()));
    }

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

                case WebSocketPeer.State.Closed:
                    HandleClosed();
                    break;
            }
        }

        if (State == ConnState.Disconnected && _autoReconnect && _reconnectAttempts < MaxReconnectAttempts)
        {
            _reconnectTimer -= dt;
            if (_reconnectTimer <= 0)
            {
                _reconnectAttempts++;
                StartConnection();
            }
        }

        while (_incomingQueue.TryDequeue(out var msg))
        {
            OnMessageReceived?.Invoke(msg.msgId, msg.message);
        }
    }

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
            var envelope = Protocol.ParseEnvelope(packet);
            if (envelope == null)
            {
                GD.PrintErr("[NetManager] Failed to parse envelope");
                continue;
            }

            var (msgId, payload) = envelope.Value;
            if (msgId == MsgIds.Heartbeat)
            {
                continue;
            }

            try
            {
                var msg = Protocol.ParsePayload(msgId, payload);
                _incomingQueue.Enqueue((msgId, msg));
            }
            catch (Exception e)
            {
                GD.PrintErr($"[NetManager] Failed to parse payload msgId={msgId}: {e.Message}");
            }
        }
    }

    private void TickHeartbeat(float dt)
    {
        _heartbeatTimer -= dt;
        if (_heartbeatTimer <= 0)
        {
            _heartbeatTimer = HeartbeatInterval;
            SendHeartbeat();
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
