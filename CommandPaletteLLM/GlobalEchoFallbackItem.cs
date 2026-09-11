using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CommandPaletteLLM;

internal sealed partial class GlobalEchoFallbackItem : FallbackCommandItem
{
    public GlobalEchoFallbackItem()
        : base(new NoOpCommand(), "Echo message in global results", "CommandPaletteLLM.GlobalEcho")
    {
        Title = string.Empty;
    }

    public override void UpdateQuery(string query)
    {
        Title = !string.IsNullOrWhiteSpace(query)
            ? query
            : string.Empty;
    }
}
