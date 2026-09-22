namespace CommandPaletteLLM;

internal sealed class JsonRequestTemplateDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ProviderId { get; set; } = "default";

    public string RequestArguments { get; set; } = "{}";

    public JsonRequestTemplateDefinition Clone() => new()
    {
        Id = Id,
        Name = Name,
        ProviderId = ProviderId,
        RequestArguments = RequestArguments,
    };
}
