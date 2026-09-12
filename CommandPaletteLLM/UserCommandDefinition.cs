using System.Text.Json.Serialization;

namespace CommandPaletteLLM;

internal enum CommandExposure
{
    Unspecified,
    None,
    FallbackCommand,
    GlobalResult,
}

internal sealed class UserCommandDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string OutputFormat { get; set; } = string.Empty;

    public int SendDelayMilliseconds { get; set; } = 650;

    public int ResponseVariations { get; set; } = 1;

    public bool EnableAdvancedOutput { get; set; }

    public string ProviderId { get; set; } = "default";

    public CommandExposure Exposure { get; set; }

    public string IconPath { get; set; } = string.Empty;

    // Retained to migrate settings written by versions that used a boolean toggle.
    public bool EnableGlobalFallback { get; set; } = true;

    [JsonIgnore]
    public CommandExposure EffectiveExposure => Exposure == CommandExposure.Unspecified
        ? EnableGlobalFallback ? CommandExposure.FallbackCommand : CommandExposure.None
        : Exposure;

    public UserCommandDefinition Clone() => new()
    {
        Id = Id,
        Name = Name,
        OutputFormat = OutputFormat,
        SendDelayMilliseconds = SendDelayMilliseconds,
        ResponseVariations = ResponseVariations,
        EnableAdvancedOutput = EnableAdvancedOutput,
        ProviderId = ProviderId,
        Exposure = Exposure,
        IconPath = IconPath,
        EnableGlobalFallback = EnableGlobalFallback,
    };
}
