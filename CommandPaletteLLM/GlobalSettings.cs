namespace CommandPaletteLLM;

internal sealed class GlobalSettings
{
    public const string DefaultAdvancedOutputSystemPrompt =
        "Return only one JSON object, with no Markdown or surrounding text. Use lowercase, case-sensitive fields: \"title\" (required concise result), \"subtitle\" (optional supporting text), \"details\" (optional full content), \"section\" (optional group name), and \"tags\" (optional array of strings). Omit unused optional fields.";

    public string AdvancedOutputSystemPrompt { get; set; } = DefaultAdvancedOutputSystemPrompt;

    public GlobalSettings Clone() => new()
    {
        AdvancedOutputSystemPrompt = AdvancedOutputSystemPrompt,
    };
}
