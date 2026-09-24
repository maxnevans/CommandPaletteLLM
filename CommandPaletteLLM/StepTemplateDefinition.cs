namespace CommandPaletteLLM;

internal sealed class StepTemplateDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Prompt { get; set; } = "{}";

    public string ProviderId { get; set; } = "default";

    public string RequestTemplateId { get; set; } = string.Empty;

    public string CustomRequestArguments { get; set; } = string.Empty;

    public StepTemplateDefinition Clone() => (StepTemplateDefinition)MemberwiseClone();

    public CommandStepDefinition CreateStep() => new()
    {
        Id = System.Guid.NewGuid().ToString("N"),
        Name = Name,
        Kind = CommandStepKind.User,
        Prompt = Prompt,
        ProviderId = ProviderId,
        RequestTemplateId = RequestTemplateId,
        CustomRequestArguments = CustomRequestArguments,
    };
}
