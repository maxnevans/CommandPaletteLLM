using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CommandPaletteLLM;

internal sealed partial class FormattedFallbackItem : FallbackCommandItem
{
    private readonly StableCopyTextCommand _copyCommand;
    private readonly string _promptFormat;
    private readonly ILlmClient _llmClient;
    private readonly TimeSpan _debounce;
    private readonly Func<string, bool>? _isTopLevelCommandQuery;
    private readonly Action? _rootQueryObserved;
    private readonly ILlmEndpointMonitor _endpointMonitor;
    private string _lastQuery = string.Empty;
    private CancellationTokenSource? _requestCancellation;
    private int _requestVersion;

    public FormattedFallbackItem(UserCommandDefinition definition)
        : this(
            definition,
            new OpenAiCompatibleLlmClient(new LlmProviderSettingsStore()),
            debounce: null)
    {
    }

    internal FormattedFallbackItem(
        UserCommandDefinition definition,
        ILlmClient llmClient,
        TimeSpan? debounce = null,
        Func<string, bool>? isTopLevelCommandQuery = null,
        Action? rootQueryObserved = null,
        ILlmEndpointMonitor? endpointMonitor = null)
        : base(
            CreateCopyCommand(definition.Id),
            definition.Name,
            GetFallbackId(definition.Id))
    {
        _copyCommand = Command as StableCopyTextCommand ??
            throw new InvalidOperationException("The fallback copy command was not initialized.");
        _promptFormat = definition.OutputFormat;
        _llmClient = llmClient;
        _debounce = debounce ?? TimeSpan.FromMilliseconds(definition.SendDelayMilliseconds);
        _isTopLevelCommandQuery = isTopLevelCommandQuery;
        _rootQueryObserved = rootQueryObserved;
        _endpointMonitor = endpointMonitor ?? new AssumedAvailableEndpointMonitor();
        Icon = CommandIconStore.GetIcon(definition.IconPath);
        Title = string.Empty;
    }

    public override void UpdateQuery(string query)
    {
        _lastQuery = query;
        _rootQueryObserved?.Invoke();
        StartQuery(query);
    }

    private void StartQuery(string query)
    {
        CancelRequest();
        var requestVersion = Interlocked.Increment(ref _requestVersion);
        _copyCommand.SetText(null);

        if (string.IsNullOrWhiteSpace(query) ||
            _endpointMonitor.Status != LlmEndpointStatus.Available ||
            _isTopLevelCommandQuery?.Invoke(query) == true ||
            !OutputFormatter.TryFormat(_promptFormat, query, out var prompt, out _))
        {
            Title = string.Empty;
            return;
        }

        var cancellation = new CancellationTokenSource();
        _requestCancellation = cancellation;
        Title = "Waiting for LLM response…";
        _ = LoadResponseAsync(prompt, requestVersion, cancellation.Token);
    }

    private async Task LoadResponseAsync(
        string prompt,
        int requestVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_debounce, cancellationToken).ConfigureAwait(false);
            var responses = await _llmClient.CompleteAsync(
                prompt,
                variations: 1,
                cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            Publish(requestVersion, responses[0], canCopy: true, cancellationToken);
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
    }

    private void PublishError(
        int requestVersion,
        string message,
        CancellationToken cancellationToken) =>
        Publish(
            requestVersion,
            $"LLM request failed: {message}",
            canCopy: false,
            cancellationToken);

    private void Publish(
        int requestVersion,
        string title,
        bool canCopy,
        CancellationToken cancellationToken)
    {
        if (!cancellationToken.IsCancellationRequested && requestVersion == _requestVersion)
        {
            _copyCommand.SetText(canCopy ? title : null);
            Title = title;
        }
    }

    internal void CancelPendingRequest()
    {
        CancelRequest();
        Interlocked.Increment(ref _requestVersion);
        _copyCommand.SetText(null);
        Title = string.Empty;
    }

    internal void EndpointStatusChanged(bool allowGlobalResults)
    {
        if (_endpointMonitor.Status != LlmEndpointStatus.Available || !allowGlobalResults)
        {
            CancelPendingRequest();
            return;
        }

        StartQuery(_lastQuery);
    }

    private void CancelRequest()
    {
        _requestCancellation?.Cancel();
        _requestCancellation?.Dispose();
        _requestCancellation = null;
    }

    private static string GetFallbackId(string definitionId) =>
        $"CommandPaletteLLM.Global.{definitionId}";

    private static StableCopyTextCommand CreateCopyCommand(string definitionId) =>
        new($"{GetFallbackId(definitionId)}.Copy");
}
