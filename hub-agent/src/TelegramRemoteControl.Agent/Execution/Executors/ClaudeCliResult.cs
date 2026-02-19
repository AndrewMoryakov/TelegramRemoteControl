using System.Text.Json.Serialization;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class ClaudeCliResult
{
    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("cost_usd")]
    public double? CostUsd { get; set; }

    [JsonPropertyName("duration_ms")]
    public long? DurationMs { get; set; }

    [JsonPropertyName("num_turns")]
    public int? NumTurns { get; set; }

    [JsonPropertyName("is_error")]
    public bool IsError { get; set; }
}
