using Telegram.Bot;
using TelegramRemoteControl.BotService;
using TelegramRemoteControl.BotService.Callbacks;
using TelegramRemoteControl.BotService.Callbacks.Impl;
using TelegramRemoteControl.BotService.Commands;
using TelegramRemoteControl.BotService.Commands.Impl;
using TelegramRemoteControl.BotService.Menu;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.Configure<BotSettings>(builder.Configuration.GetSection("BotSettings"));

var botSettings = builder.Configuration.GetSection("BotSettings").Get<BotSettings>() ?? new BotSettings();
builder.Services.AddSingleton(botSettings);

var botHttpClient = new HttpClient(new HttpClientHandler
{
    Proxy = null,
    UseProxy = false
});
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botSettings.Token, botHttpClient));
builder.Services.AddHttpClient<HubClient>(client =>
{
    client.BaseAddress = new Uri(botSettings.HubUrl);
    if (!string.IsNullOrEmpty(botSettings.HubApiKey))
        client.DefaultRequestHeaders.Add("X-Api-Key", botSettings.HubApiKey);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    Proxy = null,
    UseProxy = false
});

// Commands
builder.Services.AddSingleton<ICommand, StatusCommand>();
builder.Services.AddSingleton<ICommand, AddPcCommand>();
builder.Services.AddSingleton<ICommand, SelectPcCommand>();
builder.Services.AddSingleton<ICommand, UptimeCommand>();
builder.Services.AddSingleton<ICommand, MonitorCommand>();
builder.Services.AddSingleton<ICommand, ProcessesCommand>();
builder.Services.AddSingleton<ICommand, DrivesCommand>();
builder.Services.AddSingleton<ICommand, IpCommand>();
builder.Services.AddSingleton<ICommand, CmdCommand>();
builder.Services.AddSingleton<ICommand, PowerShellCommand>();
builder.Services.AddSingleton<ICommand, ScreenshotCommand>();
builder.Services.AddSingleton<ICommand, WindowsCommand>();
builder.Services.AddSingleton<ICommand, ServicesCommand>();
builder.Services.AddSingleton<ICommand, FilesCommand>();
builder.Services.AddSingleton<ICommand, LockCommand>();
builder.Services.AddSingleton<ICommand, KillCommand>();
builder.Services.AddSingleton<ICommand, ShutdownCommand>();
builder.Services.AddSingleton<ICommand, RestartCommand>();
builder.Services.AddSingleton<ICommand, SleepCommand>();
builder.Services.AddSingleton<ICommand, HibernateCommand>();
builder.Services.AddSingleton<ICommand, NotifyCommand>();
builder.Services.AddSingleton<ICommand, ApproveCommand>();
builder.Services.AddSingleton<ICommand, DenyCommand>();
builder.Services.AddSingleton<ICommand, RegisterCommand>();

builder.Services.AddSingleton<CommandRegistry>(sp =>
    new CommandRegistry(sp.GetServices<ICommand>()));

builder.Services.AddSingleton<MenuBuilder>();

// Callbacks
builder.Services.AddSingleton<ICallbackHandler, PcCallbackHandler>();
builder.Services.AddSingleton<ICallbackHandler, ProcCallbackHandler>();
builder.Services.AddSingleton<ICallbackHandler, WinCallbackHandler>();
builder.Services.AddSingleton<ICallbackHandler, SvcCallbackHandler>();
builder.Services.AddSingleton<ICallbackHandler, FileCallbackHandler>();
builder.Services.AddSingleton<ICallbackHandler, ConfirmCallbackHandler>();
builder.Services.AddSingleton<ICallbackHandler, NotifyCallbackHandler>();

builder.Services.AddSingleton<CallbackRegistry>(sp =>
    new CallbackRegistry(sp.GetServices<ICallbackHandler>()));

builder.Services.AddSingleton<BotHandler>();
builder.Services.AddHostedService<TelegramBotService>();
builder.Services.AddHostedService<DeviceStatusMonitor>();

var host = builder.Build();
host.Run();
