using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CommandPaletteLLM;

public partial class CommandPaletteLLMCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;

    public CommandPaletteLLMCommandsProvider()
    {
        DisplayName = "Command Palette LLM";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        _commands =
        [
            new CommandItem(new CommandPaletteLLMPage()) { Title = DisplayName },
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;
}
