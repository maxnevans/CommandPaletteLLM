using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace CommandPaletteLLM;

internal sealed class LlmEndpointMonitor : ILlmEndpointMonitor
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(1200);
    private static readonly HttpClient SharedHttpClient = new();

    private readonly object _sync = new();
    private readonly LlmProviderSettingsStore _settingsStore;
    private readonly HttpClient _httpClient;
    private Task<bool>? _activeCheck;
    private DateTimeOffset _lastCheck = DateTimeOffset.MinValue;
    private LlmEndpointStatus _status;
    private int _generation;

    public LlmEndpointMonitor(LlmProviderSettingsStore settingsStore)
        : this(settingsStore, SharedHttpClient)
    {
    }

    internal LlmEndpointMonitor(
        LlmProviderSettingsStore settingsStore,
        HttpClient httpClient)
    {
        _settingsStore = settingsStore;
        _httpClient = httpClient;
    }

    public LlmEndpointStatus Status
    {
        get
        {
            lock (_sync)
            {
                return _status;
            }
        }
    }

    public event Action? StatusChanged;

    public Task<bool> CheckAsync(bool force, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (_activeCheck is { IsCompleted: false })
            {
                return _activeCheck;
            }

            if (!force && DateTimeOffset.UtcNow - _lastCheck < CacheDuration)
            {
                return Task.FromResult(_status == LlmEndpointStatus.Available);
            }

            _activeCheck = CheckCoreAsync(_generation, cancellationToken);
            return _activeCheck;
        }
    }

    public void Invalidate()
    {
        lock (_sync)
        {
            _generation++;
            _activeCheck = null;
            _lastCheck = DateTimeOffset.MinValue;
        }

        SetStatus(LlmEndpointStatus.Unknown);
    }

    public void ReportReachable()
    {
        lock (_sync)
        {
            _lastCheck = DateTimeOffset.UtcNow;
        }

        SetStatus(LlmEndpointStatus.Available);
    }

    public void ReportUnreachable()
    {
        lock (_sync)
        {
            _lastCheck = DateTimeOffset.UtcNow;
        }

        SetStatus(LlmEndpointStatus.Unavailable);
    }

    private async Task<bool> CheckCoreAsync(
        int generation,
        CancellationToken cancellationToken)
    {
        var settings = _settingsStore.Get();
        if (!LlmProviderSettingsStore.IsValid(settings))
        {
            ReportProbeResult(LlmEndpointStatus.Unavailable, generation);
            return false;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildModelsUri(settings.BaseUrl));
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                settings.ApiKey.Trim());
        }

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token).ConfigureAwait(false);
            ReportProbeResult(LlmEndpointStatus.Available, generation);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ReportProbeResult(LlmEndpointStatus.Unavailable, generation);
            return false;
        }
        catch (HttpRequestException)
        {
            ReportProbeResult(LlmEndpointStatus.Unavailable, generation);
            return false;
        }
    }

    private void SetStatus(LlmEndpointStatus status)
    {
        var changed = false;
        lock (_sync)
        {
            if (_status != status)
            {
                _status = status;
                changed = true;
            }
        }

        if (changed)
        {
            StatusChanged?.Invoke();
        }
    }

    private void ReportProbeResult(LlmEndpointStatus status, int generation)
    {
        var changed = false;
        lock (_sync)
        {
            if (generation != _generation)
            {
                return;
            }

            _lastCheck = DateTimeOffset.UtcNow;
            if (_status != status)
            {
                _status = status;
                changed = true;
            }
        }

        if (changed)
        {
            StatusChanged?.Invoke();
        }
    }

    private static Uri BuildModelsUri(string baseUrl)
    {
        var normalized = baseUrl.Trim().TrimEnd('/');
        const string completionsPath = "/chat/completions";
        if (normalized.EndsWith(completionsPath, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^completionsPath.Length];
        }

        return new Uri($"{normalized}/models", UriKind.Absolute);
    }
}
