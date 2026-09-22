using System;
using System.Collections.Generic;

namespace CommandPaletteLLM;

internal sealed class SettingsDocument
{
    public bool? BeautifulFormatting { get; set; }

    public GlobalSettings Global { get; set; } = new();

    public List<StoredLlmProviderSettings> Providers { get; set; } = [];

    public List<UserCommandDefinition> Commands { get; set; } = [];
}

internal readonly record struct SettingsFileStatus(
    bool Exists,
    string? FilePath,
    DateTime? LastModified,
    bool BeautifulFormatting);
