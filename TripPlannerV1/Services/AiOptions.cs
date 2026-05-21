namespace TripPlannerV1.Services;

/// <summary>
/// Configuration for the LLM provider. Bind from the "Ai" config section.
/// Example (appsettings.json / user secrets):
///   "Ai": {
///     "Provider": "OpenAI",      // or "Anthropic"
///     "ApiKey":   "sk-...",
///     "Model":    "gpt-4o-mini"  // or e.g. "claude-3-5-sonnet-latest"
///   }
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public string Provider { get; set; } = "OpenAI";
    public string? ApiKey { get; set; }
    public string? Model { get; set; }

    /// <summary>Maximum tokens for the model response. Anthropic requires this; OpenAI caps optional.</summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>Sampling temperature (0.0 - 2.0). Lower = more deterministic.</summary>
    public double Temperature { get; set; } = 0.7;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Provider);
}
