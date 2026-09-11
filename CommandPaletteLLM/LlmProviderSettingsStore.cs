using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CommandPaletteLLM;

internal sealed class LlmProviderSettingsStore
{
    private readonly object _sync = new();
    private readonly string? _filePath;
    private List<LlmProviderSettings> _providers;

    public LlmProviderSettingsStore()
        : this(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CommandPaletteLLM",
                "providers.json"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CommandPaletteLLM",
                "provider.json"))
    {
    }

    internal LlmProviderSettingsStore(string? filePath)
        : this(filePath, legacyFilePath: null)
    {
    }

    internal LlmProviderSettingsStore(string? filePath, string? legacyFilePath)
    {
        _filePath = filePath;
        _providers = Load(filePath, legacyFilePath);
    }

    public LlmProviderSettings Get()
    {
        lock (_sync)
        {
            return _providers[0].Clone();
        }
    }

    public LlmProviderSettings? Get(string providerId)
    {
        lock (_sync)
        {
            return _providers.FirstOrDefault(provider =>
                string.Equals(provider.Id, providerId, StringComparison.Ordinal))?.Clone();
        }
    }

    public IReadOnlyList<LlmProviderSettings> GetProviders()
    {
        lock (_sync)
        {
            return _providers.Select(provider => provider.Clone()).ToArray();
        }
    }

    public void Replace(LlmProviderSettings settings)
    {
        var providers = GetProviders().Select(provider => provider.Clone()).ToList();
        var replacement = settings.Clone();
        var index = providers.FindIndex(provider =>
            string.Equals(provider.Id, replacement.Id, StringComparison.Ordinal));
        if (index < 0 && providers.Count == 1 &&
            string.Equals(replacement.Id, "default", StringComparison.Ordinal))
        {
            index = 0;
        }

        if (index < 0)
        {
            providers.Add(replacement);
        }
        else
        {
            providers[index] = replacement;
        }

        ReplaceAll(providers);
    }

    public void ReplaceAll(IEnumerable<LlmProviderSettings> providers)
    {
        var replacement = providers.Select(Normalize).ToList();
        if (replacement.Count == 0)
        {
            replacement.Add(new LlmProviderSettings());
        }

        lock (_sync)
        {
            Save(replacement);
            _providers = replacement;
        }
    }

    private static List<LlmProviderSettings> Load(string? filePath, string? legacyFilePath)
    {
        var sourcePath = filePath is not null && File.Exists(filePath)
            ? filePath
            : legacyFilePath is not null && File.Exists(legacyFilePath)
                ? legacyFilePath
                : null;
        if (sourcePath is null)
        {
            return [new LlmProviderSettings()];
        }

        try
        {
            var json = File.ReadAllText(sourcePath);
            using var document = JsonDocument.Parse(json);
            List<LlmProviderSettings> providers;
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                providers = JsonSerializer.Deserialize(
                    json,
                    CommandPaletteJsonContext.Default.ListLlmProviderSettings) ?? [];
            }
            else
            {
                var provider = JsonSerializer.Deserialize(
                    json,
                    CommandPaletteJsonContext.Default.LlmProviderSettings);
                providers = provider is null ? [] : [provider];
            }

            var valid = providers
                .Select(Normalize)
                .Where(IsValid)
                .GroupBy(provider => provider.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
            return valid.Count == 0 ? [new LlmProviderSettings()] : valid;
        }
        catch (IOException)
        {
            return [new LlmProviderSettings()];
        }
        catch (JsonException)
        {
            return [new LlmProviderSettings()];
        }
        catch (UnauthorizedAccessException)
        {
            return [new LlmProviderSettings()];
        }
    }

    private void Save(List<LlmProviderSettings> providers)
    {
        if (_filePath is null)
        {
            return;
        }

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{_filePath}.tmp";
        var json = JsonSerializer.Serialize(
            providers,
            CommandPaletteJsonContext.Default.ListLlmProviderSettings);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _filePath, true);
    }

    private static LlmProviderSettings Normalize(LlmProviderSettings provider)
    {
        var normalized = provider.Clone();
        normalized.Id = string.IsNullOrWhiteSpace(normalized.Id) ? "default" : normalized.Id.Trim();
        normalized.Name = string.IsNullOrWhiteSpace(normalized.Name) ? "Local LLM" : normalized.Name.Trim();
        normalized.BaseUrl = normalized.BaseUrl.Trim();
        normalized.Model = normalized.Model.Trim();
        return normalized;
    }

    internal static bool IsValid(LlmProviderSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.Id) &&
        !string.IsNullOrWhiteSpace(settings.Name) &&
        Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var baseUri) &&
        (baseUri.Scheme == Uri.UriSchemeHttp || baseUri.Scheme == Uri.UriSchemeHttps) &&
        !string.IsNullOrWhiteSpace(settings.Model);
}
