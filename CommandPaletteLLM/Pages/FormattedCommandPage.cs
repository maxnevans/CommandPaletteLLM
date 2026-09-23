using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CommandPaletteLLM;

internal sealed partial class FormattedCommandPage : DynamicListPage, IDisposable
{
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultStatusUpdateInterval = TimeSpan.FromMilliseconds(100);
    private const int CompactResultCharacterLimit = 120;

    private readonly string _promptFormat;
    private readonly string _customRequestArguments;
    private readonly string _templateRequestArguments;
    private readonly bool _enableAdvancedOutput;
    private readonly string? _systemPrompt;
    private readonly ILlmClient _llmClient;
    private readonly TimeSpan _debounce;
    private readonly TimeSpan _retryDelay;
    private readonly TimeSpan _statusUpdateInterval;
    private readonly Action? _pageAccessed;
    private readonly ILlmEndpointMonitor _endpointMonitor;
    private CancellationTokenSource? _requestCancellation;
    private CancellationTokenSource? _visitCancellation;
    private int _requestVersion;
    private int _retryLoopRunning;
    private bool _active;
    private volatile bool _requestInFlight;
    private string _currentQuery = string.Empty;
    private IListItem[] _items = [];

    public FormattedCommandPage(UserCommandDefinition definition)
        : this(
            definition,
            new OpenAiCompatibleLlmClient(new LlmProviderSettingsStore()),
            debounce: null)
    {
    }

    internal FormattedCommandPage(
        UserCommandDefinition definition,
        ILlmClient llmClient,
        TimeSpan? debounce = null,
        Action? pageAccessed = null,
        ILlmEndpointMonitor? endpointMonitor = null,
        TimeSpan? retryDelay = null,
        TimeSpan? statusUpdateInterval = null,
        string advancedOutputSystemPrompt = "",
        string templateRequestArguments = "")
    {
        _promptFormat = definition.Mode == CommandMode.Pipeline ? "{}" : definition.OutputFormat;
        _customRequestArguments = definition.CustomRequestArguments;
        _templateRequestArguments = templateRequestArguments;
        _enableAdvancedOutput = definition.EnableAdvancedOutput;
        _systemPrompt = definition.Mode == CommandMode.Default &&
            definition.EnableAdvancedOutput && definition.UseGlobalAdvancedOutputSystemPrompt &&
            !string.IsNullOrWhiteSpace(advancedOutputSystemPrompt)
                ? advancedOutputSystemPrompt : null;
        _llmClient = llmClient;
        _debounce = debounce ?? TimeSpan.FromMilliseconds(definition.SendDelayMilliseconds);
        _retryDelay = retryDelay ?? DefaultRetryDelay;
        _statusUpdateInterval = statusUpdateInterval ?? DefaultStatusUpdateInterval;
        _pageAccessed = pageAccessed;
        _endpointMonitor = endpointMonitor ?? new AssumedAvailableEndpointMonitor();
        _endpointMonitor.StatusChanged += EndpointStatusChanged;
        Icon = CommandIconStore.GetIcon(definition.IconPath);
        Id = $"CommandPaletteLLM.Command.{definition.Id}";
        Title = definition.Name;
        Name = "Open";
        PlaceholderText = "Type a message";
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        EnsureActive();
        _currentQuery = newSearch;
        StartCurrentQuery();
    }

    public override IListItem[] GetItems()
    {
        EnsureActive();
        return _items;
    }

    internal void CancelPendingRequest()
    {
        if (!_active && _requestCancellation is null && _items.Length == 0 && !IsLoading)
        {
            return;
        }

        _active = false;
        _currentQuery = string.Empty;
        CancelRequest();
        _visitCancellation?.Cancel();
        _visitCancellation?.Dispose();
        _visitCancellation = null;
        Interlocked.Increment(ref _requestVersion);
        _items = [];
        IsLoading = false;
        RaiseItemsChanged();
    }

    public void Dispose()
    {
        _endpointMonitor.StatusChanged -= EndpointStatusChanged;
        CancelPendingRequest();
    }

    private void EnsureActive()
    {
        if (_active)
        {
            return;
        }

        _active = true;
        _visitCancellation = new CancellationTokenSource();
        _pageAccessed?.Invoke();
        ShowEndpointStatus();
        _ = CheckOnActivationAsync(_visitCancellation.Token);
    }

