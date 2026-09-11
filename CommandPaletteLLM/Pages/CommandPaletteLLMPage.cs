using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CommandPaletteLLM;

internal sealed partial class CommandPaletteLLMPage : ListPage
{
    public CommandPaletteLLMPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Command Palette LLM";
        Name = "Open";
    }

    public override IListItem[] GetItems() =>
    [
        new ListItem(new NoOpCommand()) { Title = "TODO: Implement the extension" },
    ];
}
