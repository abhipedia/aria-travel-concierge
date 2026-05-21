using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace TripPlannerV1.Services;

/// <summary>
/// Minimal multi-provider LLM client. Supports OpenAI Chat Completions and
/// Anthropic (Claude) Messages APIs behind the same interface.
/// </summary>
public sealed class AiClient : IAiClient
{
    private const string OpenAiEndpoint = "https://api.openai.com/v1/chat/completions";
    private const string AnthropicEndpoint = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";

    private const string ProviderOpenAi = "OpenAI";
    private const string ProviderAnthropic = "Anthropic";

    private const string SystemPrompt =
        "You are Aria, an elite private travel concierge. Follow the user's instructions exactly, " +
        "respect the OUTPUT CONTRACT, and never include disclaimers about being an AI.";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly AiOptions _options;
    private readonly ILogger<AiClient> _logger;

    public AiClient(HttpClient http, IOptions<AiOptions> options, ILogger<AiClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiCompletionResult> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            return AiCompletionResult.NotConfigured();
        }

        var provider = NormalizeProvider(_options.Provider);
        var model = string.IsNullOrWhiteSpace(_options.Model)
            ? DefaultModelFor(provider)
            : _options.Model!.Trim();

        // Defensive cleanup: trim whitespace, strip stray quotes or a "Bearer " prefix users
        // sometimes paste in along with the key. These are the #1 cause of 401s.
        var apiKey = (_options.ApiKey ?? string.Empty).Trim().Trim('"', '\'');
        if (apiKey.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            apiKey = apiKey["Bearer ".Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return AiCompletionResult.Fail(
                "Ai:ApiKey is empty after trimming. Set it via user-secrets or the Ai__ApiKey environment variable.",
                provider, model);
        }

        if (apiKey.Length < 20)
        {
            return AiCompletionResult.Fail(
                $"Ai:ApiKey looks too short ({apiKey.Length} chars) — likely a placeholder. Replace it with the real key.",
                provider, model);
        }

        // Provider-aware key-prefix sanity check. Cheap to do, catches the common
        // "I pasted my OpenAI key into the Anthropic config" mistake before we round-trip.
        var prefixError = ValidateKeyPrefix(provider, apiKey);
        if (prefixError is not null)
        {
            return AiCompletionResult.Fail(prefixError, provider, model);
        }

        try
        {
            return provider switch
            {
                ProviderAnthropic => await CallAnthropicAsync(prompt, apiKey, model, cancellationToken),
                ProviderOpenAi    => await CallOpenAiAsync(prompt, apiKey, model, cancellationToken),
                _                 => AiCompletionResult.Fail(
                    $"Unknown Ai:Provider '{_options.Provider}'. Supported: OpenAI, Anthropic.",
                    provider, model),
            };
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "AI call timed out for provider {Provider}", provider);
            return AiCompletionResult.Fail("AI call timed out. Try again or reduce the prompt size.", provider, model);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "AI call network failure for provider {Provider}", provider);
            return AiCompletionResult.Fail($"Network error reaching {provider}: {ex.Message}", provider, model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI call failed for provider {Provider}", provider);
            return AiCompletionResult.Fail(ex.Message, provider, model);
        }
    }

    private static string NormalizeProvider(string? provider)
    {
        var p = (provider ?? string.Empty).Trim();
        if (p.Equals(ProviderAnthropic, StringComparison.OrdinalIgnoreCase) ||
            p.Equals("Claude", StringComparison.OrdinalIgnoreCase))
        {
            return ProviderAnthropic;
        }
        if (p.Equals(ProviderOpenAi, StringComparison.OrdinalIgnoreCase) ||
            p.Equals("GPT", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(p))
        {
            return ProviderOpenAi;
        }
        return p; // unknown — surfaces a helpful error in CompleteAsync
    }

    private static string DefaultModelFor(string provider) => provider switch
    {
        ProviderAnthropic => "claude-3-5-sonnet-latest",
        _                 => "gpt-4o-mini",
    };

    private static string? ValidateKeyPrefix(string provider, string apiKey) => provider switch
    {
        ProviderAnthropic when !apiKey.StartsWith("sk-ant-", StringComparison.Ordinal) =>
            "Ai:ApiKey does not look like an Anthropic key (expected 'sk-ant-…'). Check Ai:Provider and Ai:ApiKey.",
        ProviderOpenAi when !apiKey.StartsWith("sk-", StringComparison.Ordinal) =>
            "Ai:ApiKey does not look like an OpenAI key (expected 'sk-…'). Check Ai:Provider and Ai:ApiKey.",
        ProviderOpenAi when apiKey.StartsWith("sk-ant-", StringComparison.Ordinal) =>
            "Ai:ApiKey looks like an Anthropic key but Ai:Provider is OpenAI. Set Ai:Provider to 'Anthropic'.",
        _ => null,
    };

    private async Task<AiCompletionResult> CallOpenAiAsync(string prompt, string apiKey, string model, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, OpenAiEndpoint);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = JsonContent.Create(new OpenAiRequest(
            Model: model,
            Messages: new[]
            {
                new OpenAiMessage("system", SystemPrompt),
                new OpenAiMessage("user", prompt),
            },
            MaxTokens: _options.MaxTokens,
            Temperature: _options.Temperature));

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            return AiCompletionResult.Fail($"OpenAI returned {(int)resp.StatusCode}: {body}", ProviderOpenAi, model);
        }

        var parsed = JsonSerializer.Deserialize<OpenAiResponse>(body, JsonOpts);
        var content = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
        return string.IsNullOrWhiteSpace(content)
            ? AiCompletionResult.Fail("OpenAI returned an empty response.", ProviderOpenAi, model)
            : AiCompletionResult.Ok(content!, ProviderOpenAi, model);
    }

    private async Task<AiCompletionResult> CallAnthropicAsync(string prompt, string apiKey, string model, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, AnthropicEndpoint);
        req.Headers.Add("x-api-key", apiKey);
        req.Headers.Add("anthropic-version", AnthropicVersion);
        req.Content = JsonContent.Create(new AnthropicRequest(
            Model: model,
            MaxTokens: _options.MaxTokens,
            Temperature: _options.Temperature,
            System: SystemPrompt,
            Messages: new[] { new AnthropicMessage("user", prompt) }));

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            return AiCompletionResult.Fail($"Anthropic returned {(int)resp.StatusCode}: {body}", ProviderAnthropic, model);
        }

        var parsed = JsonSerializer.Deserialize<AnthropicResponse>(body, JsonOpts);

        // Claude returns an array of typed content blocks. Concatenate every "text" block
        // (non-text blocks like tool_use are ignored here — we don't request tools).
        var content = parsed?.Content is { Count: > 0 } blocks
            ? string.Concat(blocks
                .Where(b => string.Equals(b.Type, "text", StringComparison.Ordinal) && !string.IsNullOrEmpty(b.Text))
                .Select(b => b.Text))
            : null;

        if (string.IsNullOrWhiteSpace(content))
        {
            return AiCompletionResult.Fail(
                $"Anthropic returned no text content (stop_reason='{parsed?.StopReason ?? "unknown"}').",
                ProviderAnthropic, model);
        }

        if (string.Equals(parsed?.StopReason, "max_tokens", StringComparison.Ordinal))
        {
            _logger.LogWarning("Anthropic response was truncated by max_tokens={MaxTokens}. Consider raising Ai:MaxTokens.",
                _options.MaxTokens);
        }

        return AiCompletionResult.Ok(content!, ProviderAnthropic, model);
    }

    // ---- Wire types ----

    private sealed record OpenAiRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IEnumerable<OpenAiMessage> Messages,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("temperature")] double Temperature);

    private sealed record OpenAiMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OpenAiResponse(
        [property: JsonPropertyName("choices")] List<OpenAiChoice>? Choices);

    private sealed record OpenAiChoice(
        [property: JsonPropertyName("message")] OpenAiMessage? Message);

    private sealed record AnthropicRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("messages")] IEnumerable<AnthropicMessage> Messages);

    private sealed record AnthropicMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record AnthropicResponse(
        [property: JsonPropertyName("content")] List<AnthropicContent>? Content,
        [property: JsonPropertyName("stop_reason")] string? StopReason);

    private sealed record AnthropicContent(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("text")] string? Text);
}
