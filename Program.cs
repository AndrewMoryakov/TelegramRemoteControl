using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramRemoteControl;
using TelegramRemoteControl.Callbacks;
using TelegramRemoteControl.Callbacks.Impl;
using TelegramRemoteControl.Commands;
using TelegramRemoteControl.Commands.Impl;
using TelegramRemoteControl.Helpers;
using TelegramRemoteControl.Menu;

// Legacy: headless screenshot mode (no longer used — screenshots now via PowerShell)
if (args.Length >= 2 && args[0] == "--screenshot")
{
    ScreenshotHelper.CaptureAndSave(args[1]);
    return;
}

// Регистрация кодировок
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = Host.CreateApplicationBuilder(args);

// Конфигурация
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Логирование
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Регистрация сервисов
builder.Services.Configure<BotSettings>(builder.Configuration.GetSection("BotSettings"));
builder.Services.AddSingleton<CommandRegistry>(sp =>
{
    return new CommandRegistry().Register(
        new StatusCommand(),
        new ProcessesCommand(),
        new DrivesCommand(),
        new IpCommand(),
        new MonitorCommand(),
        new UptimeCommand(),
        new FilesCommand(),
        new ScreenshotCommand(),
        new CmdCommand(),
        new PowerShellCommand(),
        new KillCommand(),
        new LockCommand(),
        new ShutdownCommand(),
        new RestartCommand(),
        new SleepCommand(),
        new HibernateCommand(),
        new ServicesCommand(),
        new WindowsCommand()
    );
});

builder.Services.AddSingleton<MenuBuilder>(sp => new MenuBuilder(sp.GetRequiredService<CommandRegistry>()));
builder.Services.AddSingleton<CallbackRegistry>(sp =>
{
    var menu = sp.GetRequiredService<MenuBuilder>();
    return new CallbackRegistry().Register(
        new ProcessCallbackHandler(menu),
        new ServiceCallbackHandler(menu),
        new FileCallbackHandler(menu),
        new WindowCallbackHandler(menu)
    );
});

builder.Services.AddHostedService<BotService>();

// Windows Service support
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "TelegramRemoteControl";
});

var host = builder.Build();
await host.RunAsync();

/// <summary>
/// Фоновый сервис Telegram бота
/// </summary>
public class BotService : BackgroundService
{
    private readonly ILogger<BotService> _logger;
    private readonly IConfiguration _config;
    private readonly CommandRegistry _commands;
    private readonly CallbackRegistry _callbacks;
    private readonly MenuBuilder _menu;

    private TelegramBotClient? _bot;
    private BotHandler? _handler;

    public BotService(
        ILogger<BotService> logger,
        IConfiguration config,
        CommandRegistry commands,
        CallbackRegistry callbacks,
        MenuBuilder menu)
    {
        _logger = logger;
        _config = config;
        _commands = commands;
        _callbacks = callbacks;
        _menu = menu;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = _config.GetSection("BotSettings").Get<BotSettings>();

        if (settings is null || string.IsNullOrEmpty(settings.Token) || settings.Token == "YOUR_BOT_TOKEN_HERE")
        {
            _logger.LogError("Bot token not configured in appsettings.json");
            return;
        }

        _bot = new TelegramBotClient(settings.Token);
        _handler = new BotHandler(_bot, settings, _commands, _callbacks, _menu, _logger);

        // Retry loop — перезапуск polling при сбоях
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var me = await _bot.GetMe(stoppingToken);
                _logger.LogInformation("Bot started: @{Username}", me.Username);

                // Устанавливаем меню команд
                var botCommands = _commands.All
                    .Where(c => c.Aliases.Length > 0)
                    .Select(c => new BotCommand
                    {
                        Command = c.Aliases[0].TrimStart('/'),
                        Description = $"{c.Icon} {c.Description}"
                    })
                    .ToList();

                botCommands.Insert(0, new BotCommand { Command = "menu", Description = "🤖 Главное меню" });
                await _bot.SetMyCommands(botCommands, cancellationToken: stoppingToken);

                _logger.LogInformation("Loaded {Count} commands, authorized users: [{Users}]",
                    _commands.All.Count, string.Join(", ", settings.AuthorizedUsers));

                // Запуск polling
                _bot.StartReceiving(
                    updateHandler: HandleUpdateAsync,
                    errorHandler: HandleErrorAsync,
                    receiverOptions: new ReceiverOptions
                    {
                        AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
                    },
                    cancellationToken: stoppingToken
                );

                // Ожидание завершения
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Bot service stopping...");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bot service error, restarting in 5 seconds...");
                try { await Task.Delay(5000, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(2);

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        long? chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(CommandTimeout);

            if (update.Message is { } message)
            {
                var from = message.From?.Username ?? message.From?.Id.ToString() ?? "?";
                _logger.LogInformation("[MSG] {From}: {Text}", from, message.Text);
                await _handler!.HandleMessageAsync(message, cts.Token);
            }
            else if (update.CallbackQuery is { } callback)
            {
                var from = callback.From.Username ?? callback.From.Id.ToString();
                _logger.LogInformation("[BTN] {From}: {Data}", from, callback.Data);
                await _handler!.HandleCallbackAsync(callback, cts.Token);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Command timed out after {Timeout}", CommandTimeout);
            await TryNotifyUser(chatId, "⚠️ Команда превысила время ожидания и была отменена.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update");
            await TryNotifyUser(chatId, $"❌ Внутренняя ошибка: {ex.Message}");
        }
    }

    private async Task TryNotifyUser(long? chatId, string text)
    {
        if (chatId is null || _bot is null) return;
        try
        {
            await _bot.SendMessage(chatId.Value, text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify user about error");
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Telegram API error");
        return Task.CompletedTask;
    }
}
