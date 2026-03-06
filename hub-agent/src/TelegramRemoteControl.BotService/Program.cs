using Telegram.Bot;
using TelegramRemoteControl.BotService;
using TelegramRemoteControl.BotService.Callbacks;
using TelegramRemoteControl.BotService.Callbacks.Impl;
using TelegramRemoteControl.BotService.Commands;
using TelegramRemoteControl.BotService.Commands.Impl;
using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.BotService.Services;

Console.Error.WriteLine("[DIAG] Program starting");

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.Configure<BotSettings>(builder.Configuration.GetSection("BotSettings"));

var botSettings = builder.Configuration.GetSection("BotSettings").Get<BotSettings>() ?? new BotSettings();
builder.Services.AddSingleton(botSettings);

Console.Error.WriteLine("[DIAG] Creating TelegramBotClient");
var botHttpClient = new HttpClient();
Console.Error.WriteLine("[DIAG] TelegramBotClient HttpClient created");
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botSettings.Token, botHttpClient));
Console.Error.WriteLine("[DIAG] TelegramBotClient registered");
builder.Services.AddHttpClient<HubClient>(client =>
{
    client.BaseAddress = new Uri(botSettings.HubUrl);
    client.Timeout = TimeSpan.FromSeconds(360);
    if (!string.IsNullOrEmpty(botSettings.HubApiKey))
        client.DefaultRequestHeaders.Add("X-Api-Key", botSettings.HubApiKey);
});
Console.Error.WriteLine("[DIAG] HubClient HttpClient registered");

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
builder.Services.AddSingleton<ICommand, AiAgentCommand>();
builder.Services.AddSingleton<ICommand, AiConfigCommand>();
builder.Services.AddSingleton<ICommand, HelpCommand>();
builder.Services.AddSingleton<ICommand, SysHealthCommand>();
builder.Services.AddSingleton<ICommand, LogCommand>();
builder.Services.AddSingleton<ICommand, UsersCommand>();
builder.Services.AddSingleton<ICommand, ClipboardCommand>();
builder.Services.AddSingleton<ICommand, UploadCommand>();
builder.Services.AddSingleton<ICommand, ScreenRecordCommand>();
builder.Services.AddSingleton<ICommand, MediaCommand>();
builder.Services.AddSingleton<ICommand, BroadcastCommand>();

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
builder.Services.AddSingleton<ICallbackHandler, AiCallbackHandler>();
builder.Services.AddSingleton<ICallbackHandler, AiConfigCallbackHandler>();
builder.Services.AddSingleton<ICallbackHandler, ShellCallbackHandler>();
builder.Services.AddSingleton<ICallbackHandler, AdminUserCallbackHandler>();
builder.Services.AddSingleton<ICallbackHandler, MediaCallbackHandler>();
builder.Services.AddSingleton<ICallbackHandler, BroadcastCallbackHandler>();

builder.Services.AddSingleton<CallbackRegistry>(sp =>
    new CallbackRegistry(sp.GetServices<ICallbackHandler>()));

builder.Services.AddSingleton<BotHandler>();
builder.Services.AddHostedService<TelegramBotService>();
builder.Services.AddHostedService<DeviceStatusMonitor>();
builder.Services.AddHostedService<HubHealthMonitor>();

Console.Error.WriteLine("[DIAG] Before host.Build()");
var host = builder.Build();
Console.Error.WriteLine("[DIAG] host.Build() done - DI container built");
Console.Error.WriteLine("[DIAG] After host.Build(), calling host.Run()");
host.Run();
Console.Error.WriteLine("[DIAG] host.Run() returned");
