using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CommandPaletteLLM;

[JsonSerializable(typeof(List<UserCommandDefinition>))]
[JsonSerializable(typeof(LlmProviderSettings))]
[JsonSerializable(typeof(ChatCompletionRequest))]
[JsonSerializable(typeof(ChatCompletionResponse))]
internal sealed partial class CommandPaletteJsonContext : JsonSerializerContext
{
}
