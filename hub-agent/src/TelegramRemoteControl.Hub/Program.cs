using TelegramRemoteControl.Hub;
using TelegramRemoteControl.Hub.Data;
using TelegramRemoteControl.Hub.Hubs;
using TelegramRemoteControl.Hub.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.Configure<HubSettings>(builder.Configuration.GetSection("HubSettings"));
builder.Services.AddSingleton<HubDbContext>();
builder.Services.AddSingleton<AgentManager>();
builder.Services.AddSingleton<PendingCommandStore>();
builder.Services.AddControllers();

var hubSettings = builder.Configuration.GetSection("HubSettings").Get<HubSettings>() ?? new HubSettings();

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = hubSettings.MaxMessageSizeBytes;
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
}).AddMessagePackProtocol();

var app = builder.Build();

// Initialize database
var db = app.Services.GetRequiredService<HubDbContext>();
await db.InitializeAsync();

app.MapControllers();
app.MapHub<AgentHub>("/agent-hub");
app.MapGet("/", () => "TelegramRemoteControl Hub");

app.Run();
