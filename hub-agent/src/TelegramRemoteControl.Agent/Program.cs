using System.Text;
using TelegramRemoteControl.Agent;
using TelegramRemoteControl.Agent.Execution;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddWindowsService();

var settingsPath = Path.Combine(builder.Environment.ContentRootPath, "appsettings.json");
var currentSettings = builder.Configuration.GetSection("Agent").Get<AgentSettings>() ?? new AgentSettings();
if (AgentSetupWizard.TryRun(settingsPath, currentSettings, args))
{
    builder.Configuration["Agent:HubUrl"] = currentSettings.HubUrl;
    builder.Configuration["Agent:AgentToken"] = currentSettings.AgentToken;
    builder.Configuration["Agent:PairingCode"] = currentSettings.PairingCode;
    builder.Configuration["Agent:FriendlyName"] = currentSettings.FriendlyName;
}

builder.Services.Configure<AgentSettings>(builder.Configuration.GetSection("Agent"));
builder.Services.AddSingleton<CommandExecutor>();
builder.Services.AddHostedService<AgentService>();

var host = builder.Build();
host.Run();
