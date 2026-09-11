using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CommandPaletteLLM;

internal sealed partial class FormattedFallbackItem : FallbackCommandItem
{
    private readonly string _outputFormat;

    public FormattedFallbackItem(UserCommandDefinition definition)
        : base(
            new NoOpCommand(),
            definition.Name,
            $"CommandPaletteLLM.Global.{definition.Id}")
    {
        _outputFormat = definition.OutputFormat;
        Title = string.Empty;
    }

    public override void UpdateQuery(string query)
    {
        Title = !string.IsNullOrWhiteSpace(query) &&
            OutputFormatter.TryFormat(_outputFormat, query, out var output, out _)
                ? output
                : string.Empty;
    }
}
