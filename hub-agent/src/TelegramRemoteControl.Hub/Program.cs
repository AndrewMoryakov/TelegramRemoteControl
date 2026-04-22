using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using TelegramRemoteControl.Hub;
using TelegramRemoteControl.Hub.Data;
using TelegramRemoteControl.Hub.Hubs;
using TelegramRemoteControl.Hub.Middleware;
using TelegramRemoteControl.Hub.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.Configure<HubSettings>(builder.Configuration.GetSection("HubSettings"));
builder.Services.AddSingleton<HubDbContext>();
builder.Services.AddSingleton<AgentManager>();
builder.Services.AddSingleton<PendingCommandStore>();
builder.Services.AddSingleton<PairingAttemptTracker>();
builder.Services.AddHostedService<AgentLivenessMonitor>();
builder.Services.AddControllers();

var hubSettings = builder.Configuration.GetSection("HubSettings").Get<HubSettings>() ?? new HubSettings();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", o =>
    {
        o.PermitLimit = hubSettings.RateLimitRequests;
        o.Window = TimeSpan.FromSeconds(hubSettings.RateLimitWindowSeconds);
        o.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = hubSettings.MaxMessageSizeBytes;
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    // BL-07: without an explicit client timeout SignalR can leave a dead agent's
    // connection registered for minutes, so commands hang. Tie it to AgentTimeoutSeconds.
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(Math.Max(30, hubSettings.AgentTimeoutSeconds));
}).AddMessagePackProtocol();

var app = builder.Build();

// Initialize database
var db = app.Services.GetRequiredService<HubDbContext>();
await db.InitializeAsync();

// Защищаем только /api/*, SignalR (/agent-hub) не трогаем
app.UseWhen(
    ctx => ctx.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseMiddleware<ApiKeyMiddleware>()
);

app.UseRateLimiter();

app.MapControllers().RequireRateLimiting("api");
app.MapHub<AgentHub>("/agent-hub");
app.MapGet("/", () => "TelegramRemoteControl Hub");

app.Run();
