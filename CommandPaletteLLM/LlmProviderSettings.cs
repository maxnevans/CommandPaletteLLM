namespace CommandPaletteLLM;

internal sealed class LlmProviderSettings
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:8080/v1";

    public string Model { get; set; } = "local-model";

    public string ApiKey { get; set; } = string.Empty;

    public LlmProviderSettings Clone() => new()
    {
        BaseUrl = BaseUrl,
        Model = Model,
        ApiKey = ApiKey,
    };
}
