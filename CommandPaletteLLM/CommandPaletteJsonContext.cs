using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CommandPaletteLLM;

[JsonSerializable(typeof(List<UserCommandDefinition>))]
internal sealed partial class CommandPaletteJsonContext : JsonSerializerContext
{
}
