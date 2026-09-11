using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CommandPaletteLLM;

internal sealed partial class FormattedCommandPage : DynamicListPage
{
    private readonly string _outputFormat;
    private IListItem[] _items = [];

    public FormattedCommandPage(UserCommandDefinition definition)
    {
        _outputFormat = definition.OutputFormat;
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Id = $"CommandPaletteLLM.Command.{definition.Id}";
        Title = definition.Name;
        Name = "Open";
        PlaceholderText = "Type a search term";
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _items = CreateItems(newSearch);
        RaiseItemsChanged();
    }

    public override IListItem[] GetItems() => _items;

    private IListItem[] CreateItems(string query)
    {
        if (string.IsNullOrWhiteSpace(query) ||
            !OutputFormatter.TryFormat(_outputFormat, query, out var output, out _) ||
            string.IsNullOrEmpty(output))
        {
            return [];
        }

        return [new ListItem(new NoOpCommand()) { Title = output }];
    }
}
