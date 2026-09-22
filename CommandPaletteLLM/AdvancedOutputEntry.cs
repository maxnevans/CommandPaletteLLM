namespace CommandPaletteLLM;

internal sealed record AdvancedOutputEntry(
    AdvancedOutput? Output,
    string ErrorTitle,
    string RawContent)
{
    public static AdvancedOutputEntry Valid(AdvancedOutput output) =>
        new(output, string.Empty, string.Empty);

    public static AdvancedOutputEntry Invalid(string errorTitle, string rawContent) =>
        new(null, errorTitle, rawContent);
}
