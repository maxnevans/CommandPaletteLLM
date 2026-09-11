using System;
using System.IO;
using System.Text.Json;

namespace CommandPaletteLLM;

internal sealed class LlmProviderSettingsStore
{
    private readonly object _sync = new();
    private readonly string? _filePath;
    private LlmProviderSettings _settings;

    public LlmProviderSettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CommandPaletteLLM",
            "provider.json"))
    {
    }

    internal LlmProviderSettingsStore(string? filePath)
    {
        _filePath = filePath;
        _settings = Load();
    }

    public LlmProviderSettings Get()
    {
        lock (_sync)
        {
            return _settings.Clone();
        }
    }

    public void Replace(LlmProviderSettings settings)
    {
        var replacement = settings.Clone();

        lock (_sync)
        {
            Save(replacement);
            _settings = replacement;
        }
    }

    private LlmProviderSettings Load()
    {
        if (_filePath is null || !File.Exists(_filePath))
        {
            return new LlmProviderSettings();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var settings = JsonSerializer.Deserialize(
                json,
                CommandPaletteJsonContext.Default.LlmProviderSettings);
            return settings is not null && IsValid(settings)
                ? settings
                : new LlmProviderSettings();
        }
        catch (IOException)
        {
            return new LlmProviderSettings();
        }
        catch (JsonException)
        {
            return new LlmProviderSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new LlmProviderSettings();
        }
    }

    private void Save(LlmProviderSettings settings)
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
            settings,
            CommandPaletteJsonContext.Default.LlmProviderSettings);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _filePath, true);
    }

    internal static bool IsValid(LlmProviderSettings settings) =>
        Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var baseUri) &&
        (baseUri.Scheme == Uri.UriSchemeHttp || baseUri.Scheme == Uri.UriSchemeHttps) &&
        !string.IsNullOrWhiteSpace(settings.Model);
}
