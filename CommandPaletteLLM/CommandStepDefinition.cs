namespace CommandPaletteLLM;

internal enum CommandMode
{
    Default,
    Pipeline,
}

internal enum CommandStepKind
{
    User,
    AdvancedOutput,
}

internal sealed class CommandStepDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public CommandStepKind Kind { get; set; }

    public string Prompt { get; set; } = "{}";

    public string ProviderId { get; set; } = "default";

    public string RequestTemplateId { get; set; } = string.Empty;

    public string CustomRequestArguments { get; set; } = string.Empty;

    public CommandStepDefinition Clone() => (CommandStepDefinition)MemberwiseClone();
}
