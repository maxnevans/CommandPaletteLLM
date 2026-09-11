using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace CommandPaletteLLM;

internal sealed class OpenAiCompatibleLlmClient : ILlmClient
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(90),
    };

    private readonly LlmProviderSettingsStore _settingsStore;
    private readonly HttpClient _httpClient;
    private readonly ILlmEndpointMonitor _endpointMonitor;

    public OpenAiCompatibleLlmClient(LlmProviderSettingsStore settingsStore)
        : this(settingsStore, SharedHttpClient, new LlmEndpointMonitor(settingsStore))
    {
    }

    internal OpenAiCompatibleLlmClient(
        LlmProviderSettingsStore settingsStore,
        ILlmEndpointMonitor endpointMonitor)
        : this(settingsStore, SharedHttpClient, endpointMonitor)
    {
    }

    internal OpenAiCompatibleLlmClient(
        LlmProviderSettingsStore settingsStore,
        HttpClient httpClient,
        ILlmEndpointMonitor? endpointMonitor = null)
    {
        _settingsStore = settingsStore;
        _httpClient = httpClient;
        _endpointMonitor = endpointMonitor ?? new AssumedAvailableEndpointMonitor();
    }

    public async Task<string> CompleteAsync(
        string prompt,
        CancellationToken cancellationToken)
    {
        var settings = _settingsStore.Get();
        if (!LlmProviderSettingsStore.IsValid(settings))
        {
            throw new LlmRequestException("Configure a valid provider URL and model in extension settings.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildChatCompletionsUri(settings.BaseUrl));
        request.Content = JsonContent.Create(
            new ChatCompletionRequest
            {
                Model = settings.Model,
                Messages =
                [
                    new ChatMessage { Role = "user", Content = prompt },
                ],
            },
            CommandPaletteJsonContext.Default.ChatCompletionRequest);

        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                settings.ApiKey.Trim());
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _endpointMonitor.ReportUnreachable();
            throw;
        }
        catch (HttpRequestException)
        {
            _endpointMonitor.ReportUnreachable();
            throw;
        }

        using (response)
        {
            _endpointMonitor.ReportReachable();
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            ChatCompletionResponse? completion = null;
            try
            {
                completion = JsonSerializer.Deserialize(
                    responseJson,
                    CommandPaletteJsonContext.Default.ChatCompletionResponse);
            }
            catch (JsonException) when (!response.IsSuccessStatusCode)
            {
                // The HTTP error below is more useful than a JSON parsing error.
            }

            if (!response.IsSuccessStatusCode)
            {
                var detail = completion?.Error?.Message;
                throw new LlmRequestException(string.IsNullOrWhiteSpace(detail)
                    ? $"The provider returned HTTP {(int)response.StatusCode}."
                    : $"The provider returned HTTP {(int)response.StatusCode}: {detail}");
            }

            var content = completion?.Choices is { Count: > 0 }
                ? completion.Choices[0].Message?.Content
                : null;
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new LlmRequestException("The provider returned an empty response.");
            }

            return content.Trim();
        }
    }

    private static Uri BuildChatCompletionsUri(string baseUrl)
    {
        var normalized = baseUrl.Trim().TrimEnd('/');
        if (!normalized.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            normalized += "/chat/completions";
        }

        return new Uri(normalized, UriKind.Absolute);
    }
}

internal sealed class LlmRequestException : Exception
{
    public LlmRequestException(string message)
        : base(message)
    {
    }
}

internal sealed class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = [];
}

internal sealed class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

internal sealed class ChatCompletionResponse
{
    [JsonPropertyName("choices")]
    public List<ChatChoice> Choices { get; set; } = [];

    [JsonPropertyName("error")]
    public ChatCompletionError? Error { get; set; }
}

internal sealed class ChatChoice
{
    [JsonPropertyName("message")]
    public ChatMessage? Message { get; set; }
}

internal sealed class ChatCompletionError
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
