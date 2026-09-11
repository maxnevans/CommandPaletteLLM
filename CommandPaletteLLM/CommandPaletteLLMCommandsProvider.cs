using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Linq;

namespace CommandPaletteLLM;

public partial class CommandPaletteLLMCommandsProvider : CommandProvider
{
    private readonly UserCommandStore _store;
    private readonly LlmProviderSettingsStore _providerSettingsStore;
    private readonly ILlmClient _llmClient;
    private FormattedCommandPage[] _commandPages = [];
    private FormattedFallbackItem[] _formattedFallbackItems = [];
    private ICommandItem[] _commands = [];
    private IFallbackCommandItem[] _fallbackCommands = [];

    public CommandPaletteLLMCommandsProvider()
        : this(new UserCommandStore(), new LlmProviderSettingsStore())
    {
    }

    internal CommandPaletteLLMCommandsProvider(UserCommandStore store)
        : this(store, new LlmProviderSettingsStore(filePath: null))
    {
    }

    internal CommandPaletteLLMCommandsProvider(
        UserCommandStore store,
        LlmProviderSettingsStore providerSettingsStore,
        ILlmClient? llmClient = null)
    {
        _store = store;
        _providerSettingsStore = providerSettingsStore;
        _llmClient = llmClient ?? new OpenAiCompatibleLlmClient(_providerSettingsStore);
        DisplayName = "Command Palette LLM";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Frozen = false;
        Settings = new UserCommandsSettings(_store, _providerSettingsStore, ReloadCommands);
        ReloadCommands(raiseItemsChanged: false);
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override IFallbackCommandItem[] FallbackCommands() => _fallbackCommands;

    private void ReloadCommands() => ReloadCommands(raiseItemsChanged: true);

    private void ReloadCommands(bool raiseItemsChanged)
    {
        CancelFallbackRequests();
        CancelCommandPageRequests();
        var definitions = _store.GetCommands();
        var commandNames = definitions
            .Select(definition => definition.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _commandPages = definitions
            .Select(definition => new FormattedCommandPage(
                definition,
                _llmClient,
                pageAccessed: CancelFallbackRequests))
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
                rootQueryObserved: CancelCommandPageRequests))
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
}
