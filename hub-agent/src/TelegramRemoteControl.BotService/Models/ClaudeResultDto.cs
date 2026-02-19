using System.Text.Json.Serialization;

namespace TelegramRemoteControl.BotService.Models;

public class ClaudeResultDto
{
    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }
}
