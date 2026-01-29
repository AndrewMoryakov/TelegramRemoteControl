using System.ServiceProcess;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.Menu;

namespace TelegramRemoteControl.Callbacks.Impl;

/// <summary>
/// Обработчик callback для управления службами (svc:*)
/// </summary>
public class ServiceCallbackHandler : ICallbackHandler
{
    public string Prefix => "svc";

    private readonly MenuBuilder _menu;

    public ServiceCallbackHandler(MenuBuilder menu)
    {
        _menu = menu;
    }

    public async Task HandleAsync(CallbackContext ctx)
    {
        if (ctx.Args.Length < 2)
        {
            await ctx.AnswerAsync("❌ Неверный формат");
            return;
        }

        var action = ctx.Args[0];
        var serviceName = ctx.Args[1];

        try
        {
            using var svc = new ServiceController(serviceName);

            switch (action)
            {
                case "start":
                    if (svc.Status != ServiceControllerStatus.Running)
                    {
                        svc.Start();
                        svc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    }
                    await ctx.AnswerAsync("▶️ Служба запущена");
                    await ShowServiceInfo(ctx, svc);
                    break;

                case "stop":
                    if (svc.Status == ServiceControllerStatus.Running)
                    {
                        svc.Stop();
                        svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    }
                    await ctx.AnswerAsync("⏹ Служба остановлена");
                    await ShowServiceInfo(ctx, svc);
                    break;

                case "restart":
                    if (svc.Status == ServiceControllerStatus.Running)
                    {
                        svc.Stop();
                        svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    }
                    svc.Start();
                    svc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    await ctx.AnswerAsync("🔄 Служба перезапущена");
                    await ShowServiceInfo(ctx, svc);
                    break;

                case "info":
                    svc.Refresh();
                    await ShowServiceInfo(ctx, svc);
                    await ctx.AnswerAsync();
                    break;

                default:
                    await ctx.AnswerAsync("❓ Неизвестное действие");
                    break;
            }
        }
        catch (Exception ex)
        {
            await ctx.AnswerAsync($"❌ {ex.Message}", showAlert: true);
        }
    }

    private async Task ShowServiceInfo(CallbackContext ctx, ServiceController svc)
    {
        svc.Refresh();

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
                InlineKeyboardButton.WithCallbackData("▶️ Старт", $"svc:start:{svc.ServiceName}"),
                InlineKeyboardButton.WithCallbackData("⏹ Стоп", $"svc:stop:{svc.ServiceName}")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔄 Рестарт", $"svc:restart:{svc.ServiceName}"),
                InlineKeyboardButton.WithCallbackData("🔃 Обновить", $"svc:info:{svc.ServiceName}")
            },
            new[] { InlineKeyboardButton.WithCallbackData("◀️ Меню", "menu") }
        });

        var text = $"""
            ⚙️ *Служба*

            📝 *Имя:* `{svc.ServiceName}`
            📋 *Отображаемое:* `{svc.DisplayName}`
            {statusIcon} *Статус:* `{svc.Status}`
            """;

        await ctx.EditTextAsync(text, keyboard);
    }
}
