using System.Collections.Generic;

namespace CommandPaletteLLM;

internal sealed record AdvancedOutput(
    string Title,
    string Subtitle,
    string Details,
    string Section,
    IReadOnlyList<string> Tags);
