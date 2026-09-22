using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace CommandPaletteLLM;

public partial class CommandPaletteLLMCommandsProvider : CommandProvider
{
    private readonly UserCommandStore _store;
    private readonly LlmProviderSettingsStore _providerSettingsStore;
    private readonly JsonRequestTemplateStore _requestTemplateStore;
    private readonly GlobalSettingsStore _globalSettingsStore;
    private readonly ILlmClient? _llmClientOverride;
    private readonly ILlmEndpointMonitor? _endpointMonitorOverride;
    private readonly Dictionary<string, ProviderRuntime> _providerRuntimes = [];
    private FormattedCommandPage[] _commandPages = [];
    private FormattedFallbackItem[] _formattedFallbackItems = [];
    private bool _commandPageActive;
    private ICommandItem[] _commands = [];
    private IFallbackCommandItem[] _fallbackCommands = [];

    public CommandPaletteLLMCommandsProvider()
        : this(new SettingsDocumentStore())
    {
    }

    private CommandPaletteLLMCommandsProvider(SettingsDocumentStore settingsDocumentStore)
        : this(
            new UserCommandStore(settingsDocumentStore),
            new LlmProviderSettingsStore(settingsDocumentStore),
            new GlobalSettingsStore(settingsDocumentStore),
            new JsonRequestTemplateStore(settingsDocumentStore))
    {
    }

    internal CommandPaletteLLMCommandsProvider(UserCommandStore store)
        : this(
            store,
            new LlmProviderSettingsStore(filePath: null),
            new GlobalSettingsStore(filePath: null),
            new JsonRequestTemplateStore(),
            endpointMonitor: new AssumedAvailableEndpointMonitor())
    {
    }

    internal CommandPaletteLLMCommandsProvider(
        UserCommandStore store,
        LlmProviderSettingsStore providerSettingsStore,
        ILlmClient? llmClient = null,
        ILlmEndpointMonitor? endpointMonitor = null)
        : this(
            store,
            providerSettingsStore,
            new GlobalSettingsStore(filePath: null),
            new JsonRequestTemplateStore(),
            llmClient,
            endpointMonitor)
    {
    }

    internal CommandPaletteLLMCommandsProvider(
        UserCommandStore store,
        LlmProviderSettingsStore providerSettingsStore,
        GlobalSettingsStore globalSettingsStore,
        ILlmClient? llmClient = null,
        ILlmEndpointMonitor? endpointMonitor = null)
        : this(
            store,
            providerSettingsStore,
            globalSettingsStore,
            new JsonRequestTemplateStore(),
            llmClient,
            endpointMonitor)
    {
    }

