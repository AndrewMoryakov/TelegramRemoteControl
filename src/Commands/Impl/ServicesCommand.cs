using System.ServiceProcess;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramRemoteControl.Commands.Impl;

public class ServicesCommand : CommandBase
{
    public override string Id => "services";
    public override string[] Aliases => new[] { "/services", "/svc" };
    public override string Title => "Службы";
    public override string Icon => "⚙️";
    public override string Description => "Управление службами";
    public override string Category => Categories.Control;

    private static readonly Dictionary<long, ServiceInfo[]> _cache = new();

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        // Если передано имя службы — показать детали
        if (!string.IsNullOrWhiteSpace(ctx.Arguments))
        {
            await ShowServiceDetails(ctx, ctx.Arguments);
            return;
        }

        await SendAsync(ctx, "⏳ Загрузка служб...");

        var services = ServiceController.GetServices()
            .OrderBy(s => s.DisplayName)
            .Select(s => new ServiceInfo
            {
                Name = s.ServiceName,
                DisplayName = s.DisplayName,
                Status = s.Status
            })
            .ToArray();

        _cache[ctx.ChatId] = services;

        // Показываем только запущенные
        var running = services.Where(s => s.Status == ServiceControllerStatus.Running).Take(20).ToList();

        var lines = running.Select((s, i) =>
            $"{i + 1,2}. {Truncate(s.DisplayName, 30)}");

        var text = $"""
            ⚙️ *Запущенные службы (топ-20):*
            ```
            {string.Join("\n", lines)}
            ```

            💡 Детали: `/svc <имя>`
            Примеры:
            `/svc wuauserv` — Windows Update
            `/svc spooler` — Печать
            """;

        await ctx.ReplyWithBack(text);
    }

    private async Task ShowServiceDetails(CommandContext ctx, string serviceName)
    {
        try
        {
            using var svc = new ServiceController(serviceName.Trim());

            var statusIcon = svc.Status switch
            {
                ServiceControllerStatus.Running => "🟢",
                ServiceControllerStatus.Stopped => "🔴",
                ServiceControllerStatus.Paused => "🟡",
                _ => "⚪"
            };

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("▶️ Запустить", $"svc:start:{svc.ServiceName}"),
                    InlineKeyboardButton.WithCallbackData("⏹ Остановить", $"svc:stop:{svc.ServiceName}")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🔄 Перезапустить", $"svc:restart:{svc.ServiceName}"),
                    InlineKeyboardButton.WithCallbackData("🔃 Обновить", $"svc:info:{svc.ServiceName}")
                },
                new[] { InlineKeyboardButton.WithCallbackData("◀️ Меню", "menu") }
            });

            var text = $"""
                ⚙️ *Служба*

                📝 *Имя:* `{svc.ServiceName}`
                📋 *Отображаемое:* `{svc.DisplayName}`
                {statusIcon} *Статус:* `{svc.Status}`
                🔧 *Тип:* `{svc.StartType}`
                """;

            await ctx.Bot.SendMessage(ctx.ChatId, text,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: keyboard,
                cancellationToken: ctx.CancellationToken);
        }
        catch (Exception ex)
        {
            await ctx.ReplyWithBack($"❌ Служба не найдена: `{serviceName}`\n\n{ex.Message}");
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 2)] + "..";

    public class ServiceInfo
    {
        public string Name { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public ServiceControllerStatus Status { get; set; }
    }
}
