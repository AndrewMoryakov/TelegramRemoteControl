namespace TelegramRemoteControl.Hub.Middleware;

public class ApiKeyMiddleware
{
    private const string HeaderName = "X-Api-Key";
    private readonly RequestDelegate _next;
    private readonly string _apiKey;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _apiKey = config.GetSection("HubSettings:ApiKey").Value ?? string.Empty;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Если ключ не настроен — пропускаем (dev/backward compat)
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var incoming)
            || incoming != _apiKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Unauthorized\"}");
            return;
        }

        await _next(context);
    }
}
