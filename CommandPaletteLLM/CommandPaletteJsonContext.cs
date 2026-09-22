using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CommandPaletteLLM;

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(List<UserCommandDefinition>))]
[JsonSerializable(typeof(GlobalSettings))]
[JsonSerializable(typeof(LlmProviderSettings))]
[JsonSerializable(typeof(List<LlmProviderSettings>))]
[JsonSerializable(typeof(StoredLlmProviderSettings))]
[JsonSerializable(typeof(List<StoredLlmProviderSettings>))]
[JsonSerializable(typeof(JsonRequestTemplateDefinition))]
[JsonSerializable(typeof(List<JsonRequestTemplateDefinition>))]
[JsonSerializable(typeof(SettingsDocument))]
[JsonSerializable(typeof(ChatCompletionResponse))]
internal sealed partial class CommandPaletteJsonContext : JsonSerializerContext
{
}
