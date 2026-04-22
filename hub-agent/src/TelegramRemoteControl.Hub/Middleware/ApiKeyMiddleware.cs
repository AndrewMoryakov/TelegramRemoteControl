using System.Security.Cryptography;
using System.Text;

namespace TelegramRemoteControl.Hub.Middleware;

public class ApiKeyMiddleware
{
    private const string HeaderName = "X-Api-Key";
    private readonly RequestDelegate _next;
    private readonly byte[] _apiKeyBytes;
    private readonly bool _keyConfigured;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        var key = config.GetSection("HubSettings:ApiKey").Value ?? string.Empty;
        _keyConfigured = !string.IsNullOrWhiteSpace(key);
        _apiKeyBytes = _keyConfigured ? Encoding.UTF8.GetBytes(key) : Array.Empty<byte>();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Если ключ не настроен — пропускаем (dev/backward compat). Startup emits a warning.
        if (!_keyConfigured)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var incoming)
            || !FixedTimeEquals(incoming.ToString(), _apiKeyBytes))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Unauthorized\"}");
            return;
        }

        await _next(context);
    }

    private static bool FixedTimeEquals(string incoming, byte[] expected)
    {
        var incomingBytes = Encoding.UTF8.GetBytes(incoming);
        return CryptographicOperations.FixedTimeEquals(incomingBytes, expected);
    }
}