    internal CommandPaletteLLMCommandsProvider(
        UserCommandStore store,
        LlmProviderSettingsStore providerSettingsStore,
        GlobalSettingsStore globalSettingsStore,
        JsonRequestTemplateStore requestTemplateStore,
        ILlmClient? llmClient = null,
        ILlmEndpointMonitor? endpointMonitor = null)
    {
        _store = store;
        _providerSettingsStore = providerSettingsStore;
        _globalSettingsStore = globalSettingsStore;
        _requestTemplateStore = requestTemplateStore;
        _llmClientOverride = llmClient;
        _endpointMonitorOverride = endpointMonitor ?? (llmClient is null
            ? null
            : new AssumedAvailableEndpointMonitor());
        DisplayName = "Command Palette LLM";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Frozen = false;
        Settings = new UserCommandsSettings(
            _store,
            _providerSettingsStore,
            _globalSettingsStore,
            _requestTemplateStore,
            ReloadCommands,
            ReloadCommands,
            ProviderSettingsChanged,
            ReloadCommands);
        ReloadCommands(raiseItemsChanged: false);
        CheckProviderConnections();
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override IFallbackCommandItem[] FallbackCommands() => _fallbackCommands;

    private void ReloadCommands() => ReloadCommands(raiseItemsChanged: true);

    private void ReloadCommands(bool raiseItemsChanged)
    {
        CancelFallbackRequests();
        foreach (var page in _commandPages)
        {
            page.Dispose();
        }

        _commandPageActive = false;
        var definitions = _store.GetCommands();
        var advancedOutputSystemPrompt = _globalSettingsStore.Get().AdvancedOutputSystemPrompt;
        var commandNames = definitions
            .Select(definition => definition.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _commandPages = definitions
            .Select(definition =>
            {
                var runtime = GetProviderRuntime(definition.ProviderId);
                var templateArguments = GetTemplateArguments(definition);
                return new FormattedCommandPage(
                    definition,
                    runtime.Client,
                    pageAccessed: CommandPageAccessed,
                    endpointMonitor: runtime.Monitor,
                    advancedOutputSystemPrompt: advancedOutputSystemPrompt,
                    templateRequestArguments: templateArguments);
            })
            .ToArray();
        _commands = _commandPages
            .Select(page => (ICommandItem)new CommandItem(page)
            {
                Title = page.Title,
            })
            .ToArray();
        _formattedFallbackItems = definitions
            .Where(definition => definition.EffectiveExposure != CommandExposure.None)
            .Select(definition =>
            {
                var runtime = GetProviderRuntime(definition.ProviderId);
                var templateArguments = GetTemplateArguments(definition);
                return new FormattedFallbackItem(
                    definition,
                    runtime.Client,
                    isTopLevelCommandQuery: query => commandNames.Contains(query.Trim()),
                    rootQueryObserved: RootQueryObserved,
                    endpointMonitor: runtime.Monitor,
                    advancedOutputSystemPrompt: advancedOutputSystemPrompt,
                    templateRequestArguments: templateArguments);
            })
            .ToArray();
        _fallbackCommands = _formattedFallbackItems
            .Cast<IFallbackCommandItem>()
            .ToArray();

        if (raiseItemsChanged)
        {
            RaiseItemsChanged();
        }

        CheckProviderConnections();
    }

    private string GetTemplateArguments(UserCommandDefinition definition)
    {
        if (string.IsNullOrEmpty(definition.RequestTemplateId))
        {
            return string.Empty;
        }

        var template = _requestTemplateStore.Get(definition.RequestTemplateId);
        return template is not null && string.Equals(
            template.ProviderId,
            definition.ProviderId,
            StringComparison.Ordinal)
                ? template.RequestArguments
                : string.Empty;
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
        CheckProviderConnections(force: true);
    }

    private void ProviderSettingsChanged()
    {
        foreach (var runtime in _providerRuntimes.Values)
        {
            runtime.Monitor.Invalidate();
        }

        CheckProviderConnections(force: true);
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

    private ProviderRuntime GetProviderRuntime(string providerId)
    {
        var resolvedId = _providerSettingsStore.Get(providerId)?.Id ??
            _providerSettingsStore.Get().Id;
        if (_providerRuntimes.TryGetValue(resolvedId, out var runtime))
        {
            return runtime;
        }

        var monitor = _endpointMonitorOverride ??
            new LlmEndpointMonitor(_providerSettingsStore, resolvedId);
        var client = _llmClientOverride ??
            new OpenAiCompatibleLlmClient(_providerSettingsStore, monitor, resolvedId);
        runtime = new ProviderRuntime(client, monitor);
        _providerRuntimes.Add(resolvedId, runtime);
        monitor.StatusChanged += EndpointStatusChanged;
        return runtime;
    }

    private void CheckProviderConnections(bool force = false)
    {
        foreach (var monitor in _providerRuntimes.Values
            .Select(runtime => runtime.Monitor)
            .Distinct())
        {
            _ = monitor.CheckAsync(force, CancellationToken.None);
        }
    }

    private sealed record ProviderRuntime(ILlmClient Client, ILlmEndpointMonitor Monitor);
}
