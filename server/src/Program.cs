using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Server.Hosting;
using Server.Match;
using Server.Network;
using Server.Room;
using Server.Session;

var builder = WebApplication.CreateBuilder(args);

// ========== 服务注册 ==========

// 网络层
builder.Services.AddSingleton<ConnectionManager>();
builder.Services.AddSingleton<WebSocketHandler>();
builder.Services.AddSingleton<MessageRouter>();

// 会话层
builder.Services.AddSingleton<SessionManager>();

// 房间层
builder.Services.AddSingleton<RoomManager>();

// 匹配服务（需要房间管理器）
builder.Services.AddSingleton<MatchService>();

// 游戏主循环
builder.Services.AddHostedService<GameLoopService>();

var app = builder.Build();

// ========== 中间件 ==========

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(10)
});

// 健康检查
app.MapGet("/healthz", () => Results.Ok(new { ok = true, timestamp = DateTime.UtcNow }));

// WebSocket 入口
app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket request required");
        return;
    }

    var socket = await context.WebSockets.AcceptWebSocketAsync();
    var connectionManager = context.RequestServices.GetRequiredService<ConnectionManager>();
    var sessionManager = context.RequestServices.GetRequiredService<SessionManager>();
    var roomManager = context.RequestServices.GetRequiredService<RoomManager>();
    var matchService = context.RequestServices.GetRequiredService<MatchService>();
    var handler = context.RequestServices.GetRequiredService<WebSocketHandler>();

    var connectionId = connectionManager.Add(socket);
    
    // 创建临时会话（等待客户端发送 PlayerId）
    var tempPlayerId = Guid.NewGuid().ToString("N");
    sessionManager.Create(connectionId, tempPlayerId, "Anonymous");
    
    // 创建消息路由器
    var router = new MessageRouter(
        context.RequestServices.GetRequiredService<ILogger<MessageRouter>>(),
        sessionManager,
        roomManager,
        matchService,
        async (connId, msgId, msg) => await handler.SendAsync(connId, msgId, msg)
    );
    
    // 订阅断线事件
    handler.OnDisconnected += (connId) =>
    {
        if (sessionManager.TryGetByConnection(connId, out var session))
        {
            session.IsDisconnected = true;
            session.DisconnectTime = DateTime.UtcNow;
        }
    };
    
    // 处理 WebSocket 消息
    var buffer = new byte[8 * 1024];
    try
    {
        while (socket.State == System.Net.WebSockets.WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            
            if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
            {
                await socket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                break;
            }
            
            if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Binary)
            {
                var payload = buffer.AsMemory(0, result.Count);
                await router.RouteAsync(connectionId, payload, CancellationToken.None);
            }
        }
    }
    finally
    {
        connectionManager.Remove(connectionId);
    }
});

Console.WriteLine("Server starting on port 8081...");
app.Urls.Add("http://0.0.0.0:8081");
app.Run();
