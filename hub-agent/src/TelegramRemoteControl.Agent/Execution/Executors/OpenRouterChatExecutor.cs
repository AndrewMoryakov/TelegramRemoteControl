using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class OpenRouterChatExecutor : ICommandExecutor
{
    private readonly AiSettings _settings;
    private static readonly HttpClient _http = new();

    public OpenRouterChatExecutor(AiSettings settings)
    {
        _settings = settings;
    }

    public async Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Arguments))
            return Error(command, "Укажите текст запроса");

        if (string.IsNullOrWhiteSpace(_settings.OpenRouterApiKey))
            return Error(command, "OpenRouter API ключ не настроен. Введите его через /aiconfig");

        if (string.IsNullOrWhiteSpace(_settings.Model))
            return Error(command, "Модель не выбрана. Выберите её через /aiconfig → 📋 Выбрать модель");

        var body = new
        {
            model    = _settings.Model,
            messages = new[] { new { role = "user", content = command.Arguments } }
        };

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));

            using var req = new HttpRequestMessage(HttpMethod.Post,
                "https://openrouter.ai/api/v1/chat/completions");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.OpenRouterApiKey);
            req.Headers.Add("HTTP-Referer", "https://github.com/TelegramRemoteControl");
            req.Headers.Add("X-Title", "TelepilotHub");
            req.Content = JsonContent.Create(body);

            var resp = await _http.SendAsync(req, cts.Token);

            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return Error(command, "Неверный OpenRouter API ключ (401)");

            resp.EnsureSuccessStatusCode();

            using var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(
                cancellationToken: cts.Token);

            var text = doc!.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "(пустой ответ)";

            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type      = ResponseType.Text,
                Success   = true,
                Text      = text
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Error(command, $"Таймаут запроса ({_settings.TimeoutSeconds} с)");
        }
        catch (Exception ex)
        {
            return Error(command, $"Ошибка OpenRouter: {ex.Message}");
        }
    }

    private static AgentResponse Error(AgentCommand command, string message) => new()
    {
        RequestId    = command.RequestId,
        Type         = ResponseType.Error,
        Success      = false,
        ErrorMessage = message
    };
}
