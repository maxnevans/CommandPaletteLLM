using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Linq;
using System.Threading;

namespace CommandPaletteLLM;

public partial class CommandPaletteLLMCommandsProvider : CommandProvider
{
    private readonly UserCommandStore _store;
    private readonly LlmProviderSettingsStore _providerSettingsStore;
    private readonly ILlmClient _llmClient;
    private readonly ILlmEndpointMonitor _endpointMonitor;
    private FormattedCommandPage[] _commandPages = [];
    private FormattedFallbackItem[] _formattedFallbackItems = [];
    private bool _commandPageActive;
    private ICommandItem[] _commands = [];
    private IFallbackCommandItem[] _fallbackCommands = [];

    public CommandPaletteLLMCommandsProvider()
        : this(new UserCommandStore(), new LlmProviderSettingsStore())
    {
    }

    internal CommandPaletteLLMCommandsProvider(UserCommandStore store)
        : this(
            store,
            new LlmProviderSettingsStore(filePath: null),
            endpointMonitor: new AssumedAvailableEndpointMonitor())
    {
    }

    internal CommandPaletteLLMCommandsProvider(
        UserCommandStore store,
        LlmProviderSettingsStore providerSettingsStore,
        ILlmClient? llmClient = null,
        ILlmEndpointMonitor? endpointMonitor = null)
    {
        _store = store;
        _providerSettingsStore = providerSettingsStore;
        _endpointMonitor = endpointMonitor ?? (llmClient is null
            ? new LlmEndpointMonitor(_providerSettingsStore)
            : new AssumedAvailableEndpointMonitor());
        _llmClient = llmClient ?? new OpenAiCompatibleLlmClient(
            _providerSettingsStore,
            _endpointMonitor);
        _endpointMonitor.StatusChanged += EndpointStatusChanged;
        DisplayName = "Command Palette LLM";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Frozen = false;
        Settings = new UserCommandsSettings(
            _store,
            _providerSettingsStore,
            ReloadCommands,
            ProviderSettingsChanged);
        ReloadCommands(raiseItemsChanged: false);
        _ = _endpointMonitor.CheckAsync(force: true, CancellationToken.None);
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override IFallbackCommandItem[] FallbackCommands() => _fallbackCommands;

    private void ReloadCommands() => ReloadCommands(raiseItemsChanged: true);

    private void ReloadCommands(bool raiseItemsChanged)
    {
        CancelFallbackRequests();
        CancelCommandPageRequests();
        _commandPageActive = false;
        var definitions = _store.GetCommands();
        var commandNames = definitions
            .Select(definition => definition.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _commandPages = definitions
            .Select(definition => new FormattedCommandPage(
                definition,
                _llmClient,
                pageAccessed: CommandPageAccessed,
                endpointMonitor: _endpointMonitor))
            .ToArray();
        _commands = _commandPages
            .Select(page => (ICommandItem)new CommandItem(page)
            {
                Title = page.Title,
            })
            .ToArray();
        _formattedFallbackItems = definitions
            .Select(definition => new FormattedFallbackItem(
                definition,
                _llmClient,
                isTopLevelCommandQuery: query => commandNames.Contains(query.Trim()),
                rootQueryObserved: RootQueryObserved,
                endpointMonitor: _endpointMonitor))
            .ToArray();
        _fallbackCommands = _formattedFallbackItems
            .Cast<IFallbackCommandItem>()
            .ToArray();

        if (raiseItemsChanged)
        {
            RaiseItemsChanged();
        }
    }

    private void CancelFallbackRequests()
    {
        foreach (var fallback in _formattedFallbackItems)
        {
            fallback.CancelPendingRequest();
        }
    }

    private void CancelCommandPageRequests()
    {
        foreach (var page in _commandPages)
        {
            page.CancelPendingRequest();
        }
    }

    private void CommandPageAccessed()
    {
        _commandPageActive = true;
        CancelFallbackRequests();
        _ = _endpointMonitor.CheckAsync(force: true, CancellationToken.None);
    }

    private void ProviderSettingsChanged()
    {
        _endpointMonitor.Invalidate();
        _ = _endpointMonitor.CheckAsync(force: true, CancellationToken.None);
    }

    private void EndpointStatusChanged()
    {
        foreach (var fallback in _formattedFallbackItems)
        {
            fallback.EndpointStatusChanged(allowGlobalResults: !_commandPageActive);
        }
    }

    private void RootQueryObserved()
    {
        _commandPageActive = false;
        CancelCommandPageRequests();
    }
}
