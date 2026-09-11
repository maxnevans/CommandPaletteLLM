namespace CommandPaletteLLM;

internal sealed class UserCommandDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string OutputFormat { get; set; } = string.Empty;

    public UserCommandDefinition Clone() => new()
    {
        Id = Id,
        Name = Name,
        OutputFormat = OutputFormat,
    };
}
