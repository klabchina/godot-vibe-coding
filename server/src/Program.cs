using Microsoft.AspNetCore.Builder;
using Server.Hosting;
using Server.Match;
using Server.Network;
using Server.Room;
using Server.Session;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ConnectionManager>();
builder.Services.AddSingleton<MessageRouter>();
builder.Services.AddSingleton<WebSocketHandler>();

builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<RoomManager>();
builder.Services.AddSingleton<MatchService>();

builder.Services.AddHostedService<GameLoopService>();

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(10)
});

app.MapGet("/healthz", () => Results.Ok(new { ok = true }));

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
    var handler = context.RequestServices.GetRequiredService<WebSocketHandler>();

    var connectionId = connectionManager.Add(socket);
    await handler.RunAsync(connectionId, socket, context.RequestAborted);
});

app.Run();
