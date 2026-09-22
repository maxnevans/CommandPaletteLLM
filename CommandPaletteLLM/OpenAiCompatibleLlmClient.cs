using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
    private readonly string _providerId;
    private readonly HttpClient _httpClient;
    private readonly ILlmEndpointMonitor _endpointMonitor;

    public OpenAiCompatibleLlmClient(LlmProviderSettingsStore settingsStore)
        : this(settingsStore, "default", SharedHttpClient, new LlmEndpointMonitor(settingsStore, "default"))
    {
    }

    internal OpenAiCompatibleLlmClient(
        LlmProviderSettingsStore settingsStore,
        ILlmEndpointMonitor endpointMonitor,
        string providerId = "default")
        : this(settingsStore, providerId, SharedHttpClient, endpointMonitor)
    {
    }

    internal OpenAiCompatibleLlmClient(
        LlmProviderSettingsStore settingsStore,
        HttpClient httpClient,
        ILlmEndpointMonitor? endpointMonitor = null)
        : this(settingsStore, "default", httpClient, endpointMonitor)
    {
    }

    internal OpenAiCompatibleLlmClient(
        LlmProviderSettingsStore settingsStore,
        string providerId,
        HttpClient httpClient,
        ILlmEndpointMonitor? endpointMonitor = null)
    {
        _settingsStore = settingsStore;
        _providerId = providerId;
        _httpClient = httpClient;
        _endpointMonitor = endpointMonitor ?? new AssumedAvailableEndpointMonitor();
    }

    public async Task<IReadOnlyList<string>> CompleteAsync(
        string prompt,
        string customRequestArguments,
        CancellationToken cancellationToken)
    {
        var settings = _settingsStore.Get(_providerId);
        if (settings is null || !LlmProviderSettingsStore.IsValid(settings))
        {
            throw new LlmRequestException("Configure a valid provider URL and model in extension settings.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildChatCompletionsUri(settings.BaseUrl));
        request.Content = BuildRequestContent(settings.Model, prompt, customRequestArguments);

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

            var contents = completion?.Choices
                .Select(choice => choice.Message?.Content?.Trim())
                .Where(content => !string.IsNullOrWhiteSpace(content))
                .Distinct(StringComparer.Ordinal)
                .Cast<string>()
                .ToArray() ?? [];
            if (contents.Length == 0)
            {
                throw new LlmRequestException("The provider returned an empty response.");
            }

            return contents;
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

    private static ByteArrayContent BuildRequestContent(
        string model,
        string prompt,
        string customRequestArguments)
    {
        if (!CustomRequestArguments.TryParse(
            customRequestArguments,
            out var customArguments,
            out var error))
        {
            throw new LlmRequestException(error ?? "Custom request arguments are invalid.");
        }

        using (customArguments)
        using (var stream = new MemoryStream())
        {
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("model", model);
                writer.WritePropertyName("messages");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteString("role", "user");
                writer.WriteString("content", prompt);
                writer.WriteEndObject();
                writer.WriteEndArray();

                if (customArguments is not null)
                {
                    foreach (var property in customArguments.RootElement.EnumerateObject())
                    {
                        property.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }

            var content = new ByteArrayContent(stream.ToArray());
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8",
            };
            return content;
        }
    }
}

internal sealed class LlmRequestException : Exception
{
    public LlmRequestException(string message)
        : base(message)
    {
    }
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
