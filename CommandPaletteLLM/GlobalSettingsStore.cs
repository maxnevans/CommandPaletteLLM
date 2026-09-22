using System;
using System.IO;
using System.Text.Json;

namespace CommandPaletteLLM;

internal sealed class GlobalSettingsStore
{
    private readonly object _sync = new();
    private readonly string? _filePath;
    private GlobalSettings _settings;

    public GlobalSettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CommandPaletteLLM",
            "settings.json"))
    {
    }

    internal GlobalSettingsStore(string? filePath)
    {
        _filePath = filePath;
        _settings = Load(filePath);
    }

    public GlobalSettings Get()
    {
        lock (_sync)
        {
            return _settings.Clone();
        }
    }

    public void Replace(GlobalSettings settings)
    {
        var replacement = settings.Clone();
        replacement.AdvancedOutputSystemPrompt ??= string.Empty;

        lock (_sync)
        {
            Save(replacement);
            _settings = replacement;
        }
    }

    public void ResetAdvancedOutputSystemPrompt() => Replace(new GlobalSettings());

    private static GlobalSettings Load(string? filePath)
    {
        if (filePath is null || !File.Exists(filePath))
        {
            return new GlobalSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize(
                File.ReadAllText(filePath),
                CommandPaletteJsonContext.Default.GlobalSettings);
            if (settings?.AdvancedOutputSystemPrompt is null)
            {
                return new GlobalSettings();
            }

            return settings;
        }
        catch (IOException)
        {
            return new GlobalSettings();
        }
        catch (JsonException)
        {
            return new GlobalSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new GlobalSettings();
        }
    }

    private void Save(GlobalSettings settings)
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
            CommandPaletteJsonContext.Default.GlobalSettings);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _filePath, true);
    }
}
