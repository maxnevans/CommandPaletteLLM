using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CommandPaletteLLM;

internal sealed partial class FormattedCommandPage : DynamicListPage
{
    private readonly string _promptFormat;
    private readonly ILlmClient _llmClient;
    private readonly TimeSpan _debounce;
    private readonly Action? _pageAccessed;
    private CancellationTokenSource? _requestCancellation;
    private int _requestVersion;
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
        Action? pageAccessed = null)
    {
        _promptFormat = definition.OutputFormat;
        _llmClient = llmClient;
        _debounce = debounce ?? TimeSpan.FromMilliseconds(definition.SendDelayMilliseconds);
        _pageAccessed = pageAccessed;
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Id = $"CommandPaletteLLM.Command.{definition.Id}";
        Title = definition.Name;
        Name = "Open";
        PlaceholderText = "Type a message";
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        CancelRequest();
        var requestVersion = Interlocked.Increment(ref _requestVersion);

        if (string.IsNullOrWhiteSpace(newSearch))
        {
            _items = [];
            IsLoading = false;
            RaiseItemsChanged();
            return;
        }

        if (!OutputFormatter.TryFormat(_promptFormat, newSearch, out var prompt, out var error))
        {
            _items = [CreateErrorItem($"Invalid prompt format: {error}")];
            IsLoading = false;
            RaiseItemsChanged();
            return;
        }

        var cancellation = new CancellationTokenSource();
        _requestCancellation = cancellation;
        _items = [];
        IsLoading = true;
        RaiseItemsChanged();
        _ = LoadResponseAsync(prompt, requestVersion, cancellation.Token);
    }

    public override IListItem[] GetItems()
    {
        _pageAccessed?.Invoke();
        return _items;
    }

    internal void CancelPendingRequest()
    {
        if (_requestCancellation is null && _items.Length == 0 && !IsLoading)
        {
            return;
        }

        CancelRequest();
        Interlocked.Increment(ref _requestVersion);
        _items = [];
        IsLoading = false;
        RaiseItemsChanged();
    }

    private async Task LoadResponseAsync(
        string prompt,
        int requestVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_debounce, cancellationToken).ConfigureAwait(false);
            var response = await _llmClient.CompleteAsync(prompt, cancellationToken)
                .ConfigureAwait(false);
            Publish(
                requestVersion,
                [new ListItem(new CopyTextCommand(response)) { Title = response }],
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
    }

    private void PublishError(
        int requestVersion,
        string message,
        CancellationToken cancellationToken) =>
        Publish(requestVersion, [CreateErrorItem($"LLM request failed: {message}")], cancellationToken);

    private void Publish(
        int requestVersion,
        IListItem[] items,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || requestVersion != _requestVersion)
        {
            return;
        }

        _items = items;
        IsLoading = false;
        RaiseItemsChanged();
    }

    private static ListItem CreateErrorItem(string message) =>
        new(new NoOpCommand()) { Title = message };

    private void CancelRequest()
    {
        _requestCancellation?.Cancel();
        _requestCancellation?.Dispose();
        _requestCancellation = null;
    }
}
