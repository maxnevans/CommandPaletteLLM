namespace CommandPaletteLLM;

internal sealed class GlobalSettings
{
    public const string LegacyAdvancedOutputSystemPrompt =
        "Return only one JSON object, with no Markdown or surrounding text. Use lowercase, case-sensitive fields: \"title\" (required concise result), \"subtitle\" (optional supporting text), \"details\" (optional full content), \"section\" (optional group name), and \"tags\" (optional array of strings). Omit unused optional fields.";

    public const string DefaultAdvancedOutputSystemPrompt =
        "Return only JSON, with no Markdown or surrounding text. A result object uses lowercase, case-sensitive fields: \"title\" (required concise result), \"subtitle\" (optional supporting text), \"details\" (optional full content), \"section\" (optional group name), and \"tags\" (optional array of strings). Omit unused optional fields. If the user's prompt asks in natural language for variations, alternatives, options, or multiple versions, return a JSON array containing one result object per variation; an empty array is allowed. Otherwise, return one result object. Choose the array form only from the user's wording, not request parameters such as \"n\".";

    public string AdvancedOutputSystemPrompt { get; set; } = DefaultAdvancedOutputSystemPrompt;

    public GlobalSettings Clone() => new()
    {
        AdvancedOutputSystemPrompt = AdvancedOutputSystemPrompt,
    };
}