    private async Task CheckOnActivationAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await _endpointMonitor.CheckAsync(force: true, cancellationToken)
                .ConfigureAwait(false))
            {
                EnsureRetryLoop();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void StartCurrentQuery()
    {
        CancelRequest();
        var requestVersion = Interlocked.Increment(ref _requestVersion);

        if (string.IsNullOrWhiteSpace(_currentQuery))
        {
            ShowEndpointStatus();
            return;
        }

        if (_endpointMonitor.Status != LlmEndpointStatus.Available)
        {
            ShowEndpointStatus();
            if (_endpointMonitor.Status == LlmEndpointStatus.Unavailable)
            {
                EnsureRetryLoop();
            }

            return;
        }

        if (!OutputFormatter.TryFormat(_promptFormat, _currentQuery, out var prompt, out var error))
        {
            Publish([CreateStatusItem($"Invalid prompt format: {error}")], isLoading: false);
            return;
        }

        var cancellation = new CancellationTokenSource();
        _requestCancellation = cancellation;
        _ = LoadResponseAsync(prompt, requestVersion, cancellation.Token);
    }

    private async Task LoadResponseAsync(
        string prompt,
        int requestVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            var sendAt = DateTimeOffset.UtcNow + _debounce;
            while (sendAt > DateTimeOffset.UtcNow)
            {
                var remaining = sendAt - DateTimeOffset.UtcNow;
                PublishStatus(
                    requestVersion,
                    $"Sending to LLM in {FormatDuration(remaining)}…",
                    isLoading: true,
                    cancellationToken);
                await Task.Delay(
                    remaining < _statusUpdateInterval ? remaining : _statusUpdateInterval,
                    cancellationToken).ConfigureAwait(false);
            }

            PublishStatus(
                requestVersion,
                "Checking LLM connection…",
                isLoading: true,
                cancellationToken);
            if (!await _endpointMonitor.CheckAsync(force: false, cancellationToken)
                .ConfigureAwait(false))
            {
                EnsureRetryLoop();
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            PublishStatus(
                requestVersion,
                "Waiting for LLM response…",
                isLoading: true,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _requestInFlight = true;
            var responses = await _llmClient.CompleteAsync(
                prompt,
                systemPrompt: _systemPrompt,
                _templateRequestArguments,
                _customRequestArguments,
                cancellationToken).ConfigureAwait(false);
            Publish(
                requestVersion,
                responses
                    .SelectMany((response, index) =>
                        CreateResponseItems(
                            response,
                            index,
                            responses.Count,
                            _enableAdvancedOutput))
                    .ToArray(),
                isLoading: false,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (LlmRequestException exception)
        {
            PublishError(requestVersion, exception.Message, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            PublishError(requestVersion, exception.Message, cancellationToken);
        }
        catch (JsonException)
        {
            PublishError(requestVersion, "The provider returned invalid JSON.", cancellationToken);
        }
        catch (TaskCanceledException)
        {
            PublishError(requestVersion, "The provider request timed out.", cancellationToken);
        }
        finally
        {
            if (requestVersion == _requestVersion)
            {
                _requestInFlight = false;
            }
        }
    }

    private void EndpointStatusChanged()
    {
        if (!_active)
        {
            return;
        }

        if (_endpointMonitor.Status == LlmEndpointStatus.Available && _requestInFlight)
        {
            return;
        }

        StartCurrentQuery();
        if (_endpointMonitor.Status == LlmEndpointStatus.Unavailable)
        {
            EnsureRetryLoop();
        }
    }

    private void EnsureRetryLoop()
    {
        if (!_active || _visitCancellation is null ||
            Interlocked.Exchange(ref _retryLoopRunning, 1) != 0)
        {
            return;
        }

        _ = RetryUntilAvailableAsync(_visitCancellation.Token);
    }

    private async Task RetryUntilAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (_active && _endpointMonitor.Status == LlmEndpointStatus.Unavailable)
            {
                var retryAt = DateTimeOffset.UtcNow + _retryDelay;
                while (retryAt > DateTimeOffset.UtcNow &&
                    _endpointMonitor.Status == LlmEndpointStatus.Unavailable)
                {
                    ShowUnavailable(retryAt - DateTimeOffset.UtcNow);
                    var remaining = retryAt - DateTimeOffset.UtcNow;
                    await Task.Delay(
                        remaining < _statusUpdateInterval ? remaining : _statusUpdateInterval,
                        cancellationToken).ConfigureAwait(false);
                }

                if (_endpointMonitor.Status == LlmEndpointStatus.Available)
                {
                    return;
                }

                Publish([CreateStatusItem("Checking LLM connection…")], isLoading: true);
                await _endpointMonitor.CheckAsync(force: true, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            Interlocked.Exchange(ref _retryLoopRunning, 0);
            if (_active && _endpointMonitor.Status == LlmEndpointStatus.Unavailable)
            {
                EnsureRetryLoop();
            }
        }
    }

    private void ShowEndpointStatus()
    {
        switch (_endpointMonitor.Status)
        {
            case LlmEndpointStatus.Unknown:
                Publish([CreateStatusItem("Checking LLM connection…")], isLoading: true);
                break;
            case LlmEndpointStatus.Unavailable:
                ShowUnavailable(_retryDelay);
                break;
            default:
                Publish([], isLoading: false);
                break;
        }
    }

    private void ShowUnavailable(TimeSpan retryIn) =>
        Publish(
            [CreateStatusItem($"LLM connection unavailable. Retrying in {FormatDuration(retryIn)}…")],
            isLoading: false);

    private void PublishError(
        int requestVersion,
        string message,
        CancellationToken cancellationToken) =>
        Publish(
            requestVersion,
            [CreateStatusItem($"LLM request failed: {message}")],
            isLoading: false,
            cancellationToken);

    private void PublishStatus(
        int requestVersion,
        string message,
        bool isLoading,
        CancellationToken cancellationToken) =>
        Publish(
            requestVersion,
            [CreateStatusItem(message)],
            isLoading,
            cancellationToken);

    private void Publish(
        int requestVersion,
        IListItem[] items,
        bool isLoading,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || requestVersion != _requestVersion)
        {
            return;
        }

        Publish(items, isLoading);
    }

    private void Publish(IListItem[] items, bool isLoading)
    {
        if (!_active)
        {
            return;
        }

        _items = items;
        ShowDetails = items.Any(item => item.Details is not null);
        IsLoading = isLoading;
        RaiseItemsChanged();
    }

    private static ListItem CreateStatusItem(string message) =>
        new(new NoOpCommand()) { Title = message };

    private static IListItem[] CreateResponseItems(
        string response,
        int index,
        int responseCount,
        bool enableAdvancedOutput)
    {
        if (enableAdvancedOutput && AdvancedOutputParser.TryParse(response, out var entries))
        {
            return entries
                .Select(entry => (IListItem)CreateAdvancedResponseItem(entry))
                .ToArray();
        }

        return [CreateDefaultResponseItem(response, index, responseCount)];
    }

    private static ListItem CreateAdvancedResponseItem(AdvancedOutputEntry entry)
    {
        if (entry.Output is not null)
        {
            return CreateAdvancedResponseItem(entry.Output);
        }

        return new ListItem(new CopyTextCommand(entry.RawContent))
        {
            Title = entry.ErrorTitle,
            TextToSuggest = entry.RawContent,
            Details = new Details
            {
                Title = entry.ErrorTitle,
                Body = entry.RawContent,
                Size = ContentSize.Large,
            },
        };
    }

    private static ListItem CreateAdvancedResponseItem(AdvancedOutput output)
    {
        var copyText = string.IsNullOrEmpty(output.Details) ? output.Title : output.Details;
        return new ListItem(new CopyTextCommand(copyText))
        {
            Title = output.Title,
            Subtitle = output.Subtitle,
            TextToSuggest = copyText,
            Section = output.Section,
            Tags = output.Tags.Select(tag => new Tag(tag)).ToArray(),
            Details = string.IsNullOrEmpty(output.Details) ? null : new Details
            {
                Title = output.Title,
                Body = output.Details,
                Size = ContentSize.Large,
            },
        };
    }

    private static ListItem CreateDefaultResponseItem(
        string response,
        int index,
        int responseCount)
    {
        var lines = response
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var title = lines.FirstOrDefault() ?? response;
        var subtitle = lines.Length > 1
            ? string.Join(" ", lines.Skip(1))
            : responseCount > 1
                ? $"Variation {index + 1}"
                : "Select to copy the full response";
        var needsDetails = response.Length > CompactResultCharacterLimit ||
            response.Contains('\r') ||
            response.Contains('\n');

        return new ListItem(new CopyTextCommand(response))
        {
            Title = title,
            Subtitle = subtitle,
            TextToSuggest = response,
            Details = needsDetails ? new Details
            {
                Title = responseCount > 1
                    ? $"Response variation {index + 1}"
                    : "LLM response",
                Body = response,
                Size = ContentSize.Large,
            } : null,
        };
    }

    private static string FormatDuration(TimeSpan duration) =>
        $"{Math.Max(0, duration.TotalSeconds):0.0}s";

    private void CancelRequest()
    {
        _requestInFlight = false;
        _requestCancellation?.Cancel();
        _requestCancellation?.Dispose();
        _requestCancellation = null;
    }
}
