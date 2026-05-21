namespace TripPlannerV1.Services;

public interface IAiClient
{
    /// <summary>
    /// Sends the prompt to the configured LLM and returns the generated text.
    /// Returns null if the AI provider is not configured (e.g. missing API key).
    /// </summary>
    Task<AiCompletionResult> CompleteAsync(string prompt, CancellationToken cancellationToken = default);
}

public sealed record AiCompletionResult(bool Success, string? Content, string? Error, string? Provider, string? Model)
{
    public static AiCompletionResult NotConfigured() =>
        new(false, null, "AI provider is not configured. Set 'Ai:Provider', 'Ai:ApiKey' and 'Ai:Model' in configuration (or user secrets).", null, null);

    public static AiCompletionResult Ok(string content, string provider, string model) =>
        new(true, content, null, provider, model);

    public static AiCompletionResult Fail(string error, string provider, string? model) =>
        new(false, null, error, provider, model);
}
