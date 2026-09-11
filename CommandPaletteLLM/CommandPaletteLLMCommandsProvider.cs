using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System.Linq;

namespace CommandPaletteLLM;

public partial class CommandPaletteLLMCommandsProvider : CommandProvider
{
    private readonly UserCommandStore _store;
    private ICommandItem[] _commands = [];
    private IFallbackCommandItem[] _fallbackCommands = [];

    public CommandPaletteLLMCommandsProvider()
        : this(new UserCommandStore())
    {
    }

    internal CommandPaletteLLMCommandsProvider(UserCommandStore store)
    {
        _store = store;
        DisplayName = "Command Palette LLM";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Frozen = false;
        Settings = new UserCommandsSettings(_store, ReloadCommands);
        ReloadCommands(raiseItemsChanged: false);
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override IFallbackCommandItem[] FallbackCommands() => _fallbackCommands;

    private void ReloadCommands() => ReloadCommands(raiseItemsChanged: true);

    private void ReloadCommands(bool raiseItemsChanged)
    {
        var definitions = _store.GetCommands();
        _commands = definitions
            .Select(definition => (ICommandItem)new CommandItem(new FormattedCommandPage(definition))
            {
                Title = definition.Name,
            })
            .ToArray();
        _fallbackCommands = definitions
            .Select(definition => (IFallbackCommandItem)new FormattedFallbackItem(definition))
            .ToArray();

        if (raiseItemsChanged)
        {
            RaiseItemsChanged();
        }
    }
}
