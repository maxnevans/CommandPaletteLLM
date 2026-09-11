using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CommandPaletteLLM;

internal sealed partial class CommandPaletteLLMPage : DynamicListPage
{
    private IListItem[] _items = [];

    public CommandPaletteLLMPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Command Palette LLM";
        Name = "Open";
        Id = "CommandPaletteLLM.Echo";
        PlaceholderText = "Type a message";
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _items = string.IsNullOrWhiteSpace(newSearch)
            ? []
            : [new ListItem(new NoOpCommand()) { Title = newSearch }];

        RaiseItemsChanged();
    }

    public override IListItem[] GetItems() => _items;
}
